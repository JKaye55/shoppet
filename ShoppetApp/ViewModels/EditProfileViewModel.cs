using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels;

public partial class EditProfileViewModel : ObservableObject
{
    private const long MaxProfilePhotoBytes = 5 * 1024 * 1024;
    private readonly ApiService _api;
    private readonly DatabaseService _db;

    public EditProfileViewModel(ApiService api, DatabaseService db)
    {
        _api = api;
        _db = db;
        _ = LoadDataAsync();
    }

    [ObservableProperty] public partial string FullName { get; set; } = string.Empty;
    [ObservableProperty] public partial string MobileNumber { get; set; } = string.Empty;
    [ObservableProperty] public partial string FacebookUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string InstagramUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string OtherSocialUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial bool ShowSocialLinksOnMarketplace { get; set; }
    [ObservableProperty] public partial bool ShowMobileOnPublicPetId { get; set; }
    [ObservableProperty] public partial string ProfilePictureBase64 { get; set; } = string.Empty;
    [ObservableProperty] public partial ImageSource? ProfileImageSource { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var userId = Preferences.Get("LoggedInUserId", 0);
            FullName = Preferences.Get("LoggedInUserName", string.Empty);

            var profile = await _api.GetProfileAsync(userId);
            if (profile is null) return;

            FullName = profile.FullName;
            MobileNumber = profile.MobileNumber;
            FacebookUrl = profile.FacebookUrl;
            InstagramUrl = profile.InstagramUrl;
            OtherSocialUrl = profile.OtherSocialUrl;
            ShowSocialLinksOnMarketplace = profile.ShowSocialLinksOnMarketplace;
            ShowMobileOnPublicPetId = profile.ShowMobileOnPublicPetId;
            ProfilePictureBase64 = profile.ProfilePicture;

            if (!string.IsNullOrWhiteSpace(ProfilePictureBase64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(ProfilePictureBase64);
                    ProfileImageSource = ImageSource.FromStream(() => new MemoryStream(bytes));
                }
                catch
                {
                    ProfilePictureBase64 = string.Empty;
                    ProfileImageSource = null;
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        if (IsBusy) return;

        try
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(
                new MediaPickerOptions { Title = "Choose a profile photo" });

            var photo = photos.FirstOrDefault();
            if (photo is null) return;

            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
            {
                await Shell.Current.DisplayAlertAsync(
                    "Unsupported photo",
                    "Choose a JPG, PNG, or WebP image.",
                    "OK");
                return;
            }

            await using var stream = await photo.OpenReadAsync();
            if (stream.CanSeek && stream.Length > MaxProfilePhotoBytes)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Photo too large",
                    "Choose a profile photo smaller than 5 MB.",
                    "OK");
                return;
            }

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            ProfilePictureBase64 = Convert.ToBase64String(bytes);
            ProfileImageSource = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch
        {
            await Shell.Current.DisplayAlertAsync(
                "Photo error",
                "The profile photo could not be selected. Please try another image.",
                "OK");
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        var cleanName = (FullName ?? string.Empty).Trim();
        var cleanMobile = new string((MobileNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        var facebook = (FacebookUrl ?? string.Empty).Trim();
        var instagram = (InstagramUrl ?? string.Empty).Trim();
        var other = (OtherSocialUrl ?? string.Empty).Trim();

        if (cleanName.Length < 2 || cleanName.Length > 80)
        {
            await Shell.Current.DisplayAlertAsync(
                "Check full name",
                "Full name must be between 2 and 80 characters.",
                "OK");
            return;
        }

        if (!string.IsNullOrWhiteSpace(cleanMobile) &&
            (cleanMobile.Length != 11 || !cleanMobile.StartsWith("09")))
        {
            await Shell.Current.DisplayAlertAsync(
                "Check mobile number",
                "Use the Philippine mobile format 09XXXXXXXXX.",
                "OK");
            return;
        }

        static bool IsValidOptionalUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            return value.Length <= 500 &&
                   Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        if (!IsValidOptionalUrl(facebook) ||
            !IsValidOptionalUrl(instagram) ||
            !IsValidOptionalUrl(other))
        {
            await Shell.Current.DisplayAlertAsync(
                "Check social links",
                "Social links must be complete web addresses beginning with http:// or https://.",
                "OK");
            return;
        }

        if (ShowSocialLinksOnMarketplace &&
            string.IsNullOrWhiteSpace(facebook) &&
            string.IsNullOrWhiteSpace(instagram) &&
            string.IsNullOrWhiteSpace(other))
        {
            await Shell.Current.DisplayAlertAsync(
                "No seller links added",
                "Add at least one social link or turn off marketplace link visibility.",
                "OK");
            return;
        }

        if (ShowMobileOnPublicPetId && string.IsNullOrWhiteSpace(cleanMobile))
        {
            await Shell.Current.DisplayAlertAsync(
                "Mobile number required",
                "Add a mobile number before allowing it to appear on a public Pet ID.",
                "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var userId = Preferences.Get("LoggedInUserId", 0);
            if (userId <= 0)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Session expired",
                    "Please sign in again before updating your profile.",
                    "OK");
                return;
            }

            var success = await _api.UpdateProfileAsync(
                userId,
                cleanName,
                ProfilePictureBase64,
                cleanMobile,
                facebook,
                instagram,
                other,
                ShowSocialLinksOnMarketplace,
                ShowMobileOnPublicPetId);

            if (success)
            {
                FullName = cleanName;
                MobileNumber = cleanMobile;

                Preferences.Set("LoggedInUserName", cleanName);
                Preferences.Set("LoggedInUserProfilePicture", ProfilePictureBase64 ?? string.Empty);

                if (_db.CurrentUser is not null)
                {
                    _db.CurrentUser.FullName = cleanName;
                    _db.CurrentUser.ProfilePicture = ProfilePictureBase64 ?? string.Empty;
                }

                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync(
                    "Could not save",
                    "ShoppetCare could not update your profile. Check the fields and try again.",
                    "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
