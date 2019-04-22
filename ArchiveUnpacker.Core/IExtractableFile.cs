using System.IO;

namespace ArchiveUnpacker.Core
{
    public interface IExtractableFile
    {
        string Path { get; }
        void WriteToStream(Stream writeTo);
    }
}
