using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels;

public class Conversation
{
    public int ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
      public string ProfilePicture { get; set; } = string.Empty;
    public string LastMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Initials => string.IsNullOrWhiteSpace(ContactName) ? "U" : ContactName.Substring(0, 1).ToUpper();
    public string FormattedTime => Timestamp.ToString("MMM dd, HH:mm");
}

public partial class MessagesViewModel : ObservableObject
{
    private readonly ApiService _api;

    [ObservableProperty]
    private ObservableCollection<Conversation> _conversations = new();
    
    [ObservableProperty]
    private ObservableCollection<UserSearchResult> _searchResults = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isBusy;
    
    [ObservableProperty]
    private bool _isSearching;

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
