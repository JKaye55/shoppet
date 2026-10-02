using ShoppetApp.Services;

namespace ShoppetApp.Pages;

public partial class OrdersPage : ContentPage
{
    private readonly ApiService _api;
    public OrdersPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Loading.IsVisible = Loading.IsRunning = true;
        try { OrdersView.ItemsSource = await _api.GetOrdersAsync(); }
        catch(Exception){await DisplayAlertAsync("Orders unavailable","Check your connection and sign in again if your session expired.","OK");}
        finally { Loading.IsRunning = Loading.IsVisible = false; }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
