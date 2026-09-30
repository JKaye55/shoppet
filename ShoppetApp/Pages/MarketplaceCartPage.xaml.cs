using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages;

public partial class MarketplaceCartPage : ContentPage
{
    private readonly MarketplaceCartViewModel _vm;

    public MarketplaceCartPage(MarketplaceCartViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
