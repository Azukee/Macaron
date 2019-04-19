using System.IO;
using System.Text;

namespace ArchiveUnpacker.Utils
{
    public static class Extensions
    {
        public static string ToCString(this byte[] bytes, Encoding enc = null) => (enc ?? Encoding.UTF8).GetString(bytes).TrimEnd('\0');

        public static string ReadCString(this BinaryReader br, Encoding enc = null)
        {
            if (enc is null)
                enc = Encoding.UTF8;

            var multiByte = enc is UnicodeEncoding;

            long startIdx = br.BaseStream.Position;
            for (int i = 0; br.BaseStream.Position < br.BaseStream.Length; i++) {
                if (br.ReadByte() == 0 && (multiByte && i % 2 == 0))
                    break;
            }
            long endIdx = br.BaseStream.Position;

            br.BaseStream.Position = startIdx;
            string name = enc.GetString(br.ReadBytes((int)(endIdx - startIdx - 1)));
            ++br.BaseStream.Position;

            if (multiByte)
                ++br.BaseStream.Position;

            return name;
        }
    }
}
