namespace ShoppetApp.Helpers;

public static class ViewExtensions
{
    public static Task<bool> AnimateHeight(this VisualElement view, double from, double to, uint length = 300)
    {
        var tcs = new TaskCompletionSource<bool>();
        var animation = new Animation(v => view.HeightRequest = v, from, to);
        animation.Commit(view, "HeightAnimation", 16, length, Easing.SinInOut,
            (v, c) => tcs.TrySetResult(!c));
        return tcs.Task;
    }

    public static Task<bool> AnimateWidth(this VisualElement view, double from, double to, uint length = 200)
    {
        var tcs = new TaskCompletionSource<bool>();
        var animation = new Animation(v => view.WidthRequest = v, from, to);
        animation.Commit(view, "WidthAnimation", 16, length, Easing.SinInOut,
            (v, c) => tcs.TrySetResult(!c));
        return tcs.Task;
    }
}

public static class NavigationHelper
{
    public static void SetRoot(Page page)
    {
        if (Application.Current?.Windows.Count > 0)
            Application.Current.Windows[0].Page = page;
    }

    public static async Task GoToMainShellAsync()
    {
        var shell = App.Services.GetRequiredService<AppShell>();
        SetRoot(shell);
        await shell.GoToAsync("//home");
    }

    public static void GoToAuth()
    {
        App.Services.GetRequiredService<ShoppetApp.Services.ApiService>().ClearToken();
        App.Services.GetRequiredService<ShoppetApp.Services.DatabaseService>().CurrentUser=null;
        SecureStorage.Remove("ApiToken");
        Preferences.Remove("LoggedInUserId");Preferences.Remove("LoggedInUserName");Preferences.Remove("LoggedInUserRole");
        var auth = App.Services.GetRequiredService<Pages.AuthPage>();
        SetRoot(new NavigationPage(auth)
        {
            BarBackgroundColor = Colors.Transparent,
            BarTextColor = Color.FromArgb("#4a7c82")
        });
    }
}
