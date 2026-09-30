using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ShoppetApp.Messages;
using ShoppetApp.Services;
using ContactModel = ShoppetApp.Models.Contact;

namespace ShoppetApp.ViewModels;

public partial class ContactFormViewModel : ObservableObject, IQueryAttributable
{
    private readonly DatabaseService _db;

    [ObservableProperty] public partial int ContactId { get; set; }
    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    [ObservableProperty] public partial string Role { get; set; } = "Veterinarian";
    [ObservableProperty] public partial string Address { get; set; } = string.Empty;
    [ObservableProperty] public partial string Phone { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsEmergency { get; set; } = true;
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public IList<string> RoleOptions { get; } = ["Veterinarian", "Clinic", "Family", "Pet Sitter", "Groomer", "Other"];

    public string Title => ContactId != 0 ? "Edit Contact" : "Add Contact";
    public bool CanDelete => ContactId != 0;

    public ContactFormViewModel(DatabaseService db) => _db = db;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("contactId", out var val))
        {
            ContactId = Convert.ToInt32(val);
            _ = LoadAsync();
        }
    }

    public async Task LoadAsync()
    {
        if (ContactId == 0 || IsBusy) return;
        var contact = await _db.GetContactAsync(ContactId);
        if (contact is null) return;

        Name = contact.Name;
        Role = string.IsNullOrEmpty(contact.Role) ? "Veterinarian" : contact.Role;
        Address = contact.Address;
        Phone = contact.Phone;
        IsEmergency = contact.IsEmergency;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(CanDelete));
    }

    [RelayCommand]
    private async Task CloseAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        var cleanName = (Name ?? string.Empty).Trim();
        var cleanAddress = (Address ?? string.Empty).Trim();
        var cleanPhone = (Phone ?? string.Empty).Trim();

        if (cleanName.Length < 2 || cleanName.Length > 80)
        {
            await Shell.Current.DisplayAlertAsync("Check name", "Contact name must be between 2 and 80 characters.", "OK");
            return;
        }

        if (!RoleOptions.Contains(Role))
        {
            await Shell.Current.DisplayAlertAsync("Choose role", "Select a contact role from the list.", "OK");
            return;
        }

        var phoneDigits = new string(cleanPhone.Where(char.IsDigit).ToArray());
        if (phoneDigits.Length < 7 || phoneDigits.Length > 15)
        {
            await Shell.Current.DisplayAlertAsync("Check phone number", "Enter a valid phone number with 7 to 15 digits.", "OK");
            return;
        }

        if (cleanAddress.Length > 200)
        {
            await Shell.Current.DisplayAlertAsync("Address too long", "Address can contain up to 200 characters.", "OK");
            return;
        }

        var contact = new ContactModel
        {
            Id = ContactId,
            UserId = Preferences.Get("LoggedInUserId", 0),
            Name = cleanName,
            Role = Role,
            Address = cleanAddress,
            Phone = cleanPhone,
            IsEmergency = IsEmergency
        };

        IsBusy = true;
        try
        {
            int result = await _db.SaveContactAsync(contact);
            if (result > 0)
            {
                WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Shell.Current.DisplayAlertAsync("Could not save", "The contact could not be saved. Please try again.", "OK");
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
        if (ContactId == 0) return;
        var contact = await _db.GetContactAsync(ContactId);
        if (contact is null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync("Delete", $"Remove {contact.Name}?", "Delete", "Cancel");
        if (!confirm) return;

        int result = await _db.DeleteContactAsync(contact);
        if (result > 0)
        {
            WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Shell.Current.DisplayAlertAsync(
                "Error",
                "Failed to delete contact from the server.",
                "OK");
        }
    }
}