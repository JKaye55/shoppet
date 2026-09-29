namespace ShoppetApp.Pages;

[QueryProperty(nameof(ImageUrl), "ImageUrl")]
public partial class ImagePreviewPage : ContentPage
{
    private string? _imageUrl;
    public string? ImageUrl
    {
        get => _imageUrl;
        set
        {
            _imageUrl = value;
            if (value != null && (value.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)))
            {
                PreviewImage.IsVisible = false;
                PreviewVideo.IsVisible = true;
                PreviewVideo.Source = value;
            }
            else
            {
                PreviewImage.IsVisible = true;
                PreviewVideo.IsVisible = false;
                PreviewImage.Source = value;
            }
        }
    }

    public ImagePreviewPage()
    {
        InitializeComponent();
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        if (PreviewVideo.IsVisible)
        {
            PreviewVideo.Stop();
        }
        await Shell.Current.GoToAsync("..");
    }
}
