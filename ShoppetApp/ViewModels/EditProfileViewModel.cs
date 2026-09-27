using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels
{
    public partial class EditProfileViewModel : ObservableObject
    {
        private readonly ApiService _api;

        public EditProfileViewModel(ApiService api)
        {
            _api = api;
            _ = LoadDataAsync();
        }

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _profilePictureBase64 = string.Empty;

        [ObservableProperty]
        private ImageSource? _profileImageSource;

        [ObservableProperty]
        private bool _isBusy;

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
                var photo = await MediaPicker.PickPhotoAsync();
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
                await Application.Current!.MainPage!.DisplayAlert("Photo Error", ex.Message, "OK");
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                await Application.Current!.MainPage!.DisplayAlert("Error", "Name cannot be empty", "OK");
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
                    // Force refresh of ProfileViewModel if needed by raising an event, or it will refresh on load.
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Application.Current!.MainPage!.DisplayAlert("Error", "Failed to update profile", "OK");
                }
            }
            finally { IsBusy = false; }
        }
    }
}






