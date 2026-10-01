using ShoppetApp.Helpers;

namespace ShoppetApp.Pages;

public partial class SplashPage : ContentPage
{
    private bool _navigated;

    public SplashPage()
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await Task.WhenAll(
            SplashContent.FadeToAsync(1, 500, Easing.SinOut),
            SplashContent.ScaleToAsync(1, 500, Easing.SinOut));

        await Task.Delay(2000);

        if (_navigated)
            return;

        _navigated = true;

        // Check if user session already exists locally
        int savedUserId = Preferences.Get("LoggedInUserId", 0);

        var token=await SecureStorage.GetAsync("ApiToken");
        if (savedUserId > 0 && !string.IsNullOrWhiteSpace(token))
        {
            App.Services.GetRequiredService<ShoppetApp.Services.ApiService>().SetToken(token);
            App.Services.GetRequiredService<ShoppetApp.Services.DatabaseService>().CurrentUser=new ShoppetApp.Models.User { Id=savedUserId, FullName=Preferences.Get("LoggedInUserName",""), Role=Preferences.Get("LoggedInUserRole","Pet Owner") };
            // Already logged in! Bypass onboarding/login and go straight to the main app shell
            NavigationHelper.SetRoot(new AppShell());
        }
        else
        {
            // Not logged in, send them to onboarding/login
            var onboarding = App.Services.GetRequiredService<OnboardingPage>();
            NavigationHelper.SetRoot(new NavigationPage(onboarding)
            {
                BarBackgroundColor = Colors.Transparent,
                BarTextColor = Color.FromArgb("#4a7c82")
            });
        }
    }
}