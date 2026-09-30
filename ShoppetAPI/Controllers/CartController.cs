using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

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
            var cartId=await EnsureCartAsync(conn,userId);

            var items=new List<object>();
            decimal total=0;
            await using var cmd=new SqlCommand("""
                SELECT ci.Id,ci.MarketplaceListingId,ci.Quantity,
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
            var cartId=await EnsureCartAsync(conn,request.UserId);

            await using var cmd=new SqlCommand("""
                IF EXISTS(
                    SELECT 1 FROM MarketplaceCartItems
                    WHERE CartId=@CartId AND MarketplaceListingId=@ListingId
                )
                    UPDATE MarketplaceCartItems
                    SET Quantity=Quantity+@Quantity
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
                cmd.Parameters.AddWithValue("@Quantity",request.Quantity);
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
    public async Task<IActionResult> Checkout([FromQuery] int userId)
    {
        if(userId<=0) return BadRequest("A valid user is required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var tx=(SqlTransaction)await conn.BeginTransactionAsync();

            int? cartId=null;
            await using(var find=new SqlCommand("SELECT Id FROM MarketplaceCart WHERE UserId=@UserId",conn,tx))
            {
                find.Parameters.AddWithValue("@UserId",userId);
                var v=await find.ExecuteScalarAsync();
                if(v is not null) cartId=Convert.ToInt32(v);
            }
            if(!cartId.HasValue) return BadRequest("Cart is empty.");

            var entries=new List<(int ListingId,int Qty,decimal Price,string Title)>();
            await using(var read=new SqlCommand("""
                SELECT ci.MarketplaceListingId,ci.Quantity,m.Price,m.Title
                FROM MarketplaceCartItems ci
                JOIN MarketplaceListings m ON m.Id=ci.MarketplaceListingId
                WHERE ci.CartId=@CartId AND ISNULL(m.Status,'Available')<>'Sold';
                """,conn,tx))
            {
                read.Parameters.AddWithValue("@CartId",cartId.Value);
                await using var r=await read.ExecuteReaderAsync();
                while(await r.ReadAsync())
                    entries.Add((r.GetInt32(0),r.GetInt32(1),r.GetDecimal(2),r.GetString(3)));
            }
            if(entries.Count==0) return BadRequest("Cart is empty.");

            var total=entries.Sum(x=>x.Price*x.Qty);
            int orderId;
            await using(var order=new SqlCommand("""
                INSERT INTO MarketplaceOrders(UserId,TotalAmount,Status,OrderedAt)
                OUTPUT INSERTED.Id
                VALUES(@UserId,@Total,'Confirmed',SYSDATETIME());
                """,conn,tx))
            {
                order.Parameters.AddWithValue("@UserId",userId);
                order.Parameters.AddWithValue("@Total",total);
                orderId=Convert.ToInt32(await order.ExecuteScalarAsync());
            }

            foreach(var e in entries)
            {
                await using var item=new SqlCommand("""
                    INSERT INTO MarketplaceOrderItems(OrderId,MarketplaceListingId,Quantity,UnitPrice)
                    VALUES(@OrderId,@ListingId,@Quantity,@UnitPrice);
                    """,conn,tx);
                item.Parameters.AddWithValue("@OrderId",orderId);
                item.Parameters.AddWithValue("@ListingId",e.ListingId);
                item.Parameters.AddWithValue("@Quantity",e.Qty);
                item.Parameters.AddWithValue("@UnitPrice",e.Price);
                await item.ExecuteNonQueryAsync();
            }

            foreach(var e in entries)
            {
                await using var sold=new SqlCommand(
                    "UPDATE MarketplaceListings SET Status='Sold' WHERE Id=@ListingId",conn,tx);
                sold.Parameters.AddWithValue("@ListingId",e.ListingId);
                await sold.ExecuteNonQueryAsync();
            }

            var reference = "MOCK-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
            await using(var payment=new SqlCommand("""
                INSERT INTO Transactions
                    (UserId,Type,Amount,Reference,PaidAt,PaymentMethod,Status)
                VALUES
                    (@UserId,'MarketplacePurchase',@Amount,@Reference,SYSDATETIME(),'Mock Payment','Completed');
                """,conn,tx))
            {
                payment.Parameters.AddWithValue("@UserId",userId);
                payment.Parameters.AddWithValue("@Amount",total);
                payment.Parameters.AddWithValue("@Reference",reference);
                await payment.ExecuteNonQueryAsync();
            }

            await using(var clear=new SqlCommand("DELETE FROM MarketplaceCartItems WHERE CartId=@CartId",conn,tx))
            {
                clear.Parameters.AddWithValue("@CartId",cartId.Value);
                await clear.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return Ok(new{
                Id=orderId,UserId=userId,TotalAmount=total,Status="Confirmed",
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
            var orders=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var cmd=new SqlCommand("""
                SELECT Id,UserId,TotalAmount,Status,OrderedAt
                FROM MarketplaceOrders
                WHERE UserId=@UserId
                ORDER BY OrderedAt DESC;
                """,conn);
            cmd.Parameters.AddWithValue("@UserId",userId);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())
                orders.Add(new{
                    Id=r.GetInt32(0),UserId=r.GetInt32(1),TotalAmount=r.GetDecimal(2),
                    Status=r.GetString(3),OrderedAt=r.GetDateTime(4),
                    Items=Array.Empty<object>()
                });
            return Ok(orders);
        }
        catch(Exception ex){return StatusCode(500,$"Orders error: {ex.Message}");}
    }

    private static async Task<int> EnsureCartAsync(SqlConnection conn,int userId)
    {
        await using var find=new SqlCommand("SELECT Id FROM MarketplaceCart WHERE UserId=@UserId",conn);
        find.Parameters.AddWithValue("@UserId",userId);
        var existing=await find.ExecuteScalarAsync();
        if(existing is not null)return Convert.ToInt32(existing);

        await using var create=new SqlCommand("""
            INSERT INTO MarketplaceCart(UserId,UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES(@UserId,SYSDATETIME());
            """,conn);
        create.Parameters.AddWithValue("@UserId",userId);
        return Convert.ToInt32(await create.ExecuteScalarAsync());
    }
}

public class AddToCartRequest
{
    public int UserId{get;set;}
    public int ProductId{get;set;}
    public int Quantity{get;set;}=1;
}
public class UpdateCartItemRequest{public int Quantity{get;set;}}
