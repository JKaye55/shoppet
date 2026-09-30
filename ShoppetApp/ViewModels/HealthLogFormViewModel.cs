using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Messages;
using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Collections.ObjectModel;

namespace ShoppetApp.ViewModels;

public partial class HealthLogFormViewModel : ObservableObject, IQueryAttributable
{
    private const long MaxDocumentBytes = 10 * 1024 * 1024;
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
    [ObservableProperty] public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomRecordName))]
    public partial string SelectedCommonRecord { get; set; } = "Rabies Vaccine";

    public ObservableCollection<string> DocumentPathsList { get; } = new();
    public ObservableCollection<string> CommonRecordOptions { get; } = new();

    public IList<string> TypeOptions { get; } = ["vaccine", "medication", "vital"];
    public IList<string> ValidityUnitOptions { get; } = ["Days", "Weeks", "Months", "Years"];

    public string Title => LogId > 0 ? "Edit Health Record" : "Add Health Record";
    public bool CanDelete => LogId > 0;
    public bool IsCustomRecordName => SelectedCommonRecord == "Custom";

    public bool IsVaccine => LogType?.Equals("vaccine", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsMedication => LogType?.Equals("medication", StringComparison.OrdinalIgnoreCase) ?? false;
    public bool IsCheckup => LogType?.Equals("vital", StringComparison.OrdinalIgnoreCase) ?? false;

    public HealthLogFormViewModel(ApiService api)
    {
        _api = api;
        UpdateCommonRecordOptions();
    }

    partial void OnLogTypeChanged(string value)
    {
        UpdateCommonRecordOptions();
    }

    partial void OnSelectedCommonRecordChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value != "Custom")
            Name = value;
    }

    private void UpdateCommonRecordOptions()
    {
        CommonRecordOptions.Clear();

        string[] options = LogType?.ToLowerInvariant() switch
        {
            "vaccine" => ["Rabies Vaccine", "5-in-1 Vaccine", "6-in-1 Vaccine", "Bordetella Vaccine", "Deworming", "Custom"],
            "medication" => ["Antibiotic", "Anti-inflammatory", "Heartworm Preventive", "Flea and Tick Preventive", "Vitamin / Supplement", "Custom"],
            _ => ["Routine Checkup", "Follow-up Checkup", "Dental Checkup", "Weight Check", "Laboratory Result", "Custom"]
        };

        foreach (var option in options)
            CommonRecordOptions.Add(option);

        if (!CommonRecordOptions.Contains(SelectedCommonRecord))
            SelectedCommonRecord = CommonRecordOptions.FirstOrDefault() ?? "Custom";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("petId", out var pVal))
            PetId = Convert.ToInt32(pVal);
        if (query.TryGetValue("logId", out var lVal))
            LogId = Convert.ToInt32(lVal);
    }

    public async Task LoadAsync()
    {
        if (LogId <= 0)
        {
            UpdateCommonRecordOptions();
            return;
        }

        var log = (await _api.GetHealthLogsAsync(PetId)).FirstOrDefault(l => l.Id == LogId);
        if (log is null) return;

        LogType = TypeOptions.FirstOrDefault(t => string.Equals(t, log.Type, StringComparison.OrdinalIgnoreCase)) ?? "vital";
        UpdateCommonRecordOptions();

        Name = log.Name?.Trim() ?? string.Empty;
        SelectedCommonRecord = CommonRecordOptions.Contains(Name) ? Name : "Custom";
        Completed = log.Completed;
        ValidityIntervalText = log.ValidityInterval.ToString();
        ValidityUnit = ValidityUnitOptions.FirstOrDefault(u => string.Equals(u, log.ValidityUnit, StringComparison.OrdinalIgnoreCase))
                       ?? "Months";
        MedicationIntervalHoursText = log.MedicationIntervalHours.ToString();
        DosageTotalText = log.DosageTotal.ToString();

        DocumentPathsList.Clear();
        if (!string.IsNullOrWhiteSpace(log.DocumentPaths))
        {
            foreach (var documentPath in log.DocumentPaths.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                DocumentPathsList.Add(documentPath);
        }

        if (DateTime.TryParse(log.DueDate, out var parsedDue))
        {
            DueDate = parsedDue.Date;
            DueTime = parsedDue.TimeOfDay;
        }

        if (DateTime.TryParse(log.DateAdministered, out var parsedAdmin))
            DateAdministered = parsedAdmin.Date;

        if (DateTime.TryParse(log.CheckupDate, out var parsedCheck))
            CheckupDate = parsedCheck.Date;

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
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a health document or photo"
            });

            if (result is null) return;

            var extension = Path.GetExtension(result.FileName).ToLowerInvariant();
            string[] allowedExtensions = [".pdf", ".jpg", ".jpeg", ".png", ".webp"];

            if (!allowedExtensions.Contains(extension))
            {
                await Shell.Current.DisplayAlertAsync(
                    "Unsupported file",
                    "Choose a PDF, JPG, PNG, or WebP file.",
                    "OK");
                return;
            }

            await using var stream = await result.OpenReadAsync();
            if (stream.CanSeek && stream.Length > MaxDocumentBytes)
            {
                await Shell.Current.DisplayAlertAsync(
                    "File too large",
                    "Attachments must be 10 MB or smaller.",
                    "OK");
                return;
            }

            if (DocumentPathsList.Count >= 5)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Attachment limit",
                    "You can attach up to 5 files to one health record.",
                    "OK");
                return;
            }

            if (!DocumentPathsList.Contains(result.FullPath))
                DocumentPathsList.Add(result.FullPath);
        }
        catch
        {
            await Shell.Current.DisplayAlertAsync(
                "Attachment error",
                "The file could not be attached. Please try another file.",
                "OK");
        }
    }

    [RelayCommand]
    private void RemoveDocument(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            DocumentPathsList.Remove(path);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        var cleanName = (Name ?? string.Empty).Trim();

        if (PetId <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Pet required", "This record must belong to a valid pet.", "OK");
            return;
        }

        if (cleanName.Length < 2 || cleanName.Length > 80)
        {
            await Shell.Current.DisplayAlertAsync("Check record name", "Record name must be between 2 and 80 characters.", "OK");
            return;
        }

        if (!TypeOptions.Contains(LogType))
        {
            await Shell.Current.DisplayAlertAsync("Choose record type", "Please select a valid health record type.", "OK");
            return;
        }

        int.TryParse(ValidityIntervalText, out var validityInterval);
        double.TryParse(MedicationIntervalHoursText, out var medInterval);
        int.TryParse(DosageTotalText, out var dosageTotal);

        if (IsVaccine)
        {
            if (DateAdministered.Date > DateTime.Today)
            {
                await Shell.Current.DisplayAlertAsync("Check administered date", "Date administered cannot be in the future.", "OK");
                return;
            }

            if (validityInterval < 1 || validityInterval > 120)
            {
                await Shell.Current.DisplayAlertAsync("Check validity", "Validity interval must be between 1 and 120.", "OK");
                return;
            }
        }

        if (IsMedication)
        {
            if (medInterval <= 0 || medInterval > 168)
            {
                await Shell.Current.DisplayAlertAsync("Check medication interval", "Medication interval must be greater than 0 and no more than 168 hours.", "OK");
                return;
            }

            if (dosageTotal < 1 || dosageTotal > 1000)
            {
                await Shell.Current.DisplayAlertAsync("Check dosage", "Total dosage must be between 1 and 1,000 administrations.", "OK");
                return;
            }
        }

        var finalDueDate = DueDate.Date.Add(DueTime);
        var finalTimeStarted = TimeStartedDate.Date.Add(TimeStartedTime);

        int dosageRemaining = dosageTotal;
        if (LogId > 0)
        {
            var existing = (await _api.GetHealthLogsAsync(PetId)).FirstOrDefault(x => x.Id == LogId);
            if (existing is not null)
                dosageRemaining = Math.Min(existing.DosageRemaining, dosageTotal);
        }

        IsBusy = true;
        try
        {
            var log = new HealthLog
            {
                Id = LogId,
                PetId = PetId,
                Type = LogType,
                Name = cleanName,
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
                await Shell.Current.DisplayAlertAsync("Could not save", "ShoppetCare could not save this health record. Please try again.", "OK");
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

        var log = (await _api.GetHealthLogsAsync(PetId)).FirstOrDefault(l => l.Id == LogId);
        if (log is null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Delete health record?",
            $"Delete {log.Name} from this pet's health history?",
            "Delete",
            "Cancel");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            bool deleted = await _api.DeleteHealthLogAsync(PetId, log.Id);
            if (deleted)
            {
                WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Could not delete", "The health record could not be deleted from the server.", "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
