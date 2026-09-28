using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShoppetApp.Models
{
    public partial class CommunityPost : ObservableObject
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? PetId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string ProfilePicture { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? ImageUrls { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsEdited { get; set; } = false;

        [ObservableProperty]
        private int _likesCount;

        [ObservableProperty]
        private int _commentsCount;

        [ObservableProperty]
        private bool _isLikedByMe;

        [ObservableProperty]
        private bool _isOptionsVisible;

        public List<string> ImageList => string.IsNullOrEmpty(ImageUrls)
            ? new List<string>()
            : ImageUrls.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        public bool HasImages => ImageList.Any();
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }

        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - Timestamp;
                if (span.TotalSeconds < 60) return "1m";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d";
                return Timestamp.ToString("MMM d");
            }
        }
        
        public string Initials => string.IsNullOrWhiteSpace(AuthorName) ? "U" : AuthorName.Substring(0, 1).ToUpper();
    }
}

