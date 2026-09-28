using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages;

public partial class MessagesPage : ContentPage
{
    private readonly MessagesViewModel _viewModel;
    private bool _isFirstLoad = true;

    public MessagesPage(MessagesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Always reload when appearing so the conversation list reflects 
        // the latest state (including unread counts reset by the API)
        await _viewModel.LoadConversationsAsync();
        _isFirstLoad = false;
    }
}