using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels;

public partial class Conversation : ObservableObject
{
    public int ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ProfilePicture { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    public partial int UnreadCount { get; set; }
    
    public bool HasUnread => UnreadCount > 0;
    public string Initials => string.IsNullOrWhiteSpace(ContactName) ? "U" : ContactName.Substring(0, 1).ToUpper();
    public string FormattedTime => Timestamp.ToString("MMM dd, HH:mm");
}

public partial class MessagesViewModel : ObservableObject
{
    private readonly ApiService _api;

    [ObservableProperty]
    public partial ObservableCollection<Conversation> Conversations { get; set; } = new();
    
    [ObservableProperty]
    public partial ObservableCollection<UserSearchResult> SearchResults { get; set; } = new();

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }
    
    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    public MessagesViewModel(ApiService api)
    {
        _api = api;
    }

    [RelayCommand]
    public async Task LoadConversationsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await _api.GetConversationsAsync();
            Conversations.Clear();
            foreach (var conv in result)
            {
                Conversations.Add(conv);
            }
        }
        catch { }
        finally { IsBusy = false; }
    }
    
    async partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            IsSearching = false;
            SearchResults.Clear();
            return;
        }
        
        IsSearching = true;
        try
        {
            var results = await _api.SearchUsersAsync(value);
            SearchResults.Clear();
            foreach (var user in results)
            {
                SearchResults.Add(user);
            }
        }
        catch { }
    }

    [RelayCommand]
    private async Task OpenChatAsync(Conversation conv)
    {
        if (conv == null) return;
        
        if (conv.UnreadCount > 0)
        {
            conv.UnreadCount = 0;
        }
        
        SearchQuery = string.Empty; // Clear search when opening chat
        await Shell.Current.GoToAsync("chat", new Dictionary<string, object>
        {
            { "ContactId", conv.ContactId.ToString() },
            { "ContactName", conv.ContactName },
            { "ProfilePicture", conv.ProfilePicture }
        });
    }
    
    [RelayCommand]
    private async Task StartChatWithUserAsync(UserSearchResult user)
    {
        if (user == null) return;
        SearchQuery = string.Empty; // Clear search
        await Shell.Current.GoToAsync("chat", new Dictionary<string, object>
        {
            { "ContactId", user.UserId.ToString() },
            { "ContactName", user.FullName },
            { "ProfilePicture", user.ProfilePicture }
        });
    }

    [RelayCommand]
    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}