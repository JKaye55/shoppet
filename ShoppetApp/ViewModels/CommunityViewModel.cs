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
        public partial ObservableCollection<CommunityPost> Posts { get; set; } = new();

        [ObservableProperty]
        public partial bool IsRefreshing { get; set; }

        [ObservableProperty]
        public partial string UserInitials { get; set; } = "U";

        [ObservableProperty]
        public partial string UserProfilePicture { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string LoadError { get; set; } = string.Empty;

        public bool HasPosts => Posts.Count > 0;
        public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadError);

        public CommunityViewModel(ApiService api, DatabaseService db)
        {
            _api = api;
            _db = db;
            Posts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPosts));
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
            LoadError = string.Empty;
            OnPropertyChanged(nameof(HasLoadError));

            try
            {
                int userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
                if (userId <= 0)
                {
                    LoadError = "Please sign in again to load the Community.";
                    OnPropertyChanged(nameof(HasLoadError));
                    return;
                }

                var data = await _api.GetCommunityPostsAsync(userId);
                Posts.Clear();
                foreach (var p in data)
                    Posts.Add(p);

                OnPropertyChanged(nameof(HasPosts));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Community load error: {ex.Message}");
                LoadError = "Community could not be loaded. Pull down to try again.";
                OnPropertyChanged(nameof(HasLoadError));
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

