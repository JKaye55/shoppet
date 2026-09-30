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
        ?? throw new InvalidOperationException("SharedSqlServer connection string not found.");

    [HttpGet]
    public async Task<IActionResult> GetCart([FromQuery]int userId)
    {
        if(userId<=0) return BadRequest("A valid user ID is required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            return Ok(await BuildCartAsync(conn,userId,true));
        }
        catch(Exception ex){ return StatusCode(500,$"Error fetching cart: {ex.Message}"); }
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddToCart([FromBody]AddToCartRequest request)
    {
        if(request.UserId<=0||request.ProductId<=0||request.Quantity<=0)
            return BadRequest("Valid user, product, and quantity are required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            int stock; bool available;
            await using(var pcmd=new SqlCommand("SELECT StockQuantity,IsAvailable FROM ShopProducts WHERE Id=@Id",conn))
            {
                pcmd.Parameters.AddWithValue("@Id",request.ProductId);
                await using var r=await pcmd.ExecuteReaderAsync();
                if(!await r.ReadAsync()) return NotFound("Product not found.");
                stock=r.GetInt32(0); available=r.GetBoolean(1);
            }
            if(!available||stock<=0) return BadRequest("Product is not available.");

            int cartId=await GetOrCreateCartIdAsync(conn,request.UserId);
            int? itemId=null; int existing=0;
            await using(var ecmd=new SqlCommand("SELECT Id,Quantity FROM CartItems WHERE CartId=@CartId AND ProductId=@ProductId",conn))
            {
                ecmd.Parameters.AddWithValue("@CartId",cartId);
                ecmd.Parameters.AddWithValue("@ProductId",request.ProductId);
                await using var r=await ecmd.ExecuteReaderAsync();
                if(await r.ReadAsync()){ itemId=r.GetInt32(0); existing=r.GetInt32(1); }
            }

            int newQty=existing+request.Quantity;
            if(newQty>stock) return BadRequest($"Only {stock} item(s) are available.");

            if(itemId.HasValue)
            {
                await using var cmd=new SqlCommand("UPDATE CartItems SET Quantity=@Qty WHERE Id=@Id",conn);
                cmd.Parameters.AddWithValue("@Qty",newQty); cmd.Parameters.AddWithValue("@Id",itemId.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                await using var cmd=new SqlCommand("INSERT INTO CartItems(CartId,ProductId,Quantity) VALUES(@CartId,@ProductId,@Qty)",conn);
                cmd.Parameters.AddWithValue("@CartId",cartId); cmd.Parameters.AddWithValue("@ProductId",request.ProductId); cmd.Parameters.AddWithValue("@Qty",request.Quantity);
                await cmd.ExecuteNonQueryAsync();
            }

            await TouchCartAsync(conn,cartId);
            return Ok(await BuildCartAsync(conn,request.UserId,false));
        }
        catch(Exception ex){ return StatusCode(500,$"Error adding to cart: {ex.Message}"); }
    }

    [HttpPut("items/{itemId:int}")]
    public async Task<IActionResult> UpdateCartItem(int itemId,[FromQuery]int userId,[FromBody]UpdateCartItemRequest request)
    {
        if(userId<=0||itemId<=0||request.Quantity<=0) return BadRequest("Valid user, item, and quantity are required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();

            int cartId,stock;
            await using(var cmd=new SqlCommand(@"
                SELECT ci.CartId,p.StockQuantity
                FROM CartItems ci
                JOIN ShoppingCarts c ON c.Id=ci.CartId
                JOIN ShopProducts p ON p.Id=ci.ProductId
                WHERE ci.Id=@ItemId AND c.UserId=@UserId;",conn))
            {
                cmd.Parameters.AddWithValue("@ItemId",itemId); cmd.Parameters.AddWithValue("@UserId",userId);
                await using var r=await cmd.ExecuteReaderAsync();
                if(!await r.ReadAsync()) return NotFound("Cart item not found.");
                cartId=r.GetInt32(0); stock=r.GetInt32(1);
            }

            if(request.Quantity>stock) return BadRequest($"Only {stock} item(s) are available.");
            await using(var cmd=new SqlCommand("UPDATE CartItems SET Quantity=@Qty WHERE Id=@Id",conn))
            {
                cmd.Parameters.AddWithValue("@Qty",request.Quantity); cmd.Parameters.AddWithValue("@Id",itemId);
                await cmd.ExecuteNonQueryAsync();
            }
            await TouchCartAsync(conn,cartId);
            return Ok(await BuildCartAsync(conn,userId,false));
        }
        catch(Exception ex){ return StatusCode(500,$"Error updating cart item: {ex.Message}"); }
    }

    [HttpDelete("items/{itemId:int}")]
    public async Task<IActionResult> RemoveCartItem(int itemId,[FromQuery]int userId)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            int? cartId=null;
            await using(var cmd=new SqlCommand(@"
                SELECT ci.CartId
                FROM CartItems ci JOIN ShoppingCarts c ON c.Id=ci.CartId
                WHERE ci.Id=@ItemId AND c.UserId=@UserId;",conn))
            {
                cmd.Parameters.AddWithValue("@ItemId",itemId); cmd.Parameters.AddWithValue("@UserId",userId);
                var val=await cmd.ExecuteScalarAsync();
                if(val is null) return NotFound("Cart item not found.");
                cartId=Convert.ToInt32(val);
            }
            await using(var del=new SqlCommand("DELETE FROM CartItems WHERE Id=@Id AND CartId=@CartId",conn))
            {
                del.Parameters.AddWithValue("@Id",itemId); del.Parameters.AddWithValue("@CartId",cartId.Value);
                await del.ExecuteNonQueryAsync();
            }
            await TouchCartAsync(conn,cartId.Value);
            return Ok(await BuildCartAsync(conn,userId,false));
        }
        catch(Exception ex){ return StatusCode(500,$"Error removing cart item: {ex.Message}"); }
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart([FromQuery]int userId)
    {
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            int cartId=await GetOrCreateCartIdAsync(conn,userId);
            await using(var cmd=new SqlCommand("DELETE FROM CartItems WHERE CartId=@CartId",conn))
            {
                cmd.Parameters.AddWithValue("@CartId",cartId);
                await cmd.ExecuteNonQueryAsync();
            }
            await TouchCartAsync(conn,cartId);
            return Ok(await BuildCartAsync(conn,userId,false));
        }
        catch(Exception ex){ return StatusCode(500,$"Error clearing cart: {ex.Message}"); }
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromQuery]int userId)
    {
        if(userId<=0) return BadRequest("A valid user ID is required.");
        try
        {
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            await using var tx=(SqlTransaction)await conn.BeginTransactionAsync();

            int? cartId=null;
            await using(var cmd=new SqlCommand("SELECT Id FROM ShoppingCarts WHERE UserId=@UserId",conn,tx))
            {
                cmd.Parameters.AddWithValue("@UserId",userId);
                var val=await cmd.ExecuteScalarAsync();
                if(val is not null) cartId=Convert.ToInt32(val);
            }
            if(!cartId.HasValue){ await tx.RollbackAsync(); return BadRequest("Cart is empty."); }

            var items=new List<(int ProductId,string Name,int Qty,decimal Price,int Stock)>();
            await using(var cmd=new SqlCommand(@"
                SELECT ci.ProductId,ci.Quantity,p.Name,p.Price,p.StockQuantity,p.IsAvailable
                FROM CartItems ci
                JOIN ShopProducts p ON p.Id=ci.ProductId
                WHERE ci.CartId=@CartId;",conn,tx))
            {
                cmd.Parameters.AddWithValue("@CartId",cartId.Value);
                await using var r=await cmd.ExecuteReaderAsync();
                while(await r.ReadAsync())
                {
                    int qty=r.GetInt32(1),stock=r.GetInt32(4); bool available=r.GetBoolean(5);
                    if(!available||qty>stock){ await r.DisposeAsync(); await tx.RollbackAsync(); return BadRequest($"{r.GetString(2)} no longer has enough stock."); }
                    items.Add((r.GetInt32(0),r.GetString(2),qty,r.GetDecimal(3),stock));
                }
            }
            if(items.Count==0){ await tx.RollbackAsync(); return BadRequest("Cart is empty."); }

            decimal total=items.Sum(i=>i.Qty*i.Price);
            int orderId;
            await using(var cmd=new SqlCommand(@"
                INSERT INTO ShopOrders(UserId,TotalAmount,Status,OrderedAt)
                OUTPUT INSERTED.Id
                VALUES(@UserId,@Total,'Confirmed',SYSUTCDATETIME());",conn,tx))
            {
                cmd.Parameters.AddWithValue("@UserId",userId); cmd.Parameters.AddWithValue("@Total",total);
                orderId=Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            foreach(var item in items)
            {
                await using(var cmd=new SqlCommand("INSERT INTO ShopOrderItems(OrderId,ProductId,Quantity,UnitPrice) VALUES(@OrderId,@ProductId,@Qty,@Price)",conn,tx))
                {
                    cmd.Parameters.AddWithValue("@OrderId",orderId); cmd.Parameters.AddWithValue("@ProductId",item.ProductId); cmd.Parameters.AddWithValue("@Qty",item.Qty); cmd.Parameters.AddWithValue("@Price",item.Price);
                    await cmd.ExecuteNonQueryAsync();
                }
                int remaining=item.Stock-item.Qty;
                await using(var cmd=new SqlCommand("UPDATE ShopProducts SET StockQuantity=@Stock,IsAvailable=@Available WHERE Id=@Id",conn,tx))
                {
                    cmd.Parameters.AddWithValue("@Stock",remaining); cmd.Parameters.AddWithValue("@Available",remaining>0); cmd.Parameters.AddWithValue("@Id",item.ProductId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await using(var clear=new SqlCommand("DELETE FROM CartItems WHERE CartId=@CartId",conn,tx))
            { clear.Parameters.AddWithValue("@CartId",cartId.Value); await clear.ExecuteNonQueryAsync(); }

            await tx.CommitAsync();
            return Ok(new{
                Id=orderId,UserId=userId,TotalAmount=total,Status="Confirmed",OrderedAt=DateTime.UtcNow,
                Items=items.Select(i=>new{i.ProductId,ProductName=i.Name,Quantity=i.Qty,UnitPrice=i.Price,LineTotal=i.Qty*i.Price}).ToList()
            });
        }
        catch(Exception ex){ return StatusCode(500,$"Checkout failed: {ex.Message}"); }
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders([FromQuery]int userId)
    {
        try
        {
            var orders=new List<object>();
            await using var conn=new SqlConnection(ConnectionString);
            await conn.OpenAsync();
            var rows=new List<(int Id,int UserId,decimal Total,string Status,DateTime OrderedAt)>();
            await using(var cmd=new SqlCommand("SELECT Id,UserId,TotalAmount,Status,OrderedAt FROM ShopOrders WHERE UserId=@UserId ORDER BY OrderedAt DESC",conn))
            {
                cmd.Parameters.AddWithValue("@UserId",userId);
                await using var r=await cmd.ExecuteReaderAsync();
                while(await r.ReadAsync()) rows.Add((r.GetInt32(0),r.GetInt32(1),r.GetDecimal(2),r.GetString(3),r.GetDateTime(4)));
            }
            foreach(var row in rows)
            {
                var items=new List<object>();
                await using var cmd=new SqlCommand(@"
                    SELECT oi.ProductId,p.Name,oi.Quantity,oi.UnitPrice
                    FROM ShopOrderItems oi JOIN ShopProducts p ON p.Id=oi.ProductId
                    WHERE oi.OrderId=@OrderId;",conn);
                cmd.Parameters.AddWithValue("@OrderId",row.Id);
                await using var r=await cmd.ExecuteReaderAsync();
                while(await r.ReadAsync())
                {
                    int q=r.GetInt32(2); decimal price=r.GetDecimal(3);
                    items.Add(new{ProductId=r.GetInt32(0),ProductName=r.GetString(1),Quantity=q,UnitPrice=price,LineTotal=q*price});
                }
                orders.Add(new{row.Id,row.UserId,TotalAmount=row.Total,row.Status,row.OrderedAt,Items=items});
            }
            return Ok(orders);
        }
        catch(Exception ex){ return StatusCode(500,$"Error fetching orders: {ex.Message}"); }
    }

    private async Task<int> GetOrCreateCartIdAsync(SqlConnection conn,int userId)
    {
        await using(var cmd=new SqlCommand("SELECT Id FROM ShoppingCarts WHERE UserId=@UserId",conn))
        {
            cmd.Parameters.AddWithValue("@UserId",userId);
            var val=await cmd.ExecuteScalarAsync();
            if(val is not null) return Convert.ToInt32(val);
        }
        await using(var cmd=new SqlCommand("INSERT INTO ShoppingCarts(UserId,UpdatedAt) OUTPUT INSERTED.Id VALUES(@UserId,SYSUTCDATETIME())",conn))
        {
            cmd.Parameters.AddWithValue("@UserId",userId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
    }

    private static async Task TouchCartAsync(SqlConnection conn,int cartId)
    {
        await using var cmd=new SqlCommand("UPDATE ShoppingCarts SET UpdatedAt=SYSUTCDATETIME() WHERE Id=@Id",conn);
        cmd.Parameters.AddWithValue("@Id",cartId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<object> BuildCartAsync(SqlConnection conn,int userId,bool create)
    {
        int cartId=0;
        if(create) cartId=await GetOrCreateCartIdAsync(conn,userId);
        else
        {
            await using var c=new SqlCommand("SELECT Id FROM ShoppingCarts WHERE UserId=@UserId",conn);
            c.Parameters.AddWithValue("@UserId",userId);
            var v=await c.ExecuteScalarAsync();
            if(v is not null) cartId=Convert.ToInt32(v);
        }

        var items=new List<object>(); decimal total=0;
        if(cartId>0)
        {
            await using var cmd=new SqlCommand(@"
                SELECT ci.Id,ci.ProductId,ci.Quantity,p.Name,p.Price,p.ImageUrl
                FROM CartItems ci JOIN ShopProducts p ON p.Id=ci.ProductId
                WHERE ci.CartId=@CartId ORDER BY ci.Id;",conn);
            cmd.Parameters.AddWithValue("@CartId",cartId);
            await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())
            {
                int q=r.GetInt32(2); decimal price=r.GetDecimal(4); decimal line=q*price; total+=line;
                items.Add(new{Id=r.GetInt32(0),ProductId=r.GetInt32(1),ProductName=r.GetString(3),UnitPrice=price,Quantity=q,LineTotal=line,ImageUrl=r.IsDBNull(5)?"":r.GetString(5)});
            }
        }
        return new{CartId=cartId,UserId=userId,Items=items,TotalAmount=total};
    }
}

public class AddToCartRequest
{
    public int UserId{get;set;}
    public int ProductId{get;set;}
    public int Quantity{get;set;}
}
public class UpdateCartItemRequest
{
    public int Quantity{get;set;}
}
