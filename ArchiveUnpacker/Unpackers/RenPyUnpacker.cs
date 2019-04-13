using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArchiveUnpacker.Framework;

namespace ArchiveUnpacker.Unpackers
{
    /// <summary>
    /// Unpacks files from VNs created using the Ren'Py engine.
    ///
    /// Tested on renpy v6.99.3.
    /// </summary>
    internal class RenPyUnpacker : IUnpacker
    {
        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            throw new NotImplementedException();
        }

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) => Directory.GetFiles(gameDirectory, "*.rpa");

        public static bool IsGameFolder(string folder) => File.Exists(Path.Combine(folder, "renpy", "main.py"));
    }
}
