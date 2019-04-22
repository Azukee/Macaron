using System;
using System.IO;
using System.Linq;
using ArchiveUnpacker.CommandLine.CommandLineParsing;
using ArchiveUnpacker.Core;
using ArchiveUnpacker.Unpackers;
using CommandLine;

namespace ArchiveUnpacker.CommandLine
{
    internal static class Program
    {
        private const string ExtractDirectory = "Extracted";

        private static void Main(string[] args)
        {
            // register the built-in unpackers
            BuiltInUnpackers.RegisterAll();

            if (args.Length == 1 && Directory.Exists(args[0]))
                Extract(new ExtractOptions { Directory = args[0] });
            else
                Parser.Default.ParseArguments<ExtractOptions, ListOptions, DetectOptions>(args)
                    .WithParsed<ExtractOptions>(Extract)
                    .WithParsed<ListOptions>(List)
                    .WithParsed<DetectOptions>(Detect);
        }

        private static void Extract(ExtractOptions opt)
        {
            // Get unpacker
            var unpacker = UnpackerRegistry.Get(opt.Directory);

            if (unpacker is null) {
                Console.WriteLine("Couldn't find an unpacker for this game/engine.");
                return;
            }

            foreach (IExtractableFile file in unpacker.LoadFiles(opt.Directory)) {
                if (file.Path is null) {
                    // TODO: make up your own path I guess
                    Console.WriteLine("File had no path, not extracting for now!");
                    continue;
                }

                Console.WriteLine("Extracting " + file.Path);

                // could add another directory to this for the game or something
                string fullPath = Path.Combine(Environment.CurrentDirectory, ExtractDirectory, file.Path);

                string fileDir = Path.GetDirectoryName(fullPath) ?? string.Empty;
                if (!Directory.Exists(fileDir))
                    Directory.CreateDirectory(fileDir);

                using (var stream = File.OpenWrite(fullPath))
                    file.WriteToStream(stream);
            }
        }

        private static void List(ListOptions opt)
        {
            // Get unpacker
            var unpacker = UnpackerRegistry.Get(opt.Directory);

            if (unpacker is null) {
                Console.WriteLine("Couldn't find an unpacker for this game/engine.");
                return;
            }

            foreach (IExtractableFile file in unpacker.LoadFiles(opt.Directory).OrderBy(x => x.Path))
                Console.WriteLine(file.Path);
        }

        private static void Detect(DetectOptions opt)
        {
            // Get unpacker
            var unpacker = UnpackerRegistry.Get(opt.Directory);

            if (unpacker is null) {
                Console.WriteLine("Couldn't find an unpacker for this game/engine.");
                return;
            }

            Console.WriteLine(unpacker.GetType().Name);
        }
    }
}
