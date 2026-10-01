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
    private int originalRemaining,originalTotal;private string originalDate="";
    [ObservableProperty]private string notes="";
    [ObservableProperty]private string vetName="";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVaccine))]
    [NotifyPropertyChangedFor(nameof(IsMedication))]
    [NotifyPropertyChangedFor(nameof(IsCheckup))]
    private string _logType = "vaccine";

    [ObservableProperty] private int _petId;
    [ObservableProperty] private int _logId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private DateTime _dueDate = DateTime.Today.AddDays(1);
    [ObservableProperty] private TimeSpan _dueTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private bool _completed;
    [ObservableProperty] private DateTime _dateAdministered = DateTime.Today;
    [ObservableProperty] private string _validityIntervalText = "1";
    [ObservableProperty] private string _validityUnit = "Months";
    [ObservableProperty] private string _medicationIntervalHoursText = "8";
    [ObservableProperty] private string _dosageTotalText = "14";
    [ObservableProperty] private DateTime _checkupDate = DateTime.Today;
    [ObservableProperty] private DateTime _timeStartedDate = DateTime.Today;
    [ObservableProperty] private TimeSpan _timeStartedTime = DateTime.Now.TimeOfDay;
    [ObservableProperty] private bool _isEditMode;

    public System.Collections.ObjectModel.ObservableCollection<string> DocumentPathsList { get; } = new();

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
        originalRemaining=log.DosageRemaining;originalTotal=log.DosageTotal;originalDate=log.RecordDate;Notes=log.Notes;VetName=log.VetName;
        DocumentPathsList.Clear();foreach(var path in log.DocumentsList)DocumentPathsList.Add(path);
        ValidityIntervalText = log.ValidityInterval.ToString();
        ValidityUnit = ValidityUnitOptions.FirstOrDefault(u => string.Equals(u, log.ValidityUnit, StringComparison.OrdinalIgnoreCase)) ?? (string.IsNullOrEmpty(log.ValidityUnit) ? "Months" : log.ValidityUnit);
        MedicationIntervalHoursText = log.MedicationIntervalHours.ToString();
        DosageTotalText = log.DosageTotal.ToString();

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
    private async Task OpenDocumentAsync(string path)
    {try{await _api.OpenCareDocumentAsync(path);}catch(Exception){await Shell.Current.DisplayAlertAsync("Document unavailable","Check the connection, or reattach an old local-only document.","OK");}}
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
                var path=Path.Combine(FileSystem.CacheDirectory,Guid.NewGuid().ToString("N")+Path.GetExtension(result.FileName));
                await using(var source=await result.OpenReadAsync())await using(var target=File.Create(path))await source.CopyToAsync(target);
                DocumentPathsList.Add(path);
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

        if(validityInterval<0||medInterval<0||dosageTotal<0){await Shell.Current.DisplayAlertAsync("Validation","Care values cannot be negative.","OK");return;}
        var local=DocumentPathsList.Where(p=>!Uri.TryCreate(p,UriKind.Absolute,out var u)||u.Scheme is not ("http" or "https")).ToList();
        try{if(local.Count>0){var urls=await _api.UploadDocumentsAsync(local);foreach(var p in local)DocumentPathsList.Remove(p);foreach(var url in urls)DocumentPathsList.Add(url);}}
        catch{await Shell.Current.DisplayAlertAsync("Upload failed","Documents could not be uploaded. Your form is unchanged.","OK");return;}
        var finalDueDate = DueDate.Date.Add(DueTime);
        var finalTimeStarted = TimeStartedDate.Date.Add(TimeStartedTime);

        var log = new HealthLog
        {
            Id = LogId,
            PetId = PetId,
            Type = LogType,
            Name = Name.Trim(),
            Notes=Notes,VetName=VetName,RecordDate=LogId>0?originalDate:DateTime.Now.ToString("O"),
            DueDate = finalDueDate.ToString("yyyy/MM/dd, HH:mm"),
            Completed = Completed,
            DateAdministered = DateAdministered.ToString("yyyy/MM/dd, 00:00"),
            ValidityInterval = validityInterval,
            ValidityUnit = ValidityUnit,
            MedicationIntervalHours = medInterval,
            TimeStarted = finalTimeStarted.ToString("yyyy/MM/dd, HH:mm"),
            DosageTotal = dosageTotal,
            DosageRemaining = LogId>0?Math.Clamp(originalRemaining+dosageTotal-originalTotal,0,dosageTotal):dosageTotal,
            CheckupDate = CheckupDate.ToString("yyyy/MM/dd, 00:00"),
            DocumentPaths = string.Join("|", DocumentPathsList)
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





