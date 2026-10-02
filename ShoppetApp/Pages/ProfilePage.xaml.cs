using ShoppetApp.ViewModels;
using ContactModel = ShoppetApp.Models.Contact;

namespace ShoppetApp.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;
    private ContactModel? _activeContact;

    public ProfilePage() : this(App.Services.GetRequiredService<ProfileViewModel>()) { }

    public ProfilePage(ProfileViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
        _viewModel.ShowContactRequested += OnShowContactRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Opacity = 0;
        await this.FadeToAsync(1, 200);
        await _viewModel.LoadAsync();
    }

    // -- Contact Detail Modal --

    private void OnShowContactRequested(object? sender, ContactModel contact)
    {
        _activeContact = contact;

        ModalContactInitial.Text = string.IsNullOrWhiteSpace(contact.Name)
            ? "?"
            : contact.Name.Trim()[0].ToString().ToUpper();

        ModalContactName.Text = contact.Name;
        ModalContactRole.Text = string.IsNullOrWhiteSpace(contact.Role) ? "Contact" : contact.Role;
        ModalContactPhone.Text = string.IsNullOrWhiteSpace(contact.Phone) ? "No phone saved" : contact.Phone;
        ModalContactAddress.Text = string.IsNullOrWhiteSpace(contact.Address) ? "No address saved" : contact.Address;

        ModalEmergencyBadge.IsVisible = contact.IsEmergency;
        ModalAddressRow.IsVisible = !string.IsNullOrWhiteSpace(contact.Address);

        ModalCallBtn.IsEnabled = !string.IsNullOrWhiteSpace(contact.Phone);
        ModalCallBtn.Opacity = ModalCallBtn.IsEnabled ? 1.0 : 0.45;

        ContactDetailModal.IsVisible = true;
        ContactModalCard.Scale = 0.85;
        ContactModalCard.Opacity = 0;
        _ = Task.WhenAll(
            ContactModalCard.ScaleToAsync(1, 220, Easing.CubicOut),
            ContactModalCard.FadeToAsync(1, 180)
        );
    }

    private void OnDismissContactModal(object? sender, EventArgs e)
    {
        _ = Task.WhenAll(
            ContactModalCard.ScaleToAsync(0.88, 160, Easing.CubicIn),
            ContactModalCard.FadeToAsync(0, 140)
        ).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
        {
            ContactDetailModal.IsVisible = false;
            _activeContact = null;
        }));
    }

    private void OnModalBodyTapped(object? sender, TappedEventArgs e) { }

    private async void OnModalCallClicked(object? sender, EventArgs e)
    {
        if (_activeContact == null || string.IsNullOrWhiteSpace(_activeContact.Phone)) return;
        try
        {
            if (PhoneDialer.Default.IsSupported)
                PhoneDialer.Default.Open(_activeContact.Phone);
            else
                await Launcher.Default.OpenAsync($"tel:{_activeContact.Phone}");
        }
        catch
        {
            await DisplayAlertAsync("Phone", "Unable to open dialer.", "OK");
        }
    }
}
