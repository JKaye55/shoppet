using CommunityToolkit.Mvvm.ComponentModel;

namespace ShoppetApp.Models
{
    public partial class MarketplaceListing : ObservableObject
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string SellerName { get; set; } = string.Empty;
        public string SellerProfilePic { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = "General";
        public string Condition { get; set; } = "Used";
        public string Location { get; set; } = string.Empty;
        public string? ImageUrls { get; set; }
        public bool IsAvailable { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string FacebookUrl { get; set; } = string.Empty;
        public string InstagramUrl { get; set; } = string.Empty;
        public string OtherSocialUrl { get; set; } = string.Empty;

        public bool HasSocialLinks =>
            !string.IsNullOrWhiteSpace(FacebookUrl) ||
            !string.IsNullOrWhiteSpace(InstagramUrl) ||
            !string.IsNullOrWhiteSpace(OtherSocialUrl);

        [ObservableProperty]
        private bool _isOptionsVisible;

        public List<string> ImageList => string.IsNullOrEmpty(ImageUrls)
            ? new List<string>()
            : ImageUrls.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

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
    }
}
