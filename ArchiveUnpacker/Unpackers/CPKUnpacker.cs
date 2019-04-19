using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ArchiveUnpacker.Framework;
using ArchiveUnpacker.Framework.Exceptions;
using ArchiveUnpacker.Framework.ExtractableFileTypes;
using ArchiveUnpacker.Utils;

namespace ArchiveUnpacker.Unpackers
{
    public class CPKUnpacker : IUnpacker
    {
        private const string FileMagic = "CPK ";

        private readonly byte[] UTFMagic = {0x40, 0x55, 0x54, 0x46};

        public IEnumerable<IExtractableFile> LoadFiles(string gameDirectory)
        {
            return GetArchivesFromGameFolder(gameDirectory).SelectMany(LoadFilesFromArchive);
        }

        public IEnumerable<IExtractableFile> LoadFilesFromArchive(string inputArchive)
        {
            using (var fs = File.OpenRead(inputArchive))
            using (var br = new BinaryReader(fs)) {
                var magic = Encoding.ASCII.GetString(br.ReadBytes(FileMagic.Length));
                if (magic != FileMagic)
                    throw new InvalidMagicException();

                Dictionary<int, FileIndex> files = new Dictionary<int, FileIndex>();
                br.ReadBytes(0x4);

                byte[] chunk = ReadUTF(br);
                var header = DecryptUTF(chunk).First();
                long contentOffset = (long) header["ContentOffset"];
                if (header.ContainsKey("TocOffset")) {
                    long offset = Math.Min(contentOffset, (long) header["TocOffset"]);
                    
                    fs.Seek(offset, SeekOrigin.Begin);
                    var tocMagic = Encoding.ASCII.GetString(br.ReadBytes(4));
                    if (tocMagic != "TOC ")
                        throw new InvalidMagicException();

                    byte[] tocChunk = ReadUTF(br);
                    var decrypted = DecryptUTF(tocChunk);

                    foreach (var entry in decrypted) {
                        FileIndex fi = new FileIndex {
                            id = (int) entry["ID"],
                            offset = (long) entry["FileOffset"] + offset,
                            size = (uint) entry["FileSize"]
                        };
                        fi.upSize = fi.size;
                        if (entry.ContainsKey("ExtractSize"))
                            fi.upSize = (uint) entry["ExtractSize"];
                        fi.name = (string) entry["FileName"];
                        if (entry.ContainsKey("DirName"))
                            fi.name = Path.Combine((string) entry["DirName"], fi.name);
                        files[fi.id] = fi;
                    }
                }

                if (header.ContainsKey("ItocOffset")) {
                    uint align = (uint)(int) header["Align"];
                    long offset = (long) header["ItocOffset"];
                    
                    fs.Seek(offset, SeekOrigin.Begin);
                    var tocMagic = Encoding.ASCII.GetString(br.ReadBytes(4));
                    if (tocMagic != "ITOC")
                        throw new InvalidMagicException();

                    br.ReadBytes(4);
                    byte[] itocChunk = ReadUTF(br);
                    var itoc = DecryptUTF(itocChunk).First();

                    var dataL = DecryptUTF((byte[]) itoc["DataL"]);
                    var dataH = DecryptUTF((byte[]) itoc["DataH"]);

                    foreach (var entry in dataL.Concat(dataH)) {
                        int id = (int) entry["ID"];
                        FileIndex fi;
                        if (!files.TryGetValue (id, out fi)) {
                            fi = new FileIndex { id = id };
                            files[id] = fi;
                        }

                        fi.size = (uint)(int) entry["FileSize"];
                        fi.upSize = fi.size;
                        if (entry.ContainsKey("ExtractSize"))
                            fi.upSize = (uint)(int) entry["ExtractSize"];
                    }

                    long currPos = contentOffset;
                    foreach (var id in files.Keys.OrderBy(x => x)) {
                        var entry = files[id];
                        entry.offset = currPos;
                        currPos += entry.size;
                        if (align != 0) {
                            var remainder = entry.size % align;
                            if (remainder > 0)
                                currPos += align - remainder;
                        }

                        if (entry.name == null) 
                            entry.name = id.ToString("D5");
                    }

                }
                
                //This is *NOT* correct, this is just a placeholder
                foreach(FileIndex fi in files.Values)
                    yield return new FileSlice(fi.name, fi.offset, fi.size, inputArchive);
            }
        }

        private byte[] ReadUTF(BinaryReader br) {
            var chunkSize = br.ReadInt64();
            var chunk = br.ReadBytes((int) chunkSize);
            byte[] magicBytes = new byte[4];
            Array.Copy(chunk, 0, magicBytes, 0, 4);
            if (!magicBytes.SequenceEqual(UTFMagic)) {
                var key = 0x655F;
                for (var i = 0x0; i < chunk.Length; i++) {
                    chunk[i] ^= (byte) key;
                    key *= 0x4115;
                }
            }

            return chunk;
        }

        public uint ToUInt32<TArray>(TArray value, int index) where TArray : IList<byte> => (uint) ((value[index] << 24) | (value[index + 1] << 16) | (value[index + 2] << 8) | value[index + 3]);

        private List<Dictionary<string, object>> DecryptUTF(byte[] chunk)
        {
            byte[] magicBytes = new byte[4];
            Array.Copy(chunk, 0, magicBytes, 0, 4);
            if (!magicBytes.SequenceEqual(UTFMagic))
                throw new InvalidMagicException();

            var chunkSize = (int) ToUInt32(chunk, 4);

            using (var ms = new MemoryStream(chunk, 8, chunkSize))
            using (var br = new BinaryReader(ms)) {
                var rowsOffset = br.ReadInt32BE();
                var stringsOffset = br.ReadInt32BE();
                var dataOffset = br.ReadInt32BE() + 8;
                br.ReadBytes(4);
                int columnCount = br.ReadInt16BE();
                int rowLength = br.ReadInt16BE();
                var rows = br.ReadInt32BE();

                var columns = new List<Column>(columnCount);
                for (var i = 0; i < columnCount; i++) {
                    var flags = br.ReadByte();
                    if (flags == 0) {
                        br.ReadBytes(3);
                        flags = br.ReadByte();
                    }

                    var nameOffset = stringsOffset + br.ReadInt32BE();
                    var posBefore = ms.Position;
                    
                    StringBuilder name = new StringBuilder();
                    
                    ms.Seek(nameOffset, SeekOrigin.Begin);
                    char c;
                    while ((c = br.ReadChar()) != 0x00)
                        name.Append(c);
                    columns.Add(new Column {Flags = (Flags) flags, Name = name.ToString()});
                    ms.Seek(posBefore, SeekOrigin.Begin);
                }

                var returnTable = new List<Dictionary<string, object>>(rows);
                var nextOffset = rowsOffset;
                for (var i = 0; i < rows; i++) {
                    ms.Seek(nextOffset, SeekOrigin.Begin);
                    nextOffset += rowLength;

                    var row = new Dictionary<string, object>(columnCount);
                    returnTable.Add(row);
                    foreach (var column in columns) {
                        var storageFlag = column.Flags & Flags.StorageMask;
                        if (Flags.StorageNone == storageFlag || Flags.StorageZero == storageFlag || Flags.StorageConstant == storageFlag)
                            continue;
                        switch (column.Flags & Flags.Mask) {
                            case Flags.Byte:
                                row[column.Name] = (int) br.ReadByte();
                                break;
                            case Flags.SByte:
                                row[column.Name] = (int) br.ReadSByte();
                                break;
                            case Flags.UInt16:
                                row[column.Name] = (int) br.ReadUInt16BE();
                                break;
                            case Flags.Int16:
                                row[column.Name] = (int) br.ReadInt16BE();
                                break;
                            case Flags.UInt32:
                            case Flags.Int32:
                                row[column.Name] = br.ReadInt32BE();
                                break;
                            case Flags.UInt64:
                            case Flags.Int64:
                                row[column.Name] = br.ReadInt64BE();
                                break;
                            case Flags.Float32:
                                row[column.Name] = br.ReadSingleBE();
                                break;
                            case Flags.String: {
                                var posBefore = ms.Position;
                                var offset = stringsOffset + br.ReadInt32BE();
                                ms.Seek(offset, SeekOrigin.Begin);
                                row[column.Name] = br.ReadCString();
                                ms.Seek(posBefore, SeekOrigin.Begin);
                                break;
                            }
                            case Flags.Data: {
                                var posBefore = ms.Position;
                                var offset = dataOffset + br.ReadInt32BE();
                                var length = br.ReadInt32BE();
                                row[column.Name] = chunk.Skip(offset).Take(length).ToArray();
                                ms.Seek(posBefore, SeekOrigin.Begin);
                                break;
                            }
                        }
                    }
                }

                return returnTable;
            }
        }
        
        public static bool IsGameFolder(string folder) => Directory.GetFiles(folder, "*.cpk").Count(FileStartsWithMagic) > 0;

        private IEnumerable<string> GetArchivesFromGameFolder(string gameDirectory) => Directory.GetFiles(gameDirectory, "*.cpk").Where(FileStartsWithMagic);

        private static bool FileStartsWithMagic(string fileName)
        {
            var buffer = new byte[FileMagic.Length];

            using (var file = File.OpenRead(fileName)) {
                if (file.Length <= FileMagic.Length) return false;
                file.Read(buffer, 0, FileMagic.Length);
                return Encoding.ASCII.GetString(buffer) == FileMagic;
            }
        }

        class Column
        {
            public Flags Flags;
            public string Name;
        }
        
        class FileIndex {
            public int id;
            public long offset;
            public uint size;
            public uint upSize;
            public string name;
        }

        [Flags]
        enum Flags : byte
        {
            StorageMask = 0xF0, StorageNone = 0x00, StorageZero = 0x10,
            StorageConstant = 0x30, Mask = 0x0F, Byte = 0x00,
            SByte = 0x01, UInt16 = 0x02, Int16 = 0x03,
            UInt32 = 0x04, Int32 = 0x05, UInt64 = 0x06,
            Int64 = 0x07, Float32 = 0x08, Float64 = 0x09,
            String = 0x0A, Data = 0x0B
        }
    }

    
}