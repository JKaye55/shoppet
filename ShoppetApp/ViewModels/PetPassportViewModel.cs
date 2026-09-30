using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Messages;
using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Collections.ObjectModel;

namespace ShoppetApp.ViewModels;

public partial class PetPassportViewModel : ObservableObject, IQueryAttributable, IRecipient<DataChangedMessage>
{
    private readonly ApiService _api;

    [ObservableProperty] private Pet? _pet;

    partial void OnPetChanged(Pet? value)
    {
        OnPropertyChanged(nameof(PublicPetIdUrl));
        OnPropertyChanged(nameof(QrImageUrl));
        OnPropertyChanged(nameof(HasPetId));
    }

    public bool HasPetId => Pet is not null && !string.IsNullOrWhiteSpace(Pet.CardId);
    public string PublicPetIdUrl => HasPetId
        ? $"https://shoppetcare.com/pet/card/{Pet!.CardId}"
        : string.Empty;
    public string QrImageUrl => string.IsNullOrWhiteSpace(PublicPetIdUrl)
        ? string.Empty
        : $"https://quickchart.io/qr?size=220&text={Uri.EscapeDataString(PublicPetIdUrl)}";
    [ObservableProperty] private ObservableCollection<HealthLog> _healthLogs = [];
    [ObservableProperty] private ObservableCollection<FoodLog> _foodLogs = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasLoadError;
    [ObservableProperty] private string _loadErrorMessage = string.Empty;

    public int PetId { get; private set; }

    public PetPassportViewModel(ApiService api)
    {
        _api = api;
        WeakReferenceMessenger.Default.Register(this);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("petId", out var value))
            PetId = Convert.ToInt32(value);
    }

    public void Receive(DataChangedMessage message) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

    public async Task LoadAsync()
    {
        if (PetId <= 0 || IsBusy)
            return;

        IsBusy = true;
        HasLoadError = false;
        LoadErrorMessage = string.Empty;
        try
        {
            Pet = (await _api.GetPetsAsync()).FirstOrDefault(p => p.Id == PetId);
            if (Pet is null)
                throw new InvalidOperationException("The selected pet was not returned by the shared database.");
            
            var logs = await _api.GetHealthLogsAsync(PetId);
            var sortedHLogs = logs
                .OrderBy(x => x.Completed)
                .ThenBy(x => x.Completed ? DateTime.MaxValue : (DateTime.TryParse(x.DueDate, out var dt) ? dt : DateTime.MaxValue))
                .ThenByDescending(x => x.CompletedAt ?? DateTime.MinValue)
                .ToList();
            HealthLogs = new ObservableCollection<HealthLog>(sortedHLogs);
            
            var foodLogs = await _api.GetFoodLogsAsync(PetId);
            var sortedFLogs = foodLogs
                .OrderBy(x => x.IsCompleted)
                .ThenBy(x => x.IsCompleted ? DateTime.MaxValue : (x.NextFeedingAt ?? DateTime.MaxValue))
                .ThenByDescending(x => x.CompletedAt ?? DateTime.MinValue)
                .ToList();
            FoodLogs = new ObservableCollection<FoodLog>(sortedFLogs);
        }
        catch (Exception ex)
        {
            HasLoadError = true;
            LoadErrorMessage = "Could not load this pet's shared health and feeding data. Check ShoppetAPI and retry.";
            System.Diagnostics.Debug.WriteLine($"Pet passport load failed: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RetryAsync() => await LoadAsync();

    [RelayCommand]
    private async Task GoBackAsync() =>
        await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task CopyPetIdAsync()
    {
        if (!HasPetId) return;
        await Clipboard.Default.SetTextAsync(PublicPetIdUrl);
        await Shell.Current.DisplayAlertAsync("Digital Pet ID", "Public Pet ID link copied.", "OK");
    }

    [RelayCommand]
    private async Task SharePetIdAsync()
    {
        if (!HasPetId) return;
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = $"{Pet!.Name}'s Digital Pet ID",
            Text = $"ShoppetCare Digital Pet ID\n{Pet.CardId}\n{PublicPetIdUrl}"
        });
    }

    [RelayCommand]
    private async Task EditPetAsync()
    {
        if (Pet is null)
            return;
        await Shell.Current.GoToAsync($"petform?petId={Pet.Id}");
    }

    [RelayCommand]
    private async Task AddLogAsync()
    {
        if (Pet is null)
            return;
        await Shell.Current.GoToAsync($"healthlogform?petId={Pet.Id}");
    }

    [RelayCommand]
    private async Task EditLogAsync(HealthLog log) =>
        await Shell.Current.GoToAsync($"healthlogform?petId={PetId}&logId={log.Id}");

    [RelayCommand]
    private async Task MarkDoneAsync(HealthLog log)
    {
        if (log == null || log.Completed) return;

        string nextDueDate = log.DueDate;
        bool shouldComplete = false;

        if (log.Type == "vaccine")
        {
            if (DateTime.TryParse(log.DueDate, out var currentDue))
            {
                if (log.ValidityUnit == "Years")
                    nextDueDate = currentDue.AddYears(log.ValidityInterval).ToString("yyyy/MM/dd, HH:mm:ss");
                else if (log.ValidityUnit == "Weeks")
                    nextDueDate = currentDue.AddDays(log.ValidityInterval * 7).ToString("yyyy/MM/dd, HH:mm:ss");
                else // Months default
                    nextDueDate = currentDue.AddMonths(log.ValidityInterval).ToString("yyyy/MM/dd, HH:mm:ss");
            }
        }
        else if (log.Type == "medication")
        {
            if (DateTime.TryParse(log.DueDate, out var currentDue))
            {
                nextDueDate = currentDue.AddHours(log.MedicationIntervalHours).ToString("yyyy/MM/dd, HH:mm:ss");
            }
            
            if (log.DosageRemaining > 0)
                log.DosageRemaining--;
                
            if (log.DosageRemaining <= 0)
                shouldComplete = true;
        }
        else // Checkup
        {
            shouldComplete = true;
        }

        if (shouldComplete)
        {
            await _api.CompleteHealthLogAsync(PetId, log.Id, nextDueDate);
        }
        else
        {
            log.DueDate = nextDueDate;
            await _api.SaveHealthLogAsync(PetId, log);
        }
        
        WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
    }
    
    [RelayCommand]
    private async Task MarkCompletedAsync(HealthLog log)
    {
        if (log == null) return;
        await _api.CompleteHealthLogAsync(PetId, log.Id, log.DueDate);
        WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
    }

    [RelayCommand]
    private async Task AddFoodLogAsync()
    {
        if (Pet is null)
            return;
        await Shell.Current.GoToAsync($"foodlogform?petId={Pet.Id}");
    }

    [RelayCommand]
    private async Task EditFoodLogAsync(FoodLog log) =>
        await Shell.Current.GoToAsync($"foodlogform?petId={PetId}&logId={log.Id}");

    [RelayCommand]
    private async Task MarkFedDoneAsync(FoodLog log)
    {
        if (log is null) return;
        await _api.CompleteFoodLogAsync(PetId, log.Id);
        WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
    }
}



