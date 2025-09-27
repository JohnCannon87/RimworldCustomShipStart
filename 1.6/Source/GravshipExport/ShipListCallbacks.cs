using System;

namespace GravshipExport
{
    internal sealed class ShipListCallbacks
    {
        public Action<ShipListItem> DeleteRequested { get; set; }
        public Action<ShipListItem> ApplyRequested { get; set; }
        public Action<ShipListItem> ExportRequested { get; set; }
    }
}
