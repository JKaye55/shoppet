using ShoppetApp.Helpers;

namespace ShoppetApp.Pages;

public partial class OnboardingPage : ContentPage
{
    private readonly (string Title, string Sub, string ImageUrl)[] _slides =
    [
        ("Welcome to Shoppet Care", "Your pet's health and happiness, beautifully organized.",
            "cat.jpg"),
        ("Track Health Records", "Keep vaccines, medications, and vitals right in your pocket.",
            "dog_1.jpg"),
        ("Emergency Ready", "Instantly access vet contacts when you need them most.",
            "dog_3.jpg")
    ];

    private int _currentSlide;

    public OnboardingPage()
    {
        InitializeComponent();
        NavigationPage.SetHasBackButton(this, false);
        ApplySlide(animate: false);
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        if (_currentSlide < 2)
        {
            _currentSlide++;
            await AnimateSlideChange();
            return;
        }

        NavigationHelper.GoToAuth();
    }

    private async Task AnimateSlideChange()
    {
        await TextContent.FadeToAsync(0, 125, Easing.Linear);
        ApplySlide(animate: true);
        await TextContent.FadeToAsync(1, 125, Easing.Linear);
    }

    private void ApplySlide(bool animate)
    {
        var slide = _slides[_currentSlide];
        SlideImage.Source = slide.ImageUrl;
        TitleLabel.Text = slide.Title;
        SubtitleLabel.Text = slide.Sub;
        NextButton.Text = _currentSlide == 2 ? "Get Started" : "Next";
        UpdateDots(animate);
    }

    private void UpdateDots(bool animate)
    {
        var dots = new[] { Dot0, Dot1, Dot2 };
        for (var i = 0; i < dots.Length; i++)
        {
            var dot = dots[i];
            var isActive = i == _currentSlide;
            var targetWidth = isActive ? 32.0 : 8.0;
            dot.Color = isActive
                ? (Color)Application.Current!.Resources["Primary"]
                : (Color)Application.Current!.Resources["Secondary30"];

            if (animate)
                dot.AnimateWidth(dot.WidthRequest, targetWidth, 200);
            else
                dot.WidthRequest = targetWidth;
        }
    }
}
