using CommunityToolkit.Mvvm.ComponentModel;

namespace ShoppetApp.Models
{
    public partial class CommunityComment : ObservableObject
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public int UserId { get; set; }
        public int? ParentCommentId { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string ProfilePicture { get; set; } = string.Empty;
        public string AuthorRole { get; set; } = string.Empty;
        public string? ParentAuthorName { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        [ObservableProperty]
        private int _likeCount;

        [ObservableProperty]
        private bool _isLikedByMe;

        [ObservableProperty]
        private bool _canDelete;

        // Computed helpers
        public bool IsReply => ParentCommentId.HasValue;
        public bool HasParentQuote => IsReply && !string.IsNullOrEmpty(ParentAuthorName);

        public string TimeAgo
        {
            get
            {
                var span = DateTime.Now - CreatedAt;
                if (span.TotalSeconds < 60) return "1m";
                if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m";
                if (span.TotalHours < 24) return $"{(int)span.TotalHours}h";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d";
                return CreatedAt.ToString("MMM d");
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<CommunityComment> Replies { get; } = new();

        public CommunityComment TopLevelParent { get; set; }
        public System.Collections.Generic.List<CommunityComment> AllDescendants { get; set; } = new();
        
        [ObservableProperty]
        private int _visibleDescendantsCount;

        [ObservableProperty]
        private int _totalDescendantsCount;

        [ObservableProperty]
        private bool _isPaginatorVisible;

        [ObservableProperty]
        private string _paginatorText = string.Empty;

        [ObservableProperty]
        private bool _isHideVisible;
        
        [ObservableProperty]
        private int _depth;
        
        public Microsoft.Maui.Thickness ReplyMargin => new Microsoft.Maui.Thickness(Depth == 0 ? 0 : 46 + (Depth - 1) * 36, 0, 0, 10);
        
        public int AvatarSize => Depth == 0 ? 36 : 26;
        public int AvatarRadius => AvatarSize / 2;
        public string Initials => string.IsNullOrWhiteSpace(AuthorName) ? "U" : AuthorName.Substring(0, 1).ToUpper();
    }
}










