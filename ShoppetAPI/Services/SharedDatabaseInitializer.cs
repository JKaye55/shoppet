using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Services;

public sealed class SharedDatabaseInitializer
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SharedDatabaseInitializer> _logger;

    public SharedDatabaseInitializer(
        IConfiguration configuration,
        ILogger<SharedDatabaseInitializer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("SharedSqlServer")
        ?? throw new InvalidOperationException(
            "Connection string 'SharedSqlServer' was not found.");

    public async Task InitializeAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var commands = new[]
        {
            // ---------------------------------------------------------
            // USER PROFILE / SOCIAL FIELDS
            // ---------------------------------------------------------
            @"IF COL_LENGTH('dbo.UserAccounts', 'ProfilePicture') IS NULL
                ALTER TABLE dbo.UserAccounts ADD ProfilePicture nvarchar(max) NULL;",

            @"IF COL_LENGTH('dbo.UserAccounts', 'FacebookUrl') IS NULL
                ALTER TABLE dbo.UserAccounts ADD FacebookUrl nvarchar(500) NULL;",

            @"IF COL_LENGTH('dbo.UserAccounts', 'InstagramUrl') IS NULL
                ALTER TABLE dbo.UserAccounts ADD InstagramUrl nvarchar(500) NULL;",

            @"IF COL_LENGTH('dbo.UserAccounts', 'OtherSocialUrl') IS NULL
                ALTER TABLE dbo.UserAccounts ADD OtherSocialUrl nvarchar(500) NULL;",

            @"IF COL_LENGTH('dbo.UserAccounts', 'ShowSocialLinksOnMarketplace') IS NULL
                ALTER TABLE dbo.UserAccounts
                ADD ShowSocialLinksOnMarketplace bit NOT NULL
                    CONSTRAINT DF_UserAccounts_ShowSocialLinksOnMarketplace DEFAULT(0);",

            // ---------------------------------------------------------
            // PET PROFILE MOBILE COMPATIBILITY
            // ---------------------------------------------------------
            @"IF COL_LENGTH('dbo.PetProfiles', 'PhotoUrl') IS NULL
                ALTER TABLE dbo.PetProfiles ADD PhotoUrl nvarchar(max) NULL;",

            // ---------------------------------------------------------
            // HEALTH RECORD MOBILE COMPATIBILITY
            // ---------------------------------------------------------
            @"IF COL_LENGTH('dbo.PetHealthRecords', 'Completed') IS NULL
                ALTER TABLE dbo.PetHealthRecords
                ADD Completed bit NOT NULL
                    CONSTRAINT DF_PetHealthRecords_Completed DEFAULT(0);",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'DateAdministeredText') IS NULL
                ALTER TABLE dbo.PetHealthRecords ADD DateAdministeredText nvarchar(100) NULL;",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'DueDateText') IS NULL
                ALTER TABLE dbo.PetHealthRecords ADD DueDateText nvarchar(100) NULL;",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'ValidityInterval') IS NULL
                ALTER TABLE dbo.PetHealthRecords
                ADD ValidityInterval int NOT NULL
                    CONSTRAINT DF_PetHealthRecords_ValidityInterval DEFAULT(0);",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'ValidityUnit') IS NULL
                ALTER TABLE dbo.PetHealthRecords ADD ValidityUnit nvarchar(20) NULL;",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'MedicationIntervalHours') IS NULL
                ALTER TABLE dbo.PetHealthRecords
                ADD MedicationIntervalHours float NOT NULL
                    CONSTRAINT DF_PetHealthRecords_MedicationIntervalHours DEFAULT(0);",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'TimeStarted') IS NULL
                ALTER TABLE dbo.PetHealthRecords ADD TimeStarted nvarchar(100) NULL;",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'DosageTotal') IS NULL
                ALTER TABLE dbo.PetHealthRecords
                ADD DosageTotal int NOT NULL
                    CONSTRAINT DF_PetHealthRecords_DosageTotal DEFAULT(0);",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'DosageRemaining') IS NULL
                ALTER TABLE dbo.PetHealthRecords
                ADD DosageRemaining int NOT NULL
                    CONSTRAINT DF_PetHealthRecords_DosageRemaining DEFAULT(0);",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'CheckupDateText') IS NULL
                ALTER TABLE dbo.PetHealthRecords ADD CheckupDateText nvarchar(100) NULL;",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'DocumentPaths') IS NULL
                ALTER TABLE dbo.PetHealthRecords ADD DocumentPaths nvarchar(max) NULL;",

            @"IF COL_LENGTH('dbo.PetHealthRecords', 'CompletedAt') IS NULL
                ALTER TABLE dbo.PetHealthRecords ADD CompletedAt datetime2 NULL;",

            // ---------------------------------------------------------
            // FOOD LOG MOBILE COMPATIBILITY
            // ---------------------------------------------------------
            @"IF COL_LENGTH('dbo.FoodLogs', 'AmountGrams') IS NULL
                ALTER TABLE dbo.FoodLogs
                ADD AmountGrams float NOT NULL
                    CONSTRAINT DF_FoodLogs_AmountGrams DEFAULT(0);",

            @"IF COL_LENGTH('dbo.FoodLogs', 'IntervalHours') IS NULL
                ALTER TABLE dbo.FoodLogs
                ADD IntervalHours int NOT NULL
                    CONSTRAINT DF_FoodLogs_IntervalHours DEFAULT(0);",

            @"IF COL_LENGTH('dbo.FoodLogs', 'IntervalMinutes') IS NULL
                ALTER TABLE dbo.FoodLogs
                ADD IntervalMinutes int NOT NULL
                    CONSTRAINT DF_FoodLogs_IntervalMinutes DEFAULT(0);",

            @"IF COL_LENGTH('dbo.FoodLogs', 'StartTimestamp') IS NULL
                ALTER TABLE dbo.FoodLogs ADD StartTimestamp nvarchar(100) NULL;",

            @"IF COL_LENGTH('dbo.FoodLogs', 'LastFedTimestamp') IS NULL
                ALTER TABLE dbo.FoodLogs ADD LastFedTimestamp nvarchar(100) NULL;",

            @"IF COL_LENGTH('dbo.FoodLogs', 'FedDate') IS NULL
                ALTER TABLE dbo.FoodLogs ADD FedDate nvarchar(100) NULL;",

            @"IF COL_LENGTH('dbo.FoodLogs', 'IsCompleted') IS NULL
                ALTER TABLE dbo.FoodLogs
                ADD IsCompleted bit NOT NULL
                    CONSTRAINT DF_FoodLogs_IsCompleted DEFAULT(0);",

            @"IF COL_LENGTH('dbo.FoodLogs', 'CompletedAt') IS NULL
                ALTER TABLE dbo.FoodLogs ADD CompletedAt datetime2 NULL;",

            // ---------------------------------------------------------
            // EMERGENCY CONTACTS
            // ---------------------------------------------------------
            @"IF OBJECT_ID('dbo.EmergencyContacts', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.EmergencyContacts
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId int NOT NULL,
                    Name nvarchar(200) NOT NULL,
                    Role nvarchar(100) NOT NULL,
                    Address nvarchar(max) NOT NULL,
                    Phone nvarchar(50) NOT NULL,
                    IsEmergency bit NOT NULL,
                    CONSTRAINT FK_EmergencyContacts_UserAccounts
                        FOREIGN KEY (UserId)
                        REFERENCES dbo.UserAccounts(Id)
                        ON DELETE CASCADE
                );
                CREATE INDEX IX_EmergencyContacts_UserId
                    ON dbo.EmergencyContacts(UserId);
              END;",

            // ---------------------------------------------------------
            // COMMUNITY REPLIES / COMMENT LIKES
            // ---------------------------------------------------------
            @"IF COL_LENGTH('dbo.CommunityComments', 'ParentCommentId') IS NULL
                ALTER TABLE dbo.CommunityComments ADD ParentCommentId int NULL;",

            @"IF OBJECT_ID('dbo.CommunityCommentLikes', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.CommunityCommentLikes
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CommentId int NOT NULL,
                    UserId int NOT NULL,
                    CreatedAt datetime2 NOT NULL
                        CONSTRAINT DF_CommunityCommentLikes_CreatedAt DEFAULT(SYSDATETIME()),
                    CONSTRAINT FK_CommunityCommentLikes_Comments
                        FOREIGN KEY (CommentId)
                        REFERENCES dbo.CommunityComments(Id),
                    CONSTRAINT FK_CommunityCommentLikes_Users
                        FOREIGN KEY (UserId)
                        REFERENCES dbo.UserAccounts(Id)
                );
                CREATE UNIQUE INDEX UX_CommunityCommentLikes_Comment_User
                    ON dbo.CommunityCommentLikes(CommentId, UserId);
              END;",

            // ---------------------------------------------------------
            // MARKETPLACE MOBILE COMPATIBILITY
            // ---------------------------------------------------------
            @"IF COL_LENGTH('dbo.MarketplaceListings', 'ImageUrls') IS NULL
                ALTER TABLE dbo.MarketplaceListings ADD ImageUrls nvarchar(max) NULL;",

            @"IF COL_LENGTH('dbo.MarketplaceListings', 'UpdatedAt') IS NULL
                ALTER TABLE dbo.MarketplaceListings ADD UpdatedAt datetime2 NULL;",

            // ---------------------------------------------------------
            // CONVERSATIONS / INTERNAL MESSAGING
            // ---------------------------------------------------------
            @"IF OBJECT_ID('dbo.Conversations', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.Conversations
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    User1Id int NOT NULL,
                    User2Id int NOT NULL,
                    MessagesJson nvarchar(max) NOT NULL,
                    LastUpdated datetime2 NOT NULL,
                    User1UnreadCount int NOT NULL
                        CONSTRAINT DF_Conversations_User1Unread DEFAULT(0),
                    User2UnreadCount int NOT NULL
                        CONSTRAINT DF_Conversations_User2Unread DEFAULT(0),
                    CONSTRAINT FK_Conversations_User1
                        FOREIGN KEY (User1Id) REFERENCES dbo.UserAccounts(Id),
                    CONSTRAINT FK_Conversations_User2
                        FOREIGN KEY (User2Id) REFERENCES dbo.UserAccounts(Id)
                );
                CREATE UNIQUE INDEX UX_Conversations_UserPair
                    ON dbo.Conversations(User1Id, User2Id);
              END;",

            // ---------------------------------------------------------
            // EXISTING SHOP/CART FEATURE - SQL SERVER COMPATIBILITY
            // Kept so evaluated cart/checkout functionality still works.
            // ---------------------------------------------------------
            @"IF OBJECT_ID('dbo.ShopCategories', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.ShopCategories
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Name nvarchar(100) NOT NULL,
                    Icon nvarchar(20) NOT NULL
                );
              END;",

            @"IF OBJECT_ID('dbo.PetCategories', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.PetCategories
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Name nvarchar(100) NOT NULL
                );
              END;",

            @"IF OBJECT_ID('dbo.ShopProducts', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.ShopProducts
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Name nvarchar(300) NOT NULL,
                    Description nvarchar(max) NOT NULL,
                    Price decimal(18,2) NOT NULL,
                    ImageUrl nvarchar(max) NULL,
                    StockQuantity int NOT NULL,
                    IsAvailable bit NOT NULL,
                    CreatedAt datetime2 NOT NULL
                );
              END;",

            @"IF OBJECT_ID('dbo.ProductPetCategories', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.ProductPetCategories
                (
                    ProductId int NOT NULL,
                    PetCategoryId int NOT NULL,
                    CONSTRAINT PK_ProductPetCategories PRIMARY KEY(ProductId, PetCategoryId),
                    CONSTRAINT FK_ProductPetCategories_Product
                        FOREIGN KEY(ProductId) REFERENCES dbo.ShopProducts(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_ProductPetCategories_Category
                        FOREIGN KEY(PetCategoryId) REFERENCES dbo.PetCategories(Id) ON DELETE CASCADE
                );
              END;",

            @"IF OBJECT_ID('dbo.ProductShopCategories', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.ProductShopCategories
                (
                    ProductId int NOT NULL,
                    ShopCategoryId int NOT NULL,
                    CONSTRAINT PK_ProductShopCategories PRIMARY KEY(ProductId, ShopCategoryId),
                    CONSTRAINT FK_ProductShopCategories_Product
                        FOREIGN KEY(ProductId) REFERENCES dbo.ShopProducts(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_ProductShopCategories_Category
                        FOREIGN KEY(ShopCategoryId) REFERENCES dbo.ShopCategories(Id) ON DELETE CASCADE
                );
              END;",

            @"IF OBJECT_ID('dbo.ShoppingCarts', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.ShoppingCarts
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId int NOT NULL,
                    UpdatedAt datetime2 NOT NULL,
                    CONSTRAINT FK_ShoppingCarts_User
                        FOREIGN KEY(UserId) REFERENCES dbo.UserAccounts(Id) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX UX_ShoppingCarts_UserId
                    ON dbo.ShoppingCarts(UserId);
              END;",

            @"IF OBJECT_ID('dbo.CartItems', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.CartItems
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CartId int NOT NULL,
                    ProductId int NOT NULL,
                    Quantity int NOT NULL,
                    CONSTRAINT FK_CartItems_Cart
                        FOREIGN KEY(CartId) REFERENCES dbo.ShoppingCarts(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_CartItems_Product
                        FOREIGN KEY(ProductId) REFERENCES dbo.ShopProducts(Id)
                );
                CREATE INDEX IX_CartItems_CartId ON dbo.CartItems(CartId);
              END;",

            @"IF OBJECT_ID('dbo.ShopOrders', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.ShopOrders
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId int NOT NULL,
                    TotalAmount decimal(18,2) NOT NULL,
                    Status nvarchar(50) NOT NULL,
                    OrderedAt datetime2 NOT NULL,
                    CONSTRAINT FK_ShopOrders_User
                        FOREIGN KEY(UserId) REFERENCES dbo.UserAccounts(Id)
                );
                CREATE INDEX IX_ShopOrders_UserId ON dbo.ShopOrders(UserId);
              END;",

            @"IF OBJECT_ID('dbo.ShopOrderItems', 'U') IS NULL
              BEGIN
                CREATE TABLE dbo.ShopOrderItems
                (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    OrderId int NOT NULL,
                    ProductId int NOT NULL,
                    Quantity int NOT NULL,
                    UnitPrice decimal(18,2) NOT NULL,
                    CONSTRAINT FK_ShopOrderItems_Order
                        FOREIGN KEY(OrderId) REFERENCES dbo.ShopOrders(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_ShopOrderItems_Product
                        FOREIGN KEY(ProductId) REFERENCES dbo.ShopProducts(Id)
                );
              END;",

            // ---------------------------------------------------------
            // TESTABLE CATALOG SEED
            // Only inserted when the SQL Server shop catalog is empty.
            // ---------------------------------------------------------
            @"IF NOT EXISTS (SELECT 1 FROM dbo.ShopCategories)
              BEGIN
                INSERT INTO dbo.ShopCategories(Name, Icon)
                VALUES ('Food',''), ('Accessories',''), ('Medication',''), ('Grooming','');
              END;",

            @"IF NOT EXISTS (SELECT 1 FROM dbo.PetCategories)
              BEGIN
                INSERT INTO dbo.PetCategories(Name)
                VALUES ('Dog'), ('Cat'), ('Bird'), ('Small Pet');
              END;",

            @"IF NOT EXISTS (SELECT 1 FROM dbo.ShopProducts)
              BEGIN
                INSERT INTO dbo.ShopProducts
                    (Name, Description, Price, ImageUrl, StockQuantity, IsAvailable, CreatedAt)
                VALUES
                    ('Sample Pet Accessory',
                     'Local integration test product.',
                     99.00,
                     '',
                     25,
                     1,
                     SYSDATETIME());

                DECLARE @ProductId int = SCOPE_IDENTITY();
                DECLARE @AccessoryId int =
                    (SELECT TOP 1 Id FROM dbo.ShopCategories WHERE Name='Accessories');
                DECLARE @DogId int =
                    (SELECT TOP 1 Id FROM dbo.PetCategories WHERE Name='Dog');

                IF @AccessoryId IS NOT NULL
                    INSERT INTO dbo.ProductShopCategories(ProductId, ShopCategoryId)
                    VALUES (@ProductId, @AccessoryId);

                IF @DogId IS NOT NULL
                    INSERT INTO dbo.ProductPetCategories(ProductId, PetCategoryId)
                    VALUES (@ProductId, @DogId);
              END;"
        };

        foreach (string sql in commands)
        {
            await using var command = new SqlCommand(sql, connection)
            {
                CommandTimeout = 60
            };
            await command.ExecuteNonQueryAsync();
        }

        _logger.LogInformation(
            "Shared SQL Server schema initialization completed.");
    }
}
