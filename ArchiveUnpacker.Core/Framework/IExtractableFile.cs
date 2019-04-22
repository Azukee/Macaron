using System.IO;

namespace ArchiveUnpacker.Core.Framework
{
    public interface IExtractableFile
    {
        string Path { get; }
        void WriteToStream(Stream writeTo);
    }
}
