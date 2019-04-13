using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ArchiveUnpacker.Framework.ExtractableFileTypes
{
    public class FileSlice : IExtractableFile
    {
        public string Path { get; }
        private readonly long offset;
        private readonly uint size;
        private readonly string sourceFile;

        public FileSlice(string path, long offset, uint size, string sourceFile)
        {
            Path = path;
            this.offset = offset;
            this.size = size;
            this.sourceFile = sourceFile;
        }

        public void WriteToStream(Stream writeTo)
        {
            const int bufferSize = 2048;
            byte[] buffer = new byte[bufferSize];
            using (var fs = File.OpenRead(sourceFile)) {
                fs.Seek(offset, SeekOrigin.Begin);

                for (int i = 0; i < size; i += bufferSize) {
                    int toCopy = (int)Math.Min(size - i, bufferSize);
                    fs.Read(buffer, 0, toCopy);
                    writeTo.Write(buffer, 0, toCopy);
                }
            }
        }
    }
}
