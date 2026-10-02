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
            StatusIconLabel.Text = "⚠️";
            ActivateButton.IsEnabled = false;
            return;
        }

        if (status.IsPremium)
        {
            StatusLabel.Text = $"Premium Member{(status.ActivatedAt is DateTime d ? $" • since {d:MMM d, yyyy}" : string.Empty)}";
            StatusIconLabel.Text = "💎";
            ActivateButton.IsVisible = false;
        }
        else
        {
            StatusLabel.Text = "Free Account (Standard Limits)";
            StatusIconLabel.Text = "🐾";
            ActivateButton.IsVisible = true;
            ActivateButton.IsEnabled = true;
        }
    }

    private async void OnActivateClicked(object? sender, EventArgs e)
    {
        // Step 1: Predefined payment method selection
        var method = await DisplayActionSheetAsync(
            "Select Predefined Demo Payment Method",
            "Cancel",
            null,
            "💳 Demo Visa (•••• 4242)",
            "📱 Demo GCash (0917-•••-1234)",
            "⚡ Demo Maya (0918-•••-5678)");

        if (string.IsNullOrEmpty(method) || method == "Cancel")
            return;

        // Step 2: Simulation outcome selection
        var outcome = await DisplayActionSheetAsync(
            $"Demo Checkout: {method} • ₱150",
            "Cancel",
            null,
            "✅ Approved (Simulate Success)",
            "❌ Declined (Simulate Card Failure)");

        if (string.IsNullOrEmpty(outcome) || outcome == "Cancel")
            return;

        if (outcome.Contains("Declined"))
        {
            await DisplayAlertAsync(
                "Simulated Payment Declined",
                "The demo transaction was declined. Your account remains on the Free tier and no charges were made.",
                "OK");
            return;
        }

        // Step 3: Confirmation
        var confirm = await DisplayAlertAsync(
            "Confirm Demo Upgrade",
            $"Activate ShoppetCare Premium for 2 months (₱150.00) using {method}? This is an academic sandbox transaction.",
            "Confirm & Activate",
            "Cancel");

        if (!confirm) return;

        ActivateButton.IsEnabled = false;
        ActivateButton.Text = "Activating Simulation...";

        try
        {
            var success = await _api.ActivatePremiumAsync();
            if (success)
            {
                Preferences.Set("LoggedInUserIsPremium", true);
                var refCode = "PREM-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
                ReceiptRefLabel.Text = $"Ref: {refCode} • {method}";
                ReceiptDurationLabel.Text = $"Valid Until: {DateTime.Now.AddDays(60):MMMM d, yyyy} (60 Days)";
                ReceiptAmountLabel.Text = "Amount Paid: ₱150.00 (Demo Sandbox)";
                ReceiptCard.IsVisible = true;

                await DisplayAlertAsync(
                    "Upgrade Complete!",
                    "ShoppetCare Premium has been activated for 2 months. All limits have been lifted.",
                    "View Receipt");

                await RefreshAsync();
            }
            else
            {
                await DisplayAlertAsync("Premium", "The simulated upgrade could not be completed. Please check your connection.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Could not reach ShoppetAPI: {ex.Message}", "OK");
        }
        finally
        {
            ActivateButton.IsEnabled = true;
            ActivateButton.Text = "Upgrade to Premium · ₱150 (Demo)";
        }
    }

    private async void OnOpenWebPremiumClicked(object? sender, EventArgs e)
    {
        var webUrl = Preferences.Get("PublicWebBaseUrl", "http://localhost:5253").TrimEnd('/');
        var target = $"{webUrl}/premium";
        try
        {
            await Launcher.Default.OpenAsync(new Uri(target));
        }
        catch
        {
            await DisplayAlertAsync("Web Portal", $"Please visit {target} in your browser to avail Premium.", "OK");
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
}
