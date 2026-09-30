using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Controllers;

[ApiController]
[Route("api/marketplace-purchases")]
public class MarketplacePurchasesController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public MarketplacePurchasesController(IConfiguration configuration) =>
        _configuration = configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpGet("cart/{userId:int}")]
    public async Task<IActionResult> GetCart(int userId)
    {
        if (userId <= 0) return BadRequest("A valid user ID is required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            return Ok(await LoadCartAsync(connection, userId, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Marketplace cart error: {ex.Message}");
        }
    }

    [HttpPost("cart")]
    public async Task<IActionResult> AddToCart([FromBody] MarketplaceCartRequest request)
    {
        if (request.UserId <= 0 || request.ListingId <= 0)
            return BadRequest("A valid user and listing are required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            int sellerUserId;
            string status;
            await using (var listingCmd = new SqlCommand(@"
                SELECT SellerUserId,Status
                FROM MarketplaceListings
                WHERE Id=@ListingId;", connection))
            {
                listingCmd.Parameters.AddWithValue("@ListingId", request.ListingId);
                await using var reader = await listingCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync()) return NotFound("Listing not found.");
                sellerUserId = reader.GetInt32(0);
                status = reader.GetString(1);
            }

            if (sellerUserId == request.UserId)
                return BadRequest("You cannot add your own listing to your cart.");

            if (!string.Equals(status, "Available", StringComparison.OrdinalIgnoreCase))
                return BadRequest("This listing is no longer available.");

            await using var existsCmd = new SqlCommand(@"
                SELECT COUNT(1)
                FROM MarketplaceCartItems
                WHERE UserId=@UserId AND ListingId=@ListingId;", connection);
            existsCmd.Parameters.AddWithValue("@UserId", request.UserId);
            existsCmd.Parameters.AddWithValue("@ListingId", request.ListingId);

            if (Convert.ToInt32(await existsCmd.ExecuteScalarAsync()) == 0)
            {
                await using var insert = new SqlCommand(@"
                    INSERT INTO MarketplaceCartItems(UserId,ListingId,AddedAt)
                    VALUES(@UserId,@ListingId,SYSDATETIME());", connection);
                insert.Parameters.AddWithValue("@UserId", request.UserId);
                insert.Parameters.AddWithValue("@ListingId", request.ListingId);
                await insert.ExecuteNonQueryAsync();
            }

            return Ok(await LoadCartAsync(connection, request.UserId, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Add to cart error: {ex.Message}");
        }
    }

    [HttpDelete("cart/{listingId:int}")]
    public async Task<IActionResult> RemoveFromCart(int listingId, [FromQuery] int userId)
    {
        if (userId <= 0 || listingId <= 0)
            return BadRequest("A valid user and listing are required.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cmd = new SqlCommand(@"
                DELETE FROM MarketplaceCartItems
                WHERE UserId=@UserId AND ListingId=@ListingId;", connection);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ListingId", listingId);
            await cmd.ExecuteNonQueryAsync();

            return Ok(await LoadCartAsync(connection, userId, null));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Remove from cart error: {ex.Message}");
        }
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] MarketplaceCheckoutRequest request)
    {
        if (request.UserId <= 0)
            return BadRequest("A valid user ID is required.");

        string[] allowedMethods = ["GCash Simulation", "Cash on Meetup"];
        if (!allowedMethods.Contains(request.PaymentMethod))
            return BadRequest("Select a valid payment simulation method.");

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            var items = await LoadCartRowsAsync(connection, request.UserId, transaction);
            if (items.Count == 0)
            {
                await transaction.RollbackAsync();
                return BadRequest("Your marketplace cart is empty.");
            }

            foreach (var item in items)
            {
                if (item.SellerUserId == request.UserId)
                {
                    await transaction.RollbackAsync();
                    return BadRequest("Your cart contains one of your own listings.");
                }

                if (!string.Equals(item.Status, "Available", StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.RollbackAsync();
                    return Conflict($"{item.Title} is no longer available.");
                }
            }

            if (!request.SimulateSuccess)
            {
                foreach (var item in items)
                {
                    await InsertTransactionAsync(
                        connection,
                        transaction,
                        request.UserId,
                        item.Id,
                        item.Price,
                        request.PaymentMethod,
                        "SimulatedFailed");
                }

                await transaction.CommitAsync();
                return Ok(new
                {
                    Success = false,
                    IsSimulation = true,
                    Message = "Payment simulation failed. No real money was processed. Your cart and listings were not changed."
                });
            }

            var createdOrders = new List<object>();

            foreach (var item in items)
            {
                await using var markSold = new SqlCommand(@"
                    UPDATE MarketplaceListings
                    SET Status='Sold',UpdatedAt=SYSDATETIME()
                    WHERE Id=@ListingId AND Status='Available';", connection, transaction);
                markSold.Parameters.AddWithValue("@ListingId", item.Id);

                if (await markSold.ExecuteNonQueryAsync() == 0)
                {
                    await transaction.RollbackAsync();
                    return Conflict($"{item.Title} was purchased by someone else.");
                }

                int orderId;
                await using (var orderCmd = new SqlCommand(@"
                    INSERT INTO MarketplaceOrders
                        (BuyerUserId,SellerUserId,ListingId,ItemTitle,Amount,
                         PaymentMethod,PaymentStatus,Status,IsSimulation,OrderedAt)
                    OUTPUT INSERTED.Id
                    VALUES
                        (@BuyerUserId,@SellerUserId,@ListingId,@ItemTitle,@Amount,
                         @PaymentMethod,'SimulatedPaid','Confirmed',1,SYSDATETIME());",
                    connection, transaction))
                {
                    orderCmd.Parameters.AddWithValue("@BuyerUserId", request.UserId);
                    orderCmd.Parameters.AddWithValue("@SellerUserId", item.SellerUserId);
                    orderCmd.Parameters.AddWithValue("@ListingId", item.Id);
                    orderCmd.Parameters.AddWithValue("@ItemTitle", item.Title);
                    orderCmd.Parameters.AddWithValue("@Amount", item.Price);
                    orderCmd.Parameters.AddWithValue("@PaymentMethod", request.PaymentMethod);
                    orderId = Convert.ToInt32(await orderCmd.ExecuteScalarAsync());
                }

                await InsertTransactionAsync(
                    connection,
                    transaction,
                    request.UserId,
                    item.Id,
                    item.Price,
                    request.PaymentMethod,
                    "SimulatedPaid");

                createdOrders.Add(new
                {
                    Id = orderId,
                    ListingId = item.Id,
                    item.Title,
                    Amount = item.Price,
                    SellerUserId = item.SellerUserId,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = "SimulatedPaid",
                    Status = "Confirmed",
                    IsSimulation = true
                });
            }

            await using (var clear = new SqlCommand(
                "DELETE FROM MarketplaceCartItems WHERE UserId=@UserId;",
                connection,
                transaction))
            {
                clear.Parameters.AddWithValue("@UserId", request.UserId);
                await clear.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            return Ok(new
            {
                Success = true,
                IsSimulation = true,
                Message = "Payment simulation successful. No real money was processed.",
                Orders = createdOrders
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Marketplace checkout error: {ex.Message}");
        }
    }

    [HttpGet("purchases/{userId:int}")]
    public Task<IActionResult> GetPurchases(int userId) =>
        LoadOrdersAsync(userId, isSeller: false);

    [HttpGet("sales/{userId:int}")]
    public Task<IActionResult> GetSales(int userId) =>
        LoadOrdersAsync(userId, isSeller: true);

    private async Task<IActionResult> LoadOrdersAsync(int userId, bool isSeller)
    {
        if (userId <= 0) return BadRequest("A valid user ID is required.");

        try
        {
            var orders = new List<object>();
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            string filterColumn = isSeller ? "SellerUserId" : "BuyerUserId";
            await using var cmd = new SqlCommand($@"
                SELECT o.Id,o.BuyerUserId,o.SellerUserId,o.ListingId,o.ItemTitle,o.Amount,
                       o.PaymentMethod,o.PaymentStatus,o.Status,o.IsSimulation,o.OrderedAt,
                       buyer.FullName,seller.FullName
                FROM MarketplaceOrders o
                LEFT JOIN UserAccounts buyer ON buyer.Id=o.BuyerUserId
                LEFT JOIN UserAccounts seller ON seller.Id=o.SellerUserId
                WHERE o.{filterColumn}=@UserId
                ORDER BY o.OrderedAt DESC;", connection);
            cmd.Parameters.AddWithValue("@UserId", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                orders.Add(new
                {
                    Id = reader.GetInt32(0),
                    BuyerUserId = reader.GetInt32(1),
                    SellerUserId = reader.GetInt32(2),
                    ListingId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                    ItemTitle = reader.GetString(4),
                    Amount = reader.GetDecimal(5),
                    PaymentMethod = reader.GetString(6),
                    PaymentStatus = reader.GetString(7),
                    Status = reader.GetString(8),
                    IsSimulation = reader.GetBoolean(9),
                    OrderedAt = reader.GetDateTime(10),
                    BuyerName = reader.IsDBNull(11) ? "Pet Owner" : reader.GetString(11),
                    SellerName = reader.IsDBNull(12) ? "Pet Owner" : reader.GetString(12)
                });
            }

            return Ok(orders);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Marketplace orders error: {ex.Message}");
        }
    }

    private static async Task<object> LoadCartAsync(
        SqlConnection connection,
        int userId,
        SqlTransaction? transaction)
    {
        var rows = await LoadCartRowsAsync(connection, userId, transaction);
        decimal total = rows.Sum(x => x.Price);

        return new
        {
            UserId = userId,
            TotalAmount = total,
            Items = rows.Select(x => new
            {
                ListingId = x.Id,
                x.SellerUserId,
                x.Title,
                Price = x.Price,
                x.Status,
                x.ImageUrls,
                x.SellerName
            }).ToList()
        };
    }

    private static async Task<List<CartRow>> LoadCartRowsAsync(
        SqlConnection connection,
        int userId,
        SqlTransaction? transaction)
    {
        var rows = new List<CartRow>();
        await using var cmd = new SqlCommand(@"
            SELECT m.Id,m.SellerUserId,m.Title,m.Price,m.Status,
                   COALESCE(m.ImageUrls,m.ImageUrl,'') AS ImageUrls,
                   COALESCE(u.FullName,'Pet Owner') AS SellerName
            FROM MarketplaceCartItems c
            JOIN MarketplaceListings m ON m.Id=c.ListingId
            LEFT JOIN UserAccounts u ON u.Id=m.SellerUserId
            WHERE c.UserId=@UserId
            ORDER BY c.AddedAt DESC;", connection, transaction);
        cmd.Parameters.AddWithValue("@UserId", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new CartRow
            {
                Id = reader.GetInt32(0),
                SellerUserId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Price = reader.GetDecimal(3),
                Status = reader.GetString(4),
                ImageUrls = reader.IsDBNull(5) ? "" : reader.GetString(5),
                SellerName = reader.GetString(6)
            });
        }

        return rows;
    }

    private static async Task InsertTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int userId,
        int listingId,
        decimal amount,
        string paymentMethod,
        string status)
    {
        string reference =
            $"MOCK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        await using var cmd = new SqlCommand(@"
            INSERT INTO Transactions
                (UserId,ClinicId,Type,Amount,Reference,PaidAt,PaymentMethod,Status,
                 RelatedListingId,IsSimulation)
            VALUES
                (@UserId,NULL,'MarketplacePurchase',@Amount,@Reference,SYSDATETIME(),
                 @PaymentMethod,@Status,@ListingId,1);", connection, transaction);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@Amount", amount);
        cmd.Parameters.AddWithValue("@Reference", reference);
        cmd.Parameters.AddWithValue("@PaymentMethod", paymentMethod);
        cmd.Parameters.AddWithValue("@Status", status);
        cmd.Parameters.AddWithValue("@ListingId", listingId);
        await cmd.ExecuteNonQueryAsync();
    }

    private sealed class CartRow
    {
        public int Id { get; set; }
        public int SellerUserId { get; set; }
        public string Title { get; set; } = "";
        public decimal Price { get; set; }
        public string Status { get; set; } = "";
        public string ImageUrls { get; set; } = "";
        public string SellerName { get; set; } = "";
    }
}

public sealed class MarketplaceCartRequest
{
    public int UserId { get; set; }
    public int ListingId { get; set; }
}

public sealed class MarketplaceCheckoutRequest
{
    public int UserId { get; set; }
    public string PaymentMethod { get; set; } = "GCash Simulation";
    public bool SimulateSuccess { get; set; } = true;
}
