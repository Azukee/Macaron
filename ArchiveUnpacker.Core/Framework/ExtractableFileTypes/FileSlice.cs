using System;
using System.IO;

namespace ArchiveUnpacker.Core.Framework.ExtractableFileTypes
{
    public class FileSlice : IExtractableFile
    {
        protected const int BufferSize = 2048;

        public string Path { get; }
        protected readonly long Offset;
        protected readonly uint Size;
        protected readonly string SourceFile;

        public FileSlice(string path, long offset, uint size, string sourceFile)
        {
            Path = path;
            Offset = offset;
            Size = size;
            SourceFile = sourceFile;
        }

        public virtual void WriteToStream(Stream writeTo)
        {
            using (var fs = File.OpenRead(SourceFile)) {
                fs.Seek(Offset, SeekOrigin.Begin);

                var buffer = new byte[BufferSize];
                for (int i = 0; i < Size; i += buffer.Length) {
                    int toCopy = (int)Math.Min(Size - i, buffer.Length);
                    fs.Read(buffer, 0, toCopy);
                    writeTo.Write(buffer, 0, toCopy);
                }
            }
        }
    }
}
