using ShoppetApp.ViewModels;

namespace ShoppetApp.Pages
{
    public partial class EditProfilePage : ContentPage
    {
        public EditProfilePage(EditProfileViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}
