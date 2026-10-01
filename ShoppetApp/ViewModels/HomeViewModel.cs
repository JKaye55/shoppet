using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Messages;
using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Collections.ObjectModel;

namespace ShoppetApp.ViewModels;

public partial class HomeViewModel : ObservableObject, IRecipient<DataChangedMessage>
{
    private readonly DatabaseService _db;
    private readonly ApiService _api;

    [ObservableProperty] private ObservableCollection<Pet> _pets = [];
    [ObservableProperty] private ObservableCollection<HealthLog> _actionRequiredLogs = [];
    [ObservableProperty] private ObservableCollection<StoryCard> _stories = [];
    [ObservableProperty] private ObservableCollection<CommunityRanking> _rankings = [];
    [ObservableProperty] private StoryCard? _activeStory;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPremium;

    public string Greeting
    {
        get
        {
            var hour = DateTime.Now.Hour;
            return hour switch
            {
                < 12 => "Good morning",
                < 17 => "Good afternoon",
                _ => "Good evening"
            };
        }
    }

    public HomeViewModel(DatabaseService db, ApiService api)
    {
        _db = db;
        _api = api;
        WeakReferenceMessenger.Default.Register(this);
        InitializeStories();
    }

    public void Receive(DataChangedMessage message) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    public void InitializeStories()
    {
        Stories =
        [
            new StoryCard
            {
                Title = "Shoppet Care",
                Text = "Your complete pet health companion",
                Image = "hero.png",
                Rotation = 5,
                TranslationY = 14,
                TranslationX = 0,
                Scale = 0.92,
                Opacity = 0.7,
                ZIndex = 0,
                IsActive = false
            },
            new StoryCard
            {
                Title = "Always Prepared",
                Text = "Emergency contacts at your fingertips",
                Image = "image.png",
                Rotation = -3,
                TranslationY = 7,
                TranslationX = 0,
                Scale = 0.96,
                Opacity = 0.85,
                ZIndex = 1,
                IsActive = false
            },
            new StoryCard
            {
                Title = "Healthy & Happy",
                Text = "Track every milestone in your pet's life",
                Image = "https://images.unsplash.com/photo-1548199973-03cce0bbc87b?w=800",
                Rotation = 0,
                TranslationY = 0,
                TranslationX = 0,
                Scale = 1,
                Opacity = 1,
                ZIndex = 2,
                IsActive = true
            }
        ];
        ActiveStory = Stories.LastOrDefault();
    }

    public async Task LoadAsync()
    {
        if (IsBusy || _db == null)
            return;

        IsBusy = true;
        try
        {
            var pets = await _db.GetPetsAsync();
            Pets = new ObservableCollection<Pet>(pets ?? new List<Pet>());

            var logs = await _db.GetAllActionRequiredLogsAsync();
            ActionRequiredLogs = new ObservableCollection<HealthLog>(logs ?? new List<HealthLog>());

            try
            {
                var rankList = await _api.GetCommunityRankingsAsync(5);
                Rankings = new ObservableCollection<CommunityRanking>(rankList ?? new List<CommunityRanking>());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Rankings error: {ex.Message}");
            }

            try
            {
                var premiumStatus = await _api.GetPremiumStatusAsync();
                if (premiumStatus != null)
                {
                    IsPremium = premiumStatus.IsPremium;
                    Preferences.Set("LoggedInUserIsPremium", premiumStatus.IsPremium);
                }
                else
                {
                    IsPremium = Preferences.Get("LoggedInUserIsPremium", false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Premium status check error: {ex.Message}");
                IsPremium = Preferences.Get("LoggedInUserIsPremium", false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenWebPortalAsync()
    {
        var url = Preferences.Get("PublicWebBaseUrl", "https://localhost:7198");
        try
        {
            await Launcher.OpenAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] OpenWebPortal error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenUpgradePromptAsync()
    {
        await Shell.Current.GoToAsync("premium");
    }

    [RelayCommand]
    private async Task OpenCommunityRankingsAsync()
    {
        await Shell.Current.GoToAsync("//community");
    }

    [RelayCommand]
    private async Task MarkDoneAsync(HealthLog log)
    {
        if (log == null) return;
        log.Completed = true;
        await _db.SaveHealthLogAsync(log);
        WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
    }

    [RelayCommand]
    private async Task OpenPetAsync(Pet pet)
    {
        if (pet != null)
            await Shell.Current.GoToAsync($"petpassport?petId={pet.Id}");
    }

    [RelayCommand]
    private async Task AddPetAsync() =>
        await Shell.Current.GoToAsync("petform");

    [RelayCommand]
    private async Task SeeAllPetsAsync() =>
        await Shell.Current.GoToAsync("//pets");

    [RelayCommand]
    private async Task OpenPassportAsync(HealthLog log)
    {
        if (log != null)
            await Shell.Current.GoToAsync($"petpassport?petId={log.PetId}");
    }
}