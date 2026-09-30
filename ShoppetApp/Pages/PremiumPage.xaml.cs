using ShoppetApp.Services;

namespace ShoppetApp.Pages;

public partial class PremiumPage : ContentPage
{
    private readonly ApiService _api;
    public PremiumPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var status = await _api.GetPremiumStatusAsync();
        if (status is null)
        {
            StatusLabel.Text = "Unable to load Premium status";
            ActivateButton.IsEnabled = false;
            return;
        }

        StatusLabel.Text = status.IsPremium
            ? $"Premium active{(status.ActivatedAt is DateTime d ? $" • {d:MMM d, yyyy}" : string.Empty)}"
            : "Free account";
        ActivateButton.IsVisible = !status.IsPremium;
    }

    private async void OnActivateClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Premium Simulation",
            "Activate the ₱49 Premium upgrade as a mock/demo transaction? No real money will be charged.",
            "Simulate Upgrade",
            "Cancel");
        if (!confirm) return;

        ActivateButton.IsEnabled = false;
        try
        {
            if (await _api.ActivatePremiumAsync())
            {
                await DisplayAlertAsync("Premium Active", "Simulation completed. Your shared account is now Premium.", "OK");
                await RefreshAsync();
            }
            else
            {
                await DisplayAlertAsync("Premium", "The simulated upgrade could not be completed.", "OK");
            }
        }
        finally { ActivateButton.IsEnabled = true; }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
