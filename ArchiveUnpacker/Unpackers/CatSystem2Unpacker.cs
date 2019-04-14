using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ArchiveUnpacker.EncryptionSchemes;
using ArchiveUnpacker.Framework;
using ArchiveUnpacker.Framework.Exceptions;
using ArchiveUnpacker.Utils;
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
        private uint indexRngSeed;

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory) => GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            // find keys from exe, if not present yet
            if  (key1 is null || key2 is null)
                (key1, key2, indexRngSeed) = LoadKeys(Path.GetDirectoryName(inputArchive));

            using (var fs = File.OpenRead(inputArchive))
            using (var br = new BinaryReader(fs)) {
                string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != FileMagic)
                    throw new InvalidMagicException();

                int entries = br.ReadInt32();
                BlowfishDecryptor dec = null;
                for (int i = 0; i < entries + 1; i++) {
                    string fileName = br.ReadBytes(0x40).ToCString();
                    ulong read = br.ReadUInt64();

                    if (fileName == "__key__.dat") {
                        // don't make key multiple times
                        if (!(dec is null))
                            continue;

                        var key = MersenneTwister.GenRand((uint)(read >> 32));
                        dec = new BlowfishDecryptor(new Blowfish(BitConverter.GetBytes(key)));
                        continue;
                    }

                    fileName = DeobfuscateFileName(fileName, (uint)unchecked(indexRngSeed + i));
                    read += (ulong)i;
                    var newBytes = dec.TransformFinalBlock(BitConverter.GetBytes(read), 0, 8);

                    uint pos = BitConverter.ToUInt32(newBytes, 0);
                    uint len = BitConverter.ToUInt32(newBytes, 4);

                    // Debugger.Break();
                }
            }

            throw new NotImplementedException();
        }

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) => Directory.GetFiles(gameDirectory, "*.int").Where(FileStartsWithMagic);

        public static bool IsGameFolder(string folder) => Directory.GetFiles(folder, "*.int").Count(FileStartsWithMagic) > 0;

        private static (string vCode1, string vCode2, uint indexRngSeed) LoadKeys(string startFolder)
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
                using (var resInfo = new ResourceInfo(file, true)) {
                    // only attempt files where all resources are present
                    if (!pairs.All(pair => resInfo.ResourceTypes.Any(resType => resType == pair.Type)))
                        continue;

                    byte[][] resData = pairs.Select(pair => resInfo[pair].ToBytes()).ToArray();

                    Debug.Assert(resData.All(x => x.Length == 16), "Keys were not 16 bytes long");

                    var bf = new BlowfishDecryptor(new Blowfish(resData[0].Select(x => (byte)(x ^ 0xCD)).ToArray()));
                    var vCode1 = bf.TransformFinalBlock(resData[1], 0, 16).ToCString();
                    var vCode2 = bf.TransformFinalBlock(resData[2], 0, 16).ToCString();

                    return (vCode1, vCode2, GenerateIndexRngSeed(vCode2));
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

        private static uint GenerateIndexRngSeed(string vcode2)
        {
            const uint xorKey = 0x04C11DB7;
            uint seed = uint.MaxValue;

            foreach (char c in vcode2) {
                // xor top byte
                seed ^= (uint) c << 24;

                // shift this byte away, xor if shifted bit was 1
                for (int j = 0; j < 8; j++) {
                    bool topBitSet = seed >= (uint)1 << 31;
                    seed <<= 1;
                    if (topBitSet) seed ^= xorKey;
                }

                // flip the entire thing
                seed = ~seed;
            }

            return seed;
        }

        private static string DeobfuscateFileName(string fileName, uint seed)
        {
            const int length = 52;
            const string keyspace = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

            var sb = new StringBuilder(fileName.Length);

            uint key = MersenneTwister.GenRand(seed);
            int shift = (byte) ((key >> 24) + (key >> 16) + (key >> 8) + key);

            for (int i = 0; i < fileName.Length; i++, shift++) {
                char c = fileName[i];

                // the crypto: caesar cipher on reversed keyspace, with shifting index
                if (keyspace.Contains(c)) {
                    int idx = keyspace.IndexOf(c);
                    int reverseIdx = length - idx - 1;
                    c = keyspace[mod(reverseIdx - shift, length)];
                }

                sb.Append(c);

                // mod function, because % operator is remainder
                int mod(int x, int m) => (x % m + m) % m;
            }

            return sb.ToString();
        }
    }
}
