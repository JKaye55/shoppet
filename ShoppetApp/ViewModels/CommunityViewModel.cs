using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels;

public partial class CommunityViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly DatabaseService _db;

    [ObservableProperty] private ObservableCollection<CommunityPost> _posts = new();
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _userInitials = "U";
    [ObservableProperty] private string _userProfilePicture = string.Empty;

    public bool HasPosts => Posts.Count > 0;
    public bool IsEmpty => !IsLoading && !HasError && Posts.Count == 0;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnHasErrorChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    public CommunityViewModel(ApiService api, DatabaseService db)
    {
        _api = api;
        _db = db;
        Posts.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPosts));
            OnPropertyChanged(nameof(IsEmpty));
        };
    }

    [RelayCommand]
    public async Task LoadPostsAsync()
    {
        if (IsLoading) return;

        IsLoading = !IsRefreshing;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
            var userName = _db.CurrentUser?.FullName ?? Preferences.Get("LoggedInUserName", "User");

            UserInitials = string.IsNullOrWhiteSpace(userName)
                ? "U"
                : userName.Trim()[0].ToString().ToUpperInvariant();

            UserProfilePicture = _db.CurrentUser?.ProfilePicture ?? string.Empty;

            var data = await _api.GetCommunityPostsAsync(userId);

            Posts.Clear();
            foreach (var post in data.OrderByDescending(p => p.Timestamp))
                Posts.Add(post);
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = "Community could not be loaded. Make sure ShoppetAPI is running on port 5020, then try again.";
            System.Diagnostics.Debug.WriteLine($"Community load failed: {ex}");
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadPostsAsync();
    }

    [RelayCommand]
    private async Task RetryAsync() => await LoadPostsAsync();

    [RelayCommand]
    private async Task ToggleLikeAsync(CommunityPost? post)
    {
        if (post is null) return;

        var userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
        if (userId <= 0)
        {
            await Shell.Current.DisplayAlert("Sign in required", "Please sign in to like community posts.", "OK");
            return;
        }

        var oldLiked = post.IsLikedByMe;
        var oldCount = post.LikesCount;

        post.IsLikedByMe = !oldLiked;
        post.LikesCount = Math.Max(0, oldCount + (post.IsLikedByMe ? 1 : -1));

        try
        {
            var serverLiked = await _api.ToggleLikeAsync(post.Id, userId);
            if (serverLiked != post.IsLikedByMe)
            {
                post.IsLikedByMe = serverLiked;
                post.LikesCount = Math.Max(0, oldCount + (serverLiked ? 1 : 0) - (oldLiked ? 1 : 0));
            }
        }
        catch
        {
            post.IsLikedByMe = oldLiked;
            post.LikesCount = oldCount;
        }
    }

    [RelayCommand]
    private async Task GoToCreatePostAsync()
    {
        var userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
        if (userId <= 0)
        {
            await Shell.Current.DisplayAlert("Sign in required", "Please sign in before creating a post.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("CreatePostPage");
    }

    [RelayCommand]
    private async Task GoToPostDetailsAsync(CommunityPost? post)
    {
        if (post is null) return;
        await Shell.Current.GoToAsync("PostDetailsPage",
            new Dictionary<string, object> { ["Post"] = post });
    }
}
