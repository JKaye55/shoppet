using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Messages;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels;

public partial class FoodLogFormViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiService _api;

    [ObservableProperty] private int _petId;
    [ObservableProperty] private int _logId;
    [ObservableProperty] private string _foodName = string.Empty;
    [ObservableProperty] private DateTime _fedDate = DateTime.Today;
    [ObservableProperty] private TimeSpan _startTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _notes = string.Empty;

    // ── Interval: split into integer Hours + Minutes ──────────────────────────
    private string _intervalHoursText = string.Empty;
    public string IntervalHoursText
    {
        get => _intervalHoursText;
        set
        {
            var clean = string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : new string(value.Where(char.IsDigit).ToArray());
            if (!SetProperty(ref _intervalHoursText, clean) && value != clean)
                OnPropertyChanged(nameof(IntervalHoursText));
        }
    }

    private string _intervalMinutesText = string.Empty;
    public string IntervalMinutesText
    {
        get => _intervalMinutesText;
        set
        {
            var clean = string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : new string(value.Where(char.IsDigit).ToArray());
            if (!SetProperty(ref _intervalMinutesText, clean) && value != clean)
                OnPropertyChanged(nameof(IntervalMinutesText));
        }
    }

    // ── Amount ────────────────────────────────────────────────────────────────
    private string _amountGramsText = string.Empty;
    public string AmountGramsText
    {
        get => _amountGramsText;
        set
        {
            var clean = string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : new string(value.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (!SetProperty(ref _amountGramsText, clean) && value != clean)
                OnPropertyChanged(nameof(AmountGramsText));
        }
    }

    public string Title => IsEditMode ? "Edit Food Log" : "Add Food Log";
    public bool CanDelete => IsEditMode;

    public FoodLogFormViewModel(ApiService api) => _api = api;

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
        if (log is null)
            return;

        IsEditMode = true;
        PetId = log.PetId;
        FoodName = log.FoodName;
        AmountGramsText = log.AmountGrams > 0 ? log.AmountGrams.ToString("G") : string.Empty;
        IntervalHoursText = log.IntervalHours > 0 ? log.IntervalHours.ToString() : string.Empty;
        IntervalMinutesText = log.IntervalMinutes > 0 ? log.IntervalMinutes.ToString() : string.Empty;
        Notes = log.Notes ?? string.Empty;

        if (DateTime.TryParse(log.FedDate, out var fedDate))
            FedDate = fedDate.Date;

        // Restore start time from StartTimestamp
        if (!string.IsNullOrEmpty(log.StartTimestamp) &&
            DateTime.TryParse(log.StartTimestamp, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var startDt))
        {
            StartTime = startDt.TimeOfDay;
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task CloseAsync() =>
        await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FoodName))
        {
            await Shell.Current.DisplayAlert("Validation", "Food name is required.", "OK");
            return;
        }

        _ = double.TryParse(AmountGramsText, out var grams);
        _ = int.TryParse(IntervalHoursText, out var hours);
        _ = int.TryParse(IntervalMinutesText, out var minutes);

        if (minutes >= 60)
        {
            await Shell.Current.DisplayAlert("Validation", "Minutes must be between 0 and 59.", "OK");
            return;
        }

        // Combine FedDate + StartTime into a full datetime for StartTimestamp
        var startDt = FedDate.Date.Add(StartTime);

        // Preserve existing LastFedTimestamp if editing
        var existingLastFed = string.Empty;
        if (LogId > 0)
        {
            var existing = (await _api.GetFoodLogsAsync(PetId)).FirstOrDefault(l => l.Id == LogId);
            existingLastFed = existing?.LastFedTimestamp ?? string.Empty;
        }

        var log = new FoodLog
        {
            Id = LogId,
            PetId = PetId,
            FoodName = FoodName.Trim(),
            AmountGrams = grams,
            IntervalHours = hours,
            IntervalMinutes = minutes,
            StartTimestamp = startDt.ToString("yyyy/MM/dd, HH:mm:ss"),
            LastFedTimestamp = existingLastFed,
            FedDate = FedDate.ToString("yyyy/MM/dd, HH:mm:ss"),
            Notes = Notes.Trim()
        };

        var result = await _api.SaveFoodLogAsync(PetId, log);
        if (result != null)
        {
            WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Shell.Current.DisplayAlert("Error", "Failed to save food log to server.", "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (LogId <= 0)
            return;

        var log = (await _api.GetFoodLogsAsync(PetId)).FirstOrDefault(l => l.Id == LogId);
        if (log is null)
            return;

        var confirm = await Shell.Current.DisplayAlert(
            "Delete Food Log",
            $"Remove {log.FoodName}?",
            "Delete",
            "Cancel");

        if (!confirm)
            return;

        await _api.DeleteFoodLogAsync(PetId, log.Id);
        WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
        await Shell.Current.GoToAsync("..");
    }
}



