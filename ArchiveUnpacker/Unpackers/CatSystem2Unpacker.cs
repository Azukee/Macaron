using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using ArchiveUnpacker.Framework;

namespace ArchiveUnpacker.Unpackers
{
    /// <summary>
    /// Unpacker for CatSystem2
    ///
    /// Used in games like Idol Magical Girl Chiruchiru Michiru part 2.
    /// </summary>
    public class CatSystem2Unpacker : IUnpacker
    {
        private const string FileMagic = "KIF\0";

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            throw new System.NotImplementedException();
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
