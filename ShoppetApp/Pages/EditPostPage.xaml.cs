using Microsoft.Maui.Controls;
using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages
{
    public partial class EditPostPage : ContentPage
    {
        public EditPostPage(EditPostViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
