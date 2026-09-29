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
        public partial ObservableCollection<CommunityPost> MyPosts { get; set; } = new();

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public PostSettingsViewModel(ApiService api, DatabaseService db)
        {
            _api = api;
            _db = db;
        }

        [RelayCommand]
        public async Task LoadMyPostsAsync()
        {
            if (_db.CurrentUser == null) return;
            IsBusy = true;
            try
            {
                var allPosts = await _api.GetCommunityPostsAsync(_db.CurrentUser.Id);
                var myPosts = allPosts.Where(p => p.UserId == _db.CurrentUser.Id).ToList();
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
            if (post == null || _db.CurrentUser == null) return;
            post.IsLikedByMe = !post.IsLikedByMe;
            post.LikesCount += post.IsLikedByMe ? 1 : -1;
            await _api.ToggleLikeAsync(post.Id, _db.CurrentUser.Id);
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




