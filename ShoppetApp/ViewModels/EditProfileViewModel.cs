using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels
{
    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly ApiService _api;
        private readonly DatabaseService _db;

        public EditProfileViewModel(ApiService api, DatabaseService db)
        {
            _api = api;
            _db = db;
            _ = LoadDataAsync();
        }

        [ObservableProperty]
        public partial string FullName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ProfilePictureBase64 { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ImageSource? ProfileImageSource { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        private async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var userId = Preferences.Get("LoggedInUserId", 0);
                FullName = Preferences.Get("LoggedInUserName", string.Empty);

                var profile = await _api.GetProfileAsync(userId);
                if (profile != null)
                {
                    FullName = profile.FullName;
                    ProfilePictureBase64 = profile.ProfilePicture;
                    
                    if (!string.IsNullOrEmpty(ProfilePictureBase64))
                    {
                        var bytes = Convert.FromBase64String(ProfilePictureBase64);
                        ProfileImageSource = ImageSource.FromStream(() => new MemoryStream(bytes));
                    }
                }
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

                        [RelayCommand]
        private async Task PickPhotoAsync()
        {
            try
            {
                var photos = await MediaPicker.Default.PickPhotosAsync();
                var photo = photos.FirstOrDefault();

                if (photo != null)
                {
                    using var stream = await photo.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    
                    var bytes = ms.ToArray();
                    ProfilePictureBase64 = Convert.ToBase64String(bytes);
                    ProfileImageSource = ImageSource.FromStream(() => new MemoryStream(bytes));
                }
            }
            catch (Exception ex)
            {
                await Shell.Current!.DisplayAlertAsync("Photo Error", ex.Message, "OK");
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                await Shell.Current!.DisplayAlertAsync("Error", "Name cannot be empty", "OK");
                return;
            }

            IsBusy = true;
            try
            {
                var userId = Preferences.Get("LoggedInUserId", 0);
                var success = await _api.UpdateProfileAsync(userId, FullName, ProfilePictureBase64);
                if (success)
                {
                    Preferences.Set("LoggedInUserName", FullName);
                    Preferences.Set("LoggedInUserProfilePicture", ProfilePictureBase64 ?? string.Empty);

                    if (_db.CurrentUser is not null)
                    {
                        _db.CurrentUser.FullName = FullName;
                        _db.CurrentUser.ProfilePicture = ProfilePictureBase64 ?? string.Empty;
                    }

                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current!.DisplayAlertAsync("Error", "Failed to update profile", "OK");
                }
            }
            finally { IsBusy = false; }
        }
    }
}






