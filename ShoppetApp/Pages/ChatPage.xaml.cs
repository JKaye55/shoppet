using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;

        public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        _viewModel.Messages.CollectionChanged += (s, e) =>
        {
            if (_viewModel.Messages.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MessagesList.ScrollTo(_viewModel.Messages.LastOrDefault(), null, ScrollToPosition.End, true);
                });
            }
        };
    }
    
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopPolling();
    }
}

