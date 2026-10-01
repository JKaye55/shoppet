using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels
{
    [QueryProperty(nameof(PostToEdit), "PostToEdit")]
    public partial class EditPostViewModel : ObservableObject
    {
        private readonly ApiService _api;
        private readonly DatabaseService _db;

        [ObservableProperty]
        private CommunityPost _postToEdit = new CommunityPost();

        [ObservableProperty]
        private string _content = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Pet> _myPets = new();

        [ObservableProperty]
        private ObservableCollection<object> _selectedPets = new();

        [ObservableProperty]
        private ObservableCollection<MediaAttachment> _attachedMedia = new();

        [ObservableProperty]
        private bool _isPetModalVisible;

        [ObservableProperty]
        private bool _isBusy;

        public EditPostViewModel(ApiService api, DatabaseService db)
        {
            _api = api;
            _db = db;
        }

        partial void OnPostToEditChanged(CommunityPost value)
        {
            if (value != null)
            {
                Content = value.Content;
                AttachedMedia.Clear();
                foreach(var img in value.ImageList)
                {
                    AttachedMedia.Add(new MediaAttachment { FilePath = img, IsVideo = false });
                }
                _ = LoadPetsAsync();
            }
        }

        private async Task LoadPetsAsync()
        {
            var pets = await _api.GetPetsAsync();
            MyPets.Clear();
            SelectedPets.Clear();
            foreach (var p in pets)
            {
                MyPets.Add(p);
                if (PostToEdit != null && p.Name == PostToEdit.PetName)
                {
                    SelectedPets.Add(p);
                }
            }
        }

        [RelayCommand]
        private void OpenPetModal() => IsPetModalVisible = true;

        [RelayCommand]
        private void ClosePetModal() => IsPetModalVisible = false;

        [RelayCommand]
        private void RemoveMedia(MediaAttachment media)
        {
            if (AttachedMedia.Contains(media))
            {
                AttachedMedia.Remove(media);
            }
        }

        [RelayCommand]
        private async Task AttachPhotoAsync()
        {
            try
            {
                var result = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select Photos",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    foreach (var file in result)
                    {
                        if (AttachedMedia.Count >= 5)
                        {
                            await Shell.Current.DisplayAlertAsync("Limit Reached", "You can only attach a maximum of 5 photos.", "OK");
                            break;
                        }

                        var localCachePath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}_{file.FileName}");
                        using (var sourceStream = await file.OpenReadAsync())
                        using (var targetStream = File.Create(localCachePath))
                        {
                            await sourceStream.CopyToAsync(targetStream);
                        }

                        AttachedMedia.Add(new MediaAttachment { FilePath = localCachePath, IsVideo = false });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Photo pick error: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var existingUrls = AttachedMedia
                    .Select(m => m.FilePath)
                    .Where(p => p.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                             || p.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var localPaths = AttachedMedia
                    .Select(m => m.FilePath)
                    .Where(File.Exists)
                    .ToList();

                var uploadedUrls = localPaths.Count > 0
                    ? await _api.UploadCommunityMediaAsync(localPaths)
                    : new List<string>();

                if (localPaths.Count > 0 && uploadedUrls.Count != localPaths.Count)
                {
                    await Shell.Current.DisplayAlertAsync("Upload failed", "One or more photos could not be uploaded.", "OK");
                    return;
                }

                string imageUrls = string.Join(",", existingUrls.Concat(uploadedUrls));
                
                // Determine PetId and PetName
                int? petId = null;
                string petName = string.Empty;
                var selectedPet = SelectedPets.FirstOrDefault() as Pet;
                if (selectedPet != null)
                {
                    petId = selectedPet.Id;
                    petName = selectedPet.Name;
                }
                
                bool success = await _api.EditPostAsync(PostToEdit.Id, Content, imageUrls, petId, petName);
                if (success)
                {
                    PostToEdit.Content = Content;
                    PostToEdit.PetId = petId;
                    PostToEdit.PetName = petName;
                    PostToEdit.ImageUrls = imageUrls;
                    
                    await Shell.Current.DisplayAlertAsync("Success", "Post updated successfully.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlertAsync("Error", "Failed to update post.", "OK");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}




