using System.Collections.Generic;

namespace ArchiveUnpacker.Core
{
    public interface IUnpacker
    {
        IEnumerable<IExtractableFile> LoadFiles(string gameDirectory);
        IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive);
    }
}
