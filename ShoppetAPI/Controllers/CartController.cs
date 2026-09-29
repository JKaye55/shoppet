using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ShoppetAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public CartController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string ConnectionString =>
            _configuration.GetConnectionString("DefaultConnection")!;

        [HttpGet]
        public async Task<IActionResult> GetCart([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("A valid user ID is required.");

            try
            {
                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();
                var cart = await BuildCartAsync(connection, userId, createIfMissing: true);
                return Ok(cart);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching cart: {ex.Message}");
            }
        }

        [HttpPost("items")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            if (request.UserId <= 0 || request.ProductId <= 0 || request.Quantity <= 0)
                return BadRequest("Valid user, product, and quantity are required.");

            try
            {
                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();

                var productCmd = new MySqlCommand(
                    "SELECT StockQuantity, IsAvailable FROM products WHERE Id = @ProductId",
                    connection);
                productCmd.Parameters.AddWithValue("@ProductId", request.ProductId);

                using (var reader = await productCmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return NotFound("Product not found.");

                    int stock = reader.GetInt32("StockQuantity");
                    bool available = reader.GetBoolean("IsAvailable");
                    if (!available || stock <= 0)
                        return BadRequest("Product is not available.");
                }

                int cartId = await GetOrCreateCartIdAsync(connection, request.UserId);

                var existingCmd = new MySqlCommand(
                    "SELECT Id, Quantity FROM cartitems WHERE CartId = @CartId AND ProductId = @ProductId",
                    connection);
                existingCmd.Parameters.AddWithValue("@CartId", cartId);
                existingCmd.Parameters.AddWithValue("@ProductId", request.ProductId);

                int? itemId = null;
                int existingQuantity = 0;
                using (var reader = await existingCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        itemId = reader.GetInt32("Id");
                        existingQuantity = reader.GetInt32("Quantity");
                    }
                }

                var stockCmd = new MySqlCommand(
                    "SELECT StockQuantity FROM products WHERE Id = @ProductId",
                    connection);
                stockCmd.Parameters.AddWithValue("@ProductId", request.ProductId);
                int stockQuantity = Convert.ToInt32(await stockCmd.ExecuteScalarAsync());

                int newQuantity = existingQuantity + request.Quantity;
                if (newQuantity > stockQuantity)
                    return BadRequest($"Only {stockQuantity} item(s) are available.");

                if (itemId.HasValue)
                {
                    var updateCmd = new MySqlCommand(
                        "UPDATE cartitems SET Quantity = @Quantity WHERE Id = @Id",
                        connection);
                    updateCmd.Parameters.AddWithValue("@Quantity", newQuantity);
                    updateCmd.Parameters.AddWithValue("@Id", itemId.Value);
                    await updateCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    var insertCmd = new MySqlCommand(
                        "INSERT INTO cartitems (CartId, ProductId, Quantity) VALUES (@CartId, @ProductId, @Quantity)",
                        connection);
                    insertCmd.Parameters.AddWithValue("@CartId", cartId);
                    insertCmd.Parameters.AddWithValue("@ProductId", request.ProductId);
                    insertCmd.Parameters.AddWithValue("@Quantity", request.Quantity);
                    await insertCmd.ExecuteNonQueryAsync();
                }

                await TouchCartAsync(connection, cartId);
                return Ok(await BuildCartAsync(connection, request.UserId, createIfMissing: false));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error adding to cart: {ex.Message}");
            }
        }

        [HttpPut("items/{itemId}")]
        public async Task<IActionResult> UpdateCartItem(
            int itemId,
            [FromQuery] int userId,
            [FromBody] UpdateCartItemRequest request)
        {
            if (userId <= 0 || itemId <= 0 || request.Quantity <= 0)
                return BadRequest("Valid user, item, and quantity are required.");

            try
            {
                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();

                var infoCmd = new MySqlCommand(@"
                    SELECT ci.CartId, ci.ProductId, p.StockQuantity
                    FROM cartitems ci
                    JOIN shoppingcarts c ON c.Id = ci.CartId
                    JOIN products p ON p.Id = ci.ProductId
                    WHERE ci.Id = @ItemId AND c.UserId = @UserId", connection);
                infoCmd.Parameters.AddWithValue("@ItemId", itemId);
                infoCmd.Parameters.AddWithValue("@UserId", userId);

                int cartId;
                int stock;
                using (var reader = await infoCmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return NotFound("Cart item not found.");

                    cartId = reader.GetInt32("CartId");
                    stock = reader.GetInt32("StockQuantity");
                }

                if (request.Quantity > stock)
                    return BadRequest($"Only {stock} item(s) are available.");

                var cmd = new MySqlCommand(
                    "UPDATE cartitems SET Quantity = @Quantity WHERE Id = @ItemId",
                    connection);
                cmd.Parameters.AddWithValue("@Quantity", request.Quantity);
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                await cmd.ExecuteNonQueryAsync();

                await TouchCartAsync(connection, cartId);
                return Ok(await BuildCartAsync(connection, userId, createIfMissing: false));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error updating cart item: {ex.Message}");
            }
        }

        [HttpDelete("items/{itemId}")]
        public async Task<IActionResult> RemoveCartItem(
            int itemId,
            [FromQuery] int userId)
        {
            if (userId <= 0 || itemId <= 0)
                return BadRequest("Valid user and item IDs are required.");

            try
            {
                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();

                var cartIdCmd = new MySqlCommand(@"
                    SELECT ci.CartId
                    FROM cartitems ci
                    JOIN shoppingcarts c ON c.Id = ci.CartId
                    WHERE ci.Id = @ItemId AND c.UserId = @UserId", connection);
                cartIdCmd.Parameters.AddWithValue("@ItemId", itemId);
                cartIdCmd.Parameters.AddWithValue("@UserId", userId);

                var cartIdResult = await cartIdCmd.ExecuteScalarAsync();
                if (cartIdResult is null)
                    return NotFound("Cart item not found.");

                int cartId = Convert.ToInt32(cartIdResult);

                var deleteCmd = new MySqlCommand(
                    "DELETE FROM cartitems WHERE Id = @ItemId AND CartId = @CartId",
                    connection);
                deleteCmd.Parameters.AddWithValue("@ItemId", itemId);
                deleteCmd.Parameters.AddWithValue("@CartId", cartId);
                await deleteCmd.ExecuteNonQueryAsync();

                await TouchCartAsync(connection, cartId);
                return Ok(await BuildCartAsync(connection, userId, createIfMissing: false));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error removing cart item: {ex.Message}");
            }
        }

        [HttpDelete]
        public async Task<IActionResult> ClearCart([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("A valid user ID is required.");

            try
            {
                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();

                int cartId = await GetOrCreateCartIdAsync(connection, userId);
                var cmd = new MySqlCommand(
                    "DELETE FROM cartitems WHERE CartId = @CartId",
                    connection);
                cmd.Parameters.AddWithValue("@CartId", cartId);
                await cmd.ExecuteNonQueryAsync();

                await TouchCartAsync(connection, cartId);
                return Ok(await BuildCartAsync(connection, userId, createIfMissing: false));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error clearing cart: {ex.Message}");
            }
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("A valid user ID is required.");

            try
            {
                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();
                using var transaction = await connection.BeginTransactionAsync();

                var cartCmd = new MySqlCommand(
                    "SELECT Id FROM shoppingcarts WHERE UserId = @UserId",
                    connection, transaction);
                cartCmd.Parameters.AddWithValue("@UserId", userId);
                var cartIdResult = await cartCmd.ExecuteScalarAsync();

                if (cartIdResult is null)
                    return BadRequest("Cart is empty.");

                int cartId = Convert.ToInt32(cartIdResult);
                var cartItems = new List<(int ProductId, string Name, int Quantity, decimal Price, int Stock)>();
                decimal totalAmount = 0;

                var itemsCmd = new MySqlCommand(@"
                    SELECT ci.ProductId, ci.Quantity, p.Name, p.Price, p.StockQuantity, p.IsAvailable
                    FROM cartitems ci
                    JOIN products p ON p.Id = ci.ProductId
                    WHERE ci.CartId = @CartId
                    FOR UPDATE", connection, transaction);
                itemsCmd.Parameters.AddWithValue("@CartId", cartId);

                using (var reader = await itemsCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int quantity = reader.GetInt32("Quantity");
                        int stock = reader.GetInt32("StockQuantity");
                        bool available = reader.GetBoolean("IsAvailable");

                        if (!available || quantity > stock)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest($"{reader.GetString("Name")} no longer has enough stock.");
                        }

                        decimal price = reader.GetDecimal("Price");
                        totalAmount += quantity * price;
                        cartItems.Add((
                            reader.GetInt32("ProductId"),
                            reader.GetString("Name"),
                            quantity,
                            price,
                            stock));
                    }
                }

                if (cartItems.Count == 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest("Cart is empty.");
                }

                var orderCmd = new MySqlCommand(@"
                    INSERT INTO orders (UserId, TotalAmount, Status, OrderedAt)
                    VALUES (@UserId, @TotalAmount, 'Confirmed', @OrderedAt)",
                    connection, transaction);
                orderCmd.Parameters.AddWithValue("@UserId", userId);
                orderCmd.Parameters.AddWithValue("@TotalAmount", totalAmount);
                orderCmd.Parameters.AddWithValue("@OrderedAt", DateTime.UtcNow);
                await orderCmd.ExecuteNonQueryAsync();
                int orderId = Convert.ToInt32(orderCmd.LastInsertedId);

                foreach (var item in cartItems)
                {
                    var orderItemCmd = new MySqlCommand(@"
                        INSERT INTO orderitems (OrderId, ProductId, Quantity, UnitPrice)
                        VALUES (@OrderId, @ProductId, @Quantity, @UnitPrice)",
                        connection, transaction);
                    orderItemCmd.Parameters.AddWithValue("@OrderId", orderId);
                    orderItemCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                    orderItemCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                    orderItemCmd.Parameters.AddWithValue("@UnitPrice", item.Price);
                    await orderItemCmd.ExecuteNonQueryAsync();

                    var stockCmd = new MySqlCommand(@"
                        UPDATE products
                        SET StockQuantity = StockQuantity - @Quantity,
                            IsAvailable = CASE WHEN StockQuantity - @Quantity > 0 THEN 1 ELSE 0 END
                        WHERE Id = @ProductId", connection, transaction);
                    stockCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                    stockCmd.Parameters.AddWithValue("@ProductId", item.ProductId);
                    await stockCmd.ExecuteNonQueryAsync();
                }

                var clearCmd = new MySqlCommand(
                    "DELETE FROM cartitems WHERE CartId = @CartId",
                    connection, transaction);
                clearCmd.Parameters.AddWithValue("@CartId", cartId);
                await clearCmd.ExecuteNonQueryAsync();

                var touchCmd = new MySqlCommand(
                    "UPDATE shoppingcarts SET UpdatedAt = @UpdatedAt WHERE Id = @CartId",
                    connection, transaction);
                touchCmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
                touchCmd.Parameters.AddWithValue("@CartId", cartId);
                await touchCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    Id = orderId,
                    UserId = userId,
                    TotalAmount = totalAmount,
                    Status = "Confirmed",
                    OrderedAt = DateTime.UtcNow,
                    Items = cartItems.Select(item => new
                    {
                        item.ProductId,
                        ProductName = item.Name,
                        item.Quantity,
                        UnitPrice = item.Price,
                        LineTotal = item.Price * item.Quantity
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Checkout failed: {ex.Message}");
            }
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] int userId)
        {
            if (userId <= 0)
                return BadRequest("A valid user ID is required.");

            try
            {
                using var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();

                var orders = new List<object>();
                var orderCmd = new MySqlCommand(@"
                    SELECT Id, UserId, TotalAmount, Status, OrderedAt
                    FROM orders
                    WHERE UserId = @UserId
                    ORDER BY OrderedAt DESC", connection);
                orderCmd.Parameters.AddWithValue("@UserId", userId);

                var orderRows = new List<(int Id, int UserId, decimal Total, string Status, DateTime OrderedAt)>();
                using (var reader = await orderCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        orderRows.Add((
                            reader.GetInt32("Id"),
                            reader.GetInt32("UserId"),
                            reader.GetDecimal("TotalAmount"),
                            reader.GetString("Status"),
                            reader.GetDateTime("OrderedAt")));
                    }
                }

                foreach (var order in orderRows)
                {
                    var items = new List<object>();
                    var itemsCmd = new MySqlCommand(@"
                        SELECT oi.ProductId, p.Name, oi.Quantity, oi.UnitPrice
                        FROM orderitems oi
                        JOIN products p ON p.Id = oi.ProductId
                        WHERE oi.OrderId = @OrderId", connection);
                    itemsCmd.Parameters.AddWithValue("@OrderId", order.Id);

                    using var reader = await itemsCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        decimal unitPrice = reader.GetDecimal("UnitPrice");
                        int quantity = reader.GetInt32("Quantity");
                        items.Add(new
                        {
                            ProductId = reader.GetInt32("ProductId"),
                            ProductName = reader.GetString("Name"),
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            LineTotal = unitPrice * quantity
                        });
                    }

                    orders.Add(new
                    {
                        order.Id,
                        order.UserId,
                        TotalAmount = order.Total,
                        order.Status,
                        order.OrderedAt,
                        Items = items
                    });
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching orders: {ex.Message}");
            }
        }

        private async Task<int> GetOrCreateCartIdAsync(
            MySqlConnection connection,
            int userId)
        {
            var cartCmd = new MySqlCommand(
                "SELECT Id FROM shoppingcarts WHERE UserId = @UserId",
                connection);
            cartCmd.Parameters.AddWithValue("@UserId", userId);
            var result = await cartCmd.ExecuteScalarAsync();

            if (result is not null)
                return Convert.ToInt32(result);

            var createCmd = new MySqlCommand(@"
                INSERT INTO shoppingcarts (UserId, UpdatedAt)
                VALUES (@UserId, @UpdatedAt)", connection);
            createCmd.Parameters.AddWithValue("@UserId", userId);
            createCmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
            await createCmd.ExecuteNonQueryAsync();
            return Convert.ToInt32(createCmd.LastInsertedId);
        }

        private static async Task TouchCartAsync(
            MySqlConnection connection,
            int cartId)
        {
            var cmd = new MySqlCommand(
                "UPDATE shoppingcarts SET UpdatedAt = @UpdatedAt WHERE Id = @CartId",
                connection);
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@CartId", cartId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<object> BuildCartAsync(
            MySqlConnection connection,
            int userId,
            bool createIfMissing)
        {
            int cartId;
            if (createIfMissing)
            {
                cartId = await GetOrCreateCartIdAsync(connection, userId);
            }
            else
            {
                var cartCmd = new MySqlCommand(
                    "SELECT Id FROM shoppingcarts WHERE UserId = @UserId",
                    connection);
                cartCmd.Parameters.AddWithValue("@UserId", userId);
                var result = await cartCmd.ExecuteScalarAsync();
                cartId = result is null ? 0 : Convert.ToInt32(result);
            }

            var items = new List<object>();
            decimal totalAmount = 0;

            if (cartId > 0)
            {
                var cmd = new MySqlCommand(@"
                    SELECT ci.Id, ci.ProductId, ci.Quantity, p.Name, p.Price, p.ImageUrl
                    FROM cartitems ci
                    JOIN products p ON p.Id = ci.ProductId
                    WHERE ci.CartId = @CartId
                    ORDER BY ci.Id", connection);
                cmd.Parameters.AddWithValue("@CartId", cartId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int quantity = reader.GetInt32("Quantity");
                    decimal unitPrice = reader.GetDecimal("Price");
                    decimal lineTotal = unitPrice * quantity;
                    totalAmount += lineTotal;

                    items.Add(new
                    {
                        Id = reader.GetInt32("Id"),
                        ProductId = reader.GetInt32("ProductId"),
                        ProductName = reader.GetString("Name"),
                        UnitPrice = unitPrice,
                        Quantity = quantity,
                        LineTotal = lineTotal,
                        ImageUrl = reader.IsDBNull(reader.GetOrdinal("ImageUrl"))
                            ? string.Empty
                            : reader.GetString("ImageUrl")
                    });
                }
            }

            return new
            {
                CartId = cartId,
                UserId = userId,
                Items = items,
                TotalAmount = totalAmount
            };
        }
    }

    public class AddToCartRequest
    {
        public int UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateCartItemRequest
    {
        public int Quantity { get; set; }
    }
}
