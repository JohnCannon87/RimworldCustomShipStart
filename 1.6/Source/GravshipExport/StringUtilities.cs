namespace GravshipExport
{
    internal static class StringUtilities
    {
        public static string HardCut(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (text.Length <= maxChars)
            {
                return text;
            }

            return text.Substring(0, maxChars);
        }
    }
}
