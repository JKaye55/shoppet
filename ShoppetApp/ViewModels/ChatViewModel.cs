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
    public bool IsRead { get; set; }
    public string FormattedTime => Timestamp.ToLocalTime().ToString("HH:mm");
    public string ReadReceipt => IsMine ? (IsRead ? "✓✓" : "✓") : string.Empty;
    public Color ReadReceiptColor => IsRead ? Color.FromArgb("#4FC3F7") : Color.FromArgb("#AAAAAA");
    public bool ShowReadReceipt => IsMine;
    public string Initials => string.IsNullOrWhiteSpace(SenderName) ? "U" : SenderName.Substring(0, 1).ToUpper();
}

public partial class ChatViewModel : ObservableObject, IDisposable, Microsoft.Maui.Controls.IQueryAttributable
{
    private readonly ApiService _api;
    private IDispatcherTimer? _timer;
    private bool _isLoadingMessages;

    [ObservableProperty]
    public partial string ContactIdStr { get; set; } = string.Empty;

    public int ContactId => int.TryParse(ContactIdStr, out var id) ? id : 0;

    [ObservableProperty]
    public partial string ContactName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProfilePicture { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<ChatMessage> Messages { get; set; } = new();

    [ObservableProperty]
    public partial string NewMessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public ChatViewModel(ApiService api)
    {
        _api = api;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        StopPolling();
        Messages.Clear();
        _isLoadingMessages = false;

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
            var myId = Preferences.Get("LoggedInUserId", 0);

            if (myId > 0 && ContactId > 0)
            {
                await _api.ResetUnreadCountAsync(myId, ContactId);
            }

            var result = await _api.GetMessagesAsync(ContactId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var myName = Preferences.Get("LoggedInUserName", "Me");

                var ordered = result.OrderBy(m => m.Timestamp).ToList();
                var currentRealMessages = Messages.Where(m => !m.IsDivider).ToList();

                if (ordered.Count > 0 && ordered.Count == currentRealMessages.Count)
                {
                    bool isSame = true;
                    // Because Messages is now reversed, we compare it backwards
                    // currentRealMessages is Newest-to-Oldest, ordered is Oldest-to-Newest
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        var currentMsg = currentRealMessages[currentRealMessages.Count - 1 - i];
                        if (ordered[i].Timestamp != currentMsg.Timestamp ||
                            ordered[i].Text != currentMsg.Text ||
                            ordered[i].IsRead != currentMsg.IsRead)
                        {
                            isSame = false;
                            break;
                        }
                    }
                    if (isSame) return;
                }

                var newMessages = new ObservableCollection<ChatMessage>();
                DateTime? lastDate = null;
                foreach (var m in ordered)
                {
                    var localTime = m.Timestamp.ToLocalTime();
                    if (lastDate == null || lastDate.Value.Date != localTime.Date)
                    {
                        newMessages.Add(new ChatMessage
                        {
                            IsDivider = true,
                            DividerText = localTime.ToString("MMM dd, yyyy")
                        });
                        lastDate = localTime;
                    }
                    m.IsMine = m.SenderId == myId;
                    m.SenderName = m.IsMine ? myName : ContactName;
                    m.ProfilePicture = m.IsMine ? string.Empty : ProfilePicture;
                    newMessages.Add(m);
                }

                // Reverse the collection because the CollectionView is inverted (ScaleY="-1")
                Messages = new ObservableCollection<ChatMessage>(newMessages.Reverse());
            });
        }
        catch { }
        finally { _isLoadingMessages = false; }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (ContactId == 0 || IsBusy) return;

        var cleanText=(NewMessageText??string.Empty).Trim();
        if(cleanText.Length==0) return;

        if(cleanText.Length>1000)
        {
            await Shell.Current.DisplayAlertAsync("Message too long","Messages can contain up to 1,000 characters.","OK");
            return;
        }

        var senderId=Preferences.Get("LoggedInUserId",0);
        if(senderId<=0)
        {
            await Shell.Current.DisplayAlertAsync("Sign in required","Please sign in again before sending a message.","OK");
            return;
        }

        var tempMsg = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = ContactId,
            Text = cleanText,
            Timestamp = DateTime.UtcNow,
            IsMine = true,
            SenderName = Preferences.Get("LoggedInUserName", "Me")
        };
        
        // Insert at index 0 because the list is visually inverted
        Messages.Insert(0, tempMsg); 

        var textToSend = cleanText;
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