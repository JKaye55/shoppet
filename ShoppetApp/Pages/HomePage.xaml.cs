using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage() : this(App.Services.GetRequiredService<HomeViewModel>()) { }

    public HomePage(HomeViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Opacity = 0;
        await this.FadeToAsync(1, 200);
        await _viewModel.LoadAsync();
    }

    private bool _slideLeft = false;

    private async void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border tappedCard || tappedCard.BindingContext is not Models.StoryCard tappedStory)
            return;

        // Ensure we only swipe the top card
        var topStory = _viewModel.Stories.OrderByDescending(s => s.ZIndex).FirstOrDefault();
        if (topStory != tappedStory)
            return; // Not the top card

        // Determine swipe direction (alternating)
        int swipeDirection = _slideLeft ? -1 : 1;
        _slideLeft = !_slideLeft;

        var random = new Random();
        double randomRotation = (random.NextDouble() * 16) - 8; // Between -8 and 8 degrees

        // Animate the tapped card out horizontally
        await Task.WhenAll(
            tappedCard.TranslateToAsync(300 * swipeDirection, 0, 250, Easing.CubicIn),
            tappedCard.RotateToAsync(15 * swipeDirection, 250, Easing.CubicIn)
        );

        // Reassign ZIndexes to push the tapped card to the back
        foreach (var story in _viewModel.Stories)
        {
            if (story == tappedStory)
            {
                story.ZIndex = 0;
            }
            else
            {
                story.ZIndex += 1;
            }
        }

        // Animate everyone to their new layer positions
        var animationTasks = new List<Task>();
        foreach (var view in CardsContainer.Children)
        {
            if (view is Border cardView && cardView.BindingContext is Models.StoryCard storyCard)
            {
                if (storyCard == tappedStory)
                {
                    // Animate the swiped card back into the center (at the back)
                    animationTasks.Add(cardView.TranslateToAsync(randomRotation * 0.5, 12, 350, Easing.CubicOut));
                    animationTasks.Add(cardView.RotateToAsync(randomRotation, 350, Easing.CubicOut));
                    animationTasks.Add(cardView.ScaleToAsync(0.9, 350, Easing.CubicOut));
                    animationTasks.Add(cardView.FadeToAsync(0.8, 350));
                    storyCard.Rotation = randomRotation; // Sync model
                }
                else
                {
                    // Animate other cards forward based on new ZIndex
                    int z = storyCard.ZIndex;
                    double targetScale = 1.0 - ((2 - z) * 0.05);
                    double targetTransY = (2 - z) * 6;
                    double targetOpacity = 1.0 - ((2 - z) * 0.1);
                    double targetRot = z == 2 ? 0 : storyCard.Rotation;

                    animationTasks.Add(cardView.TranslateToAsync(0, targetTransY, 350, Easing.SpringOut));
                    animationTasks.Add(cardView.ScaleToAsync(targetScale, 350, Easing.SpringOut));
                    animationTasks.Add(cardView.RotateToAsync(targetRot, 350, Easing.SpringOut));
                    animationTasks.Add(cardView.FadeToAsync(targetOpacity, 350));
                    storyCard.Rotation = targetRot; // Sync model
                }
            }
        }

        await Task.WhenAll(animationTasks);
    }
}
