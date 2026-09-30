using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.Pages;

public partial class NotificationsPage : ContentPage
{
    private readonly ApiService _api;
    public NotificationsPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Loading.IsVisible = Loading.IsRunning = true;
        try { NotificationsView.ItemsSource = await _api.GetNotificationsAsync(); }
        finally { Loading.IsRunning = Loading.IsVisible = false; }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnReadAllClicked(object? sender, EventArgs e)
    {
        await _api.MarkAllNotificationsReadAsync();
        await LoadAsync();
    }

    private async void OnMarkReadClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: NotificationItem item })
        {
            await _api.MarkNotificationReadAsync(item.Id);
            await LoadAsync();
        }
    }
}
