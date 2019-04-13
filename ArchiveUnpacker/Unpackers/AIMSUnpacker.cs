/*
Program Architecture & Framework:   @HoLLy-HaCKeR
Archive Format and Engine Reversed: @Azukee
*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ArchiveUnpacker.EncryptionSchemes;
using ArchiveUnpacker.Framework;
using ArchiveUnpacker.Framework.Exceptions;

namespace ArchiveUnpacker.Unpackers {
    public class AIMSUnpacker : IUnpacker {
        private static readonly byte[] BlowfishKey = {
            0x7D, 0x73, 0xF6, 0xE4, 0xF5, 0x81, 0x5F, 0x7C, 0x78, 0x30, 0xC2, 0x36, 0xEA, 0x3E, 0x8A, 0x76, 0xF7, 0xE0, 0x48, 0xB5, 0x85, 0xD7, 0x77,
            0x49, 0x4C, 0x3D, 0xF5, 0x0C, 0xBB, 0xFB, 0x2E, 0x44, 0xFE, 0x25, 0xB7, 0xEB, 0xC7, 0xD9, 0x33, 0xAB, 0xA8, 0x2C, 0x64, 0xE8, 0xF0, 0xBD,
            0xEB, 0x8D, 0x9D, 0x1D, 0xA2, 0xFC, 0x59, 0x09, 0xAA, 0xA4
        };

        public static Blowfish Blowfish = new Blowfish(BlowfishKey);

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive) {
            using (var fs = File.OpenRead(inputArchive)) {
                using (var br = new BinaryReader(fs)) {
                    var magic = br.ReadBytes(4);
                    if (!magic.SequenceEqual(new byte[] {0x50 /*P*/, 0x41 /*A*/, 0x43 /*C*/, 0x4B /*K*/}))
                        throw new InvalidMagicException();

                    // read the individual entries
                    var entries = br.ReadInt32();
                    for (var i = 0; i < entries; i++) {
                        var path = new string(br.ReadChars(64)).Replace("\0", "");
                        br.ReadBytes(8); // 8 unknown bytes
                        var offset = br.ReadUInt32();
                        var size = br.ReadUInt32();
                        yield return new AIMSFile(path, size, offset, inputArchive);
                    }
                }
            }
        }

        public static bool IsGameFolder(string folder) {
            // TODO: make this proper
            return Directory.Exists(folder) && File.Exists(Path.Combine(folder, "tropical_liquor.p"));
        }

        private static IEnumerable<string> GetArchivesFromGameFolder(string folder) {
            // TODO: make this proper
            yield return Path.Combine(folder, "tropical_liquor.p");
        }

        private class AIMSFile : IExtractableFile {
            private readonly uint offset;
            private readonly uint size;
            private readonly string sourceFile;

            public AIMSFile(string path, uint size, uint offset, string sourceFile) {
                Path = path;
                this.size = size;
                this.offset = offset;
                this.sourceFile = sourceFile;
            }

            public string Path { get; }

            public void WriteToStream(Stream writeTo) {
                using (var fs = File.OpenRead(sourceFile)) {
                    using (var br = new BinaryReader(fs)) {
                        fs.Seek(offset, SeekOrigin.Begin);
                        var Encrypted = br.ReadBytes(4).SequenceEqual(new byte[] {0x4C /*L*/, 0x5A /*Z*/, 0x53 /*S*/, 0x53 /*S*/});
                        var FileBytesToSave = new byte[0];

                        if (Encrypted) {
                            var DecryptedSize = br.ReadUInt32();
                            var FileBytes = br.ReadBytes((int) size);
                            FileBytesToSave = new byte[DecryptedSize];
                            using (var r = new BinaryReader(new CryptoStream(new MemoryStream(FileBytes, 0, FileBytes.Length),
                                Blowfish.CreateDecryptor(), CryptoStreamMode.Read))) {
                                r.Read(FileBytesToSave, 0, FileBytesToSave.Length);
                            }
                        } else {
                            //Reset Location, so we don't skip first four bytes, after reading FileMagic at LOC:74
                            br.BaseStream.Seek(offset, SeekOrigin.Begin);
                            FileBytesToSave = br.ReadBytes((int) size);
                        }

                        writeTo.Write(FileBytesToSave, 0, FileBytesToSave.Length);
                    }
                }
            }
        }
    }
}