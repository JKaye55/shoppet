using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Helpers;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly ApiService _apiService;

    public AuthViewModel(DatabaseService databaseService, ApiService apiService)
    {
        _databaseService = databaseService;
        _apiService = apiService;
    }

    [ObservableProperty] private bool _isLogin = true;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool _isPasswordVisible;
    [ObservableProperty] private bool _isConfirmPasswordVisible;
    [ObservableProperty] private string _fullNameError = string.Empty;
    [ObservableProperty] private string _emailError = string.Empty;
    [ObservableProperty] private string _passwordError = string.Empty;
    [ObservableProperty] private string _confirmPasswordError = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _generalError = string.Empty;

    public string WelcomeTitle => IsLogin ? "Welcome Back" : "Create Account";
    public string SubmitText => IsLogin ? "Log In" : "Create Account";
    public string ToggleText => IsLogin ? "Don't have an account? Sign up" : "Already have an account? Log in";

    public bool HasFullNameError => !string.IsNullOrEmpty(FullNameError);
    public bool HasEmailError => !string.IsNullOrEmpty(EmailError);
    public bool HasPasswordError => !string.IsNullOrEmpty(PasswordError);
    public bool HasConfirmPasswordError => !string.IsNullOrEmpty(ConfirmPasswordError);
    public bool HasGeneralError => !string.IsNullOrEmpty(GeneralError);

    partial void OnIsLoginChanged(bool value)
    {
        OnPropertyChanged(nameof(WelcomeTitle));
        OnPropertyChanged(nameof(SubmitText));
        OnPropertyChanged(nameof(ToggleText));
    }

    partial void OnFullNameErrorChanged(string value) => OnPropertyChanged(nameof(HasFullNameError));
    partial void OnEmailErrorChanged(string value) => OnPropertyChanged(nameof(HasEmailError));
    partial void OnPasswordErrorChanged(string value) => OnPropertyChanged(nameof(HasPasswordError));
    partial void OnConfirmPasswordErrorChanged(string value) => OnPropertyChanged(nameof(HasConfirmPasswordError));
    partial void OnGeneralErrorChanged(string value) => OnPropertyChanged(nameof(HasGeneralError));

    [RelayCommand]
    private void ToggleMode()
    {
        IsLogin = !IsLogin;
        ClearErrors();
    }

    private void ClearErrors()
    {
        FullNameError = string.Empty;
        EmailError = string.Empty;
        PasswordError = string.Empty;
        ConfirmPasswordError = string.Empty;
        GeneralError = string.Empty;
    }

    [RelayCommand]
    private void TogglePassword() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private void ToggleConfirmPassword() => IsConfirmPasswordVisible = !IsConfirmPasswordVisible;

    [RelayCommand]
    private void AutoFillPetOwner()
    {
        IsLogin = true;
        Email = "petowner@shoppetcare.local";
        Password = "ShoppetCarePass123!";
        ClearErrors();
    }

    [RelayCommand]
    private void AutoFillDemoUser(string role)
    {
        IsLogin = true;
        if (role == "admin")
        {
            Email = "admin@shoppetcare.local";
            Password = "ShoppetCareAdmin123!";
        }
        else
        {
            Email = "demo-owner@shoppetcare.local";
            Password = "A happy ShoppetCare demo passphrase";
        }
        ClearErrors();
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        ClearErrors();
        bool hasError = false;

        if (string.IsNullOrWhiteSpace(Email))
        {
            EmailError = "Email is required.";
            hasError = true;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            PasswordError = "Password is required.";
            hasError = true;
        }

        if (hasError) return;

        IsBusy = true;
        try
        {
            if (IsLogin)
            {
                // ── LOGIN via API ──────────────────────────────────────────────
                var result = await _apiService.LoginAsync(Email, Password);
                if (result.Success && result.Data is not null)
                {
                    _apiService.SetToken(result.Data.Token);
                    await SecureStorage.SetAsync("ApiToken",result.Data.Token);

                    // ⭐ Save user session locally for API mapping + Community RBAC
                    Preferences.Set("LoggedInUserId", result.Data.UserId);
                    Preferences.Set("LoggedInUserName", result.Data.FullName);
                    Preferences.Set("LoggedInUserRole", string.IsNullOrEmpty(result.Data.Role) ? "PetOwner" : result.Data.Role);

                    _databaseService.CurrentUser = new User
                    {
                        Id = result.Data.UserId,
                        FullName = result.Data.FullName,
                        Email = result.Data.Email,
                        Password = string.Empty,   // never store plaintext from API
                        Role = string.IsNullOrEmpty(result.Data.Role) ? "PetOwner" : result.Data.Role
                    };
                    await NavigationHelper.GoToMainShellAsync();
                }
                else
                {
                    PasswordError = result.Error ?? "Invalid email or password.";
                }
            }
            else
            {
                // ── SIGN UP ────────────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(FullName))
                {
                    FullNameError = "Full Name is required.";
                    hasError = true;
                }
                else if (FullName.Trim().Length < 4)
                {
                    FullNameError = "Full Name must be at least 4 characters.";
                    hasError = true;
                }

                if (Password.Length < 15 || Password.Length > 64) { PasswordError="Use a passphrase of 15–64 characters.";hasError=true; }
                if (Password != ConfirmPassword)
                {
                    ConfirmPasswordError = "Passwords do not match.";
                    hasError = true;
                }

                if (hasError) return;

                // ── REGISTER via API ───────────────────────────────────────────
                var result = await _apiService.RegisterAsync(FullName.Trim(), Email.Trim(), Password);
                if (result.Success && result.Data is not null)
                {
                    _apiService.SetToken(result.Data.Token);
                    await SecureStorage.SetAsync("ApiToken",result.Data.Token);

                    // ⭐ Save user session locally for API mapping + Community RBAC
                    Preferences.Set("LoggedInUserId", result.Data.UserId);
                    Preferences.Set("LoggedInUserName", result.Data.FullName);
                    Preferences.Set("LoggedInUserRole", string.IsNullOrEmpty(result.Data.Role) ? "PetOwner" : result.Data.Role);

                    _databaseService.CurrentUser = new User
                    {
                        Id = result.Data.UserId,
                        FullName = result.Data.FullName,
                        Email = result.Data.Email,
                        Password = string.Empty,
                        Role = string.IsNullOrEmpty(result.Data.Role) ? "PetOwner" : result.Data.Role
                    };
                    await NavigationHelper.GoToMainShellAsync();
                }
                else
                {
                    var error = result.Error ?? "Registration failed.";
                    if (error.Contains("Email", StringComparison.OrdinalIgnoreCase))
                        EmailError = error;
                    else
                        GeneralError = error;
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}