using ArchiveUnpacker.Core;
using ArchiveUnpacker.Unpackers.Unpackers;

namespace ArchiveUnpacker.Unpackers
{
    public static class BuiltInUnpackers
    {
        public static void RegisterAll()
        {
            // Register all types
            UnpackerRegistry.Register<ArtemisUnpacker>(ArtemisUnpacker.IsGameFolder);
            UnpackerRegistry.Register<AIMSUnpacker>(AIMSUnpacker.IsGameFolder);
            UnpackerRegistry.Register<RenPyUnpacker>(RenPyUnpacker.IsGameFolder);
            UnpackerRegistry.Register<CatSystem2Unpacker>(CatSystem2Unpacker.IsGameFolder);
            UnpackerRegistry.Register<MajiroArcUnpacker>(MajiroArcUnpacker.IsGameFolder);
            UnpackerRegistry.Register<AdvHDUnpacker>(AdvHDUnpacker.IsGameFolder);
            UnpackerRegistry.Register<HyPackUnpacker>(HyPackUnpacker.IsGameFolder);
            UnpackerRegistry.Register<NekoPackUnpacker>(NekoPackUnpacker.IsGameFolder);
            UnpackerRegistry.Register<CriUnpacker>(CriUnpacker.IsGameFolder);
        }
    }
}
