using System.IO;

namespace ArchiveUnpacker.Utils.Pickle
{
    public static class PickleReader
    {
        public static object ReadFromStream(Stream s)
        {
            var state = new PickleState();
            return state.ReadFromStream(s);
        }
    }
}
