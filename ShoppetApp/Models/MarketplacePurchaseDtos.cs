namespace ShoppetApp.Models;

public class MarketplaceCartDto
{
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public List<MarketplaceCartItemDto> Items { get; set; } = [];
}

public class MarketplaceCartItemDto
{
    public int ListingId { get; set; }
    public int SellerUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ImageUrls { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;

    public string PriceDisplay => $"₱{Price:N2}";
    public string FirstImage => string.IsNullOrWhiteSpace(ImageUrls)
        ? string.Empty
        : ImageUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .FirstOrDefault() ?? string.Empty;
    public bool HasImage => !string.IsNullOrWhiteSpace(FirstImage);
}

public class MarketplaceOrderDto
{
    public int Id { get; set; }
    public int BuyerUserId { get; set; }
    public int SellerUserId { get; set; }
    public int? ListingId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsSimulation { get; set; }
    public DateTime OrderedAt { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;

    public string AmountDisplay => $"₱{Amount:N2}";
    public string DateDisplay => OrderedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
}

public class MarketplaceCheckoutResponse
{
    public bool Success { get; set; }
    public bool IsSimulation { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<MarketplaceOrderDto> Orders { get; set; } = [];
}
