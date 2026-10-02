using CommunityToolkit.Mvvm.ComponentModel;

namespace ShoppetApp.Models
{
    public partial class MarketplaceListing : ObservableObject
    {
        public int Id { get; set; }
        public int SellerUserId { get; set; }
        public int UserId { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public string SellerProfilePic { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = "General";
        public string ItemCondition { get; set; } = "Used";
        public string Condition { get; set; } = "Used";
        public string Location { get; set; } = string.Empty;
        public string? ImageUrls { get; set; }
        public string Status { get; set; } = "Active";
        public bool IsAvailable { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string FacebookUrl { get; set; } = string.Empty;
        public string InstagramUrl { get; set; } = string.Empty;
        public string OtherSocialUrl { get; set; } = string.Empty;
        public bool IsSellerPremium { get; set; }

        public bool HasSocialLinks =>
            !string.IsNullOrWhiteSpace(FacebookUrl) ||
            !string.IsNullOrWhiteSpace(InstagramUrl) ||
            !string.IsNullOrWhiteSpace(OtherSocialUrl);

        [ObservableProperty]
        private bool _isOptionsVisible;

        public List<string> ImageList
        {
            get
            {
                if (string.IsNullOrEmpty(ImageUrls)) return new List<string>();
                var publicWeb = Microsoft.Maui.Storage.Preferences.Get("PublicWebBaseUrl", "http://localhost:5253").TrimEnd('/');
#if ANDROID
                if (publicWeb.Contains("localhost")) publicWeb = publicWeb.Replace("localhost", "10.0.2.2");
                if (publicWeb.Contains("127.0.0.1")) publicWeb = publicWeb.Replace("127.0.0.1", "10.0.2.2");
#endif
                return ImageUrls.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(u =>
                    {
                        var trimmed = u.Trim();
                        if (trimmed.StartsWith("/")) trimmed = publicWeb + trimmed;
#if ANDROID
                        trimmed = trimmed.Replace("http://localhost:", "http://10.0.2.2:")
                                         .Replace("http://127.0.0.1:", "http://10.0.2.2:");
#endif
                        return trimmed;
                    }).ToList();
            }
        }

        public bool HasImages => ImageList.Any();
        public string FirstImage => ImageList.Any() ? ImageList[0] : string.Empty;

        public string PriceDisplay => $"₱{Price:N0}";
        public string Initials => string.IsNullOrWhiteSpace(SellerName) ? "U" : SellerName.Substring(0, 1).ToUpper();

        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - CreatedAt;
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return CreatedAt.ToString("MMM d");
            }
        }

        // Rubric E-Commerce Catalog Specifications
        public string Sku => $"SHP-{(string.IsNullOrWhiteSpace(Category) ? "GEN" : (Category.Length >= 3 ? Category.Substring(0, 3) : Category)).ToUpperInvariant()}-{Id:D4}";
        public string StockDisplay => IsAvailable ? "In Stock (1 available)" : "Out of Stock";
        public int StockQuantity => IsAvailable ? 1 : 0;
    }
}
