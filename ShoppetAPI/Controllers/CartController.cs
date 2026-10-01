using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ShoppetAPI.Services;

namespace ShoppetAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CartController : ControllerBase
{
    private readonly IConfiguration _configuration;
    public CartController(IConfiguration configuration)=>_configuration=configuration;

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException("SharedSqlServer connection is missing.");

    [HttpGet]
    public async Task<IActionResult> GetCart([FromQuery] int userId)
    {
        if(userId<=0) return BadRequest("A valid user is required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            if(!await RbacService.IsPetOwnerAsync(conn,userId))
                return StatusCode(403,"Access denied.");

            var cartId=await EnsureCartAsync(conn,userId);

            var items=new List<object>();
            decimal total=0;
            await using var cmd=new SqlCommand("""
                SELECT ci.Id,ci.MarketplaceListingId,1,
                       m.Title,m.Price,ISNULL(m.ImageUrl,'')
                FROM MarketplaceCartItems ci
                JOIN MarketplaceListings m ON m.Id=ci.MarketplaceListingId
                WHERE ci.CartId=@CartId
                ORDER BY ci.Id DESC;
                """,conn);
            cmd.Parameters.AddWithValue("@CartId",cartId);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())
            {
                var qty=r.GetInt32(2);
                var price=r.GetDecimal(4);
                var line=price*qty;
                total+=line;
                items.Add(new{
                    Id=r.GetInt32(0),
                    ProductId=r.GetInt32(1),
                    ProductName=r.GetString(3),
                    UnitPrice=price,
                    Quantity=qty,
                    LineTotal=line,
                    ImageUrl=r.GetString(5)
                });
            }

            return Ok(new{CartId=cartId,UserId=userId,Items=items,TotalAmount=total});
        }
        catch(Exception ex){return StatusCode(500,$"Cart error: {ex.Message}");}
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
    {
        if(request.UserId<=0||request.ProductId<=0)
            return BadRequest("A valid user and listing are required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            if(!await RbacService.IsPetOwnerAsync(conn,request.UserId))
                return StatusCode(403,"Access denied.");

            await using(var validate=new SqlCommand("""
                SELECT SellerUserId,ISNULL(Status,'Available')
                FROM MarketplaceListings
                WHERE Id=@ListingId;
                """,conn))
            {
                validate.Parameters.AddWithValue("@ListingId",request.ProductId);
                await using var vr=await validate.ExecuteReaderAsync();
                if(!await vr.ReadAsync()) return NotFound("Listing not found.");
                if(vr.GetInt32(0)==request.UserId)
                    return BadRequest("You cannot add your own listing to cart.");
                if(!new[]{"Available","Active"}.Contains(vr.GetString(1),StringComparer.OrdinalIgnoreCase))
                    return BadRequest("This listing has already been sold.");
            }

            var cartId=await EnsureCartAsync(conn,request.UserId);

            await using var cmd=new SqlCommand("""
                IF EXISTS(
                    SELECT 1 FROM MarketplaceCartItems
                    WHERE CartId=@CartId AND MarketplaceListingId=@ListingId
                )
                    UPDATE MarketplaceCartItems
                    SET Quantity=1
                    WHERE CartId=@CartId AND MarketplaceListingId=@ListingId;
                ELSE
                    INSERT INTO MarketplaceCartItems(CartId,MarketplaceListingId,Quantity)
                    VALUES(@CartId,@ListingId,@Quantity);

                UPDATE MarketplaceCart SET UpdatedAt=SYSDATETIME() WHERE Id=@CartId;
                """,conn);
            cmd.Parameters.AddWithValue("@CartId",cartId);
            cmd.Parameters.AddWithValue("@ListingId",request.ProductId);
            cmd.Parameters.AddWithValue("@Quantity",1);
            await cmd.ExecuteNonQueryAsync();
            return await GetCart(request.UserId);
        }
        catch(Exception ex){return StatusCode(500,$"Add to cart error: {ex.Message}");}
    }

    [HttpPut("items/{itemId:int}")]
    public async Task<IActionResult> UpdateItem(int itemId,[FromBody] UpdateCartItemRequest request,[FromQuery] int userId)
    {
        if(userId<=0) return BadRequest("A valid user is required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            if(request.Quantity<=0)
            {
                await using var del=new SqlCommand("""
                    DELETE ci FROM MarketplaceCartItems ci
                    JOIN MarketplaceCart c ON c.Id=ci.CartId
                    WHERE ci.Id=@Id AND c.UserId=@UserId;
                    """,conn);
                del.Parameters.AddWithValue("@Id",itemId);
                del.Parameters.AddWithValue("@UserId",userId);
                await del.ExecuteNonQueryAsync();
            }
            else
            {
                await using var cmd=new SqlCommand("""
                    UPDATE ci SET Quantity=@Quantity
                    FROM MarketplaceCartItems ci
                    JOIN MarketplaceCart c ON c.Id=ci.CartId
                    WHERE ci.Id=@Id AND c.UserId=@UserId;
                    """,conn);
                cmd.Parameters.AddWithValue("@Id",itemId);
                cmd.Parameters.AddWithValue("@UserId",userId);
                cmd.Parameters.AddWithValue("@Quantity",1);
                await cmd.ExecuteNonQueryAsync();
            }
            return await GetCart(userId);
        }
        catch(Exception ex){return StatusCode(500,$"Update cart error: {ex.Message}");}
    }

    [HttpDelete("items/{itemId:int}")]
    public async Task<IActionResult> RemoveItem(int itemId,[FromQuery] int userId)
    {
        if(userId<=0) return BadRequest("A valid user is required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                DELETE ci FROM MarketplaceCartItems ci
                JOIN MarketplaceCart c ON c.Id=ci.CartId
                WHERE ci.Id=@Id AND c.UserId=@UserId;
                """,conn);
            cmd.Parameters.AddWithValue("@Id",itemId);
            cmd.Parameters.AddWithValue("@UserId",userId);
            await cmd.ExecuteNonQueryAsync();
            return await GetCart(userId);
        }
        catch(Exception ex){return StatusCode(500,$"Remove cart item error: {ex.Message}");}
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart([FromQuery] int userId)
    {
        if(userId<=0) return BadRequest("A valid user is required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                DELETE ci FROM MarketplaceCartItems ci
                JOIN MarketplaceCart c ON c.Id=ci.CartId
                WHERE c.UserId=@UserId;
                """,conn);
            cmd.Parameters.AddWithValue("@UserId",userId);
            await cmd.ExecuteNonQueryAsync();
            return await GetCart(userId);
        }
        catch(Exception ex){return StatusCode(500,$"Clear cart error: {ex.Message}");}
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        [FromQuery] int userId,
        [FromQuery] string paymentMethod = "GCash Mock", [FromQuery] bool simulateSuccess=true)
    {
        if(userId<=0) return BadRequest("A valid user is required.");
        if(!new[]{"GCash Mock","Cash on Meet-up","GCash - Demo","Maya - Demo"}.Contains(paymentMethod,StringComparer.OrdinalIgnoreCase)) return BadRequest("Choose a supported simulated payment method.");
        if(!simulateSuccess)return BadRequest("Simulated payment failed. Your cart is unchanged.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            if(!await RbacService.IsPetOwnerAsync(conn,userId))
                return StatusCode(403,"Access denied.");

            await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            int? cartId=null;
            await using(var find=new SqlCommand("SELECT Id FROM MarketplaceCart WITH(UPDLOCK,HOLDLOCK) WHERE UserId=@UserId",conn,tx))
            {
                find.Parameters.AddWithValue("@UserId",userId);
                var v=await find.ExecuteScalarAsync();
                if(v is not null) cartId=Convert.ToInt32(v);
            }
            if(!cartId.HasValue) return BadRequest("Cart is empty.");

            var entries=new List<(int ListingId,int Qty,decimal Price,string Title,int SellerUserId)>();
            await using(var read=new SqlCommand("""
                SELECT ci.MarketplaceListingId,1,m.Price,m.Title,m.SellerUserId,m.Status
                FROM MarketplaceCartItems ci
                JOIN MarketplaceListings m WITH(UPDLOCK,HOLDLOCK) ON m.Id=ci.MarketplaceListingId
                WHERE ci.CartId=@CartId;
                """,conn,tx))
            {
                read.Parameters.AddWithValue("@CartId",cartId.Value);
                await using var r=await read.ExecuteReaderAsync();
                while(await r.ReadAsync())
                { if(!new[]{"Available","Active"}.Contains(r.GetString(5),StringComparer.OrdinalIgnoreCase)) return BadRequest("A cart item is no longer available. Remove it before checkout.");
                  entries.Add((r.GetInt32(0),1,r.GetDecimal(2),r.GetString(3),r.GetInt32(4))); }
            }
            if(entries.Count==0) return BadRequest("Cart is empty.");
            if(entries.Any(x=>x.SellerUserId==userId))
                return BadRequest("You cannot purchase your own marketplace listing.");

            var total=entries.Sum(x=>x.Price*x.Qty);
            int orderId=0;
            foreach(var seller in entries.GroupBy(e=>e.SellerUserId))
            {
                var reference="MOCK-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
                var sellerTotal=seller.Sum(e=>e.Price);
                await using(var order=new SqlCommand("INSERT INTO MarketplaceOrders(BuyerUserId,SellerUserId,Reference,PaymentMethod,Status,Subtotal,VoucherDiscount,Total,CreatedAt,CompletedAt) OUTPUT INSERTED.Id VALUES(@Buyer,@Seller,@Ref,@Method,'Completed',@Total,0,@Total,SYSDATETIME(),SYSDATETIME())",conn,tx))
                {order.Parameters.AddWithValue("@Buyer",userId);order.Parameters.AddWithValue("@Seller",seller.Key);order.Parameters.AddWithValue("@Ref",reference);order.Parameters.AddWithValue("@Method",paymentMethod);order.Parameters.AddWithValue("@Total",sellerTotal);orderId=Convert.ToInt32(await order.ExecuteScalarAsync());}
                foreach(var e in seller)
                {
                    await using var line=new SqlCommand("INSERT INTO MarketplaceOrderItems(OrderId,ListingId,ListingTitle,Price) VALUES(@Order,@Listing,@Title,@Price); UPDATE MarketplaceListings SET Status='Sold',UpdatedAt=SYSDATETIME() WHERE Id=@Listing;",conn,tx);
                    line.Parameters.AddWithValue("@Order",orderId);line.Parameters.AddWithValue("@Listing",e.ListingId);line.Parameters.AddWithValue("@Title",e.Title);line.Parameters.AddWithValue("@Price",e.Price);await line.ExecuteNonQueryAsync();
                }
                await using var payment=new SqlCommand("INSERT INTO Transactions(UserId,Type,Amount,Reference,PaidAt,PaymentMethod,Status) VALUES(@User,'MarketplacePurchase',@Total,@Ref,SYSDATETIME(),@Method,'SimulatedPaid')",conn,tx);
                payment.Parameters.AddWithValue("@User",userId);payment.Parameters.AddWithValue("@Total",sellerTotal);payment.Parameters.AddWithValue("@Ref",reference);payment.Parameters.AddWithValue("@Method",paymentMethod);await payment.ExecuteNonQueryAsync();
            }
            await using(var notify=new SqlCommand("""
                INSERT INTO Notifications(UserId,Title,Body,Link,Icon,IsRead,CreatedAt)
                VALUES(@UserId,'Purchase confirmed',
                       CONCAT('Mock order #',@OrderId,' was completed successfully.'),
                       'orders','',0,SYSDATETIME());
                """,conn,tx))
            {
                notify.Parameters.AddWithValue("@UserId",userId);
                notify.Parameters.AddWithValue("@OrderId",orderId);
                await notify.ExecuteNonQueryAsync();
            }

            await using(var clear=new SqlCommand("DELETE FROM MarketplaceCartItems WHERE CartId=@CartId",conn,tx))
            {
                clear.Parameters.AddWithValue("@CartId",cartId.Value);
                await clear.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new{
                Id=orderId,UserId=userId,TotalAmount=total,Status="Completed",
                OrderedAt=DateTime.Now,
                Items=entries.Select(e=>new{
                    ProductId=e.ListingId,ProductName=e.Title,Quantity=e.Qty,
                    UnitPrice=e.Price,LineTotal=e.Price*e.Qty
                }).ToList()
            });
        }
        catch(Exception ex){return StatusCode(500,$"Checkout error: {ex.Message}");}
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery] int userId)
    {
        try
        {
            var orders=new List<OrderResponse>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using(var cmd=new SqlCommand("""
                SELECT Id,BuyerUserId,Total,Status,CreatedAt
                FROM MarketplaceOrders
                WHERE BuyerUserId=@UserId OR SellerUserId=@UserId
                ORDER BY CreatedAt DESC;
                """,conn))
            {
                cmd.Parameters.AddWithValue("@UserId",userId);
                await using var r=await cmd.ExecuteReaderAsync();
                while(await r.ReadAsync())
                {
                    orders.Add(new OrderResponse
                    {
                        Id=r.GetInt32(0),
                        UserId=r.GetInt32(1),
                        TotalAmount=r.GetDecimal(2),
                        Status=r.GetString(3),
                        OrderedAt=r.GetDateTime(4)
                    });
                }
            }

            foreach(var order in orders)
            {
                await using var items=new SqlCommand("""
                    SELECT oi.ListingId,oi.ListingTitle,
                           1,oi.Price
                    FROM MarketplaceOrderItems oi
                    LEFT JOIN MarketplaceListings m ON m.Id=oi.ListingId
                    WHERE oi.OrderId=@OrderId
                    ORDER BY oi.Id;
                    """,conn);
                items.Parameters.AddWithValue("@OrderId",order.Id);
                await using var r=await items.ExecuteReaderAsync();
                while(await r.ReadAsync())
                {
                    var qty=r.GetInt32(2);
                    var price=r.GetDecimal(3);
                    order.Items.Add(new OrderItemResponse
                    {
                        ProductId=r.GetInt32(0),
                        ProductName=r.GetString(1),
                        Quantity=qty,
                        UnitPrice=price,
                        LineTotal=qty*price
                    });
                }
            }

            return Ok(orders);
        }
        catch(Exception ex){return StatusCode(500,$"Orders error: {ex.Message}");}
    }

    private static async Task<int> EnsureCartAsync(SqlConnection conn,int userId)
    {
        await using var transaction=(SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await using var q=new SqlCommand("IF NOT EXISTS(SELECT 1 FROM MarketplaceCart WITH(UPDLOCK,HOLDLOCK) WHERE UserId=@U) INSERT INTO MarketplaceCart(UserId) VALUES(@U); SELECT Id FROM MarketplaceCart WHERE UserId=@U;",conn,transaction);
        q.Parameters.AddWithValue("@U",userId);var id=Convert.ToInt32(await q.ExecuteScalarAsync());await transaction.CommitAsync();return id;
    }

}

public class OrderResponse
{
    public int Id{get;set;}
    public int UserId{get;set;}
    public decimal TotalAmount{get;set;}
    public string Status{get;set;}=string.Empty;
    public DateTime OrderedAt{get;set;}
    public List<OrderItemResponse> Items{get;set;}=new();
}
public class OrderItemResponse
{
    public int ProductId{get;set;}
    public string ProductName{get;set;}=string.Empty;
    public int Quantity{get;set;}
    public decimal UnitPrice{get;set;}
    public decimal LineTotal{get;set;}
}

public class AddToCartRequest
{
    public int UserId{get;set;}
    public int ProductId{get;set;}
    public int Quantity{get;set;}=1;
}
public class UpdateCartItemRequest{public int Quantity{get;set;}}
