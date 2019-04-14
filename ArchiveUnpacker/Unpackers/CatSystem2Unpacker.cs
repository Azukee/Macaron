using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using ArchiveUnpacker.Framework;
using ArchiveUnpacker.Framework.Exceptions;

namespace ArchiveUnpacker.Unpackers
{
    /// <summary>
    /// Unpacker for CatSystem2
    ///
    /// Used in games like Idol Magical Girl Chiruchiru Michiru part 1/2.
    /// </summary>
    public class CatSystem2Unpacker : IUnpacker
    {
        private const string FileMagic = "KIF\0";

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            using (var fs = File.OpenRead(inputArchive))
            using (var br = new BinaryReader(fs)) {
                string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != FileMagic)
                    throw new InvalidMagicException();

                int unk1 = br.ReadInt32();
                for (int i = 0; i < unk1 + 1; i++) {
                    string fileName = Encoding.ASCII.GetString(br.ReadBytes(0x40)).TrimEnd('\0');
                    uint pos = (br.ReadUInt32() ^ 0) * 0x48 + 8;
                    uint len = br.ReadUInt32() ^ 0;
                }
            }
            
            throw new NotImplementedException();
        }

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) => Directory.GetFiles(gameDirectory, "*.int").Where(FileStartsWithMagic);

        public static bool IsGameFolder(string folder) => Directory.GetFiles(folder, "*.int").Count(FileStartsWithMagic) > 0;

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
