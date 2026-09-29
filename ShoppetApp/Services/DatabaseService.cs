using MySqlConnector;
using ShoppetApp.Models;
using SQLite;
using AppContact = ShoppetApp.Models.Contact;

namespace ShoppetApp.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;

        private readonly string _dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "shoppet.db"
        );

        private readonly string _mysqlConnectionString = "Server=localhost;Database=shoppetdb;Uid=root;Pwd=;";

        public User? CurrentUser { get; set; }
        public ApiService? ApiService { get; set; }

        public DatabaseService()
        {
            _ = InitializeTablesAsync();
        }

        public DatabaseService(string dbPath)
        {
            if (!string.IsNullOrEmpty(dbPath))
            {
                _dbPath = dbPath;
            }
            _ = InitializeTablesAsync();
        }

        private SQLiteAsyncConnection Database
        {
            get
            {
                if (_database == null)
                {
                    _database = new SQLiteAsyncConnection(_dbPath);
                }
                return _database;
            }
        }

        private async Task InitializeTablesAsync()
        {
            try
            {
                await Database.CreateTableAsync<User>();
                await Database.CreateTableAsync<Pet>();
                await Database.CreateTableAsync<HealthLog>();
                await Database.CreateTableAsync<FoodLog>();
                await Database.CreateTableAsync<CartItem>();
                await Database.CreateTableAsync<AppContact>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Table Init Error: {ex.Message}");
            }
        }

        // --- Users & Authentication ---
        public async Task<List<User>> GetUsersAsync()
        {
            await Database.CreateTableAsync<User>();
            return await Database.Table<User>().ToListAsync();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            await Database.CreateTableAsync<User>();
            return await Database.Table<User>().Where(u => u.Email == email).FirstOrDefaultAsync();
        }

        public async Task<int> SaveUserAsync(User user)
        {
            await Database.CreateTableAsync<User>();
            return await Database.InsertAsync(user);
        }

        public void Login(User user) => CurrentUser = user;
        public void Logout() => CurrentUser = null;

        // =====================================================
        // --- Community Posts (MySQL Real-time) ---
        // =====================================================

        /// <summary>
        /// Loads all posts with like/comment counts and whether the viewer liked each one.
        /// </summary>
        public async Task<List<CommunityPost>> GetCommunityPostsAsync(int viewerUserId = 0)
        {
            var posts = new List<CommunityPost>();
            try
            {
                using var connection = new MySqlConnection(_mysqlConnectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT 
                        p.Id, p.AuthorName, p.PetName, p.Content, p.ImageUrl,
                        p.Timestamp, p.IsEdited, p.UserId, p.PetId,
                        (SELECT COUNT(*) FROM communitylikes l WHERE l.PostId = p.Id) AS LikeCount,
                        (SELECT COUNT(*) FROM communitycomments c WHERE c.PostId = p.Id) AS CommentCount,
                        (SELECT COUNT(*) FROM communitylikes l2 
                         WHERE l2.PostId = p.Id AND l2.UserId = @ViewerUserId) AS IsLikedByMe
                    FROM communityposts p
                    ORDER BY p.Timestamp DESC;";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@ViewerUserId", viewerUserId);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    posts.Add(new CommunityPost
                    {
                        Id = reader.GetInt32("Id"),
                        AuthorName = reader.GetString("AuthorName"),
                        PetName = reader.GetString("PetName"),
                        Content = reader.GetString("Content"),
                        ImageUrls = reader.IsDBNull(reader.GetOrdinal("ImageUrl"))
                            ? null : reader.GetString("ImageUrl"),
                        Timestamp = reader.GetDateTime("Timestamp"),
                        IsEdited = reader.GetBoolean("IsEdited"),
                        UserId = reader.GetInt32("UserId"),
                        PetId = reader.IsDBNull(reader.GetOrdinal("PetId"))
                            ? null : reader.GetInt32("PetId"),
                        LikeCount = reader.GetInt32("LikeCount"),
                        CommentCount = reader.GetInt32("CommentCount"),
                        IsLikedByMe = reader.GetInt32("IsLikedByMe") > 0
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error (GetPosts): {ex.Message}");
            }
            return posts;
        }

        public async Task<int> SaveCommunityPostAsync(CommunityPost post)
        {
            try
            {
                using var connection = new MySqlConnection(_mysqlConnectionString);
                await connection.OpenAsync();

                string query = @"
                    INSERT INTO communityposts 
                        (AuthorName, PetName, Content, ImageUrl, Timestamp, IsEdited, UserId, PetId) 
                    VALUES 
                        (@AuthorName, @PetName, @Content, @ImageUrl, @Timestamp, @IsEdited, @UserId, @PetId);
                    SELECT LAST_INSERT_ID();";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@AuthorName", post.AuthorName);
                command.Parameters.AddWithValue("@PetName", post.PetName);
                command.Parameters.AddWithValue("@Content", post.Content);
                command.Parameters.AddWithValue("@ImageUrl", (object?)post.ImageUrls ?? DBNull.Value);
                command.Parameters.AddWithValue("@Timestamp", post.Timestamp);
                command.Parameters.AddWithValue("@IsEdited", post.IsEdited);
                command.Parameters.AddWithValue("@UserId", post.UserId);
                command.Parameters.AddWithValue("@PetId", (object?)post.PetId ?? DBNull.Value);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error (SavePost): {ex.Message}");
                return 0;
            }
        }

        public async Task<bool> UpdateCommunityPostAsync(CommunityPost post)
        {
            try
            {
                using var connection = new MySqlConnection(_mysqlConnectionString);
                await connection.OpenAsync();

                string query = @"
                    UPDATE communityposts 
                    SET Content = @Content, 
                        ImageUrl = @ImageUrl, 
                        PetName = @PetName,
                        IsEdited = @IsEdited 
                    WHERE Id = @Id;";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@Content", post.Content);
                command.Parameters.AddWithValue("@ImageUrl", (object?)post.ImageUrls ?? DBNull.Value);
                command.Parameters.AddWithValue("@PetName", post.PetName);
                command.Parameters.AddWithValue("@IsEdited", post.IsEdited);
                command.Parameters.AddWithValue("@Id", post.Id);

                int rows = await command.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error (UpdatePost): {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteCommunityPostAsync(CommunityPost post)
        {
            try
            {
                using var connection = new MySqlConnection(_mysqlConnectionString);
                await connection.OpenAsync();

                // Delete child rows first
                using (var delLikes = new MySqlCommand(
                    "DELETE FROM communitylikes WHERE PostId = @Id;", connection))
                {
                    delLikes.Parameters.AddWithValue("@Id", post.Id);
                    await delLikes.ExecuteNonQueryAsync();
                }

                using (var delComments = new MySqlCommand(
                    "DELETE FROM communitycomments WHERE PostId = @Id;", connection))
                {
                    delComments.Parameters.AddWithValue("@Id", post.Id);
                    await delComments.ExecuteNonQueryAsync();
                }

                using (var delPost = new MySqlCommand(
                    "DELETE FROM communityposts WHERE Id = @Id;", connection))
                {
                    delPost.Parameters.AddWithValue("@Id", post.Id);
                    var rows = await delPost.ExecuteNonQueryAsync();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error (DeletePost): {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Toggles a like on a post for a user. Returns the new total like count.
        /// </summary>
        public async Task<int> ToggleLikeAsync(int postId, int userId)
        {
            try
            {
                using var connection = new MySqlConnection(_mysqlConnectionString);
                await connection.OpenAsync();

                using (var check = new MySqlCommand(
                    "SELECT COUNT(*) FROM communitylikes WHERE PostId = @PostId AND UserId = @UserId;",
                    connection))
                {
                    check.Parameters.AddWithValue("@PostId", postId);
                    check.Parameters.AddWithValue("@UserId", userId);

                    var existing = Convert.ToInt32(await check.ExecuteScalarAsync());

                    if (existing > 0)
                    {
                        using var del = new MySqlCommand(
                            "DELETE FROM communitylikes WHERE PostId = @PostId AND UserId = @UserId;",
                            connection);
                        del.Parameters.AddWithValue("@PostId", postId);
                        del.Parameters.AddWithValue("@UserId", userId);
                        await del.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        using var ins = new MySqlCommand(
                            "INSERT INTO communitylikes (PostId, UserId, CreatedAt) VALUES (@PostId, @UserId, @CreatedAt);",
                            connection);
                        ins.Parameters.AddWithValue("@PostId", postId);
                        ins.Parameters.AddWithValue("@UserId", userId);
                        ins.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        await ins.ExecuteNonQueryAsync();
                    }

                    using var countCmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM communitylikes WHERE PostId = @PostId;",
                        connection);
                    countCmd.Parameters.AddWithValue("@PostId", postId);
                    return Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error (ToggleLike): {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Loads all comments for a post, joined with the user's name and role.
        /// </summary>
        public async Task<List<CommunityComment>> GetCommentsAsync(int postId)
        {
            var comments = new List<CommunityComment>();
            try
            {
                using var connection = new MySqlConnection(_mysqlConnectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT 
                        c.Id, c.PostId, c.UserId, c.ParentCommentId, c.Content, c.CreatedAt,
                        u.FullName AS AuthorName, u.Role AS AuthorRole
                    FROM communitycomments c
                    LEFT JOIN users u ON u.Id = c.UserId
                    WHERE c.PostId = @PostId
                    ORDER BY c.CreatedAt ASC;";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@PostId", postId);

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    comments.Add(new CommunityComment
                    {
                        Id = reader.GetInt32("Id"),
                        PostId = reader.GetInt32("PostId"),
                        UserId = reader.GetInt32("UserId"),
                        ParentCommentId = reader.IsDBNull(reader.GetOrdinal("ParentCommentId"))
                            ? null : reader.GetInt32("ParentCommentId"),
                        Content = reader.GetString("Content"),
                        CreatedAt = reader.GetDateTime("CreatedAt"),
                        AuthorName = reader.IsDBNull(reader.GetOrdinal("AuthorName"))
                            ? "Unknown" : reader.GetString("AuthorName"),
                        AuthorRole = reader.IsDBNull(reader.GetOrdinal("AuthorRole"))
                            ? "PetOwner" : reader.GetString("AuthorRole")
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error (GetComments): {ex.Message}");
            }
            return comments;
        }

        /// <summary>
        /// Adds a new comment and returns the new comment ID (0 on failure).
        /// </summary>
        public async Task<int> AddCommentAsync(CommunityComment comment)
        {
            try
            {
                using var connection = new MySqlConnection(_mysqlConnectionString);
                await connection.OpenAsync();

                string query = @"
                    INSERT INTO communitycomments (PostId, UserId, ParentCommentId, Content, CreatedAt)
                    VALUES (@PostId, @UserId, @ParentCommentId, @Content, @CreatedAt);
                    SELECT LAST_INSERT_ID();";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@PostId", comment.PostId);
                command.Parameters.AddWithValue("@UserId", comment.UserId);
                command.Parameters.AddWithValue("@ParentCommentId", (object?)comment.ParentCommentId ?? DBNull.Value);
                command.Parameters.AddWithValue("@Content", comment.Content);
                command.Parameters.AddWithValue("@CreatedAt", comment.CreatedAt);

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database Error (AddComment): {ex.Message}");
                return 0;
            }
        }

        // =====================================================
        // --- Pets Management ---
        // =====================================================
        public async Task<List<Pet>> GetPetsAsync()
        {
            try
            {
                if (ApiService != null)
                {
                    try {
                        var apiPets = await ApiService.GetPetsAsync();
                        if (apiPets != null) {
                            await Database.CreateTableAsync<Pet>();
                            await Database.ExecuteAsync("DELETE FROM Pet WHERE Id > 0"); // Clear synced records to prevent lingering duplicates
                            foreach(var p in apiPets) {
                                await Database.InsertAsync(p);
                            }
                        }
                    } catch { }
                }

                await Database.CreateTableAsync<Pet>();
                int currentUserId = Preferences.Get("LoggedInUserId", 0);
                var pets = await Database.Table<Pet>().Where(p => p.UserId == currentUserId).ToListAsync();
                return pets ?? new List<Pet>();
            }
            catch { return new List<Pet>(); }
        }

        public async Task<Pet?> GetPetAsync(int id)
        {
            await Database.CreateTableAsync<Pet>();
            return await Database.Table<Pet>().Where(p => p.Id == id).FirstOrDefaultAsync();
        }

        public async Task<int> SavePetAsync(Pet pet)
        {
            await Database.CreateTableAsync<Pet>();
            bool isNew = pet.Id <= 0;
            if (isNew && pet.Id == 0) {
                try {
                    int minId = await Database.ExecuteScalarAsync<int>("SELECT MIN(Id) FROM Pet");
                    pet.Id = minId >= 0 ? -1 : minId - 1;
                } catch { pet.Id = -1; }
            }
            
            if (ApiService != null)
            {
                try
                {
                    var apiSaved = await ApiService.SavePetAsync(pet);
                    if (apiSaved == null)
                        return 0;

                    var oldId = pet.Id;
                    pet.Id = apiSaved.Id;
                    if (oldId < 0)
                    {
                        await Database.ExecuteAsync("DELETE FROM Pet WHERE Id = ?", oldId);
                    }

                    var existing = await Database.Table<Pet>().Where(x => x.Id == pet.Id).FirstOrDefaultAsync();
                    return existing == null ? await Database.InsertAsync(pet) : await Database.UpdateAsync(pet);
                }
                catch
                {
                    return 0;
                }
            }

            return isNew ? await Database.InsertAsync(pet) : await Database.UpdateAsync(pet);
        }

        public async Task<int> DeletePetAsync(Pet pet)
        {
            await Database.CreateTableAsync<Pet>();
            int result = await Database.DeleteAsync(pet);
            if (ApiService != null)
            {
                try { await ApiService.DeletePetAsync(pet.Id); } catch { }
            }
            return result;
        }

        // --- Health & Food Logs ---
        public async Task<List<HealthLog>> GetHealthLogsAsync()
        {
            try
            {
                await Database.CreateTableAsync<HealthLog>();
                var logs = await Database.Table<HealthLog>().ToListAsync();
                return logs ?? new List<HealthLog>();
            }
            catch { return new List<HealthLog>(); }
        }

        public async Task<List<HealthLog>> GetHealthLogsAsync(int petId)
        {
            try
            {
                if (ApiService != null)
                {
                    try {
                        var apiLogs = await ApiService.GetHealthLogsAsync(petId);
                        if (apiLogs != null) {
                            await Database.CreateTableAsync<HealthLog>();
                            await Database.ExecuteAsync("DELETE FROM HealthLog WHERE Id > 0 AND PetId = ?", petId);
                            foreach(var log in apiLogs) {
                                await Database.InsertAsync(log);
                            }
                        }
                    } catch { }
                }

                await Database.CreateTableAsync<HealthLog>();
                return await Database.Table<HealthLog>().Where(h => h.PetId == petId).ToListAsync();
            }
            catch { return new List<HealthLog>(); }
        }

        public async Task<HealthLog?> GetHealthLogAsync(int id)
        {
            await Database.CreateTableAsync<HealthLog>();
            return await Database.Table<HealthLog>().Where(h => h.Id == id).FirstOrDefaultAsync();
        }

        public async Task<int> SaveHealthLogAsync(HealthLog log)
        {
            await Database.CreateTableAsync<HealthLog>();
            bool isNew = log.Id <= 0;
            if (isNew && log.Id == 0) {
                try {
                    int minId = await Database.ExecuteScalarAsync<int>("SELECT MIN(Id) FROM HealthLog");
                    log.Id = minId >= 0 ? -1 : minId - 1;
                } catch { log.Id = -1; }
            }

            if (ApiService != null)
            {
                try {
                    var apiSaved = await ApiService.SaveHealthLogAsync(log.PetId, log);
                    if (apiSaved != null) {
                        var oldId = log.Id;
                        log.Id = apiSaved.Id;
                        if (oldId < 0) {
                            await Database.ExecuteAsync("DELETE FROM HealthLog WHERE Id = ?", oldId);
                        }
                        var existing = await Database.Table<HealthLog>().Where(x => x.Id == log.Id).FirstOrDefaultAsync();
                        return existing == null ? await Database.InsertAsync(log) : await Database.UpdateAsync(log);
                    }
                } catch { }
            }

            return isNew ? await Database.InsertAsync(log) : await Database.UpdateAsync(log);
        }

        public async Task<int> DeleteHealthLogAsync(HealthLog log)
        {
            await Database.CreateTableAsync<HealthLog>();
            int result = await Database.DeleteAsync(log);
            if (ApiService != null)
            {
                try { await ApiService.DeleteHealthLogAsync(log.PetId, log.Id); } catch { }
            }
            return result;
        }

        public async Task<List<HealthLog>> GetAllActionRequiredLogsAsync()
        {
            try
            {
                await Database.CreateTableAsync<HealthLog>();
                await Database.CreateTableAsync<Pet>();
                int currentUserId = Preferences.Get("LoggedInUserId", 0);
                
                var userPets = await Database.Table<Pet>().Where(p => p.UserId == currentUserId).ToListAsync();
                var userPetIds = userPets.Select(p => p.Id).ToList();

                var allLogs = await Database.Table<HealthLog>().ToListAsync();
                var logs = allLogs
                    .Where(h => (h.Status == "Action Required" || h.Status == "Pending") && userPetIds.Contains(h.PetId))
                    .ToList();
                    
                return logs ?? new List<HealthLog>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAllActionRequiredLogsAsync: {ex.Message}");
                return new List<HealthLog>();
            }
        }

        public async Task<List<FoodLog>> GetFoodLogsAsync()
        {
            try
            {
                await Database.CreateTableAsync<FoodLog>();
                var logs = await Database.Table<FoodLog>().ToListAsync();
                return logs ?? new List<FoodLog>();
            }
            catch { return new List<FoodLog>(); }
        }

        public async Task<List<FoodLog>> GetFoodLogsAsync(int petId)
        {
            try
            {
                if (ApiService != null)
                {
                    try {
                        var apiLogs = await ApiService.GetFoodLogsAsync(petId);
                        if (apiLogs != null) {
                            await Database.CreateTableAsync<FoodLog>();
                            await Database.ExecuteAsync("DELETE FROM FoodLog WHERE Id > 0 AND PetId = ?", petId);
                            foreach(var log in apiLogs) {
                                await Database.InsertAsync(log);
                            }
                        }
                    } catch { }
                }

                await Database.CreateTableAsync<FoodLog>();
                return await Database.Table<FoodLog>().Where(f => f.PetId == petId).ToListAsync();
            }
            catch { return new List<FoodLog>(); }
        }

        public async Task<FoodLog?> GetFoodLogAsync(int id)
        {
            await Database.CreateTableAsync<FoodLog>();
            return await Database.Table<FoodLog>().Where(f => f.Id == id).FirstOrDefaultAsync();
        }

        public async Task<int> SaveFoodLogAsync(FoodLog log)
        {
            await Database.CreateTableAsync<FoodLog>();
            bool isNew = log.Id <= 0;
            if (isNew && log.Id == 0) {
                try {
                    int minId = await Database.ExecuteScalarAsync<int>("SELECT MIN(Id) FROM FoodLog");
                    log.Id = minId >= 0 ? -1 : minId - 1;
                } catch { log.Id = -1; }
            }

            if (ApiService != null)
            {
                try {
                    var apiSaved = await ApiService.SaveFoodLogAsync(log.PetId, log);
                    if (apiSaved != null) {
                        var oldId = log.Id;
                        log.Id = apiSaved.Id;
                        if (oldId < 0) {
                            await Database.ExecuteAsync("DELETE FROM FoodLog WHERE Id = ?", oldId);
                        }
                        var existing = await Database.Table<FoodLog>().Where(x => x.Id == log.Id).FirstOrDefaultAsync();
                        return existing == null ? await Database.InsertAsync(log) : await Database.UpdateAsync(log);
                    }
                } catch { }
            }

            return isNew ? await Database.InsertAsync(log) : await Database.UpdateAsync(log);
        }

        public async Task<int> DeleteFoodLogAsync(FoodLog log)
        {
            await Database.CreateTableAsync<FoodLog>();
            int result = await Database.DeleteAsync(log);
            if (ApiService != null)
            {
                try { await ApiService.DeleteFoodLogAsync(log.PetId, log.Id); } catch { }
            }
            return result;
        }

        public async Task MarkFoodDoneAsync(FoodLog log)
        {
            await Database.CreateTableAsync<FoodLog>();
            log.IsCompleted = true;
            await Database.UpdateAsync(log);
            
            if (ApiService != null)
            {
                try { await ApiService.MarkFoodDoneAsync(log.PetId, log.Id); } catch { }
            }
        }

        // --- Products & E-Commerce Cart ---
        public Task<List<Product>> GetProductsAsync() => Task.FromResult(new List<Product>
        {
            new Product { Id = 1, Name = "Royal Canin Mini Adult", Category = "Food", Price = 950.00m, StockQuantity = 15, Description = "Balanced nutrition for small adult dogs." },
            new Product { Id = 2, Name = "NexGard Spectra (10-25kg)", Category = "Pharmacy", Price = 650.00m, StockQuantity = 20, Description = "Flea, tick, and heartworm protection." }
        });

        public Task<List<string>> GetCategoriesAsync() => Task.FromResult(new List<string> { "All", "Food", "Pharmacy", "Accessories", "Healthcare" });

        public async Task<List<CartItem>> GetCartAsync()
        {
            try
            {
                await Database.CreateTableAsync<CartItem>();
                int currentUserId = Preferences.Get("LoggedInUserId", 0);
                var items = await Database.Table<CartItem>().Where(c => c.UserId == currentUserId).ToListAsync();
                return items ?? new List<CartItem>();
            }
            catch { return new List<CartItem>(); }
        }

        public async Task<int> AddToCartAsync(CartItem item)
        {
            await Database.CreateTableAsync<CartItem>();
            item.UserId = Preferences.Get("LoggedInUserId", 0);
            return await Database.InsertAsync(item);
        }

        public async Task<int> AddToCartAsync(int productId, int quantity)
        {
            await Database.CreateTableAsync<CartItem>();
            int currentUserId = Preferences.Get("LoggedInUserId", 0);
            var existing = await Database.Table<CartItem>().Where(c => c.ProductId == productId && c.UserId == currentUserId).FirstOrDefaultAsync();
            if (existing != null)
            {
                existing.Quantity += quantity;
                return await Database.UpdateAsync(existing);
            }
            return await Database.InsertAsync(new CartItem { ProductId = productId, Quantity = quantity, UserId = currentUserId });
        }

        public async Task<int> RemoveFromCartAsync(CartItem item)
        {
            await Database.CreateTableAsync<CartItem>();
            return await Database.DeleteAsync(item);
        }

        public async Task<int> RemoveFromCartAsync(int cartItemId)
        {
            await Database.CreateTableAsync<CartItem>();
            var item = await Database.Table<CartItem>().Where(c => c.Id == cartItemId).FirstOrDefaultAsync();
            return item != null ? await Database.DeleteAsync(item) : 0;
        }

        public async Task<int> UpdateCartItemAsync(CartItem item)
        {
            await Database.CreateTableAsync<CartItem>();
            return await Database.UpdateAsync(item);
        }

        public async Task<int> UpdateCartItemAsync(int cartItemId, int quantity)
        {
            await Database.CreateTableAsync<CartItem>();
            var item = await Database.Table<CartItem>().Where(c => c.Id == cartItemId).FirstOrDefaultAsync();
            if (item != null)
            {
                item.Quantity = quantity;
                return await Database.UpdateAsync(item);
            }
            return 0;
        }

        public async Task<int> ClearCartAsync()
        {
            await Database.CreateTableAsync<CartItem>();
            int currentUserId = Preferences.Get("LoggedInUserId", 0);
            var items = await Database.Table<CartItem>().Where(c => c.UserId == currentUserId).ToListAsync();
            int count = 0;
            foreach(var item in items) {
                count += await Database.DeleteAsync(item);
            }
            return count;
        }

        public async Task<bool> CheckoutAsync()
        {
            await Database.CreateTableAsync<CartItem>();
            int currentUserId = Preferences.Get("LoggedInUserId", 0);
            var items = await Database.Table<CartItem>().Where(c => c.UserId == currentUserId).ToListAsync();
            foreach(var item in items) {
                await Database.DeleteAsync(item);
            }
            return true;
        }

        // --- Contacts ---
        public async Task<List<AppContact>> GetContactsAsync()
        {
            try
            {
                if (ApiService != null)
                {
                    try {
                        var apiContacts = await ApiService.GetContactsAsync();
                        if (apiContacts != null) {
                            await Database.CreateTableAsync<AppContact>();
                            await Database.ExecuteAsync("DELETE FROM Contact WHERE Id > 0");
                            foreach(var c in apiContacts) {
                                await Database.InsertAsync(c);
                            }
                        }
                    } catch { }
                }

                await Database.CreateTableAsync<AppContact>();
                int currentUserId = Preferences.Get("LoggedInUserId", 0);
                var contacts = await Database.Table<AppContact>().Where(c => c.UserId == currentUserId).ToListAsync();
                return contacts ?? new List<AppContact>();
            }
            catch { return new List<AppContact>(); }
        }

        public async Task<AppContact?> GetContactAsync(int id)
        {
            await Database.CreateTableAsync<AppContact>();
            return await Database.Table<AppContact>().Where(c => c.Id == id).FirstOrDefaultAsync();
        }

        public async Task<int> SaveContactAsync(AppContact contact)
        {
            await Database.CreateTableAsync<AppContact>();
            bool isNew = contact.Id <= 0;
            if (isNew && contact.Id == 0) {
                try {
                    int minId = await Database.ExecuteScalarAsync<int>("SELECT MIN(Id) FROM Contact");
                    contact.Id = minId >= 0 ? -1 : minId - 1;
                } catch { contact.Id = -1; }
            }
            
            if (ApiService != null)
            {
                try
                {
                    var apiSaved = await ApiService.SaveContactAsync(contact);
                    if (apiSaved == null)
                        return 0;

                    var oldId = contact.Id;
                    contact.Id = apiSaved.Id;
                    if (oldId < 0)
                    {
                        await Database.ExecuteAsync("DELETE FROM Contact WHERE Id = ?", oldId);
                    }

                    var existing = await Database.Table<AppContact>().Where(x => x.Id == contact.Id).FirstOrDefaultAsync();
                    return existing == null ? await Database.InsertAsync(contact) : await Database.UpdateAsync(contact);
                }
                catch
                {
                    return 0;
                }
            }

            return isNew ? await Database.InsertAsync(contact) : await Database.UpdateAsync(contact);
        }

        public async Task<int> DeleteContactAsync(AppContact contact)
        {
            await Database.CreateTableAsync<AppContact>();
            int result = await Database.DeleteAsync(contact);
            if (ApiService != null)
            {
                try { await ApiService.DeleteContactAsync(contact.Id); } catch { }
            }
            return result;
        }
    }
}
