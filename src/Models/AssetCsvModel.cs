using System;

namespace AssetInventory.Models
{
    public class AssetCsvModel
    {
        public string Type { get; set; }
        public string SerialNumber { get; set; }
        public string AssetTag { get; set; }
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string Location { get; set; }
        public string Notes { get; set; }
        public bool IsArchived { get; set; } = false;
    }
}
