using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels
{
    public partial class CommunityViewModel : ObservableObject
    {

        private readonly ApiService _api;
        private readonly DatabaseService _db;

        [ObservableProperty]
        private ObservableCollection<CommunityPost> _posts = new();

        [ObservableProperty]
        private bool _isRefreshing;

        [ObservableProperty]
        private string _userInitials = "U";

        [ObservableProperty]
        private string _userProfilePicture = string.Empty;

        public CommunityViewModel(ApiService api, DatabaseService db)
        {
            _api = api;
            _db = db;
        }

        [RelayCommand]
        public async Task LoadPostsAsync()
        {
            if (_db.CurrentUser != null)
            {
                UserInitials = string.IsNullOrWhiteSpace(_db.CurrentUser.FullName) ? "U" : _db.CurrentUser.FullName.Substring(0, 1).ToUpper();
                UserProfilePicture = _db.CurrentUser.ProfilePicture;
            }

            IsRefreshing = true;
            try
            {
                int userId = _db.CurrentUser?.Id ?? 0;
                var data = await _api.GetCommunityPostsAsync(userId);
                Posts.Clear();
                foreach (var p in data)
                {
                    Posts.Add(p);
                }
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        private async Task ToggleLikeAsync(CommunityPost post)
        {
            if (post == null || _db.CurrentUser == null) return;

            // Optimistic UI update
            post.IsLikedByMe = !post.IsLikedByMe;
            post.LikesCount += post.IsLikedByMe ? 1 : -1;

            int userId = _db.CurrentUser.Id;
            var newStatus = await _api.ToggleLikeAsync(post.Id, userId);
            
            // Sync with actual server status if it failed
            if (newStatus != post.IsLikedByMe)
            {
                post.IsLikedByMe = newStatus;
                post.LikesCount += post.IsLikedByMe ? 1 : -1;
            }
        }

        [RelayCommand]
        private async Task GoToCreatePostAsync()
        {
            await Shell.Current.GoToAsync("CreatePostPage");
        }

        [RelayCommand]
        private async Task GoToPostDetailsAsync(CommunityPost post)
        {
            if (post == null) return;
            var navParams = new Dictionary<string, object>{ { "Post", post } }; await Shell.Current.GoToAsync("PostDetailsPage", navParams);
        }
    }
}

