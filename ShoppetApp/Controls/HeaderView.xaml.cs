using ShoppetApp.Services;

namespace ShoppetApp.Controls;

public partial class HeaderView : ContentView
{
    public HeaderView()
    {
        InitializeComponent();
    }

    private async void OnMessagesClicked(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("messages");
    }
}
