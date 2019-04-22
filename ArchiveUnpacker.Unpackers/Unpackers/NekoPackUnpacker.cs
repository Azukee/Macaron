/*
Program Architecture & Framework:    @HoLLy-HaCKeR
Archive Format and Engine Reversing: @Azukee
*/

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ArchiveUnpacker.Core;
using ArchiveUnpacker.Core.Exceptions;

namespace ArchiveUnpacker.Unpackers.Unpackers
{
    public class NekoPackUnpacker : IUnpacker
    {
        private const string FileMagic = "NEKOPACK4A";

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            using (var fs = File.OpenRead(inputArchive))
            using (var br = new BinaryReader(fs)) {
                string magic = Encoding.ASCII.GetString(br.ReadBytes(FileMagic.Length));
                if (magic != FileMagic)
                    throw new InvalidMagicException();

                uint headerSize = br.ReadUInt32();
                while (fs.Position < headerSize) {
                    string name = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32())).TrimEnd('\0');

                    int key = name.Sum(arg => (int)(sbyte)arg);

                    uint offset = br.ReadUInt32() ^ (uint)key;
                    uint size   = br.ReadUInt32() ^ (uint)key;

                    yield return new NekoPackFile(name, offset, size, inputArchive);
                }
            }
        }

        public static bool IsGameFolder(string folder) => Directory.GetFiles(folder, "*.pak").Count(FileStartsWithMagic) > 0;

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) => Directory.GetFiles(gameDirectory, "*.pak").Where(FileStartsWithMagic);

        private static bool FileStartsWithMagic(string fileName)
        {
            byte[] buffer = new byte[FileMagic.Length];

            using (var file = File.OpenRead(fileName)) {
                if (file.Length <= FileMagic.Length) return false;
                file.Read(buffer, 0, FileMagic.Length);
                return Encoding.ASCII.GetString(buffer) == FileMagic;
            }
        }

        private class NekoPackFile : IExtractableFile
        {
            public string Path { get; }
            private readonly uint offset;
            private readonly uint size;
            private readonly string sourceFile;

            public NekoPackFile(string path, uint offset, uint size, string sourceFile)
            {
                Path = path;
                this.offset = offset;
                this.size = size;
                this.sourceFile = sourceFile;
            }

            public void WriteToStream(Stream writeTo)
            {
                using (var fs = File.OpenRead(sourceFile))
                using (var br = new BinaryReader(fs)) {
                    fs.Seek(offset, SeekOrigin.Begin);
                    uint key = size / 8 + 0x22;
                    byte[] header = br.ReadBytes(4);
                    for (int i = 0; i < 4; i++) {
                        header[i] ^= (byte)key;
                        key <<= 3;
                    }

                    fs.Seek(offset + size - 4, SeekOrigin.Begin);
                    uint decompressedSize = br.ReadUInt32();
                    fs.Seek(offset + 4, SeekOrigin.Begin);

                    byte[] buffer = new byte[size];
                    buffer[0] = header[2];
                    buffer[1] = header[3];
                    br.Read(buffer, 2, (int)size - 6);

                    using (var decStream = new DeflateStream(new MemoryStream(buffer, 0, buffer.Length), CompressionMode.Decompress, true)) {
                        byte[] decompressedBuffer = new byte[decompressedSize];
                        decStream.Read(decompressedBuffer, 0, decompressedBuffer.Length);
                        writeTo.Write(decompressedBuffer, 0, decompressedBuffer.Length);
                    }
                }
            }
        }
    }
}