using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ArchiveUnpacker.EncryptionSchemes;
using ArchiveUnpacker.Framework;
using ArchiveUnpacker.Framework.Exceptions;
using TriggersTools.Windows.Resources;

namespace ArchiveUnpacker.Unpackers
{
    /// <summary>
    /// Unpacker for CatSystem2.
    ///
    /// Used in games like Idol Magical Girl Chiruchiru Michiru part 1/2.
    /// </summary>
    /// <remarks>
    /// Credits to trigger-death for his TriggersTools.CatSystem2, which served as a resource and is licensed under the MIT license.
    /// </remarks>
    public class CatSystem2Unpacker : IUnpacker
    {
        private const string FileMagic = "KIF\0";

        private string key1, key2;

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            // find keys from exe, if not present yet
            if  (key1 is null || key2 is null)
                (key1, key2) = LoadKeys(Path.GetDirectoryName(inputArchive));

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

        private static (string vCode1, string vCode2) LoadKeys(string startFolder)
        {
            // I had a stroke and decided to make everything as "functional" as possible, sorry.
            var pairs = new[] {
                getPair("KEY_CODE", "KEY"),
                getPair("V_CODE", "DATA"),
                getPair("V_CODE2", "DATA"),
            };
            ResourceIdPair getPair(string type, string name) => new ResourceIdPair(new ResourceId(type), new ResourceId(name), 1041);

            // assuming all *.int files are in game exe folder
            foreach (string file in Directory.GetFiles(startFolder, "*.exe")) {
                using (var resInfo = new ResourceInfo(file, false)) {
                    // only attempt files where all resources are present
                    if (!pairs.All(resInfo.Contains))
                        continue;

                    byte[][] resData = pairs.Select(pair => resInfo.Load(pair).ToBytes()).ToArray();

                    Debug.Assert(resData.All(x => x.Length == 16), "Keys were not 16 bytes long");

                    var bf = new BlowfishDecryptor(new Blowfish(resData[0].Select(x => (byte)(x ^ 0xCD)).ToArray()));
                    var vCode1 = bf.TransformFinalBlock(resData[1], 0, 16);
                    var vCode2 = bf.TransformFinalBlock(resData[2], 0, 16);

                    return (Encoding.ASCII.GetString(vCode1).TrimEnd('\0'), Encoding.ASCII.GetString(vCode2).TrimEnd('\0'));
                }
            }

            throw new Exception("Couldn't find game executable");
        }

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
