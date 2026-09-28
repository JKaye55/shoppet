using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _viewModel;
    private bool _needsScroll = false;

    public ChatPage(ChatViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;

        // Hook into the event fired after messages load
        _viewModel.OnMessagesLoaded += OnMessagesLoaded;

        // SizeChanged fires every time the CollectionView finishes rendering items
        // This is the most reliable hook in MAUI Android to scroll after layout
        MessagesList.SizeChanged += OnMessagesListSizeChanged;
    }

    private void OnMessagesLoaded()
    {
        // Mark that we need to scroll — the SizeChanged event will do the actual scroll
        // once Android finishes rendering the new items
        _needsScroll = true;
        
        // Also attempt immediately in case SizeChanged doesn't fire (e.g. same count)
        ScrollToLatest();
    }

    private void OnMessagesListSizeChanged(object? sender, EventArgs e)
    {
        if (_needsScroll)
        {
            ScrollToLatest();
            _needsScroll = false;
        }
    }

    private void ScrollToLatest()
    {
        var lastItem = _viewModel.Messages.LastOrDefault();
        if (lastItem == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            MessagesList.ScrollTo(lastItem, null, ScrollToPosition.End, false);
        });

        // Belt-and-suspenders: retry after layout settles
        Application.Current?.Dispatcher.DispatchDelayed(
            TimeSpan.FromMilliseconds(500),
            () =>
            {
                var last = _viewModel.Messages.LastOrDefault();
                if (last != null)
                {
                    MessagesList.ScrollTo(last, null, ScrollToPosition.End, false);
                }
            });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Scroll on page appear as a final safety net
        _needsScroll = true;
        Application.Current?.Dispatcher.DispatchDelayed(
            TimeSpan.FromMilliseconds(600),
            ScrollToLatest);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopPolling();
    }
}