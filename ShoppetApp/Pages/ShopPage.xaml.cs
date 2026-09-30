using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Collections.ObjectModel;

namespace ShoppetApp.Pages;

public partial class ShopPage : ContentPage
{
    private readonly ApiService _api;
    private MarketplaceListing? _currentListing;
    private readonly DatabaseService _db;
    private ObservableCollection<MarketplaceListing> MyListings { get; } = new();
    private List<MarketplaceListing> _allExploreListings = new();
    private MarketplaceListing? _editingListing = null;
    private bool _isSellTab = false;
    private string _currentCategory = "";
    private string _currentSearch = "";
    private List<string> _pickedPhotoPaths = new();

    public ShopPage(ApiService api, DatabaseService db)
    {
        InitializeComponent();
        _api = api;
        _db = db;
        MyListingsContainer.SetValue(BindableLayout.ItemsSourceProperty, MyListings);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_isSellTab)
            await LoadMyListingsAsync();
        else
            await LoadExploreListingsAsync();
    }

    // --- Tab switching ---

    private async void OnExploreTabClicked(object? sender, EventArgs e)
    {
        _isSellTab = false;
        BtnExplore.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
        BtnExplore.TextColor = Colors.White;
        BtnSell.BackgroundColor = Colors.Transparent;
        BtnSell.TextColor = (Color)Application.Current!.Resources["Primary"];
        ExplorePanel.IsVisible = true;
        SellPanel.IsVisible = false;
        await LoadExploreListingsAsync();
    }

    private async void OnSellTabClicked(object? sender, EventArgs e)
    {
        _isSellTab = true;
        BtnSell.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
        BtnSell.TextColor = Colors.White;
        BtnExplore.BackgroundColor = Colors.Transparent;
        BtnExplore.TextColor = (Color)Application.Current!.Resources["Primary"];
        SellPanel.IsVisible = true;
        ExplorePanel.IsVisible = false;
        await LoadMyListingsAsync();
    }

    private void OnSearchTapped(object? sender, TappedEventArgs e)
    {
        SearchPanel.IsVisible = !SearchPanel.IsVisible;
        if (!SearchPanel.IsVisible)
        {
            _currentSearch = "";
            SearchEntry.Text = "";
            _ = LoadExploreListingsAsync();
        }
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _currentSearch = e.NewTextValue?.Trim() ?? "";
        if (!_isSellTab)
            await LoadExploreListingsAsync();
    }

    // --- Load Data ---

    private async Task LoadExploreListingsAsync()
    {
        var listings = await _api.GetMarketplaceListingsAsync(
            string.IsNullOrEmpty(_currentCategory) ? null : _currentCategory,
            string.IsNullOrEmpty(_currentSearch) ? null : _currentSearch);
        _allExploreListings = listings;
        ExploreGrid.ItemsSource = listings;
        ExploreEmptyLabel.IsVisible = !listings.Any();
    }

    private async Task LoadMyListingsAsync()
    {
        if (_db.CurrentUser == null) return;
        var listings = await _api.GetMyListingsAsync(_db.CurrentUser.Id);
        MyListings.Clear();
        foreach (var l in listings) MyListings.Add(l);
        SellEmptyLabel.IsVisible = !listings.Any();
    }

    // --- Category Filter ---

    private async void OnCategoryAllClicked(object? sender, EventArgs e)
    {
        _currentCategory = "";
        await LoadExploreListingsAsync();
    }

    private async void OnCategoryClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string cat)
        {
            _currentCategory = cat;
            await LoadExploreListingsAsync();
        }
    }

    // --- Explore Listing Detail Modal ---

    private void OnListingTapped(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MarketplaceListing listing)
        {
            ShowDetailModal(listing);
            ExploreGrid.SelectedItem = null;
        }
    }

    private void ShowDetailModal(MarketplaceListing listing)
    {
        _currentListing = listing;
        DetailTitle.Text = listing.Title;
        DetailPrice.Text = listing.PriceDisplay;
        DetailCategory.Text = listing.Category;
        DetailCondition.Text = listing.Condition;
        DetailDescription.Text = listing.Description;
        DetailSellerName.Text = listing.SellerName;
        DetailLocation.Text = string.IsNullOrEmpty(listing.Location) ? "Location not specified" : listing.Location;
        DetailTime.Text = listing.TimeAgo;
        DetailSellerInitial.Text = listing.Initials;
        DetailSellerImage.Source = (ImageSource?)new ShoppetApp.Converters.Base64ToImageSourceConverter().Convert(listing.SellerProfilePic, typeof(ImageSource), null, System.Globalization.CultureInfo.InvariantCulture);

        if (listing.HasImages)
        {
            DetailImage.Source = listing.FirstImage;
            DetailImage.IsVisible = true;
            
        }
        else
        {
            DetailImage.IsVisible = false;
            
        }

        // Hide "Message" button if the listing belongs to the current logged-in user
        var currentUserId = Preferences.Get("LoggedInUserId", 0);
        BtnMessageSeller.IsVisible = listing.UserId != currentUserId;

        DetailModal.IsVisible = true;
    }

    private void OnDismissModal(object? sender, EventArgs e) => DetailModal.IsVisible = false;

    private async Task MessageCurrentSellerAsync()
    {
        if (_currentListing == null) return;

        DetailModal.IsVisible = false;
        await Shell.Current.GoToAsync("chat", new Dictionary<string, object>
        {
            { "ContactId", _currentListing.UserId.ToString() },
            { "ContactName", _currentListing.SellerName }
        });
    }

    private async void OnMessageSellerClicked(object? sender, EventArgs e) =>
        await MessageCurrentSellerAsync();

    private async void OnMessageSellerTapped(object? sender, TappedEventArgs e) =>
        await MessageCurrentSellerAsync();
    private void OnModalBodyTapped(object? sender, TappedEventArgs e) { }

    // --- Sell Tab: Photo Picker ---

    private async Task PickListingPhotosAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Choose item photos",
                FileTypes = FilePickerFileType.Images
            });

            if (result == null) return;

            foreach (var file in result)
            {
                if (_pickedPhotoPaths.Count >= 5)
                {
                    await DisplayAlertAsync("Photo limit", "You can attach up to 5 photos.", "OK");
                    break;
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
                {
                    await DisplayAlertAsync("Unsupported photo", $"{file.FileName} is not a supported image. Use JPG, PNG, or WebP.", "OK");
                    continue;
                }

                await using var stream = await file.OpenReadAsync();
                if (stream.CanSeek && stream.Length > 5 * 1024 * 1024)
                {
                    await DisplayAlertAsync("Photo too large", $"{file.FileName} is larger than 5 MB.", "OK");
                    continue;
                }

                if (!_pickedPhotoPaths.Contains(file.FullPath, StringComparer.OrdinalIgnoreCase))
                    _pickedPhotoPaths.Add(file.FullPath);
            }

            RefreshPickedPhotos();
        }
        catch
        {
            await DisplayAlertAsync("Photo error", "The photos could not be selected. Please try again.", "OK");
        }
    }

    private async void OnPickPhotoClicked(object? sender, EventArgs e) =>
        await PickListingPhotosAsync();

    private async void OnPickPhotoTapped(object? sender, TappedEventArgs e) =>
        await PickListingPhotosAsync();

    private void OnRemovePickedPhoto(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is string path)
        {
            _pickedPhotoPaths.Remove(path);
            RefreshPickedPhotos();
        }
    }

    private void RefreshPickedPhotos()
    {
        PickedPhotosView.ItemsSource = null;
        PickedPhotosView.ItemsSource = _pickedPhotoPaths.ToList();
        PickedPhotosView.IsVisible = _pickedPhotoPaths.Any();
    }

    // --- Sell Tab: Create/Edit/Delete ---

    private void OnPostItemClicked(object? sender, EventArgs e)
    {
        _editingListing = null;
        PostModalTitle.Text = "List a Pet Item";
        ClearPostForm();
        PostModal.IsVisible = true;
    }

    private void OnEditListingClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is MarketplaceListing listing)
        {
            _editingListing = listing;
            PostModalTitle.Text = "Edit Listing";
            EntryTitle.Text = listing.Title;
            EntryPrice.Text = listing.Price.ToString("0.##");
            PickerLocation.SelectedItem = listing.Location;
            EditorDescription.Text = listing.Description;
            PickerCategory.SelectedItem = listing.Category;
            PickerCondition.SelectedItem = listing.Condition;
            // Pre-load existing images into picker
            _pickedPhotoPaths.Clear();
            if (!string.IsNullOrEmpty(listing.ImageUrls))
                _pickedPhotoPaths.AddRange(listing.ImageUrls.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            RefreshPickedPhotos();
            PostModal.IsVisible = true;
        }
    }

    private async void OnDeleteListingClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is MarketplaceListing listing)
        {
            bool confirm = await DisplayAlertAsync("Delete Listing", $"Are you sure you want to delete \"{listing.Title}\"?", "Yes, Delete", "Cancel");
            if (!confirm) return;
            if (_db.CurrentUser == null) return;

            bool ok = await _api.DeleteListingAsync(listing.Id, _db.CurrentUser.Id);
            if (ok)
            {
                MyListings.Remove(listing);
                SellEmptyLabel.IsVisible = !MyListings.Any();
                await DisplayAlertAsync("Deleted", "Listing removed.", "OK");
            }
            else
            {
                await DisplayAlertAsync("Error", "Could not delete listing. Try again.", "OK");
            }
        }
    }

    private void OnCancelPostClicked(object? sender, EventArgs e)
    {
        PostModal.IsVisible = false;
        ClearPostForm();
    }

    private async void OnSaveListingClicked(object? sender, EventArgs e)
    {
        if (_db.CurrentUser == null) return;

        var title = EntryTitle.Text?.Trim() ?? "";
        var desc = EditorDescription.Text?.Trim() ?? "";
        var priceStr = EntryPrice.Text?.Trim() ?? "";
        var location = PickerLocation.SelectedItem?.ToString()?.Trim() ?? "";
        var category = PickerCategory.SelectedItem?.ToString()?.Trim() ?? "";
        var condition = PickerCondition.SelectedItem?.ToString()?.Trim() ?? "";
        var imageUrls = string.Join(",", _pickedPhotoPaths);

        if (title.Length < 2 || title.Length > 80)
        {
            await DisplayAlertAsync("Check title", "Listing title must be between 2 and 80 characters.", "OK");
            return;
        }

        if (desc.Length < 10 || desc.Length > 500)
        {
            await DisplayAlertAsync("Check description", "Description must be between 10 and 500 characters.", "OK");
            return;
        }

        if (!decimal.TryParse(priceStr, out decimal price) || price <= 0 || price > 1000000)
        {
            await DisplayAlertAsync("Check price", "Enter a price greater than ₱0 and no more than ₱1,000,000.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            await DisplayAlertAsync("Category required", "Choose a category for the item.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(condition))
        {
            await DisplayAlertAsync("Condition required", "Choose the item's condition.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            await DisplayAlertAsync("Meetup area required", "Choose a general meetup area.", "OK");
            return;
        }

        if (_pickedPhotoPaths.Count == 0)
        {
            await DisplayAlertAsync("Photo required", "Add at least one clear photo of the item.", "OK");
            return;
        }

        bool success;
        if (_editingListing != null)
        {
            success = await _api.EditListingAsync(_editingListing.Id, new
            {
                UserId = _db.CurrentUser.Id,
                Title = title,
                Description = desc,
                Price = price,
                Category = category,
                Condition = condition,
                Location = location,
                ImageUrls = string.IsNullOrEmpty(imageUrls) ? (string?)null : imageUrls
            });
            if (success)
            {
                _editingListing.Title = title;
                _editingListing.Description = desc;
                _editingListing.Price = price;
                _editingListing.Category = category;
                _editingListing.Condition = condition;
                _editingListing.Location = location;
                _editingListing.ImageUrls = imageUrls;
            }
        }
        else
        {
            success = await _api.CreateListingAsync(new
            {
                UserId = _db.CurrentUser.Id,
                SellerName = _db.CurrentUser.FullName ?? "",
                Title = title,
                Description = desc,
                Price = price,
                Category = category,
                Condition = condition,
                Location = location,
                ImageUrls = string.IsNullOrEmpty(imageUrls) ? (string?)null : imageUrls
            });
        }

        if (success)
        {
            var wasEditing = _editingListing != null;
            _editingListing = null;
            PostModal.IsVisible = false;
            ClearPostForm();
            await LoadMyListingsAsync();
            await LoadExploreListingsAsync();
            await DisplayAlertAsync("Saved", wasEditing ? "Your listing was updated." : "Your item is now listed in Marketplace.", "OK");
        }
        else
        {
            await DisplayAlertAsync("Error", "Something went wrong. Please try again.", "OK");
        }
    }

    private void ClearPostForm()
    {
        EntryTitle.Text = "";
        EntryPrice.Text = "";
        PickerLocation.SelectedItem = null;
        EditorDescription.Text = "";
        PickerCategory.SelectedItem = null;
        PickerCondition.SelectedItem = null;
        _pickedPhotoPaths.Clear();
        PickedPhotosView.ItemsSource = null;
        PickedPhotosView.IsVisible = false;
    }
}






