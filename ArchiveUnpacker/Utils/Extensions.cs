using System.Text;

namespace ArchiveUnpacker.Utils
{
    public static class Extensions
    {
        public static string ToCString(this byte[] bytes, Encoding enc = null) => (enc ?? Encoding.UTF8).GetString(bytes).TrimEnd('\0');
    }
}
