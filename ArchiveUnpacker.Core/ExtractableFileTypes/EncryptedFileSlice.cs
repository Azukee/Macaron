using System;
using System.IO;
using System.Security.Cryptography;

namespace ArchiveUnpacker.Core.ExtractableFileTypes
{
    public class EncryptedFileSlice : FileSlice
    {
        private readonly ICryptoTransform crypto;

        public EncryptedFileSlice(string path, long offset, uint size, string sourceFile, ICryptoTransform crypto) : base(path, offset, size, sourceFile)
        {
            this.crypto = crypto;
        }

        public override void WriteToStream(Stream writeTo)
        {
            using (var fs = File.OpenRead(SourceFile))
            using (var cs = new CryptoStream(fs, crypto, CryptoStreamMode.Read)) {
                fs.Seek(Offset, SeekOrigin.Begin);

                var buffer = new byte[BufferSize];
                for (int i = 0; i < Size; i += buffer.Length) {
                    int toCopy = (int)Math.Min(Size - i, buffer.Length);
                    cs.Read(buffer, 0, toCopy &~(crypto.InputBlockSize - 1));
                    writeTo.Write(buffer, 0, toCopy);
                }
            }
        }
    }
}
