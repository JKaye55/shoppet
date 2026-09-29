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
        if (ContactId == 0) return;
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
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("Validation", "Name or clinic is required.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(Phone))
        {
            await Shell.Current.DisplayAlert("Validation", "Phone number is required.", "OK");
            return;
        }

        var contact = new ContactModel
        {
            Id = ContactId,
            UserId = Preferences.Get("LoggedInUserId", 0),
            Name = Name.Trim(),
            Role = Role,
            Address = Address?.Trim() ?? string.Empty,
            Phone = Phone?.Trim() ?? string.Empty,
            IsEmergency = IsEmergency
        };

        int result = await _db.SaveContactAsync(contact);
        if (result > 0)
        {
            WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await Shell.Current.DisplayAlert("Error", "Failed to save contact to server.", "OK");
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (ContactId == 0) return;
        var contact = await _db.GetContactAsync(ContactId);
        if (contact is null) return;

        bool confirm = await Shell.Current.DisplayAlert("Delete", $"Remove {contact.Name}?", "Delete", "Cancel");
        if (!confirm) return;

        await _db.DeleteContactAsync(contact);
        WeakReferenceMessenger.Default.Send(DataChangedMessage.Instance);
        await Shell.Current.GoToAsync("..");
    }
}