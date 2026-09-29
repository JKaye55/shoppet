using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using ShoppetApp.Pages;
using ShoppetApp.Services;
using ShoppetApp.ViewModels;

namespace ShoppetApp;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Nunito-Regular.ttf", "Nunito");
                fonts.AddFont("Nunito-SemiBold.ttf", "NunitoSemiBold");
                fonts.AddFont("Nunito-Bold.ttf", "NunitoBold");
                fonts.AddFont("Nunito-ExtraBold.ttf", "NunitoExtraBold");
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            builder.UseMauiCommunityToolkitMediaElement(false);
        }
#else
        builder.UseMauiCommunityToolkitMediaElement(false);
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<ApiService>();

        // FIXED: Explicitly provide the local SQLite file path to DatabaseService
        builder.Services.AddSingleton<DatabaseService>(s => { var db = new DatabaseService(Path.Combine(FileSystem.AppDataDirectory, "shoppet.db3")); db.ApiService = s.GetRequiredService<ApiService>(); return db; });

        builder.Services.AddSingleton<CartService>();

        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<AuthPage>();
        builder.Services.AddTransient<AuthViewModel>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<ShopPage>();
        builder.Services.AddTransient<ShopViewModel>();
        builder.Services.AddTransient<PetsListPage>();
        builder.Services.AddTransient<PetsListViewModel>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<PetPassportPage>();
        builder.Services.AddTransient<PetPassportViewModel>();
        builder.Services.AddTransient<PetFormPage>();
        builder.Services.AddTransient<PetFormViewModel>();
        builder.Services.AddTransient<HealthLogFormPage>();
        builder.Services.AddTransient<HealthLogFormViewModel>();
        builder.Services.AddTransient<FoodLogFormPage>();
        builder.Services.AddTransient<FoodLogFormViewModel>();
        builder.Services.AddTransient<ContactFormPage>();
        builder.Services.AddTransient<ContactFormViewModel>();
        builder.Services.AddTransient<MessagesPage>();
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<MessagesViewModel>();
        builder.Services.AddTransient<ChatViewModel>();
        builder.Services.AddTransient<AppShell>();
        builder.Services.AddTransient<CommunityPage>();
        builder.Services.AddTransient<PostSettingsPage>();
        builder.Services.AddTransient<EditPostPage>();
        builder.Services.AddTransient<CommunityViewModel>();
        builder.Services.AddTransient<PostSettingsViewModel>();
        builder.Services.AddTransient<EditPostViewModel>();
        builder.Services.AddTransient<CreatePostPage>();
        builder.Services.AddTransient<EditProfilePage>();
        builder.Services.AddTransient<CreatePostViewModel>();
        builder.Services.AddTransient<EditProfileViewModel>();
        builder.Services.AddTransient<PostDetailsPage>();
        builder.Services.AddTransient<PostDetailsViewModel>();

        var app = builder.Build();
        Services = app.Services;
        return app;
    }
}










