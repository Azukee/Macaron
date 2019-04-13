using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ArchiveUnpacker.Framework;
using ArchiveUnpacker.Framework.Exceptions;
using ArchiveUnpacker.Framework.ExtractableFileTypes;
using ArchiveUnpacker.Utils.Pickle;

namespace ArchiveUnpacker.Unpackers
{
    /// <summary>
    /// Unpacks files from VNs created using the Ren'Py engine.
    ///
    /// Tested on renpy v6.99.3.
    /// </summary>
    internal class RenPyUnpacker : IUnpacker
    {
        private const string MagicRegex = @"^RPA-3\.0 [a-f\d]{16} [a-f\d]{8}\nMade with Ren'Py.$";
        private const int MagicLength = 51;

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            using (var fs = File.OpenRead(inputArchive))
            using (var br = new BinaryReader(fs)) {
                var readMagic = Encoding.ASCII.GetString(br.ReadBytes(MagicLength));
                if (!Regex.IsMatch(readMagic, MagicRegex))
                    throw new InvalidMagicException();

                long indexOff = Convert.ToInt64(readMagic.Substring(8, 16), 16);
                uint key = Convert.ToUInt32(readMagic.Substring(25, 8), 16);

                // seek to index offset and read it
                fs.Seek(indexOff + 2, SeekOrigin.Begin);    // TODO: skipping zlib header here
                using (var decStream = new DeflateStream(fs, CompressionMode.Decompress, true)) {
                    var indexObject = PickleReader.ReadFromStream(decStream);
                    if (!(indexObject is Dictionary<object, object> dic))
                        throw new Exception("File index was not a dictionary.");

                    foreach (var o in dic) {
                        var val = (object[])((List<object>)o.Value)[0];
                        long v1 = (long)val[0] ^ key;
                        uint v2 = (uint)((int)val[1] ^ key);
                        var v3 = (string)val[2];
                        if (!string.IsNullOrEmpty(v3))
                            Debugger.Break();
                        yield return new FileSlice((string)o.Key, v1, v2, inputArchive);
                    }
                }
            }
        }

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) => Directory.GetFiles(gameDirectory, "*.rpa", SearchOption.AllDirectories);

        public static bool IsGameFolder(string folder) => File.Exists(Path.Combine(folder, "renpy", "main.py"));
    }
}
