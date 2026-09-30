using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Collections.ObjectModel;

namespace ShoppetApp.ViewModels;

public partial class MarketplaceCartViewModel : ObservableObject
{
    private readonly ApiService _api;

    [ObservableProperty] public partial ObservableCollection<MarketplaceCartItemDto> CartItems { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<MarketplaceOrderDto> Purchases { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<MarketplaceOrderDto> Sales { get; set; } = [];
    [ObservableProperty] public partial decimal TotalAmount { get; set; }
    [ObservableProperty] public partial string SelectedPaymentMethod { get; set; } = "GCash Simulation";
    [ObservableProperty] public partial string SelectedSimulationResult { get; set; } = "Successful payment";
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public IList<string> PaymentMethods { get; } = ["GCash Simulation", "Cash on Meetup"];
    public IList<string> SimulationResults { get; } = ["Successful payment", "Failed payment"];

    public bool HasCartItems => CartItems.Count > 0;
    public string TotalDisplay => $"₱{TotalAmount:N2}";

    public MarketplaceCartViewModel(ApiService api) => _api = api;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            var cart = await _api.GetMarketplaceCartAsync();
            var purchases = await _api.GetMarketplacePurchasesAsync();
            var sales = await _api.GetMarketplaceSalesAsync();

            CartItems = new ObservableCollection<MarketplaceCartItemDto>(cart?.Items ?? []);
            TotalAmount = cart?.TotalAmount ?? 0;
            Purchases = new ObservableCollection<MarketplaceOrderDto>(purchases);
            Sales = new ObservableCollection<MarketplaceOrderDto>(sales);

            OnPropertyChanged(nameof(HasCartItems));
            OnPropertyChanged(nameof(TotalDisplay));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(MarketplaceCartItemDto item)
    {
        if (item is null || IsBusy) return;

        IsBusy = true;
        try
        {
            var cart = await _api.RemoveMarketplaceCartItemAsync(item.ListingId);
            if (cart is null)
            {
                await Shell.Current.DisplayAlertAsync("Could not remove", "The item could not be removed from your cart.", "OK");
                return;
            }

            CartItems = new ObservableCollection<MarketplaceCartItemDto>(cart.Items);
            TotalAmount = cart.TotalAmount;
            OnPropertyChanged(nameof(HasCartItems));
            OnPropertyChanged(nameof(TotalDisplay));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (IsBusy || !HasCartItems) return;

        bool simulateSuccess = SelectedSimulationResult == "Successful payment";

        var confirm = await Shell.Current.DisplayAlertAsync(
            "Run payment simulation?",
            $"Method: {SelectedPaymentMethod}\nResult: {SelectedSimulationResult}\n\nNo real money will be processed.",
            "Continue",
            "Cancel");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            var result = await _api.CheckoutMarketplaceAsync(SelectedPaymentMethod, simulateSuccess);
            if (result is null)
            {
                await Shell.Current.DisplayAlertAsync("Checkout error", "The payment simulation could not be completed.", "OK");
                return;
            }

            await Shell.Current.DisplayAlertAsync(
                result.Success ? "Simulation complete" : "Simulation failed",
                result.Message,
                "OK");

            await LoadAfterCheckoutAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAfterCheckoutAsync()
    {
        var cart = await _api.GetMarketplaceCartAsync();
        var purchases = await _api.GetMarketplacePurchasesAsync();
        var sales = await _api.GetMarketplaceSalesAsync();

        CartItems = new ObservableCollection<MarketplaceCartItemDto>(cart?.Items ?? []);
        TotalAmount = cart?.TotalAmount ?? 0;
        Purchases = new ObservableCollection<MarketplaceOrderDto>(purchases);
        Sales = new ObservableCollection<MarketplaceOrderDto>(sales);
        OnPropertyChanged(nameof(HasCartItems));
        OnPropertyChanged(nameof(TotalDisplay));
    }

    [RelayCommand]
    private async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}
