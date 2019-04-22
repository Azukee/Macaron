/*
Program Architecture & Framework:    @HoLLy-HaCKeR
Archive Format and Engine Reversing: @Azukee
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ArchiveUnpacker.Core;
using ArchiveUnpacker.Core.Exceptions;
using ArchiveUnpacker.Core.ExtractableFileTypes;

namespace ArchiveUnpacker.Unpackers.Unpackers
{
    /// <summary>
    /// Unpacker for the Artemis engine
    /// </summary>
    internal class ArtemisUnpacker : IUnpacker
    {
        private const string FileMagic = "pf";
        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            using (var fs = File.OpenRead(inputArchive))
            using (var br = new BinaryReader(fs)) {
                string magic = Encoding.ASCII.GetString(br.ReadBytes(2));
                if (magic != FileMagic)
                    throw new InvalidMagicException();

                char version = br.ReadChar();

                // read the entire header and calculate the key
                byte[] shaKey;
                int headerSize = br.ReadInt32();
                using (SHA1 sha = SHA1.Create())
                    shaKey = sha.ComputeHash(br.ReadBytes(headerSize));
                br.BaseStream.Seek(-headerSize, SeekOrigin.Current);

                // read the individual entries
                int entries = br.ReadInt32();
                for (int i = 0; i < entries; i++) {
                    string path = Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                    br.ReadBytes(4); // 4 unused bytes
                    uint offset = br.ReadUInt32();
                    uint size = br.ReadUInt32();
                    if(version == '8')
                        yield return new ArtemisFile(path, offset, size, inputArchive, shaKey);
                    else
                        yield return new FileSlice(path, offset, size, inputArchive);
                }
            }
        }

        //the reason behind *.pfs* is because the initial *.pfs file is usually split in segment (*.pfs.001) so the extra * helps mask for them
        public static bool IsGameFolder(string folder) => Directory.GetFiles(folder, "*.pfs*", SearchOption.AllDirectories).Count(FileStartsWithMagic) > 0;

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) => Directory.GetFiles(gameDirectory, "*.pfs*", SearchOption.AllDirectories).Where(FileStartsWithMagic);

        private static bool FileStartsWithMagic(string fileName)
        {
            byte[] buffer = new byte[FileMagic.Length];

            using (var file = File.OpenRead(fileName)) {
                if (file.Length <= FileMagic.Length) return false;
                file.Read(buffer, 0, FileMagic.Length);
                return Encoding.ASCII.GetString(buffer) == FileMagic;
            }
        }

        private class ArtemisFile : IExtractableFile {
            public string Path { get; }
            private readonly uint offset;
            private readonly uint size;
            private readonly string sourceFile;
            private readonly byte[] shaKey;

            public ArtemisFile(string path, uint offset, uint size, string sourceFile, byte[] shaKey) {
                Path = path;
                this.offset = offset;
                this.size = size;
                this.sourceFile = sourceFile;
                this.shaKey = shaKey;
            }

            public void WriteToStream(Stream writeTo) {
                using (var fs = File.OpenRead(sourceFile))
                using (var br = new BinaryReader(fs)) {
                    fs.Seek(offset, SeekOrigin.Begin);

                    for (int i = 0; i < size; i += shaKey.Length) {
                        int toRead = (int)Math.Min(size - i, shaKey.Length);
                        var bytes = br.ReadBytes(toRead);
                        for (int j = 0; j < toRead; j++) bytes[j] ^= shaKey[j];
                        writeTo.Write(bytes, 0, toRead);
                    }
                }
            }
        }
    }
}
