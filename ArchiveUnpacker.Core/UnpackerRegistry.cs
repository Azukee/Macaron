using System;
using System.Collections.Generic;
using System.Linq;

namespace ArchiveUnpacker.Core
{
    public static class UnpackerRegistry
    {
        private static readonly Dictionary<Type, Func<string, bool>> Conditions = new Dictionary<Type, Func<string, bool>>();

        public static void Register<T>(Func<string, bool> condition) where T : IUnpacker, new()
        {
            var type = typeof(T);
            if (Conditions.ContainsKey(type))
                throw new Exception($"Condition for unpacker {type} has already been registered.");

            Conditions.Add(type, condition);
        }

        public static IUnpacker Get(string gameDir)
        {
            var match = Conditions.SingleOrDefault(x => x.Value(gameDir));

            if (match.Key is null)
                return null;

            return (IUnpacker)Activator.CreateInstance(match.Key);
        }
    }
}
