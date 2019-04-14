/*
Program Architecture & Framework:    @HoLLy-HaCKeR
Archive Format and Engine Reversing: @Azukee
Loading Function Located at:         .text:00478900 (Ame no Marginal)
*/
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArchiveUnpacker.Framework;
using ArchiveUnpacker.Framework.Exceptions;

namespace ArchiveUnpacker.Unpackers
{
    public class MajiroArcUnpacker : IUnpacker 
    {
        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive) 
        {
            using (var fs = File.OpenRead(inputArchive)) {
                using (var br = new BinaryReader(fs)) {
                    var magic = br.ReadBytes(10);
                    if (!magic.SequenceEqual(new byte[] {
                        0x4D /*M*/, 0x61 /*a*/, 0x6A /*j*/, 0x69 /*i*/, 0x72 /*r*/, 0x6F /*o*/, 0x41 /*A*/, 0x72 /*r*/, 0x63 /*c*/, 0x56 /*V*/
                    }))
                        throw new InvalidMagicException();

                    br.ReadChars(5); // unused version
                    br.ReadByte();   // 1 unused byte

                    var entries = br.ReadInt32();
                    var nameListOffset = br.ReadUInt32();
                    var nameSize = (int) (br.ReadUInt32() - nameListOffset);

                    //I do this here so it's easier in the end to index these values, in the original code it just allocates a byte array
                    br.BaseStream.Seek(nameListOffset, SeekOrigin.Begin);
                    var names = new List<string>();
                    while (br.BaseStream.Position < nameListOffset + nameSize) {
                        var chars = new List<char>();
                        byte b;
                        while ((b = br.ReadByte()) != 0x00)
                            chars.Add((char) b);
                        names.Add(new string(chars.ToArray()));
                    }

                    br.BaseStream.Seek(32, SeekOrigin.Begin);

                    for (var i = 0; i < entries; i++) {
                        br.ReadBytes(4); // 4 unknown bytes
                        var offset = br.ReadUInt32();
                        var size = br.ReadUInt32();
                        br.ReadBytes(4); // 4 unknown bytes

                        yield return new MajiroArcFile(names[i], size, offset, inputArchive);
                    }
                }
            }
        }

        public static bool IsGameFolder(string folder)
        {
            // TODO: make this proper
            return Directory.Exists(folder) && File.Exists(Path.Combine(folder, "data.arc"));
        }

        private static IEnumerable<string> GetArchivesFromGameFolder(string folder)
        {
            // TODO: make this proper
            yield return Path.Combine(folder, "data.arc");
        }

        private class MajiroArcFile : IExtractableFile 
        {
            public string Path { get; }
            private readonly uint offset;
            private readonly uint size;
            private readonly string sourceFile;

            public MajiroArcFile(string path, uint size, uint offset, string sourceFile) 
            {
                Path = path;
                this.size = size;
                this.offset = offset;
                this.sourceFile = sourceFile;
            }


            public void WriteToStream(Stream writeTo) 
            {
                using (var fs = File.OpenRead(sourceFile)) {
                    using (var br = new BinaryReader(fs)) {
                        fs.Seek(offset, SeekOrigin.Begin);

                        var fileBytesToSave = br.ReadBytes((int) size);
                        writeTo.Write(fileBytesToSave, 0, fileBytesToSave.Length);
                    }
                }
            }
        }
    }
}