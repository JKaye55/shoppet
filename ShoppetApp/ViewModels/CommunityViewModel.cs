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
    [ObservableProperty] private ObservableCollection<CommunityRanking> _rankings = new();
    [ObservableProperty] private bool _showRankingsTab;
    [ObservableProperty] private Color _feedTabBg = Color.FromArgb("#FFFFFF");
    [ObservableProperty] private Color _feedTabText = Color.FromArgb("#173D3D");
    [ObservableProperty] private Color _rankingsTabBg = Colors.Transparent;
    [ObservableProperty] private Color _rankingsTabText = Color.FromArgb("#788782");
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _userInitials = "U";
    [ObservableProperty] private string _userProfilePicture = string.Empty;

    public bool HasPosts => Posts.Count > 0;
    public bool HasRankings => Rankings.Count > 0;
    public bool IsEmpty => !IsLoading && !HasError && Posts.Count == 0 && !ShowRankingsTab;

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnHasErrorChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnShowRankingsTabChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    public CommunityViewModel(ApiService api, DatabaseService db)
    {
        _api = api;
        _db = db;
        Posts.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasPosts));
            OnPropertyChanged(nameof(IsEmpty));
        };
        Rankings.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasRankings));
        };
    }

    [RelayCommand]
    public void SelectTab(string tab)
    {
        bool isRankings = string.Equals(tab, "Rankings", StringComparison.OrdinalIgnoreCase);
        ShowRankingsTab = isRankings;
        FeedTabBg = isRankings ? Colors.Transparent : Color.FromArgb("#FFFFFF");
        FeedTabText = isRankings ? Color.FromArgb("#788782") : Color.FromArgb("#173D3D");
        RankingsTabBg = isRankings ? Color.FromArgb("#FFFFFF") : Colors.Transparent;
        RankingsTabText = isRankings ? Color.FromArgb("#173D3D") : Color.FromArgb("#788782");
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

            var dataTask = _api.GetCommunityPostsAsync(userId);
            var rankingsTask = _api.GetCommunityRankingsAsync(15);

            await Task.WhenAll(dataTask, rankingsTask);

            var data = await dataTask;
            var rankList = await rankingsTask;

            Posts.Clear();
            foreach (var post in data.OrderByDescending(p => p.Timestamp))
                Posts.Add(post);

            Rankings.Clear();
            foreach (var r in rankList)
                Rankings.Add(r);
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
            await Shell.Current.DisplayAlertAsync("Sign in required", "Please sign in to like community posts.", "OK");
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
            await Shell.Current.DisplayAlertAsync("Sign in required", "Please sign in before creating a post.", "OK");
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
