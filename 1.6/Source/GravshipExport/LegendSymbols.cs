using System;

namespace GravshipExport
{
    public static class LegendSymbols
    {
        private static readonly string symbols;

        static LegendSymbols()
        {
            // Build full sequence: 0–9, a–z, A–Z
            symbols = "0123456789" +
                      "abcdefghijklmnopqrstuvwxyz" +
                      "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        }

        public static string GetSymbol(int index)
        {
            if (index < 0 || index >= symbols.Length)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"LegendSymbols only supports up to {symbols.Length} entries");

            return symbols[index].ToString();
        }

        public static int Count => symbols.Length;
    }
}
