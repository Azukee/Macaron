using CommandLine;

namespace ArchiveUnpacker.CommandLineParsing
{
    [Verb("detect", HelpText = "Detect the packer for a game folder")]
    public class DetectOptions
    {
        [Value(0, MetaName = "Game directory", HelpText = "The root directory of the game", Required = true)]
        public string Directory { get; set; }
    }
}
