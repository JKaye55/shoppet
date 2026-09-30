using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels
{
    public partial class PostSettingsViewModel : ObservableObject
    {
        private readonly ApiService _api;
        private readonly DatabaseService _db;

        [ObservableProperty]
        private ObservableCollection<CommunityPost> _myPosts = new();

        [ObservableProperty]
        private bool _isBusy;

        public PostSettingsViewModel(ApiService api, DatabaseService db)
        {
            _api = api;
            _db = db;
        }

        [RelayCommand]
        public async Task LoadMyPostsAsync()
        {
            var userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
            if (userId <= 0) return;
            IsBusy = true;
            try
            {
                var allPosts = await _api.GetCommunityPostsAsync(userId);
                var myPosts = allPosts.Where(p => p.UserId == userId).ToList();
                MyPosts.Clear();
                foreach (var p in myPosts) MyPosts.Add(p);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void TogglePostOptions(CommunityPost post)
        {
            if (post == null) return;
            post.IsOptionsVisible = !post.IsOptionsVisible;
            // Hide others
            foreach (var p in MyPosts) {
                if (p != post && p.IsOptionsVisible)
                    p.IsOptionsVisible = false;
            }
        }

        [RelayCommand]
        private async Task EditPostActionAsync(CommunityPost post)
        {
            if (post == null) return;
            post.IsOptionsVisible = false;
            await Shell.Current.GoToAsync($"EditPostPage", new Dictionary<string, object>
            {
                { "PostToEdit", post }
            });
        }

        [RelayCommand]
        private async Task DeletePostActionAsync(CommunityPost post)
        {
            if (post == null) return;
            post.IsOptionsVisible = false;
            bool confirm = await Shell.Current.DisplayAlert("Delete Post", "Are you sure you want to delete this post?", "Yes", "No");
            if (!confirm) return;

            bool success = await _api.DeletePostAsync(post.Id);
            if (success)
            {
                MyPosts.Remove(post);
            }
            else
            {
                await Shell.Current.DisplayAlert("Error", "Failed to delete post.", "OK");
            }
        }
        
        [RelayCommand]
        private async Task ToggleLikeAsync(CommunityPost post)
        {
            if (post == null) return;
            var userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
            if (userId <= 0) return;
            post.IsLikedByMe = !post.IsLikedByMe;
            post.LikesCount += post.IsLikedByMe ? 1 : -1;
            await _api.ToggleLikeAsync(post.Id, userId);
        }

        [RelayCommand]
        private async Task GoToPostDetailsAsync(CommunityPost post)
        {
            if (post == null) return;
            await Shell.Current.GoToAsync("PostDetailsPage", new Dictionary<string, object>
            {
                { "Post", post }
            });
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}




