using ShoppetApp.Models;
using ShoppetApp.ViewModels;
using System.Net.Http.Json;

namespace ShoppetApp.Services;

public class UserSearchResult 
{ 
    public int UserId { get; set; } 
    public string FullName { get; set; } = string.Empty; 
    public string Email { get; set; } = string.Empty;
    public string ProfilePicture { get; set; } = string.Empty; 
}

public class ApiService
{
    // -- Change this URL to match wherever the API is running ------------------
    // For Android emulator use:   http://10.0.2.2:5020
    // For iOS simulator use:      http://localhost:5020
    // For Windows dev machine:    http://localhost:5020
#if ANDROID
    private const string BaseUrl = "http://10.0.2.2:5020/api";
#else
    private const string BaseUrl = "http://localhost:5020/api";
#endif

    private readonly HttpClient _http;
    private string? _token;
        public class UserProfileDto
        {
            public string FullName { get; set; } = string.Empty;
            public string ProfilePicture { get; set; } = string.Empty;
        }

        public async Task<UserProfileDto?> GetProfileAsync(int userId)
        {
            try
            {
                return await _http.GetFromJsonAsync<UserProfileDto>($"Profile/{userId}");
            }
            catch { return null; }
        }

        public async Task<bool> UpdateProfileAsync(int userId, string fullName, string profilePicBase64)
        {
            try
            {
                var req = new { UserId = userId, FullName = fullName, ProfilePictureBase64 = profilePicBase64 };
                var res = await _http.PutAsJsonAsync("Profile/update", req);
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }


    public ApiService()
    {
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl + "/") };
    }

    // -- Token management ------------------------------------------------------

    public void SetToken(string token)
    {
        _token = token;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearToken()
    {
        _token = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

    // -- Auth ------------------------------------------------------------------

    public async Task<ApiResult<AuthResponse>> RegisterAsync(string fullName, string email, string password)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("auth/register", new { fullName, email, password });
            if (res.IsSuccessStatusCode)
                return ApiResult<AuthResponse>.Ok(await res.Content.ReadFromJsonAsync<AuthResponse>()!);
            var err = await res.Content.ReadAsStringAsync();
            return ApiResult<AuthResponse>.Fail(res.StatusCode == System.Net.HttpStatusCode.Conflict
                ? "Email is already registered." : $"Registration failed: {err}");
        }
        catch (Exception ex)
        {
            return ApiResult<AuthResponse>.Fail($"Cannot reach server: {ex.Message}");
        }
    }

    public async Task<ApiResult<AuthResponse>> LoginAsync(string email, string password)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("auth/login", new { email, password });
            if (res.IsSuccessStatusCode)
                return ApiResult<AuthResponse>.Ok(await res.Content.ReadFromJsonAsync<AuthResponse>()!);
            return ApiResult<AuthResponse>.Fail("Invalid email or password.");
        }
        catch (Exception ex)
        {
            return ApiResult<AuthResponse>.Fail($"Cannot reach server: {ex.Message}");
        }
    }

    // -- Pets ------------------------------------------------------------------

    public async Task<List<Pet>> GetPetsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<Pet>>($"pets?userId={Microsoft.Maui.Storage.Preferences.Get("LoggedInUserId", 0)}") ?? []; }
        catch { return []; }
    }

    public async Task<Pet?> SavePetAsync(Pet pet)
    {
        try
        {
            var body = new
            {
                UserId = pet.UserId, // Ensures the owner's ID is sent to the backend
                pet.Name,
                pet.Species,
                pet.Breed,
                pet.AgeYears,
                pet.Weight,
                pet.PhotoUrl
            };

            HttpResponseMessage res;
            if (pet.Id <= 0)
                res = await _http.PostAsJsonAsync("pets", body);
            else
                res = await _http.PutAsJsonAsync($"pets/{pet.Id}", body);

            if (res.IsSuccessStatusCode)
            {
                if (pet.Id > 0) return pet;
                return await res.Content.ReadFromJsonAsync<Pet>();
            }
        }
        catch { }
        return null;
    }

    public async Task<bool> DeletePetAsync(int petId)
    {
        try { return (await _http.DeleteAsync($"pets/{petId}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    // -- Health Logs -----------------------------------------------------------

    public async Task<List<HealthLog>> GetHealthLogsAsync(int petId)
    {
        try { return await _http.GetFromJsonAsync<List<HealthLog>>($"pets/{petId}/healthlogs") ?? []; }
        catch { return []; }
    }

    public async Task<HealthLog?> SaveHealthLogAsync(int petId, HealthLog log)
    {
        try
        {
            var body = new
            {
                log.Type,
                log.Name,
                log.DueDate,
                log.Completed,
                log.DateAdministered,
                log.ValidityInterval,
                log.ValidityUnit,
                log.MedicationIntervalHours,
                log.TimeStarted,
                log.DosageTotal,
                log.DosageRemaining,
                log.CheckupDate,
                log.DocumentPaths
            };
            HttpResponseMessage res;
            if (log.Id == 0)
                res = await _http.PostAsJsonAsync($"pets/{petId}/healthlogs", body);
            else
                res = await _http.PutAsJsonAsync($"pets/{petId}/healthlogs/{log.Id}", body);

            if (res.IsSuccessStatusCode)
            {
                if (log.Id > 0) return log;
                return await res.Content.ReadFromJsonAsync<HealthLog>();
            }
        }
        catch { }
        return null;
    }
    public async Task<bool> CompleteFoodLogAsync(int petId, int id)
    {
        try { return (await _http.PutAsync($"pets/{petId}/foodlogs/{id}/complete", null)).IsSuccessStatusCode; }
        catch { return false; }
    }
    
    public async Task<bool> CompleteHealthLogAsync(int petId, int id, string nextDueDate)
    {
        try { 
            var body = new { NextDueDate = nextDueDate };
            return (await _http.PutAsJsonAsync($"pets/{petId}/healthlogs/{id}/complete", body)).IsSuccessStatusCode; 
        }
        catch { return false; }
    }

    public async Task<bool> DeleteHealthLogAsync(int petId, int logId)
    {
        try { return (await _http.DeleteAsync($"pets/{petId}/healthlogs/{logId}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    // -- Food Logs -------------------------------------------------------------

    public async Task<List<FoodLog>> GetFoodLogsAsync(int petId)
    {
        try { return await _http.GetFromJsonAsync<List<FoodLog>>($"pets/{petId}/foodlogs") ?? []; }
        catch { return []; }
    }

    public async Task<FoodLog?> SaveFoodLogAsync(int petId, FoodLog log)
    {
        try
        {
            var body = new
            {
                log.FoodName,
                log.AmountGrams,
                log.IntervalHours,
                log.IntervalMinutes,
                log.StartTimestamp,
                log.Notes
            };
            HttpResponseMessage res;
            if (log.Id == 0)
                res = await _http.PostAsJsonAsync($"pets/{petId}/foodlogs", body);
            else
                res = await _http.PutAsJsonAsync($"pets/{petId}/foodlogs/{log.Id}", body);

            if (res.IsSuccessStatusCode)
            {
                if (log.Id > 0) return log;
                return await res.Content.ReadFromJsonAsync<FoodLog>();
            }
        }
        catch { }
        return null;
    }

    public async Task<FoodLog?> MarkFoodDoneAsync(int petId, int logId)
    {
        try
        {
            var res = await _http.PostAsync($"pets/{petId}/foodlogs/{logId}/done", null);
            if (res.IsSuccessStatusCode)
                return await res.Content.ReadFromJsonAsync<FoodLog>();
        }
        catch { }
        return null;
    }

    public async Task<bool> DeleteFoodLogAsync(int petId, int logId)
    {
        try { return (await _http.DeleteAsync($"pets/{petId}/foodlogs/{logId}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    // -- Contacts --------------------------------------------------------------

    public async Task<List<Models.Contact>> GetContactsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<Models.Contact>>($"contacts?userId={Microsoft.Maui.Storage.Preferences.Get("LoggedInUserId", 0)}") ?? []; }
        catch { return []; }
    }

    public async Task<Models.Contact?> SaveContactAsync(Models.Contact contact)
    {
        try
        {
            var body = new { UserId = Microsoft.Maui.Storage.Preferences.Get("LoggedInUserId", 0), contact.Name, contact.Role, contact.Address, contact.Phone, contact.IsEmergency };
            HttpResponseMessage res;
            if (contact.Id <= 0)
                res = await _http.PostAsJsonAsync("contacts", body);
            else
                res = await _http.PutAsJsonAsync($"contacts/{contact.Id}", body);

            if (res.IsSuccessStatusCode)
            {
                if (contact.Id > 0) return contact;
                return await res.Content.ReadFromJsonAsync<Models.Contact>();
            }
        }
        catch { }
        return null;
    }

    public async Task<bool> DeleteContactAsync(int contactId)
    {
        try { return (await _http.DeleteAsync($"contacts/{contactId}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    // -- Shop ------------------------------------------------------------------

    public async Task<List<Product>> GetProductsAsync(string? species = null, string? category = null, string? search = null)
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrEmpty(species)) query.Add($"species={Uri.EscapeDataString(species)}");
            if (!string.IsNullOrEmpty(category)) query.Add($"category={Uri.EscapeDataString(category)}");
            if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";
            return await _http.GetFromJsonAsync<List<Product>>($"shop/products{qs}") ?? [];
        }
        catch { return []; }
    }

    public async Task<List<ShopCategory>> GetCategoriesAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ShopCategory>>("shop/categories") ?? []; }
        catch { return []; }
    }

    // -- Cart & Orders ---------------------------------------------------------

    public async Task<CartDto?> GetCartAsync()
    {
        try { return await _http.GetFromJsonAsync<CartDto>("cart"); }
        catch { return null; }
    }

    public async Task<CartDto?> AddToCartAsync(AddToCartRequest request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("cart/items", request);
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<CartDto>();
        }
        catch { }
        return null;
    }

    public async Task<CartDto?> UpdateCartItemAsync(int itemId, UpdateCartItemRequest request)
    {
        try
        {
            var res = await _http.PutAsJsonAsync($"cart/items/{itemId}", request);
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<CartDto>();
        }
        catch { }
        return null;
    }

    public async Task<CartDto?> RemoveFromCartAsync(int itemId)
    {
        try
        {
            var res = await _http.DeleteAsync($"cart/items/{itemId}");
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<CartDto>();
        }
        catch { }
        return null;
    }

    public async Task<CartDto?> ClearCartAsync()
    {
        try
        {
            var res = await _http.DeleteAsync("cart");
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<CartDto>();
        }
        catch { }
        return null;
    }

    public async Task<OrderDto?> CheckoutAsync()
    {
        try
        {
            var res = await _http.PostAsync("cart/checkout", null);
            if (res.IsSuccessStatusCode) return await res.Content.ReadFromJsonAsync<OrderDto>();
        }
        catch { }
        return null;
    }

    public async Task<List<OrderDto>> GetOrdersAsync()
    {
        try { return await _http.GetFromJsonAsync<List<OrderDto>>("cart/orders") ?? []; }
        catch { return []; }
    }

    // --- Community API ---

    public async Task<List<CommunityPost>> GetCommunityPostsAsync(int userId)
    {
        using var response = await _http.GetAsync($"community?userId={userId}");
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Community API returned {(int)response.StatusCode}: {detail}");
        }

        return await response.Content.ReadFromJsonAsync<List<CommunityPost>>()
               ?? new List<CommunityPost>();
    }

    public async Task<List<string>> UploadCommunityMediaAsync(IEnumerable<string> filePaths)
    {
        var paths = filePaths.Where(File.Exists).Take(5).ToList();
        if (paths.Count == 0) return new List<string>();

        using var form = new MultipartFormDataContent();
        var streams = new List<Stream>();

        try
        {
            foreach (var path in paths)
            {
                var stream = File.OpenRead(path);
                streams.Add(stream);

                var fileContent = new StreamContent(stream);
                var extension = Path.GetExtension(path).ToLowerInvariant();
                var mediaType = extension switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);

                form.Add(fileContent, "files", Path.GetFileName(path));
            }

            using var response = await _http.PostAsync("community/media", form);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Media upload failed ({(int)response.StatusCode}): {detail}");
            }

            return await response.Content.ReadFromJsonAsync<List<string>>()
                   ?? new List<string>();
        }
        finally
        {
            foreach (var stream in streams)
                stream.Dispose();
        }
    }

    public async Task<bool> CreateCommunityPostAsync(object request)
    {
        try
        {
            var res = await _http.PostAsJsonAsync("community", request);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating post: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ToggleLikeAsync(int postId, int userId)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"community/{postId}/like", new { UserId = userId });
            if (res.IsSuccessStatusCode)
            {
                var result = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                return result.GetProperty("isLiked").GetBoolean();
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling like: {ex.Message}");
            return false;
        }
    }

    public async Task<List<CommunityComment>> GetCommentsAsync(int postId)
    {
        try
        {
            int userId = Preferences.Get("LoggedInUserId", 0);
            return await _http.GetFromJsonAsync<List<CommunityComment>>($"community/{postId}/comments?userId={userId}") ?? new List<CommunityComment>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching comments: {ex.Message}");
            return new List<CommunityComment>();
        }
    }

    public async Task<bool> ToggleCommentLikeAsync(int commentId, int userId)
    {
        try
        {
            var res = await _http.PostAsJsonAsync($"community/comments/{commentId}/like", new { UserId = userId });
            if (res.IsSuccessStatusCode)
            {
                var result = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                return result.GetProperty("isLiked").GetBoolean();
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling comment like: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AddCommentAsync(int postId, int userId, string content, int? parentCommentId = null)
        {
            try
            {
                var res = await _http.PostAsJsonAsync($"community/{postId}/comments", new 
                { 
                    UserId = userId,
                    Content = content,
                    ParentCommentId = parentCommentId
                });
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> DeletePostAsync(int postId)
        {
            try { return (await _http.DeleteAsync($"community/{postId}")).IsSuccessStatusCode; }
            catch { return false; }
        }

        public async Task<bool> EditPostAsync(int postId, string newContent, string imageUrls, int? petId, string petName)
        {
            try { return (await _http.PutAsJsonAsync($"community/{postId}", new { Content = newContent, ImageUrls = imageUrls, PetId = petId, PetName = petName })).IsSuccessStatusCode; }
            catch { return false; }
        }

        // --- Marketplace API ---

    public async Task<List<MarketplaceListing>> GetMarketplaceListingsAsync(string? category = null, string? search = null)
    {
        try
        {
            var q = new List<string>();
            if (!string.IsNullOrEmpty(category)) q.Add($"category={Uri.EscapeDataString(category)}");
            if (!string.IsNullOrEmpty(search)) q.Add($"search={Uri.EscapeDataString(search)}");
            var qs = q.Count > 0 ? "?" + string.Join("&", q) : "";
            return await _http.GetFromJsonAsync<List<MarketplaceListing>>($"marketplace{qs}") ?? new List<MarketplaceListing>();
        }
        catch (Exception ex) { Console.WriteLine($"Marketplace fetch error: {ex.Message}"); return new List<MarketplaceListing>(); }
    }

    public async Task<List<MarketplaceListing>> GetMyListingsAsync(int userId)
    {
        try { return await _http.GetFromJsonAsync<List<MarketplaceListing>>($"marketplace/my/{userId}") ?? new List<MarketplaceListing>(); }
        catch (Exception ex) { Console.WriteLine($"My listings fetch error: {ex.Message}"); return new List<MarketplaceListing>(); }
    }

    public async Task<bool> CreateListingAsync(object request)
    {
        try { return (await _http.PostAsJsonAsync("marketplace", request)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> EditListingAsync(int id, object request)
    {
        try { return (await _http.PutAsJsonAsync($"marketplace/{id}", request)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> DeleteListingAsync(int id, int userId)
    {
        try { return (await _http.DeleteAsync($"marketplace/{id}?userId={userId}")).IsSuccessStatusCode; }
        catch { return false; }
    }
        public async Task<bool> DeleteCommentAsync(int commentId)
        {
            try { return (await _http.DeleteAsync($"community/comments/{commentId}")).IsSuccessStatusCode; }
            catch { return false; }
        }

        


        public async Task<List<UserSearchResult>> SearchUsersAsync(string query)
        {
            try
            {
                var userId = Preferences.Get("LoggedInUserId", 0);
                return await _http.GetFromJsonAsync<List<UserSearchResult>>($"messages/search?query={query}&currentUserId={userId}") ?? new List<UserSearchResult>();
            }
            catch { return new List<UserSearchResult>(); }
        }

        public async Task<IEnumerable<Conversation>> GetConversationsAsync()
        {
            try
            {
                var userId = Preferences.Get("LoggedInUserId", 0);
                return await _http.GetFromJsonAsync<IEnumerable<Conversation>>($"messages/{userId}");
            }
            catch { return new List<Conversation>(); }
        }

        public async Task<IEnumerable<ChatMessage>> GetMessagesAsync(int contactId)
        {
            try
            {
                var userId = Preferences.Get("LoggedInUserId", 0);
                var result = await _http.GetFromJsonAsync<IEnumerable<ChatMessage>>($"messages/chat/{userId}/{contactId}");
                foreach(var msg in result)
                {
                    msg.IsMine = msg.SenderId == userId;
                }
                return result;
            }
            catch { return new List<ChatMessage>(); }
        }

                public async Task<int> GetUnreadMessagesCountAsync(int userId)
        {
            try
            {
                var response = await _http.GetAsync($"messages/unreadCount/{userId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (int.TryParse(content, out int count))
                        return count;
                }
            }
            catch { }
            return 0;
        }

        public async Task ResetUnreadCountAsync(int userId, int contactId)
        {
            try
            {
                await _http.PostAsync($"messages/resetUnread/{userId}/{contactId}", null);
            }
            catch { }
        }

        public async Task<bool> SendMessageAsync(int receiverId, int? listingId, string text)
        {
            try
            {
                var userId = Preferences.Get("LoggedInUserId", 0);
                var response = await _http.PostAsJsonAsync("messages", new { SenderId = userId, ReceiverId = receiverId, ListingId = listingId, Text = text });
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }

// DTOs

public class AuthResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ProfilePicture { get; set; } = string.Empty;
    public string Role { get; set; } = "PetOwner";
    public string Token { get; set; } = string.Empty;
}

public class ApiResult<T>
{
    public bool Success { get; private set; }
    public T? Data { get; private set; }
    public string? Error { get; private set; }

    public static ApiResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResult<T> Fail(string error) => new() { Success = false, Error = error };
}




















