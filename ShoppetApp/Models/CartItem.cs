using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace ShoppetApp.Models;

public partial class CartItem : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; } // The CartItem ID in the Database

    [Indexed]
    public int UserId { get; set; }

    [ObservableProperty]
    public partial int ProductId { get; set; }

    [ObservableProperty]
    public partial string ProductName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ImageUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal UnitPrice { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPrice))]
    [NotifyPropertyChangedFor(nameof(TotalAmount))]
    public partial int Quantity { get; set; } = 1;

    // Manual property with [Ignore] so SQLite skips it without field attribute warnings
    private Product? _product;
    [Ignore]
    public Product? Product
    {
        get => _product;
        set => SetProperty(ref _product, value);
    }

    // Shorthand fallback aliases to support any page or viewmodel expecting flat properties
    public string Name => !string.IsNullOrEmpty(ProductName) ? ProductName : (Product?.Name ?? string.Empty);
    public decimal Price => UnitPrice != 0 ? UnitPrice : (Product?.Price ?? 0);
    public decimal TotalPrice => Price * Quantity;
    public decimal TotalAmount => TotalPrice;
}