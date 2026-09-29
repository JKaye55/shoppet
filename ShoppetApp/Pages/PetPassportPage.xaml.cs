using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages;

public partial class PetPassportPage : ContentPage, IQueryAttributable
{
    private readonly PetPassportViewModel _viewModel;
    private bool _isHealthTab = true;

    public PetPassportPage() : this(App.Services.GetRequiredService<PetPassportViewModel>()) { }

    public PetPassportPage(PetPassportViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
        ApplyTabStyle(activeHealth: true, animate: false);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _viewModel.ApplyQueryAttributes(query);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TranslationX = Width > 0 ? Width : 400;
        await this.TranslateToAsync(0, 0, 300, Easing.SinOut);
        await _viewModel.LoadAsync();
    }

    private async void OnHealthTabTapped(object? sender, TappedEventArgs e)
    {
        if (_isHealthTab) return;
        _isHealthTab = true;
        await SwitchTabAsync(showHealth: true);
    }

    private async void OnFoodTabTapped(object? sender, TappedEventArgs e)
    {
        if (!_isHealthTab) return;
        _isHealthTab = false;
        await SwitchTabAsync(showHealth: false);
    }

    private async Task SwitchTabAsync(bool showHealth)
    {
        // Fade out the visible section
        var hideSection = showHealth ? FoodSection : HealthSection;
        var showSection = showHealth ? HealthSection : FoodSection;

        await hideSection.FadeToAsync(0, 150, Easing.SinOut);
        hideSection.IsVisible = false;

        showSection.Opacity = 0;
        showSection.IsVisible = true;
        await showSection.FadeToAsync(1, 200, Easing.SinIn);

        ApplyTabStyle(activeHealth: showHealth, animate: true);
    }

    private void ApplyTabStyle(bool activeHealth, bool animate)
    {
        var resources = Application.Current!.Resources;
        var primary = (Color)resources["Primary"];
        var primary10 = (Color)resources["Primary10"];
        var muted = (Color)resources["NavMuted"];
        var transparent = Colors.Transparent;

        // Active pill: Primary10 background, Primary text
        HealthTabPill.BackgroundColor = activeHealth ? primary10 : transparent;
        HealthTabLabel.TextColor = activeHealth ? primary : muted;

        FoodTabPill.BackgroundColor = activeHealth ? transparent : primary10;
        FoodTabLabel.TextColor = activeHealth ? muted : primary;
    }
}
