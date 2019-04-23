using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ArchiveUnpacker.Core;
using ArchiveUnpacker.Core.Exceptions;
using ArchiveUnpacker.Core.ExtractableFileTypes;

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
                using (var ms = new MemoryStream(indexBytes, 0, indexBytes.Length))
                using (var mbr = new BinaryReader(ms, Encoding.Unicode)) {
                    uint entryMagic = mbr.ReadUInt32();
                    long entrySize = mbr.ReadInt64();

                    if (entryMagic == 0x656C6946) {
                        var entry = new Entry();
                        while (entrySize > 0) {
                            uint sectionMagic = mbr.ReadUInt32();
                            long sectionSize = mbr.ReadInt64();
                            entrySize -= 12;
                            if (sectionSize > entrySize) {
                                // fix info sections with wrongly assigned size
                                if (sectionMagic == 0x6F666E69)
                                    sectionSize = entrySize;
                            }

                            entrySize -= sectionSize;
                            long nextSection = ms.Position + sectionSize;
                            switch (sectionMagic) {
                                case 0x6F666E69: // info
                                    break;
                                case 0x6D676573: // "segm"
                                    break;
                                case 0x726C6461: // "adlr"
                                    break;
                            }

                            ms.Seek(nextSection, SeekOrigin.Begin);
                        }
                    }
                }
                
            }
            throw new NotImplementedException();
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
            public bool Encrypted { get; set; }
            public List<Segment> Segments { get; set; }
            public uint Hash { get; set; }
        }
    }
}