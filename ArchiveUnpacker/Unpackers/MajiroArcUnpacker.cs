/*
Program Architecture & Framework:    @HoLLy-HaCKeR
Archive Format and Engine Reversing: @Azukee
Loading Function Located at:         .text:00478900 (Ame no Marginal)
*/
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ArchiveUnpacker.Framework;
using ArchiveUnpacker.Framework.Exceptions;
using ArchiveUnpacker.Framework.ExtractableFileTypes;

namespace ArchiveUnpacker.Unpackers
{
    public class MajiroArcUnpacker : IUnpacker 
    {
        private const string FileMagic = "MajiroArcV";
        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive) 
        {
            using (var fs = File.OpenRead(inputArchive)) {
                using (var br = new BinaryReader(fs)) {
                    string magic = Encoding.ASCII.GetString(br.ReadBytes(10));
                    if (magic != FileMagic)
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
                        string name = "";
                        byte b;
                        while ((b = br.ReadByte()) != 0x00)
                            name += (char) b;
                        names.Add(name);
                    }

                    br.BaseStream.Seek(32, SeekOrigin.Begin);

                    for (var i = 0; i < entries; i++) {
                        br.ReadBytes(4); // 4 unknown bytes
                        var offset = br.ReadUInt32();
                        var size = br.ReadUInt32();
                        br.ReadBytes(4); // 4 unknown bytes

                        yield return new FileSlice(names[i], offset, size, inputArchive);
                    }
                }
            }
        }

        public static bool IsGameFolder(string folder) => Directory.GetFiles(folder, "*.arc").Count(FileStartsWithMagic) > 0;

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) => Directory.GetFiles(gameDirectory, "*.arc").Where(FileStartsWithMagic);
        
        private static bool FileStartsWithMagic(string fileName)
        {
            byte[] buffer = new byte[FileMagic.Length];

            using (var file = File.OpenRead(fileName)) {
                if (file.Length <= FileMagic.Length) return false;
                file.Read(buffer, 0, FileMagic.Length);
                return Encoding.ASCII.GetString(buffer) == FileMagic;
            }
        }
    }
}