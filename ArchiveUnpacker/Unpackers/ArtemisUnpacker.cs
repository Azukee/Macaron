/*
Program Architecture & Framework:    @HoLLy-HaCKeR
Archive Format and Engine Reversing: @Azukee
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ArchiveUnpacker.Framework;
using ArchiveUnpacker.Framework.Exceptions;

namespace ArchiveUnpacker.Unpackers {
    internal class ArtemisUnpacker : IUnpacker {
        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive) {
            using (var fs = File.OpenRead(inputArchive))
            using (var br = new BinaryReader(fs)) {
                var magic = br.ReadBytes(3);
                if (!magic.SequenceEqual(new byte[] { 0x70 /*p*/, 0x66 /*f*/, 0x38 /*8*/ }))
                    throw new InvalidMagicException();

                // read the entire header and calculate the key
                byte[] shaKey;
                int headerSize = br.ReadInt32();
                using (SHA1 sha = SHA1.Create())
                    shaKey = sha.ComputeHash(br.ReadBytes(headerSize));
                br.BaseStream.Seek(-headerSize, SeekOrigin.Current);

                // read the individual entries
                int entries = br.ReadInt32();
                for (int i = 0; i < entries; i++) {
                    string path = new string(br.ReadChars(br.ReadInt32()));
                    br.ReadBytes(4); // 4 unused bytes
                    uint offset = br.ReadUInt32();
                    uint size = br.ReadUInt32();
                    yield return new ArtemisFile(path, size, offset, inputArchive, shaKey);
                }
            }
        }

        public static bool IsGameFolder(string folder) {
            // TODO: make this proper
            return Directory.Exists(folder) && File.Exists(Path.Combine(folder, "root.pfs"));
        }

        private static IEnumerable<string> GetArchivesFromGameFolder(string folder) {
            // TODO: make this proper
            yield return Path.Combine(folder, "root.pfs");
        }

        private class ArtemisFile : IExtractableFile {
            public string Path { get; }
            private readonly uint offset;
            private readonly uint size;
            private readonly string sourceFile;
            private readonly byte[] shaKey;

            public ArtemisFile(string path, uint size, uint offset, string sourceFile, byte[] shaKey) {
                Path = path;
                this.size = size;
                this.offset = offset;
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
