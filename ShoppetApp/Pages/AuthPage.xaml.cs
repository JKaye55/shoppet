using ShoppetApp.Helpers;
using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages;

public partial class AuthPage : ContentPage
{
    private readonly AuthViewModel _viewModel;
    private bool _initialized;

    public AuthPage() : this(App.Services.GetRequiredService<AuthViewModel>()) { }

    public AuthPage(AuthViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_initialized)
        {
            _initialized = true;
            await UpdateSignupFieldsAsync();
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AuthViewModel.IsLogin))
            await UpdateSignupFieldsAsync();
    }

    private async Task UpdateSignupFieldsAsync()
    {
        if (_viewModel.IsLogin)
        {
            await Task.WhenAll(FullNameRow.FadeToAsync(0, 150), ConfirmPasswordRow.FadeToAsync(0, 150));
        }
        else
        {
            FullNameRow.Opacity = 0;
            ConfirmPasswordRow.Opacity = 0;
            await Task.WhenAll(FullNameRow.FadeToAsync(1, 150), ConfirmPasswordRow.FadeToAsync(1, 150));
        }
    }
}
