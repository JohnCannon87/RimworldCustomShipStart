using Verse;

namespace GravshipExport
{
    internal sealed class ShipListItem
    {
        public ShipLayoutDefV2 Ship { get; set; }
        public bool IsExported { get; set; }
        public string ExportFilename { get; set; }
        public string SourceLabel { get; set; }
    }
}
