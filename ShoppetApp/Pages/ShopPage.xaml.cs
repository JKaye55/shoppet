using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Collections.ObjectModel;

namespace ShoppetApp.Pages;

public partial class ShopPage : ContentPage
{
    private readonly ApiService _api;
    private MarketplaceListing _currentListing;
    private readonly DatabaseService _db;
    private ObservableCollection<MarketplaceListing> MyListings { get; } = new();
    private List<MarketplaceListing> _allExploreListings = new();
    private MarketplaceListing? _editingListing = null;
    private bool _isSellTab = false;
    private string _currentCategory = "";
    private string _currentSearch = "";
    private List<string> _pickedPhotoPaths = new();
    private readonly ObservableCollection<CartItemDto> _cartItems = new();

    public ShopPage(ApiService api, DatabaseService db)
    {
        InitializeComponent();
        _api = api;
        _db = db;
        MyListingsContainer.SetValue(BindableLayout.ItemsSourceProperty, MyListings);
        CartItemsView.ItemsSource = _cartItems;
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

    private async void OnExploreTabClicked(object sender, EventArgs e)
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

    private async void OnSellTabClicked(object sender, EventArgs e)
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

    private void OnSearchTapped(object sender, TappedEventArgs e)
    {
        ToggleSearchPanel();
    }

    private void OnSearchButtonClicked(object sender, EventArgs e) => ToggleSearchPanel();

    private void ToggleSearchPanel()
    {
        SearchPanel.IsVisible = !SearchPanel.IsVisible;
        if (!SearchPanel.IsVisible)
        {
            _currentSearch = "";
            SearchEntry.Text = "";
            _ = LoadExploreListingsAsync();
        }
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _currentSearch = e.NewTextValue?.Trim() ?? "";
        if (!_isSellTab)
            await LoadExploreListingsAsync();
    }

    // --- Load Data ---

    private async Task LoadExploreListingsAsync()
    {
        MarketplaceLoading.IsVisible = true;
        MarketplaceLoading.IsRunning = true;
        ExploreEmptyLabel.IsVisible = false;

        try
        {
            var listings = await _api.GetMarketplaceListingsAsync(
                string.IsNullOrEmpty(_currentCategory) ? null : _currentCategory,
                string.IsNullOrEmpty(_currentSearch) ? null : _currentSearch);

            _allExploreListings = listings;
            ExploreGrid.ItemsSource = listings;
            ExploreEmptyLabel.IsVisible = !listings.Any();
        }
        finally
        {
            MarketplaceLoading.IsRunning = false;
            MarketplaceLoading.IsVisible = false;
        }
    }

    private async Task LoadMyListingsAsync()
    {
        var userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
        if (userId <= 0) return;

        MarketplaceLoading.IsVisible = true;
        MarketplaceLoading.IsRunning = true;
        SellEmptyLabel.IsVisible = false;

        try
        {
            var listings = await _api.GetMyListingsAsync(userId);
            MyListings.Clear();
            foreach (var l in listings) MyListings.Add(l);
            SellEmptyLabel.IsVisible = !listings.Any();
        }
        finally
        {
            MarketplaceLoading.IsRunning = false;
            MarketplaceLoading.IsVisible = false;
        }
    }

    // --- Category Filter ---

    private async void OnCategoryAllClicked(object sender, EventArgs e)
    {
        _currentCategory = "";
        await LoadExploreListingsAsync();
    }

    private async void OnCategoryClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string cat)
        {
            _currentCategory = cat;
            await LoadExploreListingsAsync();
        }
    }

    // --- Explore Listing Detail Modal ---

    private void OnListingTapped(object sender, SelectionChangedEventArgs e)
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
        DetailSellerImage.Source = (ImageSource?)new ShoppetApp.Converters.Base64ToImageSourceConverter().Convert(listing.SellerProfilePic, typeof(ImageSource), null, null);

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
        var isOwnListing = listing.UserId == currentUserId;
        BtnMessageSeller.IsVisible = !isOwnListing;
        BtnAddToCart.IsVisible = !isOwnListing && listing.IsAvailable;

        DetailModal.IsVisible = true;
    }

    private async void OnAddToCartClicked(object sender, EventArgs e)
    {
        if (_currentListing == null) return;

        var userId = Preferences.Get("LoggedInUserId", 0);
        if (userId <= 0)
        {
            await DisplayAlert("Sign in required", "Please sign in before adding items to your cart.", "OK");
            return;
        }

        var cart = await _api.AddToCartAsync(new AddToCartRequest(_currentListing.Id, 1));
        if (cart == null)
        {
            await DisplayAlert("Cart", "The item could not be added. Please try again.", "OK");
            return;
        }

        DetailModal.IsVisible = false;
        await RefreshCartAsync();
        CartModal.IsVisible = true;
    }

    private async void OnCartClicked(object sender, EventArgs e)
    {
        await RefreshCartAsync();
        CartModal.IsVisible = true;
    }

    private void OnCloseCartClicked(object sender, EventArgs e) => CartModal.IsVisible = false;

    private async Task RefreshCartAsync()
    {
        var cart = await _api.GetCartAsync();
        if (cart != null)
            await ApplyCartAsync(cart);
        else
        {
            _cartItems.Clear();
            CartTotalLabel.Text = "₱0";
            CheckoutButton.IsEnabled = false;
        }
    }

    private Task ApplyCartAsync(CartDto cart)
    {
        _cartItems.Clear();
        foreach (var item in cart.Items)
            _cartItems.Add(item);

        CartTotalLabel.Text = $"₱{cart.TotalAmount:N0}";
        CheckoutButton.IsEnabled = cart.Items.Count > 0;
        return Task.CompletedTask;
    }

    private async void OnRemoveCartItemClicked(object sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not CartItemDto item)
            return;

        var cart = await _api.RemoveFromCartAsync(item.Id);
        if (cart != null)
            await RefreshCartAsync();
    }

    private async void OnCheckoutClicked(object sender, EventArgs e)
    {
        CheckoutButton.IsEnabled = false;
        try
        {
            var order = await _api.CheckoutAsync();
            if (order == null)
            {
                await DisplayAlert("Checkout", "Mock checkout could not be completed.", "OK");
                return;
            }

            await DisplayAlert(
                "Order Confirmed",
                $"Mock payment completed. Order #{order.Id} • ₱{order.TotalAmount:N0}",
                "OK");

            CartModal.IsVisible = false;
            await LoadExploreListingsAsync();
        }
        finally
        {
            CheckoutButton.IsEnabled = true;
        }
    }

    private void OnDismissModal(object sender, EventArgs e) => DetailModal.IsVisible = false;

    private async void OnMessageSellerClicked(object sender, EventArgs e)
    {
        if (_currentListing != null)
        {
            DetailModal.IsVisible = false;
            await Shell.Current.GoToAsync("chat", new Dictionary<string, object>
            {
                { "ContactId", _currentListing.UserId.ToString() },
                { "ContactName", _currentListing.SellerName }
            });
        }
    }
    private void OnModalBodyTapped(object sender, TappedEventArgs e) { }

    // --- Sell Tab: Photo Picker ---

    private async void OnPickPhotoClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Select Photos",
                FileTypes = FilePickerFileType.Images
            });

            if (result == null) return;

            foreach (var file in result)
            {
                if (_pickedPhotoPaths.Count >= 5)
                {
                    await DisplayAlert("Limit Reached", "You can only attach up to 5 photos.", "OK");
                    break;
                }
                _pickedPhotoPaths.Add(file.FullPath);
            }
            RefreshPickedPhotos();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Photo pick error: {ex.Message}");
        }
    }

    private void OnRemovePickedPhoto(object sender, TappedEventArgs e)
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

    private void OnPostItemClicked(object sender, EventArgs e)
    {
        _editingListing = null;
        PostModalTitle.Text = "Post a Pet Item";
        ClearPostForm();
        PostModal.IsVisible = true;
    }

    private void OnEditListingClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is MarketplaceListing listing)
        {
            _editingListing = listing;
            PostModalTitle.Text = "Edit Listing";
            EntryTitle.Text = listing.Title;
            EntryPrice.Text = listing.Price.ToString("0.##");
            EntryLocation.Text = listing.Location;
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

    private async void OnDeleteListingClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is MarketplaceListing listing)
        {
            bool confirm = await DisplayAlert("Delete Listing", $"Are you sure you want to delete \"{listing.Title}\"?", "Yes, Delete", "Cancel");
            if (!confirm) return;
            var userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
            if (userId <= 0) return;

            bool ok = await _api.DeleteListingAsync(listing.Id, userId);
            if (ok)
            {
                MyListings.Remove(listing);
                SellEmptyLabel.IsVisible = !MyListings.Any();
                await DisplayAlert("Deleted", "Listing removed.", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Could not delete listing. Try again.", "OK");
            }
        }
    }

    private void OnCancelPostClicked(object sender, EventArgs e)
    {
        PostModal.IsVisible = false;
        ClearPostForm();
    }

    private async void OnSaveListingClicked(object sender, EventArgs e)
    {
        var userId = _db.CurrentUser?.Id ?? Preferences.Get("LoggedInUserId", 0);
        var userName = _db.CurrentUser?.FullName ?? Preferences.Get("LoggedInUserName", "User");
        if (userId <= 0)
        {
            await DisplayAlert("Sign in required", "Please sign in before managing marketplace listings.", "OK");
            return;
        }

        var title = EntryTitle.Text?.Trim() ?? "";
        var desc = EditorDescription.Text?.Trim() ?? "";
        var priceStr = EntryPrice.Text?.Trim() ?? "0";
        var location = EntryLocation.Text?.Trim() ?? "";
        var category = PickerCategory.SelectedItem?.ToString() ?? "General";
        var condition = PickerCondition.SelectedItem?.ToString() ?? "Used";
        var existingUrls = _pickedPhotoPaths
            .Where(p => p.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || p.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var localPaths = _pickedPhotoPaths.Where(File.Exists).ToList();

        var uploadedUrls = localPaths.Count > 0
            ? await _api.UploadCommunityMediaAsync(localPaths)
            : new List<string>();

        if (localPaths.Count > 0 && uploadedUrls.Count != localPaths.Count)
        {
            await DisplayAlert("Upload failed", "One or more listing photos could not be uploaded.", "OK");
            return;
        }

        var imageUrls = string.Join(",", existingUrls.Concat(uploadedUrls));

        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(desc))
        {
            await DisplayAlert("Missing Info", "Please fill in Title and Description.", "OK");
            return;
        }

        if (!decimal.TryParse(priceStr, out decimal price) || price < 0)
        {
            await DisplayAlert("Invalid Price", "Please enter a valid price.", "OK");
            return;
        }

        bool success;
        if (_editingListing != null)
        {
            success = await _api.EditListingAsync(_editingListing.Id, new
            {
                UserId = userId,
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
                UserId = userId,
                SellerName = userName,
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
            await DisplayAlert("Success", wasEditing ? "Listing updated!" : "Item posted to Marketplace!", "OK");
        }
        else
        {
            await DisplayAlert("Error", "Something went wrong. Please try again.", "OK");
        }
    }

    private void ClearPostForm()
    {
        EntryTitle.Text = "";
        EntryPrice.Text = "";
        EntryLocation.Text = "";
        EditorDescription.Text = "";
        PickerCategory.SelectedItem = "General";
        PickerCondition.SelectedItem = "Used";
        _pickedPhotoPaths.Clear();
        PickedPhotosView.ItemsSource = null;
        PickedPhotosView.IsVisible = false;
    }
}






