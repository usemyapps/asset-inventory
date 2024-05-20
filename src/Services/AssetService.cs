using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace AssetInventory.Services
{
    public class AssetService
    {
        private readonly IAmazonDynamoDB _dynamoDBClient;
        private readonly DynamoDBContext _context;

        public AssetService(IAmazonDynamoDB dynamoDBClient)
        {
            _dynamoDBClient = dynamoDBClient;
            _context = new DynamoDBContext(_dynamoDBClient);
        }

        public async Task<List<Asset>> GetAssetsAsync()
        {
            return await _context.ScanAsync<Asset>(new List<ScanCondition>()).GetRemainingAsync();
        }

        public async Task<Asset> GetAssetByIdAsync(string id)
        {
            return await _context.LoadAsync<Asset>(id);
        }

        public async Task CreateAssetAsync(Asset asset)
        {
            if (await IsSerialNumberUnique(asset.SerialNumber) && await IsAssetTagUnique(asset.AssetTag))
            {
                await _context.SaveAsync(asset);
            }
            else
            {
                throw new InvalidOperationException("Serial Number or Asset Tag already exists.");
            }        
        }

        public async Task UpdateAssetAsync(Asset updatedAsset)
        {
            var existingAsset = await _context.LoadAsync<Asset>(updatedAsset.Id);
            if (existingAsset != null)
            {
                if (existingAsset.Location != updatedAsset.Location)
                {
                    updatedAsset.LocationHistory.Add(new LocationChange(existingAsset.Location));
                }
                if (await IsSerialNumberUnique(updatedAsset.SerialNumber, updatedAsset.Id) && await IsAssetTagUnique(updatedAsset.AssetTag, updatedAsset.Id))
                {
                    await _context.SaveAsync(updatedAsset);
                }
                else
                {
                    throw new InvalidOperationException("Serial Number or Asset Tag already exists.");
                }
            }
        }

        public async Task ArchiveAssetAsync(string id)
        {
            var asset = await _context.LoadAsync<Asset>(id);
            if (asset != null)
            {
                asset.IsArchived = true;
                await _context.SaveAsync(asset);
            }
        }

        public async Task DeleteAssetAsync(string id)
        {
            await _context.DeleteAsync<Asset>(id);
        }

        public async Task<List<Asset>> GetArchivedAssetsAsync()
        {
            return await _context.ScanAsync<Asset>(new List<ScanCondition>
            {
                new ScanCondition("IsArchived", ScanOperator.Equal, true)
            }).GetRemainingAsync();
        }

        private async Task<bool> IsSerialNumberUnique(string serialNumber, string existingId = null)
        {
            var scanConditions = new List<ScanCondition>
            {
                new ScanCondition("SerialNumber", ScanOperator.Equal, serialNumber)
            };
            var existingAssets = await _context.ScanAsync<Asset>(scanConditions).GetRemainingAsync();
            return existingAssets.All(a => a.Id == existingId);
        }

        private async Task<bool> IsAssetTagUnique(string assetTag, string existingId = null)
        {
            var scanConditions = new List<ScanCondition>
            {
                new ScanCondition("AssetTag", ScanOperator.Equal, assetTag)
            };
            var existingAssets = await _context.ScanAsync<Asset>(scanConditions).GetRemainingAsync();
            return existingAssets.All(a => a.Id == existingId);
        }
    
        public async Task<List<Asset>> GetAssetsForNextFourQuartersAsync()
        {
            var assets = await GetAssetsAsync();
            var currentDate = DateTime.UtcNow;
            var nextFourQuarters = new List<Asset>();

            foreach (var asset in assets)
            {
                if (asset.ReplaceDate > currentDate && asset.ReplaceDate <= currentDate.AddMonths(12))
                {
                    nextFourQuarters.Add(asset);
                }
            }

            return nextFourQuarters;
        }
    }

    public class LocationChange
    {
        public string PreviousLocation { get; set; }
        public DateTime ChangeTimestamp { get; set; }

        public LocationChange() { }

        public LocationChange(string previousLocation)
        {
            PreviousLocation = previousLocation;
            ChangeTimestamp = DateTime.UtcNow;
        }
    }

    public class Asset
    {
        [DynamoDBHashKey]
        [Required]
        public string Id { get; set; }
        [Required(ErrorMessage = "Type is required.")]
        [MaxLength(10, ErrorMessage = "Type cannot exceed 10 characters.")]
        [RegularExpression(@"^([A-Z][a-z]*)(\s[A-Z][a-z]*)*$", ErrorMessage = "Type must start with an uppercase letter, and each word must start with an uppercase letter.")]
        public string Type { get; set; }
        [Required(ErrorMessage = "Serial Number is required.")]
        [Display(Name = "S/N")]
        [MaxLength(20, ErrorMessage = "Serial Number cannot exceed 20 characters.")]
        [RegularExpression(@"^[A-Z0-9]*$", ErrorMessage = "Serial Number must contain only uppercase letters and numbers.")]
        public string SerialNumber { get; set; }
        [Required(ErrorMessage = "Asset Tag is required.")]
        [Display(Name = "Asset Tag")]
        [MaxLength(8, ErrorMessage = "Asset Tag cannot exceed 8 characters.")]
        public string AssetTag { get; set; }
        [Required(ErrorMessage = "Manufacturer is required.")]
        [RegularExpression(@"^[A-Z]+[a-zA-Z]*$", ErrorMessage = "Manufacturer must start with an uppercase letter and contain only letters.")]
        public string Manufacturer {get; set; }
        [Required(ErrorMessage = "Model is required.")]
        [RegularExpression(@"^[A-Z][a-zA-Z0-9\s]*$", ErrorMessage = "Model must start with an uppercase letter and contain only letters, numbers, and spaces.")]
        public string Model { get; set; }
        [Required(ErrorMessage = "Purchase Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Purchase Date")]
        public DateTime PurchaseDate { get; set; }
        [DynamoDBIgnore]
        [Display(Name = "Replace Date")]
        public DateTime ReplaceDate
        {
            get { return PurchaseDate.AddYears(3); }
        }
        [Required(ErrorMessage = "Location is required.")]
        [RegularExpression(@"^([A-Z][a-z]*)(\s[A-Z][a-z]*)*$", ErrorMessage = "Location must start with an uppercase letter, and each word must start with an uppercase letter.")]
        public string Location { get; set; }
        [DynamoDBProperty]
        [Display(Name = "Location History")]
        public List<LocationChange> LocationHistory { get; set; } = new List<LocationChange>();
    
        [MaxLength(200, ErrorMessage = "Notes cannot exceed 200 characters.")]
        public string? Notes { get; set; }
        public bool IsArchived {get; set; } = false;

        public Asset()
        {
            Id = Guid.NewGuid().ToString();
        }
    }
}
