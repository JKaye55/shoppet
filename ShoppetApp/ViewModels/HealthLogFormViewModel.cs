using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Messages;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels;

public partial class HealthLogFormViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiService _api;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVaccine))]
    [NotifyPropertyChangedFor(nameof(IsMedication))]
    [NotifyPropertyChangedFor(nameof(IsCheckup))]
    public partial string LogType { get; set; } = "vaccine";

    [ObservableProperty] public partial int PetId { get; set; }
    [ObservableProperty] public partial int LogId { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial DateTime DueDate { get; set; } = DateTime.Today.AddDays(1);
    [ObservableProperty] public partial TimeSpan DueTime { get; set; } = DateTime.Now.TimeOfDay;
    [ObservableProperty] public partial bool Completed { get; set; }
    [ObservableProperty] public partial DateTime DateAdministered { get; set; } = DateTime.Today;
    [ObservableProperty] public partial string ValidityIntervalText { get; set; } = "1";
    [ObservableProperty] public partial string ValidityUnit { get; set; } = "Months";
    [ObservableProperty] public partial string MedicationIntervalHoursText { get; set; } = "8";
    [ObservableProperty] public partial string DosageTotalText { get; set; } = "14";
    [ObservableProperty] public partial DateTime CheckupDate { get; set; } = DateTime.Today;
    [ObservableProperty] public partial DateTime TimeStartedDate { get; set; } = DateTime.Today;
    [ObservableProperty] public partial TimeSpan TimeStartedTime { get; set; } = DateTime.Now.TimeOfDay;
    [ObservableProperty] public partial bool IsEditMode { get; set; }

    public List<string> DocumentPathsList { get; } = new();

    public IList<string> TypeOptions { get; } = ["vaccine", "medication", "vital"];
    public IList<string> ValidityUnitOptions { get; } = ["Days", "Weeks", "Months", "Years"];

    public string Title => LogId > 0 ? "Edit Health Record" : "Add Health Record";
    public bool CanDelete => LogId > 0;

    public bool IsVaccine => LogType?.Equals("vaccine", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsMedication => LogType?.Equals("medication", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsCheckup => LogType?.Equals("vital", StringComparison.OrdinalIgnoreCase) ?? false;

    public HealthLogFormViewModel(ApiService api) => _api = api;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("petId", out var pVal))
            PetId = Convert.ToInt32(pVal);
        if (query.TryGetValue("logId", out var lVal))
            LogId = Convert.ToInt32(lVal);
    }

    public async Task LoadAsync()
    {
        if (LogId <= 0) return;

        var log = (await _api.GetHealthLogsAsync(PetId)).FirstOrDefault(l => l.Id == LogId);
        if (log is null) return;

        LogType = TypeOptions.FirstOrDefault(t => string.Equals(t, log.Type, StringComparison.OrdinalIgnoreCase)) ?? log.Type;
        Name = log.Name;
        Completed = log.Completed;
        ValidityIntervalText = log.ValidityInterval.ToString();
        ValidityUnit = ValidityUnitOptions.FirstOrDefault(u => string.Equals(u, log.ValidityUnit, StringComparison.OrdinalIgnoreCase)) ?? (string.IsNullOrEmpty(log.ValidityUnit) ? "Months" : log.ValidityUnit);
        MedicationIntervalHoursText = log.MedicationIntervalHours.ToString();
        DosageTotalText = log.DosageTotal.ToString();

        DocumentPathsList.Clear();
        if (!string.IsNullOrWhiteSpace(log.DocumentPaths))
        {
            foreach (var path in log.DocumentPaths.Split(
                         new[] { ';', '|' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                DocumentPathsList.Add(path);
            }
        }

        if (DateTime.TryParse(log.DueDate, out var parsedDue))
        {
            DueDate = parsedDue.Date;
            DueTime = parsedDue.TimeOfDay;
        }
        if (DateTime.TryParse(log.DateAdministered, out var parsedAdmin))
        {
            DateAdministered = parsedAdmin.Date;
        }
        if (DateTime.TryParse(log.CheckupDate, out var parsedCheck))
        {
            CheckupDate = parsedCheck.Date;
        }
        if (DateTime.TryParse(log.TimeStarted, out var parsedStart))
        {
            TimeStartedDate = parsedStart.Date;
            TimeStartedTime = parsedStart.TimeOfDay;
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task CloseAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task PickDocumentAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync();
            if (result != null)
            {
                DocumentPathsList.Add(result.FullPath);
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"Could not attach document: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private void RemoveDocument(string path)
    {
        if (DocumentPathsList.Contains(path))
            DocumentPathsList.Remove(path);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Record name is required.", "OK");
            return;
        }

        int.TryParse(ValidityIntervalText, out var validityInterval);
        double.TryParse(MedicationIntervalHoursText, out var medInterval);
        int.TryParse(DosageTotalText, out var dosageTotal);

        int dosageRemaining = dosageTotal;
        if (LogId > 0)
        {
            var existing = (await _api.GetHealthLogsAsync(PetId))
                .FirstOrDefault(x => x.Id == LogId);
            if (existing is not null)
                dosageRemaining = Math.Min(existing.DosageRemaining, dosageTotal);
        }

        var finalDueDate = DueDate.Date.Add(DueTime);
        var finalTimeStarted = TimeStartedDate.Date.Add(TimeStartedTime);

        var log = new HealthLog
        {
            Id = LogId,
            PetId = PetId,
            Type = LogType,
            Name = Name.Trim(),
            DueDate = finalDueDate.ToString("yyyy/MM/dd, HH:mm"),
            Completed = Completed,
            DateAdministered = DateAdministered.ToString("yyyy/MM/dd, 00:00"),
            ValidityInterval = validityInterval,
            ValidityUnit = ValidityUnit,
            MedicationIntervalHours = medInterval,
            TimeStarted = finalTimeStarted.ToString("yyyy/MM/dd, HH:mm"),
            DosageTotal = dosageTotal,
            DosageRemaining = dosageRemaining,
            CheckupDate = CheckupDate.ToString("yyyy/MM/dd, 00:00"),
            DocumentPaths = string.Join(";", DocumentPathsList)
        };

        var result = await _api.SaveHealthLogAsync(PetId, log);
        if (result != null)
        {
            WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Shell.Current.DisplayAlertAsync("Error", "Failed to save health record to server.", "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (LogId <= 0) return;
        var log = (await _api.GetHealthLogsAsync(PetId)).FirstOrDefault(l => l.Id == LogId);
        if (log is null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync("Delete", $"Remove {log.Name}?", "Delete", "Cancel");
        if (!confirm) return;

        await _api.DeleteHealthLogAsync(PetId, log.Id);
        WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
        await Shell.Current.GoToAsync("..");
    }
}





