using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShoppetApp.Models;
using ShoppetApp.Services;
using System.Collections.ObjectModel;

namespace ShoppetApp.ViewModels;

public partial class FilterItem : ObservableObject
{
    public string Name { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public FilterItem(string name)
    {
        Name = name;
    }
}

public partial class ShopViewModel : ObservableObject
{
    private readonly CartService _cartService;
    private readonly DatabaseService _db;
    private List<Product> _allProducts = new();

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<FilterItem> Categories { get; } = new();
    public ObservableCollection<FilterItem> SpeciesList { get; } = new();

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    public ShopViewModel(CartService cartService, DatabaseService db)
    {
        _cartService = cartService;
        _db = db;

        // Initialize static species list
        var species = new[] { "Dog", "Cat", "Bird", "Small Pet", "Other" };
        foreach (var s in species)
        {
            SpeciesList.Add(new FilterItem(s));
        }

        // Load data asynchronously
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        // 1. Fetch categories
        var cats = await _db.GetCategoriesAsync();
        Categories.Clear();
        foreach (var c in cats)
        {
            Categories.Add(new FilterItem(c)); // Fixed: c is already a string
        }

        // 2. Fetch products
        _allProducts = await _db.GetProductsAsync();
        ApplyFilters();

        // 3. Sync cart
        await _cartService.LoadCartAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var filtered = _allProducts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                           p.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        var selectedSpecies = SpeciesList.Where(s => s.IsSelected).Select(s => s.Name).ToList();
        if (selectedSpecies.Any())
        {
            filtered = filtered.Where(p => p.Species != null && p.Species.Any(s => selectedSpecies.Contains(s)));
        }

        var selectedCategories = Categories.Where(c => c.IsSelected).Select(c => c.Name).ToList();
        if (selectedCategories.Any())
        {
            filtered = filtered.Where(p => p.Categories != null && p.Categories.Any(c => selectedCategories.Contains(c)));
        }

        Products.Clear();
        foreach (var product in filtered)
        {
            Products.Add(product);
        }
    }

    [RelayCommand]
    private void SelectCategory(FilterItem category)
    {
        if (category != null)
        {
            category.IsSelected = !category.IsSelected;
            ApplyFilters();
        }
    }

    [RelayCommand]
    private void SelectSpecies(FilterItem species)
    {
        if (species != null)
        {
            species.IsSelected = !species.IsSelected;
            ApplyFilters();
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        foreach (var c in Categories) c.IsSelected = false;
        foreach (var s in SpeciesList) s.IsSelected = false;
        ApplyFilters();
    }

    [RelayCommand]
    private async Task AddToCart(Product product)
    {
        if (product == null) return;
        await _cartService.AddToCart(product);
    }
}