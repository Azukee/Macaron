using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using ArchiveUnpacker.Core;
using ArchiveUnpacker.Core.Exceptions;

namespace ArchiveUnpacker.Unpackers.Unpackers
{
    public class KirikiriUnpacker : IUnpacker
    {
        private const string FileMagic = "XP3\r";

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            using (var fs = File.OpenRead(inputArchive))
            using (var br = new BinaryReader(fs)) {
                var magic = Encoding.ASCII.GetString(br.ReadBytes(FileMagic.Length));
                if (magic != FileMagic)
                    throw new InvalidMagicException();

                br.ReadBytes(28); // 28 unknown bytes
                var indexOffset = br.ReadInt64();

                byte[] indexBytes;
                fs.Seek(indexOffset, SeekOrigin.Begin);
                var isPacked = br.ReadBoolean();
                if (!isPacked) {
                    indexBytes = br.ReadBytes((int) br.ReadInt64());
                } else {
                    var compressedSize = br.ReadInt64();
                    var indexSize = br.ReadInt64();
                    br.ReadBytes(2); // skip zlib parameters (happy holly? c:)
                    var compressedBytes = br.ReadBytes((int) compressedSize);
                    using (var ms = new MemoryStream(compressedBytes, 0, compressedBytes.Length))
                    using (var decStream = new DeflateStream(ms, CompressionMode.Decompress, true)) {
                        indexBytes = new byte[indexSize];
                        decStream.Read(indexBytes, 0, (int) indexSize);
                    }
                }

                var entries = new List<Entry>();
                var fileOffset = 0;

                var Map = new Mapper();
                using (var ms = new MemoryStream(indexBytes, 0, indexBytes.Length))
                using (var mbr = new BinaryReader(ms, Encoding.Unicode)) {
                    while (mbr.PeekChar() != -1) {
                        var entryMagic = Encoding.ASCII.GetString(mbr.ReadBytes(4));
                        var entrySize = mbr.ReadInt64();

                        fileOffset += 12 + (int) entrySize;
                        if (entryMagic == "File") {
                            // File
                            var entry = new Entry();
                            while (entrySize > 0) {
                                var sectionMagic = Encoding.ASCII.GetString(mbr.ReadBytes(4));
                                var sectionSize = mbr.ReadInt64();
                                entrySize -= 12;
                                if (sectionSize > entrySize)
                                    if (sectionMagic == "info")
                                        sectionSize = entrySize;

                                entrySize -= sectionSize;
                                var nextSection = ms.Position + sectionSize;
                                switch (sectionMagic) {
                                    case "info":
                                        entry.Encrypted = 0 != mbr.ReadUInt32();
                                        var fileSize = mbr.ReadInt64();
                                        var compressedSize = mbr.ReadInt64();
                                        entry.IsCompressed = fileSize != compressedSize;
                                        entry.Size = (uint) compressedSize;
                                        entry.UnpackedSize = (uint) fileSize;

                                        var path = new string(mbr.ReadChars(mbr.ReadInt16()));
                                        if (Map.Count > 0)
                                            path = Map.GetFromMap(entry.Hash, path);
                                        entry.Path = path;
                                        break;
                                    case "segm":
                                        var segAmount = (int) (sectionSize / 28);
                                        if (segAmount > 0) {
                                            for (var i = 0; i < segAmount; i++) {
                                                var compressed = 0 != mbr.ReadUInt32();
                                                var segOffset = mbr.ReadInt64();
                                                var segSize = mbr.ReadInt64();
                                                var segCompressedSize = mbr.ReadInt64();
                                                entry.Segments.Add(new Segment {
                                                    Compressed = compressed,
                                                    CompressedSize = (uint) segCompressedSize,
                                                    Offset = segOffset,
                                                    Size = (uint) segSize
                                                });
                                            }

                                            entry.Offset = entry.Segments.FirstOrDefault().Offset;
                                        }

                                        break;
                                    case "adlr":
                                        if (sectionSize == 4)
                                            entry.Hash = mbr.ReadUInt32();
                                        break;
                                }

                                ms.Seek(nextSection, SeekOrigin.Begin);
                            }

                            if (entry.Path != "")
                                entries.Add(entry);
                        } else if (entryMagic == "hnfn" || entryMagic == "smil" || entryMagic == "eliF" || entryMagic == "Yuzu" ||
                                   entryMagic == "neko") {
                            var hash = mbr.ReadUInt32();
                            int nameLength = mbr.ReadInt16();
                            if (nameLength != 0) {
                                entrySize -= 6;
                                if (nameLength * 2 <= entrySize) {
                                    var fileName = new string(mbr.ReadChars(nameLength));
                                    Map.AddToMap(hash, fileName);
                                }
                            }
                        }

                        ms.Seek(fileOffset, SeekOrigin.Begin);
                    }
                }

                foreach (var entry in entries)
                    if (entry.Path.Length < 100)
                        yield return new KirikiriFile(entry.Path, entry.Offset, entry.Size, entry.UnpackedSize, inputArchive);
            }
        }

        public static bool IsGameFolder(string folder) => Directory.GetFiles(folder, "*.xp3", SearchOption.AllDirectories).Count(FileStartsWithMagic) > 0;

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) => Directory.GetFiles(gameDirectory, "*.xp3", SearchOption.AllDirectories).Where(FileStartsWithMagic);

        private static bool FileStartsWithMagic(string fileName)
        {
            var buffer = new byte[FileMagic.Length];

            using (var file = File.OpenRead(fileName)) {
                if (file.Length <= FileMagic.Length) return false;
                file.Read(buffer, 0, FileMagic.Length);
                return Encoding.ASCII.GetString(buffer) == FileMagic;
            }
        }

        private struct Segment
        {
            public bool Compressed;
            public long Offset;
            public uint Size;
            public uint CompressedSize;
        }

        private class Entry
        {
            public bool Encrypted;
            public uint Hash;
            public bool IsCompressed;
            public long Offset;
            public string Path;
            public uint Size;
            public uint UnpackedSize;
            public List<Segment> Segments { get; } = new List<Segment>();
        }

        private sealed class Mapper
        {
            private readonly Dictionary<uint, string> hashMap = new Dictionary<uint, string>();
            private readonly MD5 md5Imp = MD5.Create();
            private readonly Dictionary<string, string> md5Map = new Dictionary<string, string>();
            private readonly StringBuilder md5String = new StringBuilder();

            public int Count => md5Map.Count;

            public void AddToMap(uint hash, string file)
            {
                if (!hashMap.ContainsKey(hash))
                    hashMap[hash] = file;
                md5Map[GetHash(file)] = file;
            }

            public string GetFromMap(uint hash, string md5)
            {
                string file;
                if (md5Map.TryGetValue(md5, out file))
                    return file;
                if (hashMap.TryGetValue(hash, out file))
                    return file;
                return md5;
            }

            private string GetHash(string text)
            {
                var md5 = md5Imp.ComputeHash(Encoding.Unicode.GetBytes(text.ToLower()));
                md5String.Clear();
                for (var i = 0; i < md5.Length; ++i)
                    md5String.AppendFormat("{0:x2}", md5[i]);
                return md5String.ToString();
            }
        }

        private class KirikiriFile : IExtractableFile
        {
            public string Path { get; }
            private readonly long offset;
            private readonly uint size;
            private readonly string sourceFile;
            private readonly uint unpackedSize;

            public KirikiriFile(string path, long offset, uint size, uint unpackedSize, string sourceFile)
            {
                Path = path;
                this.offset = offset;
                this.size = size;
                this.sourceFile = sourceFile;
                this.unpackedSize = unpackedSize;
            }

            public void WriteToStream(Stream writeTo)
            {
                using (var fs = File.OpenRead(sourceFile))
                using (var br = new BinaryReader(fs)) {
                    fs.Seek(offset, SeekOrigin.Begin);
                    var compressedBytes = br.ReadBytes((int) size);
                    var uncompressedBytes = new byte[unpackedSize];
                    if (size != unpackedSize)
                        using (var ms = new MemoryStream(compressedBytes, 2, compressedBytes.Length - 2))
                        using (var decStream = new DeflateStream(ms, CompressionMode.Decompress, true)) {
                            decStream.Read(uncompressedBytes, 0, uncompressedBytes.Length);
                        }
                    else
                        uncompressedBytes = compressedBytes;

                    switch (System.IO.Path.GetExtension(Path)) {
                        case ".png": {
                            byte xorKey = (byte) (uncompressedBytes[1] ^ 'P');
                            for (int i = 0; i < uncompressedBytes.Length; i++)
                                uncompressedBytes[i] ^= xorKey;
                            uncompressedBytes[0] = 0x89;
                            uncompressedBytes[1] = (byte) 'P';
                            break;
                        }
                        case ".ogg": {
                            byte xorKey = (byte) (uncompressedBytes[1] ^ 'g');
                            for (int i = 0; i < uncompressedBytes.Length; i++)
                                uncompressedBytes[i] ^= xorKey;
                            uncompressedBytes[0] = (byte) 'O';
                            uncompressedBytes[1] = (byte) 'g';
                            break;
                        }
                        case ".cur": {
                            byte xorKey = uncompressedBytes[1];
                            uncompressedBytes[0] = xorKey;
                            for (int i = 0; i < uncompressedBytes.Length; i++)
                                uncompressedBytes[i] ^= xorKey;
                            break;
                        }
                        case ".ttf": {
                            byte xorKey = uncompressedBytes[2];
                            uncompressedBytes[0] = xorKey;
                            for (int i = 0; i < uncompressedBytes.Length; i++)
                                uncompressedBytes[i] ^= xorKey;
                            break;
                        }
                        case ".tjs": case ".stage": case ".asd": case ".func": {
                            byte xorKey = (byte) (uncompressedBytes[1] ^ 0xfe);
                            for (int i = 0; i < uncompressedBytes.Length; i++)
                                uncompressedBytes[i] ^= xorKey;
                            break;
                        }
                    }

                    writeTo.Write(uncompressedBytes, 0, uncompressedBytes.Length);
                }
            }
        }
    }
}