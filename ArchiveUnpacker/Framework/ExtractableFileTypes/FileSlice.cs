using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace ArchiveUnpacker.Framework.ExtractableFileTypes
{
    internal class FileSlice : IExtractableFile
    {
        public const int BufferSize = 2048;
        
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
            byte[] buffer = new byte[BufferSize];
            using (var fs = File.OpenRead(SourceFile)) {
                fs.Seek(Offset, SeekOrigin.Begin);

                for (int i = 0; i < Size; i += buffer.Length) {
                    int toCopy = (int)Math.Min(Size - i, buffer.Length);
                    fs.Read(buffer, 0, toCopy);
                    writeTo.Write(buffer, 0, toCopy);
                }
            }
        }
    }
}
