using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Models;
using ShoppetApp.Services;

namespace ShoppetApp.ViewModels
{
    [QueryProperty(nameof(PostToEdit), "PostToEdit")]
    public partial class EditPostViewModel : ObservableObject
    {
        private readonly ApiService _api;
        private readonly DatabaseService _db;

        [ObservableProperty]
        public partial CommunityPost PostToEdit { get; set; } = new CommunityPost();

        [ObservableProperty]
        public partial string Content { get; set; } = string.Empty;

        [ObservableProperty]
        public partial ObservableCollection<Pet> MyPets { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<object> SelectedPets { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<MediaAttachment> AttachedMedia { get; set; } = new();

        [ObservableProperty]
        public partial bool IsPetModalVisible { get; set; }

        [ObservableProperty]
        public partial bool IsBusy { get; set; }

        public EditPostViewModel(ApiService api, DatabaseService db)
        {
            _api = api;
            _db = db;
        }

        partial void OnPostToEditChanged(CommunityPost value)
        {
            if (value != null)
            {
                Content = value.Content;
                AttachedMedia.Clear();
                foreach(var img in value.ImageList)
                {
                    AttachedMedia.Add(new MediaAttachment { FilePath = img, IsVideo = false });
                }
                _ = LoadPetsAsync();
            }
        }

        private async Task LoadPetsAsync()
        {
            var pets = await _api.GetPetsAsync();
            MyPets.Clear();
            SelectedPets.Clear();
            foreach (var p in pets)
            {
                MyPets.Add(p);
                if (PostToEdit != null && p.Name == PostToEdit.PetName)
                {
                    SelectedPets.Add(p);
                }
            }
        }

        [RelayCommand]
        private void OpenPetModal() => IsPetModalVisible = true;

        [RelayCommand]
        private void ClosePetModal() => IsPetModalVisible = false;

        [RelayCommand]
        private void RemoveMedia(MediaAttachment media)
        {
            if (AttachedMedia.Contains(media))
            {
                AttachedMedia.Remove(media);
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                // Serialize attached media
                string imageUrls = string.Join(",", AttachedMedia.Select(m => m.FilePath));
                
                // Determine PetId and PetName
                int? petId = null;
                string petName = string.Empty;
                var selectedPet = SelectedPets.FirstOrDefault() as Pet;
                if (selectedPet != null)
                {
                    petId = selectedPet.Id;
                    petName = selectedPet.Name;
                }
                
                bool success = await _api.EditPostAsync(PostToEdit.Id, Content, imageUrls, petId, petName);
                if (success)
                {
                    PostToEdit.Content = Content;
                    PostToEdit.PetId = petId;
                    PostToEdit.PetName = petName;
                    PostToEdit.ImageUrls = imageUrls;
                    
                    await Shell.Current.DisplayAlert("Success", "Post updated successfully.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await Shell.Current.DisplayAlert("Error", "Failed to update post.", "OK");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        [RelayCommand]
        private async Task GoBackAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}




