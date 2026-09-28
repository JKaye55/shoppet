using ShoppetApp.Services;

namespace ShoppetApp.Controls;

public partial class HeaderView : ContentView
{
    private IDispatcherTimer? _timer;

    public HeaderView()
    {
        InitializeComponent();
        
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(5);
        _timer.Tick += async (s, e) => await CheckUnreadMessages();
        _timer.Start();
    }

    private async Task CheckUnreadMessages()
    {
        try
        {
            int currentUserId = Preferences.Get("LoggedInUserId", 0);
            Console.WriteLine($"[BADGE] Checking unread for user {currentUserId}");
            if (currentUserId == 0) return;
            
            var api = MauiProgram.Services?.GetService<ApiService>();
            if (api == null) 
            {
                Console.WriteLine("[BADGE] API service is null!");
                return;
            }
            
            int count = await api.GetUnreadMessagesCountAsync(currentUserId);
            Console.WriteLine($"[BADGE] Unread count: {count}");
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Console.WriteLine($"[BADGE] Updating UI -> IsVisible: {count > 0}, Text: {count}");
                BadgeBorder.IsVisible = count > 0;
                BadgeLabel.Text = count > 99 ? "99+" : count.ToString();
            });
        }
        catch (Exception ex) 
        {
            Console.WriteLine($"[BADGE] Error: {ex.Message}");
        }
    }

    private async void OnMessagesClicked(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("messages");
    }
}
