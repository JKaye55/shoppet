using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Messages;
using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace ShoppetApp.ViewModels;

public partial class PetFormViewModel : ObservableObject, IQueryAttributable
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private readonly DatabaseService _db;
    private readonly ApiService _api;

    [ObservableProperty] public partial int PetId { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string Breed { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto))]
    public partial string PhotoUrl { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsEditMode { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial double AgeYearsValue { get; set; } = 1;
    [ObservableProperty] public partial double WeightKgValue { get; set; } = 1;

    [ObservableProperty]
    public partial string Species { get; set; } = "Dog";

    public string Title => IsEditMode ? "Edit Pet" : "Add Pet";
    public bool CanDelete => IsEditMode;
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoUrl);

    public IList<string> SpeciesOptions { get; } = ["Dog", "Cat", "Bird", "Small Pet", "Other"];
    public ObservableCollection<string> AvailableBreeds { get; } = new();

    public PetFormViewModel(DatabaseService db, ApiService api)
    {
        _db = db;
        _api = api;
        UpdateAvailableBreeds();
    }

    partial void OnSpeciesChanged(string value) => UpdateAvailableBreeds();

    private void UpdateAvailableBreeds()
    {
        AvailableBreeds.Clear();

        var list = Species switch
        {
            "Dog" => new[]
            {
                "Aspin", "Golden Retriever", "Shih Tzu", "Labrador",
                "German Shepherd", "Poodle", "Pomeranian", "Chihuahua",
                "Siberian Husky", "Mixed / Other"
            },
            "Cat" => new[]
            {
                "Puspin", "Persian", "Siamese", "Maine Coon",
                "British Shorthair", "Scottish Fold", "Sphynx", "Mixed / Other"
            },
            "Bird" => new[]
            {
                "Parakeet", "Cockatiel", "Lovebird", "Canary",
                "Finch", "Mixed / Other"
            },
            "Small Pet" => new[]
            {
                "Hamster", "Guinea Pig", "Rabbit", "Hedgehog",
                "Ferret", "Mixed / Other"
            },
            _ => new[] { "Mixed / Other" }
        };

        foreach (var item in list)
            AvailableBreeds.Add(item);

        if (!AvailableBreeds.Contains(Breed))
            Breed = AvailableBreeds.FirstOrDefault() ?? "Mixed / Other";
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
        Name = pet.Name?.Trim() ?? string.Empty;
        Species = SpeciesOptions.Contains(pet.Species) ? pet.Species : "Other";
        UpdateAvailableBreeds();

        Breed = AvailableBreeds.Contains(pet.Breed)
            ? pet.Breed
            : "Mixed / Other";

        AgeYearsValue = Math.Clamp(pet.AgeYears, 0, 40);

        if (double.TryParse(pet.Weight, NumberStyles.Any, CultureInfo.InvariantCulture, out var weight))
            WeightKgValue = Math.Clamp(weight, 0.1, 200);

        PhotoUrl = pet.PhotoUrl ?? string.Empty;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task CloseAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        if (IsBusy) return;

        try
        {
            var results = await MediaPicker.Default.PickPhotosAsync(
                new MediaPickerOptions { Title = "Choose a pet photo" });

            var result = results.FirstOrDefault();
            if (result is null) return;

            var extension = Path.GetExtension(result.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExtensions.Contains(extension))
            {
                await Shell.Current.DisplayAlertAsync(
                    "Unsupported photo",
                    "Please choose a JPG, PNG, or WebP image.",
                    "OK");
                return;
            }

            var photoDirectory = Path.Combine(FileSystem.AppDataDirectory, "pet-photos");
            Directory.CreateDirectory(photoDirectory);

            var destination = Path.Combine(photoDirectory, $"{Guid.NewGuid():N}{extension}");

            using (var source = await result.OpenReadAsync())
            using (var output = File.Create(destination))
            {
                await source.CopyToAsync(output);
            }

            var fileInfo = new FileInfo(destination);
            if (fileInfo.Length > MaxPhotoBytes)
            {
                File.Delete(destination);
                await Shell.Current.DisplayAlertAsync(
                    "Photo too large",
                    "Choose a photo smaller than 5 MB.",
                    "OK");
                return;
            }

            PhotoUrl = destination;
        }
        catch (FeatureNotSupportedException)
        {
            await Shell.Current.DisplayAlertAsync(
                "Photo unavailable",
                "Photo selection is not available on this device.",
                "OK");
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlertAsync(
                "Permission needed",
                "Allow photo access in your device settings, then try again.",
                "OK");
        }
        catch
        {
            await Shell.Current.DisplayAlertAsync(
                "Photo error",
                "The photo could not be selected. Please try another image.",
                "OK");
        }
    }

    [RelayCommand]
    private async Task RemovePhotoAsync()
    {
        if (!HasPhoto) return;

        var confirm = await Shell.Current.DisplayAlertAsync(
            "Remove photo?",
            "The pet profile will use the default photo.",
            "Remove",
            "Cancel");

        if (confirm)
            PhotoUrl = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        var cleanName = (Name ?? string.Empty).Trim();

        if (cleanName.Length < 2 || cleanName.Length > 40)
        {
            await Shell.Current.DisplayAlertAsync(
                "Check pet name",
                "Pet name must be between 2 and 40 characters.",
                "OK");
            return;
        }

        if (!SpeciesOptions.Contains(Species))
        {
            await Shell.Current.DisplayAlertAsync(
                "Choose a species",
                "Please select a species from the list.",
                "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(Breed) || !AvailableBreeds.Contains(Breed))
        {
            await Shell.Current.DisplayAlertAsync(
                "Choose a breed",
                "Please select a breed from the list.",
                "OK");
            return;
        }

        if (AgeYearsValue < 0 || AgeYearsValue > 40)
        {
            await Shell.Current.DisplayAlertAsync(
                "Check age",
                "Age must be between 0 and 40 years.",
                "OK");
            return;
        }

        if (WeightKgValue < 0.1 || WeightKgValue > 200)
        {
            await Shell.Current.DisplayAlertAsync(
                "Check weight",
                "Weight must be between 0.1 and 200 kg.",
                "OK");
            return;
        }

        int currentUserId = Preferences.Get("LoggedInUserId", 0);
        if (currentUserId <= 0)
        {
            await Shell.Current.DisplayAlertAsync(
                "Session expired",
                "Please sign in again before saving this pet.",
                "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var photoReference = PhotoUrl ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(photoReference) &&
                !ShoppetApp.Helpers.MediaUrlHelper.IsServerMedia(photoReference))
            {
                var uploaded = await _api.UploadImageAsync(photoReference, "pets");
                if (string.IsNullOrWhiteSpace(uploaded))
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Photo upload failed",
                        "The pet photo could not be uploaded. Please try again.",
                        "OK");
                    return;
                }
                photoReference = uploaded;
            }

            var pet = new Pet
            {
                Id = PetId,
                UserId = currentUserId,
                Name = cleanName,
                Species = Species,
                Breed = Breed,
                Weight = WeightKgValue.ToString("0.##", CultureInfo.InvariantCulture),
                PhotoUrl = photoReference,
                AgeYears = (int)Math.Round(AgeYearsValue)
            };

            int result = await _db.SavePetAsync(pet);
            if (result > 0)
            {
                WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync(
                    "Could not save",
                    "ShoppetCare could not save this pet. Please check your connection and try again.",
                    "OK");
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
        if (PetId <= 0 || IsBusy) return;

        var pet = await _db.GetPetAsync(PetId);
        if (pet is null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Delete pet?",
            $"Delete {pet.Name} and remove this pet profile from your account?",
            "Delete",
            "Cancel");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            int result = await _db.DeletePetAsync(pet);
            if (result > 0)
            {
                WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync(
                    "Could not delete",
                    "The pet could not be deleted from the server.",
                    "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
