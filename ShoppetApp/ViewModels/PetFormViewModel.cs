using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Messages;
using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Collections.ObjectModel;

namespace ShoppetApp.ViewModels;

public partial class PetFormViewModel : ObservableObject, IQueryAttributable
{
    private readonly DatabaseService _db;
    private readonly ApiService _api;

    [ObservableProperty] private int _petId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _breed = string.Empty;
    [ObservableProperty] private string _photoUrl = string.Empty;
    [ObservableProperty] private bool _isEditMode;

    [ObservableProperty]
    private string _species = "Dog";

    partial void OnSpeciesChanged(string value)
    {
        UpdateAvailableBreeds();
    }

    private void UpdateAvailableBreeds()
    {
        AvailableBreeds.Clear();
        var list = Species switch
        {
            "Dog" => new[] { "Aspin", "Golden Retriever", "Shih Tzu", "Labrador", "German Shepherd", "Poodle", "Pomeranian", "Chihuahua", "Husky", "Other" },
            "Cat" => new[] { "Puspin", "Persian", "Siamese", "Maine Coon", "British Shorthair", "Scottish Fold", "Sphynx", "Other" },
            "Bird" => new[] { "Parakeet", "Cockatiel", "Lovebird", "Canine", "Finch", "Other" },
            "Small Pet" => new[] { "Hamster", "Guinea Pig", "Rabbit", "Hedgehog", "Ferret", "Other" },
            _ => new[] { "Mixed / Other" }
        };

        foreach (var item in list)
        {
            AvailableBreeds.Add(item);
        }

        if (!AvailableBreeds.Contains(Breed))
        {
            Breed = AvailableBreeds.FirstOrDefault() ?? string.Empty;
        }
    }

    private string _weight = string.Empty;
    public string Weight
    {
        get => _weight;
        set
        {
            var numericValue = string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
            if (!SetProperty(ref _weight, numericValue) && value != numericValue)
            {
                OnPropertyChanged(nameof(Weight));
            }
        }
    }

    private string _ageYearsText = string.Empty;
    public string AgeYearsText
    {
        get => _ageYearsText;
        set
        {
            var numericValue = string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
            if (!SetProperty(ref _ageYearsText, numericValue) && value != numericValue)
            {
                OnPropertyChanged(nameof(AgeYearsText));
            }
        }
    }

    public string Title => IsEditMode ? "Edit Pet" : "Add Pet";
    public bool CanDelete => IsEditMode;

    public IList<string> SpeciesOptions { get; } = ["Dog", "Cat", "Bird", "Small Pet", "Other"];
    public ObservableCollection<string> AvailableBreeds { get; } = new();

    public PetFormViewModel(DatabaseService db, ApiService api)
    {
        _db = db;
        _api = api;
        UpdateAvailableBreeds();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("petId", out var value))
            PetId = Convert.ToInt32(value);
    }

    public async Task LoadAsync()
    {
        if (PetId <= 0)
        {
            IsEditMode = false;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(CanDelete));
            return;
        }

        var pet = await _db.GetPetAsync(PetId);
        if (pet is null)
            return;

        IsEditMode = true;
        Name = pet.Name;
        Species = string.IsNullOrEmpty(pet.Species) ? "Dog" : pet.Species;
        UpdateAvailableBreeds();
        Breed = pet.Breed;
        Weight = pet.Weight;
        PhotoUrl = pet.PhotoUrl;
        AgeYearsText = pet.AgeYears.ToString();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task CloseAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Please pick a photo" });
            if (result != null)
            {
                var newFile = Path.Combine(FileSystem.AppDataDirectory, result.FileName);
                using (var stream = await result.OpenReadAsync())
                using (var newStream = File.OpenWrite(newFile))
                {
                    await stream.CopyToAsync(newStream);
                }
                PhotoUrl = newFile;
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Photo picker failed: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("Validation", "Pet name is required.", "OK");
            return;
        }

        _ = int.TryParse(AgeYearsText, out var age);
        if (age > 25)
        {
            await Shell.Current.DisplayAlert("Validation", "Age cannot exceed 25 years.", "OK");
            return;
        }

        int currentUserId = Preferences.Get("LoggedInUserId", 0);
        if (currentUserId == 0)
        {
            await Shell.Current.DisplayAlert("Error", "User session not found. Please log in again.", "OK");
            return;
        }

        var sharedPhotoUrl = PhotoUrl ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(sharedPhotoUrl) && File.Exists(sharedPhotoUrl))
        {
            try
            {
                var uploaded = await _api.UploadCommunityMediaAsync(new[] { sharedPhotoUrl });
                if (uploaded.Count == 1)
                    sharedPhotoUrl = uploaded[0];
            }
            catch
            {
                await Shell.Current.DisplayAlert("Photo upload", "The pet profile could not upload the selected photo.", "OK");
                return;
            }
        }

        var pet = new Pet
        {
            Id = PetId,
            UserId = currentUserId,
            Name = Name.Trim(),
            Species = Species,
            Breed = string.IsNullOrEmpty(Breed) ? "Mixed" : Breed.Trim(),
            Weight = Weight.Trim(),
            PhotoUrl = sharedPhotoUrl,
            AgeYears = age
        };

        int result = await _db.SavePetAsync(pet);
        if (result > 0)
        {
            WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Shell.Current.DisplayAlert("Error", "Failed to save pet to the server.", "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (PetId <= 0) return;
        var pet = await _db.GetPetAsync(PetId);
        if (pet is null) return;

        bool confirm = await Shell.Current.DisplayAlert("Delete Pet", $"Remove {pet.Name}?", "Delete", "Cancel");
        if (!confirm) return;

        await _db.DeletePetAsync(pet);
        WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
        await Shell.Current.GoToAsync("..");
    }
}