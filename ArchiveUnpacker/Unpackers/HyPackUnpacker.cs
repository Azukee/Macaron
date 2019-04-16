/*
Program Architecture & Framework:    @HoLLy-HaCKeR
Archive Format and Engine Reversing: @Azukee
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
    public class HyPackUnpacker : IUnpacker
    {
        private const string FileMagic = "HyPack";
        
        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            using (var fs = File.OpenRead(inputArchive)) 
                using (var br = new BinaryReader(fs)) {
                    string magic = Encoding.ASCII.GetString(br.ReadBytes(6));
                    if (magic != FileMagic)
                        throw new InvalidMagicException();
                    
                    br.ReadUInt16(); // unknown
                    uint headerOffset = br.ReadUInt32() + 0x10;
                    uint files = br.ReadUInt32();

                    fs.Seek(headerOffset, SeekOrigin.Begin);
                    for (int i = 0; i < files; i++) {
                        string path = $"{new string(br.ReadChars(21)).Replace("\0", "")}.{new string(br.ReadChars(3))}";
                        uint offset = br.ReadUInt32() + 0x10;
                        uint size = br.ReadUInt32();
                        br.ReadBytes(16); // 16 unknown bytes
                        yield return new FileSlice(path, offset, size, inputArchive);
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
    }
}