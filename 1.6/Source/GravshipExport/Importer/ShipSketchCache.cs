using RimWorld;
using System.Collections.Generic;

namespace GravshipExport
{
    static class ShipSketchCache
    {
        static readonly Dictionary<string, Sketch> _byKey = new Dictionary<string, Sketch>();

        static string Key(ShipLayoutDefV2 l) =>
            $"{l.defName}|{l.width}x{l.height}|ge({l.gravEngineX},{l.gravEngineZ})|rows:{l.rows?.Count ?? 0}";

        public static bool TryGet(ShipLayoutDefV2 l, out Sketch s)
        {
            if (_byKey.TryGetValue(Key(l), out var cached))
            {
                s = cached.DeepCopy(); // IMPORTANT: don’t return the cached instance
                return true;
            }
            s = null; return false;
        }

        public static void Put(ShipLayoutDefV2 l, Sketch s)
        {
            _byKey[Key(l)] = s.DeepCopy(); // store a safe copy
        }
    }

}
