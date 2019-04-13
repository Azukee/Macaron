using System.Collections.Generic;

namespace ArchiveUnpacker.Framework
{
    public interface IUnpacker
    {
        IEnumerable<IExtractableFile> LoadFiles(string gameDirectory);
        IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive);
    }
}
