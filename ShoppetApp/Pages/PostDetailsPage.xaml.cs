using ShoppetApp.Models;
using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages;

public partial class PostDetailsPage : ContentPage
{
    private PostDetailsViewModel? _vm => BindingContext as PostDetailsViewModel;

    public PostDetailsPage(PostDetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnCommentLikeTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Label label && label.BindingContext is CommunityComment comment)
        {
            _vm?.ToggleCommentLikeCommand.Execute(comment);
        }
    }

    private void OnReplyTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Label label && label.BindingContext is CommunityComment comment)
        {
            _vm?.StartReplyCommand.Execute(comment);
            CommentEditor?.Focus();
        }
    }
}
