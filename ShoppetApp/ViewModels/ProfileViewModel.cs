using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Helpers;
using ShoppetApp.Messages;
using ShoppetApp.Services;
using ContactModel = ShoppetApp.Models.Contact;

namespace ShoppetApp.ViewModels
{
    public partial class ProfileViewModel : ObservableObject, IRecipient<DataChangedMessage>
    {
        private readonly DatabaseService _db;

        [ObservableProperty] public partial System.Collections.ObjectModel.ObservableCollection<ContactModel> Contacts { get; set; } = [];
        [ObservableProperty] public partial bool IsBusy { get; set; }
                        [ObservableProperty] public partial string FullName { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsAdmin { get; set; }
        [ObservableProperty] public partial ImageSource? ProfileImageSource { get; set; }
        private readonly ShoppetApp.Services.ApiService _api;

        // Raised when a contact row is tapped — page subscribes to show the modal
        public event EventHandler<ShoppetApp.Models.Contact>? ShowContactRequested;

        public ProfileViewModel(DatabaseService db, ShoppetApp.Services.ApiService api)
        {
            _db = db;
            _api = api;
            WeakReferenceMessenger.Default.Register<DataChangedMessage>(this, (r, m) => Receive(m));
        }

        [RelayCommand]
        private async Task EditProfileAsync()
        {
            await Shell.Current.GoToAsync("EditProfilePage");
        }

        public void Receive(DataChangedMessage message) =>
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());

        public async Task LoadAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            try
            {
                                if (_db.CurrentUser != null)
                {
                    FullName = _db.CurrentUser.FullName;

                    // RBAC Role check
                    IsAdmin = _db.CurrentUser.Role == "Admin";
                    
                    var profile = await _api.GetProfileAsync(Preferences.Get("LoggedInUserId", 0));
                    if (profile != null)
                    {
                        FullName = profile.FullName;
                        if (!string.IsNullOrEmpty(profile.ProfilePicture))
                        {
                            var bytes = Convert.FromBase64String(profile.ProfilePicture);
                            ProfileImageSource = ImageSource.FromStream(() => new MemoryStream(bytes));
                        }
                    }
                }
                var contacts = await _db.GetContactsAsync();
                Contacts = new System.Collections.ObjectModel.ObservableCollection<ContactModel>(contacts);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AddContactAsync() =>
            await Shell.Current.GoToAsync("contactform");

        [RelayCommand]
        private async Task EditContactAsync(ContactModel contact) =>
            await Shell.Current.GoToAsync($"contactform?contactId={contact.Id}");

        [RelayCommand]
        private async Task CallContactAsync(ContactModel contact)
        {
            if (string.IsNullOrWhiteSpace(contact.Phone))
                return;

            try
            {
                if (PhoneDialer.Default.IsSupported)
                    PhoneDialer.Default.Open(contact.Phone);
                else
                    await Launcher.Default.OpenAsync($"tel:{contact.Phone}");
            }
            catch
            {
                await Shell.Current.DisplayAlertAsync("Phone", "Unable to open dialer.", "OK");
            }
        }

        [RelayCommand]
        private async Task DeleteContactAsync(ContactModel contact)
        {
            bool confirm = await Shell.Current.DisplayAlertAsync("Delete", $"Are you sure you want to delete {contact.Name}?", "Yes", "No");
            if (!confirm)
                return;

            int result = await _db.DeleteContactAsync(contact);
            if (result > 0)
            {
                WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
                await LoadAsync();
            }
            else
            {
                await Shell.Current.DisplayAlertAsync(
                    "Error",
                    "Failed to delete contact from the server.",
                    "OK");
            }
        }

        [RelayCommand]
        private async Task OpenAdminPanelAsync() =>
            await Shell.Current.DisplayAlertAsync("Admin", "Admin tools cover users, content moderation, and transactions.", "OK");

        [RelayCommand]
        private async Task OpenPostSettingsAsync() =>
            await Shell.Current.GoToAsync("PostSettingsPage");

        [RelayCommand]
        private void ViewContact(ShoppetApp.Models.Contact contact)
        {
            ShowContactRequested?.Invoke(this, contact);
        }

        [RelayCommand]
        private void Logout()
        {
            _api.ClearToken();
            _db.Logout();
            Preferences.Remove("LoggedInUserId");
            Preferences.Remove("LoggedInUserName");
            Preferences.Remove("LoggedInUserEmail");
            Preferences.Remove("LoggedInUserRole");
            Preferences.Remove("LoggedInUserProfilePicture");
            NavigationHelper.GoToAuth();
        }
    }
}






