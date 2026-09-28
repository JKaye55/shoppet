using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels;

public class ChatMessage
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsMine { get; set; }
        public string SenderName { get; set; } = string.Empty;
    public bool IsDivider { get; set; }
    public string DividerText { get; set; } = string.Empty;
    public string ProfilePicture { get; set; } = string.Empty;
    public string FormattedTime => Timestamp.ToLocalTime().ToString("HH:mm");
    public string Initials => string.IsNullOrWhiteSpace(SenderName) ? "U" : SenderName.Substring(0, 1).ToUpper();
}

public partial class ChatViewModel : ObservableObject, IDisposable, Microsoft.Maui.Controls.IQueryAttributable
{
    private readonly ApiService _api;
    private IDispatcherTimer? _timer;

    [ObservableProperty]
    private string _contactIdStr = string.Empty;

    public int ContactId => int.TryParse(ContactIdStr, out var id) ? id : 0;

    [ObservableProperty]
    private string _contactName = string.Empty;

    [ObservableProperty]
    private string _profilePicture = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    [ObservableProperty]
    private string _newMessageText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;
    
    private bool _isLoadingMessages;

    public ChatViewModel(ApiService api)
    {
        _api = api;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ContactId", out var cid)) ContactIdStr = cid?.ToString() ?? string.Empty;
        if (query.TryGetValue("ContactName", out var cn)) ContactName = cn?.ToString() ?? string.Empty;
        if (query.TryGetValue("ProfilePicture", out var pp)) ProfilePicture = pp?.ToString() ?? string.Empty;
        _ = LoadMessagesAsync();
        StartPolling();
    }
    
    public void StartPolling()
    {
        if (_timer != null) return;
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(3);
        _timer.Tick += async (s, e) => await LoadMessagesAsync();
        _timer.Start();
    }
    
    public void StopPolling()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }
    }

    [RelayCommand]
    public async Task LoadMessagesAsync()
    {
        if (ContactId == 0 || _isLoadingMessages) return;
        _isLoadingMessages = true;
        try
        {
            var result = await _api.GetMessagesAsync(ContactId);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                                                var myId = Preferences.Get("LoggedInUserId", 0);
                var myName = Preferences.Get("LoggedInUserName", "Me");
                
                var ordered = result.OrderBy(m => m.Timestamp).ToList();
                var currentRealMessages = Messages.Where(m => !m.IsDivider).ToList();

                if (ordered.Count <= currentRealMessages.Count)
                {
                    bool isSame = true;
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        if (ordered[i].Timestamp != currentRealMessages[i].Timestamp || ordered[i].Text != currentRealMessages[i].Text)
                        {
                            isSame = false; break;
                        }
                    }
                    if (isSame) return;
                }

                Messages.Clear();
                DateTime? lastDate = null;
                foreach(var m in ordered)
                {
                    var localTime = m.Timestamp.ToLocalTime();
                    if (lastDate == null || lastDate.Value.Date != localTime.Date)
                    {
                        Messages.Add(new ChatMessage { 
                            IsDivider = true, 
                            DividerText = localTime.ToString("MMM dd, yyyy, HH:mm") 
                        });
                        lastDate = localTime;
                    }
                    m.IsMine = m.SenderId == myId;
                    m.SenderName = m.IsMine ? myName : ContactName;
                    m.ProfilePicture = m.IsMine ? string.Empty : ProfilePicture;
                    Messages.Add(m);
                }
            });
        }
        catch { }
        finally { _isLoadingMessages = false; }
    }

        [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(NewMessageText) || ContactId == 0 || IsBusy) return;
        
        // Optimistically add to UI
        var tempMsg = new ChatMessage
        {
            SenderId = Preferences.Get("LoggedInUserId", 0),
            ReceiverId = ContactId,
            Text = NewMessageText,
            Timestamp = DateTime.UtcNow,
            IsMine = true,
            SenderName = Preferences.Get("LoggedInUserName", "Me")
        };
        Messages.Add(tempMsg);
        
        var textToSend = NewMessageText;
        NewMessageText = string.Empty;
        
        IsBusy = true;
        try
        {
            var success = await _api.SendMessageAsync(ContactId, null, textToSend);
            if (!success)
            {
                Messages.Remove(tempMsg);
            }
        }
        catch 
        { 
            Messages.Remove(tempMsg);
        }
        finally { IsBusy = false; }
    } 

    [RelayCommand]
    private async Task GoBackAsync()
    {
        StopPolling();
        await Shell.Current.GoToAsync("..");
    }
    
    public void Dispose()
    {
        StopPolling();
    }
}










