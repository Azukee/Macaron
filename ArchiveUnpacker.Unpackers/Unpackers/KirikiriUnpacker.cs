using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ArchiveUnpacker.Core;
using ArchiveUnpacker.Core.Exceptions;
using ArchiveUnpacker.Core.ExtractableFileTypes;
using ArchiveUnpacker.Unpackers.Utils;

namespace ArchiveUnpacker.Unpackers.Unpackers
{
    public class KirikiriUnpacker : IUnpacker
    {
        private const string FileMagic = "XP3\r";

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) =>
            GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            using (var fs = File.OpenRead(inputArchive))
            using (var br = new BinaryReader(fs)) {
                var magic = Encoding.ASCII.GetString(br.ReadBytes(FileMagic.Length));
                if (magic != FileMagic)
                    throw new InvalidMagicException();

                br.ReadBytes(28); // 28 unknown bytes
                long indexOffset = br.ReadInt64();

                byte[] indexBytes;
                fs.Seek(indexOffset, SeekOrigin.Begin);
                bool isPacked = br.ReadBoolean();
                if (!isPacked)
                    indexBytes = br.ReadBytes((int) br.ReadInt64());
                else {
                    long compressedSize = br.ReadInt64();
                    long indexSize = br.ReadInt64();
                    br.ReadBytes(2); // skip zlib parameters (happy holly? c:)
                    byte[] compressedBytes = br.ReadBytes((int) compressedSize);
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
                        long entrySize = mbr.ReadInt64();

                        fileOffset += 12 + (int) entrySize;
                        if (entryMagic == "File") {
                            // File
                            var entry = new Entry();
                            while (entrySize > 0) {
                                var sectionMagic = Encoding.ASCII.GetString(mbr.ReadBytes(4));
                                long sectionSize = mbr.ReadInt64();
                                entrySize -= 12;
                                if (sectionSize > entrySize) {
                                    // fix info sections with wrongly assigned size
                                    if (sectionMagic == "info")
                                        sectionSize = entrySize;
                                }

                                entrySize -= sectionSize;
                                long nextSection = ms.Position + sectionSize;
                                switch (sectionMagic) {
                                    case "info":
                                        entry.Encrypted = 0 != mbr.ReadUInt32();
                                        long fileSize = mbr.ReadInt64();
                                        long compressedSize = mbr.ReadInt64();
                                        entry.IsCompressed = fileSize != compressedSize;
                                        entry.Size = (uint) compressedSize;
                                        entry.UnpackedSize = (uint) fileSize;

                                        string path = new string(mbr.ReadChars(mbr.ReadInt16()));
                                        if (Map.Count > 0)
                                            path = Map.GetFromMap(entry.Hash, path);
                                        entry.Path = path;
                                        break;
                                    case "segm":
                                        int segAmount = (int) (sectionSize / 28);
                                        if (segAmount > 0) {
                                            for (int i = 0; i < segAmount; i++) {
                                                bool compressed = 0 != mbr.ReadUInt32();
                                                long segOffset = mbr.ReadInt64();
                                                long segSize = mbr.ReadInt64();
                                                long segCompressedSize = mbr.ReadInt64();
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
                            uint hash = mbr.ReadUInt32();
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

                foreach (Entry entry in entries) {
                    // this check here is to prevent the program failing extracting "pseudo" files
                    if (entry.Path.Length < 100) {
                        fs.Seek(entry.Offset, SeekOrigin.Begin);
                        byte[] compressedBytes = br.ReadBytes((int) entry.Size);
                        byte[] uncompressedBytes = new byte[entry.UnpackedSize];
                        if (entry.IsCompressed) {
                            using (var ms = new MemoryStream(compressedBytes, 2, compressedBytes.Length - 2))
                            using (var decStream = new DeflateStream(ms, CompressionMode.Decompress, true))
                                decStream.Read(uncompressedBytes, 0, uncompressedBytes.Length);
                        } else
                            uncompressedBytes = compressedBytes;

                        yield return new KirikiriFile(entry.Path, uncompressedBytes);
                    }
                }
            }
        }

        public static bool IsGameFolder(string folder) =>
            Directory.GetFiles(folder, "*.xp3", SearchOption.AllDirectories).Count(FileStartsWithMagic) > 0;

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) =>
            Directory.GetFiles(gameDirectory, "*.xp3", SearchOption.AllDirectories).Where(FileStartsWithMagic);

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
            List<Segment> _Segments = new List<Segment>();

            public bool Encrypted;
            public List<Segment> Segments => _Segments;
            public uint Hash;
            public uint Size;
            public bool IsCompressed;
            public uint UnpackedSize;
            public string Path;
            public long Offset;
        }

        private sealed class Mapper
        {
            Dictionary<uint, string> hashMap = new Dictionary<uint, string>();
            Dictionary<string, string> md5Map = new Dictionary<string, string>();
            MD5 md5Imp = MD5.Create();
            StringBuilder md5String = new StringBuilder();

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
                for (int i = 0; i < md5.Length; ++i)
                    md5String.AppendFormat("{0:x2}", md5[i]);
                return md5String.ToString();
            }
        }

        private class KirikiriFile : IExtractableFile
        {
            public string Path { get; }
            private readonly byte[] file;

            public KirikiriFile(string path, byte[] file)
            {
                Path = path;
                this.file = file;
            }

            public void WriteToStream(Stream writeTo)
            {
                writeTo.Write(file, 0, file.Length);
            }
        }
    }
}