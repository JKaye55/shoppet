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
        private string _filePath = string.Empty;

        [ObservableProperty]
        private bool _isVideo;

        public bool IsImage => !IsVideo;
    }

    public partial class CreatePostViewModel : ObservableObject
    {
        private readonly ApiService _api;
        private readonly DatabaseService _db;

        [ObservableProperty]
        private string _content = string.Empty;

        [ObservableProperty]
        private ObservableCollection<object> _selectedPets = new();

        [ObservableProperty]
        private ObservableCollection<MediaAttachment> _attachedMedia = new();

        [ObservableProperty]
        private ObservableCollection<Pet> _myPets = new();

        [ObservableProperty]
        private bool _isPetModalVisible;

        [ObservableProperty]
        private bool _isPosting;

        public string UserFullName => _db.CurrentUser?.FullName
            ?? Preferences.Get("LoggedInUserName", "User");

        public string UserInitials => string.IsNullOrWhiteSpace(UserFullName)
            ? "U"
            : UserFullName.Trim()[0].ToString().ToUpperInvariant();

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

                        AttachedMedia.Add(new MediaAttachment { FilePath = file.FullPath, IsVideo = false });
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
            if (IsPosting) return;

            if (string.IsNullOrWhiteSpace(Content) && AttachedMedia.Count == 0)
            {
                await Shell.Current.DisplayAlertAsync("Nothing to post", "Write something or attach a photo first.", "OK");
                return;
            }

            var userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
            var userName = _db.CurrentUser?.FullName ?? Preferences.Get("LoggedInUserName", "User");

            if (userId <= 0)
            {
                await Shell.Current.DisplayAlertAsync("Sign in required", "Please sign in before creating a post.", "OK");
                return;
            }

            IsPosting = true;
            try
            {
                var uploadedUrls = AttachedMedia.Count == 0
                    ? new List<string>()
                    : await _api.UploadCommunityMediaAsync(AttachedMedia.Select(m => m.FilePath));

                if (AttachedMedia.Count > 0 && uploadedUrls.Count != AttachedMedia.Count)
                {
                    await Shell.Current.DisplayAlertAsync("Upload failed", "One or more photos could not be uploaded. Please try again.", "OK");
                    return;
                }

                var selectedPet = SelectedPets.OfType<Pet>().FirstOrDefault();

                var request = new
                {
                    UserId = userId,
                    PetId = selectedPet is null ? (int?)null : selectedPet.Id,
                    AuthorName = userName,
                    PetName = SelectedPets.Count > 0
                        ? string.Join(" and ", SelectedPets.OfType<Pet>().Select(p => p.Name))
                        : string.Empty,
                    Content = Content.Trim(),
                    ImageUrls = uploadedUrls.Count > 0
                        ? string.Join("|", uploadedUrls)
                        : null
                };

                var success = await _api.CreateCommunityPostAsync(request);
                if (!success)
                {
                    await Shell.Current.DisplayAlertAsync("Post failed", "The post could not be saved. Check the API connection and try again.", "OK");
                    return;
                }

                Content = string.Empty;
                AttachedMedia.Clear();
                SelectedPets.Clear();

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Create post failed: {ex}");
                await Shell.Current.DisplayAlertAsync("Post failed", "Could not reach ShoppetAPI or upload the selected photo.", "OK");
            }
            finally
            {
                IsPosting = false;
            }
        }
    }
}
