using CommunityToolkit.Mvvm.ComponentModel;

namespace ShoppetApp.Models;

public partial class StoryCard : ObservableObject
{
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;

    [ObservableProperty] public partial double Rotation { get; set; }
    [ObservableProperty] public partial double TranslationY { get; set; }
    [ObservableProperty] public partial double TranslationX { get; set; }
    [ObservableProperty] public partial double Scale { get; set; } = 1;
    [ObservableProperty] public partial double Opacity { get; set; } = 1;
    [ObservableProperty] public partial int ZIndex { get; set; }
    [ObservableProperty] public partial bool IsActive { get; set; }
}
