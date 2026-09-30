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
        // --- Community Posts (shared ShoppetAPI / SQL Server) ---
        // =====================================================
        public async Task<List<CommunityPost>> GetCommunityPostsAsync(int viewerUserId = 0)
        {
            if (ApiService is null) return new List<CommunityPost>();
            try { return await ApiService.GetCommunityPostsAsync(viewerUserId); }
            catch { return new List<CommunityPost>(); }
        }

        public async Task<int> SaveCommunityPostAsync(CommunityPost post)
        {
            if (ApiService is null) return 0;
            var ok = await ApiService.CreateCommunityPostAsync(new
            {
                post.UserId,
                post.PetId,
                post.AuthorName,
                post.PetName,
                post.Content,
                post.ImageUrls
            });
            return ok ? 1 : 0;
        }

        public async Task<bool> UpdateCommunityPostAsync(CommunityPost post)
        {
            if (ApiService is null) return false;
            return await ApiService.EditPostAsync(
                post.Id,
                post.Content,
                post.ImageUrls ?? string.Empty,
                post.PetId,
                post.PetName);
        }

        public async Task<bool> DeleteCommunityPostAsync(CommunityPost post)
        {
            if (ApiService is null) return false;
            return await ApiService.DeletePostAsync(post.Id);
        }

        public async Task<int> ToggleLikeAsync(int postId, int userId)
        {
            if (ApiService is null) return 0;
            await ApiService.ToggleLikeAsync(postId, userId);
            try
            {
                var post = (await ApiService.GetCommunityPostsAsync(userId))
                    .FirstOrDefault(p => p.Id == postId);
                return post?.LikesCount ?? 0;
            }
            catch { return 0; }
        }

        public async Task<List<CommunityComment>> GetCommentsAsync(int postId)
        {
            if (ApiService is null) return new List<CommunityComment>();
            return await ApiService.GetCommentsAsync(postId);
        }

        public async Task<int> AddCommentAsync(CommunityComment comment)
        {
            if (ApiService is null) return 0;
            var ok = await ApiService.AddCommentAsync(
                comment.PostId,
                comment.UserId,
                comment.Content,
                comment.ParentCommentId);
            return ok ? 1 : 0;
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
                try {
                    var apiSaved = await ApiService.SavePetAsync(pet);
                    if (apiSaved != null) {
                        var oldId = pet.Id;
                        pet.Id = apiSaved.Id;
                        if (oldId < 0) {
                            await Database.ExecuteAsync("DELETE FROM Pet WHERE Id = ?", oldId);
                        }
                        var existing = await Database.Table<Pet>().Where(x => x.Id == pet.Id).FirstOrDefaultAsync();
                        return existing == null ? await Database.InsertAsync(pet) : await Database.UpdateAsync(pet);
                    }
                } catch { }
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

                var pets = await GetPetsAsync();
                var result = new List<HealthLog>();

                foreach (var pet in pets)
                {
                    var petLogs = await GetHealthLogsAsync(pet.Id);
                    foreach (var log in petLogs.Where(h =>
                                 !h.Completed &&
                                 (h.Status == "Action Required" || h.Status == "Pending")))
                    {
                        log.PetName = pet.Name;
                        log.PetPhotoUrl = pet.PhotoUrl;
                        result.Add(log);
                    }
                }

                return result
                    .OrderBy(h => DateTime.TryParse(h.DueDate, out var due) ? due : DateTime.MaxValue)
                    .ToList();
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
        public async Task<List<Product>> GetProductsAsync()
        {
            if (ApiService is null) return new List<Product>();
            return await ApiService.GetProductsAsync();
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            if (ApiService is null) return new List<string> { "All" };
            var categories = await ApiService.GetCategoriesAsync();
            var names = categories.Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            names.Insert(0, "All");
            return names;
        }

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
                try {
                    var apiSaved = await ApiService.SaveContactAsync(contact);
                    if (apiSaved != null) {
                        var oldId = contact.Id;
                        contact.Id = apiSaved.Id;
                        if (oldId < 0) {
                            await Database.ExecuteAsync("DELETE FROM Contact WHERE Id = ?", oldId);
                        }
                        var existing = await Database.Table<AppContact>().Where(x => x.Id == contact.Id).FirstOrDefaultAsync();
                        return existing == null ? await Database.InsertAsync(contact) : await Database.UpdateAsync(contact);
                    }
                } catch { }
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
