using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Messages;
using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Globalization;

namespace ShoppetApp.ViewModels;

public partial class FoodLogFormViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiService _api;

    [ObservableProperty] public partial int PetId { get; set; }
    [ObservableProperty] public partial int LogId { get; set; }
    [ObservableProperty] public partial string FoodName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomFood))]
    public partial string SelectedFoodOption { get; set; } = "Dry Kibble";

    [ObservableProperty] public partial string CustomFoodName { get; set; } = string.Empty;
    [ObservableProperty] public partial DateTime FedDate { get; set; } = DateTime.Today;
    [ObservableProperty] public partial TimeSpan StartTime { get; set; } = DateTime.Now.TimeOfDay;
    [ObservableProperty] public partial bool IsEditMode { get; set; }
    [ObservableProperty] public partial string Notes { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial double AmountGramsValue { get; set; } = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomInterval))]
    public partial string FeedingIntervalPreset { get; set; } = "Every 8 hours";

    [ObservableProperty] public partial double CustomIntervalHours { get; set; } = 8;
    [ObservableProperty] public partial double CustomIntervalMinutes { get; set; }

    public IList<string> FoodOptions { get; } =
    [
        "Dry Kibble",
        "Wet / Canned Food",
        "Prescription Diet",
        "Puppy / Kitten Food",
        "Homemade Meal",
        "Raw Food",
        "Treats / Snacks",
        "Milk / Formula",
        "Other / Custom"
    ];

    public IList<string> FeedingIntervalOptions { get; } =
    [
        "Every 4 hours",
        "Every 6 hours",
        "Every 8 hours",
        "Every 12 hours",
        "Once a day",
        "Custom"
    ];

    public bool IsCustomFood => SelectedFoodOption == "Other / Custom";
    public bool IsCustomInterval => FeedingIntervalPreset == "Custom";
    public string Title => IsEditMode ? "Edit Food Log" : "Add Food Log";
    public bool CanDelete => IsEditMode;

    public FoodLogFormViewModel(ApiService api) => _api = api;

    partial void OnFeedingIntervalPresetChanged(string value)
    {
        switch (value)
        {
            case "Every 4 hours": CustomIntervalHours = 4; CustomIntervalMinutes = 0; break;
            case "Every 6 hours": CustomIntervalHours = 6; CustomIntervalMinutes = 0; break;
            case "Every 8 hours": CustomIntervalHours = 8; CustomIntervalMinutes = 0; break;
            case "Every 12 hours": CustomIntervalHours = 12; CustomIntervalMinutes = 0; break;
            case "Once a day": CustomIntervalHours = 24; CustomIntervalMinutes = 0; break;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("petId", out var pet))
            PetId = Convert.ToInt32(pet);
        if (query.TryGetValue("logId", out var log))
            LogId = Convert.ToInt32(log);
    }

    public async Task LoadAsync()
    {
        if (LogId <= 0)
        {
            IsEditMode = false;
            FedDate = DateTime.Today;
            StartTime = DateTime.Now.TimeOfDay;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(CanDelete));
            return;
        }

        var log = (await _api.GetFoodLogsAsync(PetId)).FirstOrDefault(l => l.Id == LogId);
        if (log is null) return;

        IsEditMode = true;
        PetId = log.PetId;
        FoodName = log.FoodName ?? string.Empty;
        if (FoodOptions.Contains(FoodName))
        {
            SelectedFoodOption = FoodName;
            CustomFoodName = string.Empty;
        }
        else
        {
            SelectedFoodOption = "Other / Custom";
            CustomFoodName = FoodName;
        }

        AmountGramsValue = Math.Clamp(log.AmountGrams > 0 ? log.AmountGrams : 100, 1, 5000);
        CustomIntervalHours = Math.Clamp((double)log.IntervalHours, 0, 24);
        CustomIntervalMinutes = Math.Clamp((double)log.IntervalMinutes, 0, 59);
        FeedingIntervalPreset = ((int)CustomIntervalHours, (int)CustomIntervalMinutes) switch
        {
            (4, 0) => "Every 4 hours",
            (6, 0) => "Every 6 hours",
            (8, 0) => "Every 8 hours",
            (12, 0) => "Every 12 hours",
            (24, 0) => "Once a day",
            _ => "Custom"
        };
        Notes = log.Notes ?? string.Empty;

        if (DateTime.TryParse(log.FedDate, out var fedDate))
            FedDate = fedDate.Date;

        if (!string.IsNullOrEmpty(log.StartTimestamp) &&
            DateTime.TryParse(log.StartTimestamp, null, DateTimeStyles.RoundtripKind, out var startDt))
            StartTime = startDt.TimeOfDay;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task CloseAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        var cleanName = IsCustomFood
            ? (CustomFoodName ?? string.Empty).Trim()
            : (SelectedFoodOption ?? string.Empty).Trim();

        FoodName = cleanName;
        var cleanNotes = (Notes ?? string.Empty).Trim();

        if (cleanName.Length < 2 || cleanName.Length > 80)
        {
            await Shell.Current.DisplayAlertAsync("Check food name", "Food name must be between 2 and 80 characters.", "OK");
            return;
        }

        if (AmountGramsValue < 1 || AmountGramsValue > 5000)
        {
            await Shell.Current.DisplayAlertAsync("Check serving amount", "Serving amount must be between 1 and 5,000 grams.", "OK");
            return;
        }

        if (cleanNotes.Length > 500)
        {
            await Shell.Current.DisplayAlertAsync("Notes too long", "Notes can contain up to 500 characters.", "OK");
            return;
        }

        var hours = (int)Math.Round(CustomIntervalHours);
        var minutes = (int)Math.Round(CustomIntervalMinutes);

        if (hours < 0 || hours > 24 || minutes < 0 || minutes > 59 || (hours == 0 && minutes == 0))
        {
            await Shell.Current.DisplayAlertAsync("Check feeding interval", "Choose an interval between 1 minute and 24 hours.", "OK");
            return;
        }

        if (hours == 24 && minutes > 0)
        {
            await Shell.Current.DisplayAlertAsync("Check feeding interval", "A 24-hour interval cannot include additional minutes.", "OK");
            return;
        }

        var startDt = FedDate.Date.Add(StartTime);
        var existingLastFed = string.Empty;

        if (LogId > 0)
        {
            var existing = (await _api.GetFoodLogsAsync(PetId)).FirstOrDefault(l => l.Id == LogId);
            existingLastFed = existing?.LastFedTimestamp ?? string.Empty;
        }

        IsBusy = true;
        try
        {
            var log = new FoodLog
            {
                Id = LogId,
                PetId = PetId,
                FoodName = cleanName,
                AmountGrams = AmountGramsValue,
                IntervalHours = hours,
                IntervalMinutes = minutes,
                StartTimestamp = startDt.ToString("yyyy/MM/dd, HH:mm:ss"),
                LastFedTimestamp = existingLastFed,
                FedDate = FedDate.ToString("yyyy/MM/dd, HH:mm:ss"),
                Notes = cleanNotes
            };

            var result = await _api.SaveFoodLogAsync(PetId, log);
            if (result != null)
            {
                WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Could not save", "ShoppetCare could not save this feeding record. Please try again.", "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (LogId <= 0 || IsBusy) return;

        var log = (await _api.GetFoodLogsAsync(PetId)).FirstOrDefault(l => l.Id == LogId);
        if (log is null) return;

        var confirm = await Shell.Current.DisplayAlertAsync(
            "Delete feeding record?",
            $"Delete the feeding record for {log.FoodName}?",
            "Delete",
            "Cancel");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            bool deleted = await _api.DeleteFoodLogAsync(PetId, log.Id);
            if (deleted)
            {
                WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Could not delete", "The feeding record could not be deleted from the server.", "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
