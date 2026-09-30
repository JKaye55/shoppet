using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels
{
    public partial class MediaAttachment : ObservableObject
    {
        [ObservableProperty]
        public partial string FilePath { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsVideo { get; set; }

        public bool IsImage => !IsVideo;
    }

    public partial class CreatePostViewModel : ObservableObject
    {
        private readonly ApiService _api;
        private readonly DatabaseService _db;

        [ObservableProperty]
        public partial string Content { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ObservableCollection<object> SelectedPets { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<MediaAttachment> AttachedMedia { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<Pet> MyPets { get; set; } = new();

        [ObservableProperty]
        public partial bool IsPetModalVisible { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public string UserFullName => _db.CurrentUser?.FullName ?? "User";
        public string UserInitials => string.IsNullOrWhiteSpace(UserFullName) ? "U" : UserFullName.Substring(0, 1).ToUpper();

        public CreatePostViewModel(ApiService api, DatabaseService db)
        {
            _api = api;
            _db = db;
        }

        public async Task LoadPetsAsync()
        {
            try
            {
                var pets = await _api.GetPetsAsync();
                MyPets.Clear();
                foreach(var p in pets)
                {
                    MyPets.Add(p);
                }
            }
            catch { }
        }

        [RelayCommand]
        private void OpenPetModal()
        {
            IsPetModalVisible = true;
        }

        [RelayCommand]
        private void ClosePetModal()
        {
            IsPetModalVisible = false;
        }

        [RelayCommand]
        private async Task CloseAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        [RelayCommand]
        private void RemoveMedia(MediaAttachment media)
        {
            if (media != null && AttachedMedia.Contains(media))
            {
                AttachedMedia.Remove(media);
            }
        }

        [RelayCommand]
        private async Task AttachPhotoAsync()
        {
            if (IsBusy) return;

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

                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
                        {
                            await Shell.Current.DisplayAlertAsync("Unsupported photo", $"{file.FileName} is not a supported image.", "OK");
                            continue;
                        }

                        await using var stream = await file.OpenReadAsync();
                        if (stream.CanSeek && stream.Length > 5 * 1024 * 1024)
                        {
                            await Shell.Current.DisplayAlertAsync("Photo too large", $"{file.FileName} is larger than 5 MB.", "OK");
                            continue;
                        }

                        var cacheDir = Path.Combine(FileSystem.CacheDirectory, "community-photos");
                        Directory.CreateDirectory(cacheDir);
                        var cachedPath = Path.Combine(cacheDir, $"{Guid.NewGuid():N}{extension}");
                        await using (var output = File.Create(cachedPath))
                        {
                            await stream.CopyToAsync(output);
                        }

                        if (AttachedMedia.Any(x => string.Equals(x.FilePath, cachedPath, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        AttachedMedia.Add(new MediaAttachment { FilePath = cachedPath, IsVideo = false });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        [RelayCommand]
        private async Task PostAsync()
        {
            if (IsBusy) return;

            var cleanContent = (Content ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(cleanContent) && AttachedMedia.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Add something", "Write a post or attach at least one photo.", "OK");
                return;
            }

            if (cleanContent.Length > 2000)
            {
                await Shell.Current.DisplayAlertAsync("Post too long", "Community posts can contain up to 2,000 characters.", "OK");
                return;
            }

            if (_db.CurrentUser == null)
            {
                await Shell.Current.DisplayAlertAsync("Sign in required", "Please sign in before posting.", "OK");
                return;
            }

            var uploadedMedia = new List<string>();
            foreach (var media in AttachedMedia)
            {
                var uploaded = await _api.UploadImageAsync(media.FilePath, "community");
                if (string.IsNullOrWhiteSpace(uploaded))
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Photo upload failed",
                        "One of the selected photos could not be uploaded. Please try again.",
                        "OK");
                    return;
                }
                uploadedMedia.Add(uploaded);
            }

            var mediaPaths = string.Join(",", uploadedMedia);

            var request = new 
            {
                UserId = _db.CurrentUser.Id,
                PetId = SelectedPets.FirstOrDefault() is Pet firstPet ? (int?)firstPet.Id : null,
                AuthorName = _db.CurrentUser.FullName,
                PetName = SelectedPets.Count > 0 ? string.Join(" and ", SelectedPets.Cast<Pet>().Select(p => p.Name)) : "",
                Content = cleanContent,
                ImageUrls = mediaPaths
            };

            IsBusy = true;
            try
            {
                var success = await _api.CreateCommunityPostAsync(request);
                if (success)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlertAsync("Could not post", "Your post could not be published. Please try again.", "OK");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
