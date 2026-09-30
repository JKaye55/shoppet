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

        private User? _currentUser;
        public User? CurrentUser
        {
            get
            {
                if (_currentUser is null)
                {
                    int id = Preferences.Get("LoggedInUserId", 0);
                    if (id > 0)
                    {
                        _currentUser = new User
                        {
                            Id = id,
                            FullName = Preferences.Get("LoggedInUserName", string.Empty),
                            Email = Preferences.Get("LoggedInUserEmail", string.Empty),
                            Role = Preferences.Get("LoggedInUserRole", "PetOwner"),
                            ProfilePicture = Preferences.Get("LoggedInUserProfilePicture", string.Empty)
                        };
                    }
                }
                return _currentUser;
            }
            set => _currentUser = value;
        }

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

        // Community, marketplace, pets, health, feeding, contacts, and messaging
        // use ShoppetAPI + the shared SQL Server database. The mobile app no longer
        // opens a direct MySQL/XAMPP connection.

        // =====================================================
        // --- Pets Management ---
        // =====================================================
        public async Task<List<Pet>> GetPetsAsync()
        {
            // SQL Server through ShoppetAPI is the source of truth.
            // Do not clear or rebuild a local Pet table during reads: an old SQLite
            // schema can fail silently and make valid server pets appear to vanish.
            if (ApiService == null)
                return new List<Pet>();

            try
            {
                int currentUserId = Preferences.Get("LoggedInUserId", 0);
                if (currentUserId <= 0)
                    return new List<Pet>();

                var apiPets = await ApiService.GetPetsAsync();
                return apiPets
                    .Where(p => p.UserId == currentUserId)
                    .OrderBy(p => p.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API Error (GetPets): {ex.Message}");
                return new List<Pet>();
            }
        }

        public async Task<Pet?> GetPetAsync(int id)
        {
            if (id <= 0 || ApiService == null)
                return null;

            var pets = await GetPetsAsync();
            return pets.FirstOrDefault(p => p.Id == id);
        }

        public async Task<int> SavePetAsync(Pet pet)
        {
            if (ApiService == null || pet is null)
                return 0;

            int currentUserId = Preferences.Get("LoggedInUserId", 0);
            if (currentUserId <= 0)
                return 0;

            // Ownership always follows the authenticated local session.
            pet.UserId = currentUserId;

            try
            {
                var apiSaved = await ApiService.SavePetAsync(pet);
                if (apiSaved == null)
                    return 0;

                pet.Id = apiSaved.Id;
                pet.UserId = apiSaved.UserId;
                pet.Name = apiSaved.Name;
                pet.Species = apiSaved.Species;
                pet.Breed = apiSaved.Breed;
                pet.AgeYears = apiSaved.AgeYears;
                pet.Weight = apiSaved.Weight;
                pet.PhotoUrl = apiSaved.PhotoUrl;

                return pet.Id > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API Error (SavePet): {ex.Message}");
                return 0;
            }
        }

        public async Task<int> DeletePetAsync(Pet pet)
        {
            if (ApiService == null || pet is null || pet.Id <= 0)
                return 0;

            int currentUserId = Preferences.Get("LoggedInUserId", 0);
            if (currentUserId <= 0 || pet.UserId != currentUserId)
                return 0;

            try
            {
                return await ApiService.DeletePetAsync(pet.Id) ? 1 : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API Error (DeletePet): {ex.Message}");
                return 0;
            }
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
                try
                {
                    var apiSaved = await ApiService.SaveHealthLogAsync(log.PetId, log);
                    if (apiSaved == null)
                        return 0;

                    var oldId = log.Id;
                    if (isNew)
                        log.Id = apiSaved.Id;

                    if (oldId < 0)
                        await Database.ExecuteAsync("DELETE FROM HealthLog WHERE Id = ?", oldId);

                    var existing = await Database.Table<HealthLog>()
                        .Where(x => x.Id == log.Id)
                        .FirstOrDefaultAsync();

                    return existing == null
                        ? await Database.InsertAsync(log)
                        : await Database.UpdateAsync(log);
                }
                catch
                {
                    return 0;
                }
            }

            return isNew ? await Database.InsertAsync(log) : await Database.UpdateAsync(log);
        }

        public async Task<int> DeleteHealthLogAsync(HealthLog log)
        {
            await Database.CreateTableAsync<HealthLog>();

            if (ApiService != null)
            {
                try
                {
                    bool deleted = await ApiService.DeleteHealthLogAsync(log.PetId, log.Id);
                    if (!deleted)
                        return 0;
                }
                catch
                {
                    return 0;
                }
            }

            return await Database.DeleteAsync(log);
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
                try
                {
                    var apiSaved = await ApiService.SaveFoodLogAsync(log.PetId, log);
                    if (apiSaved == null)
                        return 0;

                    var oldId = log.Id;
                    if (isNew)
                        log.Id = apiSaved.Id;

                    if (oldId < 0)
                        await Database.ExecuteAsync("DELETE FROM FoodLog WHERE Id = ?", oldId);

                    var existing = await Database.Table<FoodLog>()
                        .Where(x => x.Id == log.Id)
                        .FirstOrDefaultAsync();

                    return existing == null
                        ? await Database.InsertAsync(log)
                        : await Database.UpdateAsync(log);
                }
                catch
                {
                    return 0;
                }
            }

            return isNew ? await Database.InsertAsync(log) : await Database.UpdateAsync(log);
        }

        public async Task<int> DeleteFoodLogAsync(FoodLog log)
        {
            await Database.CreateTableAsync<FoodLog>();

            if (ApiService != null)
            {
                try
                {
                    bool deleted = await ApiService.DeleteFoodLogAsync(log.PetId, log.Id);
                    if (!deleted)
                        return 0;
                }
                catch
                {
                    return 0;
                }
            }

            return await Database.DeleteAsync(log);
        }

        public async Task MarkFoodDoneAsync(FoodLog log)
        {
            await Database.CreateTableAsync<FoodLog>();

            if (ApiService != null)
            {
                try
                {
                    var updated = await ApiService.MarkFoodDoneAsync(log.PetId, log.Id);
                    if (updated == null)
                        return;

                    log.IsCompleted = updated.IsCompleted;
                    log.CompletedAt = updated.CompletedAt;
                    log.LastFedTimestamp = updated.LastFedTimestamp;
                }
                catch
                {
                    return;
                }
            }
            else
            {
                log.IsCompleted = true;
            }

            await Database.UpdateAsync(log);
        }

        // --- Products & E-Commerce Cart ---
        public async Task<List<Product>> GetProductsAsync()
        {
            if (ApiService != null)
            {
                try
                {
                    return await ApiService.GetProductsAsync();
                }
                catch { }
            }

            return new List<Product>();
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            if (ApiService != null)
            {
                try
                {
                    var categories = await ApiService.GetCategoriesAsync();
                    return new[] { "All" }
                        .Concat(categories.Select(x => x.Name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch { }
            }

            return new List<string> { "All" };
        }

        private async Task<List<CartItem>> SyncServerCartAsync(CartDto? cart)
        {
            await Database.CreateTableAsync<CartItem>();
            int userId = Preferences.Get("LoggedInUserId", 0);

            if (cart == null)
                return await Database.Table<CartItem>()
                    .Where(x => x.UserId == userId)
                    .ToListAsync();

            var current = await Database.Table<CartItem>()
                .Where(x => x.UserId == userId)
                .ToListAsync();

            foreach (var item in current)
                await Database.DeleteAsync(item);

            foreach (var item in cart.Items)
            {
                await Database.InsertAsync(new CartItem
                {
                    Id = item.Id,
                    UserId = userId,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    ImageUrl = item.ImageUrl ?? string.Empty,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity
                });
            }

            return await Database.Table<CartItem>()
                .Where(x => x.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<CartItem>> GetCartAsync()
        {
            try
            {
                if (ApiService != null)
                    return await SyncServerCartAsync(await ApiService.GetCartAsync());

                await Database.CreateTableAsync<CartItem>();
                int userId = Preferences.Get("LoggedInUserId", 0);
                return await Database.Table<CartItem>()
                    .Where(x => x.UserId == userId)
                    .ToListAsync();
            }
            catch
            {
                return new List<CartItem>();
            }
        }

        public async Task<int> AddToCartAsync(CartItem item)
        {
            return await AddToCartAsync(item.ProductId, item.Quantity);
        }

        public async Task<int> AddToCartAsync(int productId, int quantity)
        {
            if (ApiService != null)
            {
                var cart = await ApiService.AddToCartAsync(
                    new AddToCartRequest(productId, quantity));

                if (cart == null)
                    return 0;

                await SyncServerCartAsync(cart);
                return 1;
            }

            return 0;
        }

        public async Task<int> RemoveFromCartAsync(CartItem item)
        {
            return await RemoveFromCartAsync(item.Id);
        }

        public async Task<int> RemoveFromCartAsync(int cartItemId)
        {
            if (ApiService != null)
            {
                var cart = await ApiService.RemoveFromCartAsync(cartItemId);
                if (cart == null)
                    return 0;

                await SyncServerCartAsync(cart);
                return 1;
            }

            return 0;
        }

        public async Task<int> UpdateCartItemAsync(CartItem item)
        {
            return await UpdateCartItemAsync(item.Id, item.Quantity);
        }

        public async Task<int> UpdateCartItemAsync(int cartItemId, int quantity)
        {
            if (ApiService != null)
            {
                var cart = await ApiService.UpdateCartItemAsync(
                    cartItemId,
                    new UpdateCartItemRequest(quantity));

                if (cart == null)
                    return 0;

                await SyncServerCartAsync(cart);
                return 1;
            }

            return 0;
        }

        public async Task<int> ClearCartAsync()
        {
            if (ApiService != null)
            {
                var cart = await ApiService.ClearCartAsync();
                if (cart == null)
                    return 0;

                await SyncServerCartAsync(cart);
                return 1;
            }

            return 0;
        }

        public async Task<bool> CheckoutAsync()
        {
            if (ApiService == null)
                return false;

            var order = await ApiService.CheckoutAsync();
            if (order == null)
                return false;

            await Database.CreateTableAsync<CartItem>();
            int userId = Preferences.Get("LoggedInUserId", 0);
            var localItems = await Database.Table<CartItem>()
                .Where(x => x.UserId == userId)
                .ToListAsync();

            foreach (var item in localItems)
                await Database.DeleteAsync(item);

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

            if (ApiService != null)
            {
                try
                {
                    bool deleted = await ApiService.DeleteContactAsync(contact.Id);
                    if (!deleted)
                        return 0;
                }
                catch
                {
                    return 0;
                }
            }

            return await Database.DeleteAsync(contact);
        }
    }
}
