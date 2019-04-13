using System;
using System.Collections.Generic;
using System.Linq;

namespace ArchiveUnpacker.Framework
{
    internal static class UnpackerRegistry
    {
        private static Dictionary<Type, Func<string, bool>> conditions = new Dictionary<Type, Func<string, bool>>();

        public static void Register<T>(Func<string, bool> condition) where T : IUnpacker
        {
            var type = typeof(T);
            if (conditions.ContainsKey(type))
                throw new Exception($"Condition for unpacker {type} has already been registered.");

            conditions.Add(typeof(T), condition);
        }

        public static IUnpacker Get(string gameDir)
        {
            var match = conditions.FirstOrDefault(x => x.Value(gameDir));

            if (match.Key is null)
                return null;

            return Activator.CreateInstance(match.Key) as IUnpacker;
        }
    }
}
