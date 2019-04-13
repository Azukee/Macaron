using System.IO;

namespace ArchiveUnpacker.Framework
{
    public interface IExtractableFile
    {
        string Path { get; }
        void WriteToStream(Stream writeTo);
    }
}
