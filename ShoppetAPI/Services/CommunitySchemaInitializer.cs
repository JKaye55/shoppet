using Microsoft.Data.SqlClient;

namespace ShoppetAPI.Services;

public static class CommunitySchemaInitializer
{
    public static async Task EnsureAsync(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SharedSqlServer");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        const string sql = """
            IF OBJECT_ID(N'dbo.PetProfiles', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.PetProfiles', 'PhotoUrl') IS NULL
                ALTER TABLE dbo.PetProfiles
                ADD PhotoUrl NVARCHAR(MAX) NULL;


            IF OBJECT_ID(N'dbo.UserAccounts', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.UserAccounts', 'ProfilePicture') IS NULL
                    ALTER TABLE dbo.UserAccounts ADD ProfilePicture NVARCHAR(MAX) NULL;
                IF COL_LENGTH('dbo.UserAccounts', 'FacebookUrl') IS NULL
                    ALTER TABLE dbo.UserAccounts ADD FacebookUrl NVARCHAR(300) NULL;
                IF COL_LENGTH('dbo.UserAccounts', 'InstagramUrl') IS NULL
                    ALTER TABLE dbo.UserAccounts ADD InstagramUrl NVARCHAR(300) NULL;
                IF COL_LENGTH('dbo.UserAccounts', 'OtherSocialUrl') IS NULL
                    ALTER TABLE dbo.UserAccounts ADD OtherSocialUrl NVARCHAR(300) NULL;
                IF COL_LENGTH('dbo.UserAccounts', 'ShowSocialLinksOnMarketplace') IS NULL
                    ALTER TABLE dbo.UserAccounts ADD ShowSocialLinksOnMarketplace BIT NOT NULL
                        CONSTRAINT DF_UserAccounts_ShowSocialLinksOnMarketplace DEFAULT(1);
            END;

            IF OBJECT_ID(N'dbo.PetHealthRecords', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.PetHealthRecords', 'Completed') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD Completed BIT NOT NULL
                        CONSTRAINT DF_PetHealthRecords_Completed DEFAULT(0);
                IF COL_LENGTH('dbo.PetHealthRecords', 'CompletedAt') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD CompletedAt DATETIME2 NULL;
                IF COL_LENGTH('dbo.PetHealthRecords', 'DateAdministered') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD DateAdministered DATETIME2 NULL;
                IF COL_LENGTH('dbo.PetHealthRecords', 'ValidityInterval') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD ValidityInterval INT NOT NULL
                        CONSTRAINT DF_PetHealthRecords_ValidityInterval DEFAULT(0);
                IF COL_LENGTH('dbo.PetHealthRecords', 'ValidityUnit') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD ValidityUnit NVARCHAR(30) NULL;
                IF COL_LENGTH('dbo.PetHealthRecords', 'MedicationIntervalHours') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD MedicationIntervalHours DECIMAL(10,2) NOT NULL
                        CONSTRAINT DF_PetHealthRecords_MedicationIntervalHours DEFAULT(0);
                IF COL_LENGTH('dbo.PetHealthRecords', 'TimeStarted') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD TimeStarted DATETIME2 NULL;
                IF COL_LENGTH('dbo.PetHealthRecords', 'DosageTotal') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD DosageTotal INT NOT NULL
                        CONSTRAINT DF_PetHealthRecords_DosageTotal DEFAULT(0);
                IF COL_LENGTH('dbo.PetHealthRecords', 'DosageRemaining') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD DosageRemaining INT NOT NULL
                        CONSTRAINT DF_PetHealthRecords_DosageRemaining DEFAULT(0);
                IF COL_LENGTH('dbo.PetHealthRecords', 'CheckupDate') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD CheckupDate DATETIME2 NULL;
                IF COL_LENGTH('dbo.PetHealthRecords', 'DocumentPaths') IS NULL
                    ALTER TABLE dbo.PetHealthRecords ADD DocumentPaths NVARCHAR(MAX) NULL;
            END;

            IF OBJECT_ID(N'dbo.FoodLogs', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.FoodLogs', 'AmountGrams') IS NULL
                    ALTER TABLE dbo.FoodLogs ADD AmountGrams FLOAT NOT NULL
                        CONSTRAINT DF_FoodLogs_AmountGrams DEFAULT(0);
                IF COL_LENGTH('dbo.FoodLogs', 'IntervalHours') IS NULL
                    ALTER TABLE dbo.FoodLogs ADD IntervalHours INT NOT NULL
                        CONSTRAINT DF_FoodLogs_IntervalHours DEFAULT(0);
                IF COL_LENGTH('dbo.FoodLogs', 'IntervalMinutes') IS NULL
                    ALTER TABLE dbo.FoodLogs ADD IntervalMinutes INT NOT NULL
                        CONSTRAINT DF_FoodLogs_IntervalMinutes DEFAULT(0);
                IF COL_LENGTH('dbo.FoodLogs', 'StartTimestamp') IS NULL
                    ALTER TABLE dbo.FoodLogs ADD StartTimestamp DATETIME2 NULL;
                IF COL_LENGTH('dbo.FoodLogs', 'LastFedTimestamp') IS NULL
                    ALTER TABLE dbo.FoodLogs ADD LastFedTimestamp DATETIME2 NULL;
                IF COL_LENGTH('dbo.FoodLogs', 'FedDate') IS NULL
                    ALTER TABLE dbo.FoodLogs ADD FedDate DATETIME2 NULL;
                IF COL_LENGTH('dbo.FoodLogs', 'IsCompleted') IS NULL
                    ALTER TABLE dbo.FoodLogs ADD IsCompleted BIT NOT NULL
                        CONSTRAINT DF_FoodLogs_IsCompleted DEFAULT(0);
                IF COL_LENGTH('dbo.FoodLogs', 'CompletedAt') IS NULL
                    ALTER TABLE dbo.FoodLogs ADD CompletedAt DATETIME2 NULL;
            END;

            IF OBJECT_ID(N'dbo.EmergencyContacts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EmergencyContacts
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NOT NULL,
                    Name NVARCHAR(150) NOT NULL,
                    Role NVARCHAR(100) NULL,
                    Address NVARCHAR(300) NULL,
                    Phone NVARCHAR(50) NULL,
                    IsEmergency BIT NOT NULL CONSTRAINT DF_EmergencyContacts_IsEmergency DEFAULT(1),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_EmergencyContacts_CreatedAt DEFAULT(SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'dbo.Conversations', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Conversations
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    User1Id INT NOT NULL,
                    User2Id INT NOT NULL,
                    LastUpdated DATETIME2 NOT NULL CONSTRAINT DF_Conversations_LastUpdated DEFAULT(SYSDATETIME()),
                    User1UnreadCount INT NOT NULL CONSTRAINT DF_Conversations_User1Unread DEFAULT(0),
                    User2UnreadCount INT NOT NULL CONSTRAINT DF_Conversations_User2Unread DEFAULT(0)
                );
                CREATE UNIQUE INDEX UX_Conversations_UserPair ON dbo.Conversations(User1Id,User2Id);
            END;

            IF OBJECT_ID(N'dbo.Messages', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Messages
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    ConversationId INT NOT NULL,
                    SenderId INT NOT NULL,
                    ReceiverId INT NOT NULL,
                    ListingId INT NULL,
                    [Text] NVARCHAR(1000) NOT NULL,
                    [Timestamp] DATETIME2 NOT NULL CONSTRAINT DF_Messages_Timestamp DEFAULT(SYSDATETIME()),
                    IsRead BIT NOT NULL CONSTRAINT DF_Messages_IsRead DEFAULT(0)
                );
                CREATE INDEX IX_Messages_Conversation_Timestamp ON dbo.Messages(ConversationId,[Timestamp]);
            END;

            IF OBJECT_ID(N'dbo.MarketplaceCart', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MarketplaceCart
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NOT NULL,
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_MarketplaceCart_UpdatedAt DEFAULT(SYSDATETIME())
                );
                CREATE UNIQUE INDEX UX_MarketplaceCart_UserId ON dbo.MarketplaceCart(UserId);
            END;

            IF OBJECT_ID(N'dbo.MarketplaceCartItems', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MarketplaceCartItems
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CartId INT NOT NULL,
                    MarketplaceListingId INT NOT NULL,
                    Quantity INT NOT NULL CONSTRAINT DF_MarketplaceCartItems_Quantity DEFAULT(1)
                );
                CREATE UNIQUE INDEX UX_MarketplaceCartItems_CartListing
                    ON dbo.MarketplaceCartItems(CartId, MarketplaceListingId);
            END;

            IF OBJECT_ID(N'dbo.MarketplaceOrders', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MarketplaceOrders
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NOT NULL,
                    TotalAmount DECIMAL(10,2) NOT NULL,
                    Status NVARCHAR(30) NOT NULL CONSTRAINT DF_MarketplaceOrders_Status DEFAULT('Confirmed'),
                    OrderedAt DATETIME2 NOT NULL CONSTRAINT DF_MarketplaceOrders_OrderedAt DEFAULT(SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'dbo.MarketplaceOrderItems', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MarketplaceOrderItems
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    OrderId INT NOT NULL,
                    MarketplaceListingId INT NOT NULL,
                    Quantity INT NOT NULL,
                    UnitPrice DECIMAL(10,2) NOT NULL
                );
            END;

            IF OBJECT_ID(N'dbo.CommunityPosts', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.CommunityPosts', 'ImageUrl') IS NOT NULL
                ALTER TABLE dbo.CommunityPosts ALTER COLUMN ImageUrl NVARCHAR(MAX) NULL;

            IF OBJECT_ID(N'dbo.MarketplaceListings', N'U') IS NOT NULL
               AND COL_LENGTH('dbo.MarketplaceListings', 'ImageUrl') IS NOT NULL
                ALTER TABLE dbo.MarketplaceListings ALTER COLUMN ImageUrl NVARCHAR(MAX) NULL;

            IF OBJECT_ID(N'dbo.CommunityPosts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CommunityPosts
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NOT NULL,
                    PetId INT NULL,
                    Caption NVARCHAR(500) NOT NULL CONSTRAINT DF_CommunityPosts_Caption DEFAULT(''),
                    ImageUrl NVARCHAR(MAX) NULL,
                    LikeCount INT NOT NULL CONSTRAINT DF_CommunityPosts_LikeCount DEFAULT(0),
                    PostedAt DATETIME2 NOT NULL CONSTRAINT DF_CommunityPosts_PostedAt DEFAULT(SYSDATETIME()),
                    IsEdited BIT NOT NULL CONSTRAINT DF_CommunityPosts_IsEdited DEFAULT(0)
                );
            END;

            IF COL_LENGTH('dbo.CommunityPosts', 'LikeCount') IS NULL
                ALTER TABLE dbo.CommunityPosts
                ADD LikeCount INT NOT NULL CONSTRAINT DF_CommunityPosts_LikeCount_Mobile DEFAULT(0);

            IF COL_LENGTH('dbo.CommunityPosts', 'IsEdited') IS NULL
                ALTER TABLE dbo.CommunityPosts
                ADD IsEdited BIT NOT NULL CONSTRAINT DF_CommunityPosts_IsEdited_Mobile DEFAULT(0);

            IF OBJECT_ID(N'dbo.CommunityLikes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CommunityLikes
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    PostId INT NOT NULL,
                    UserId INT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CommunityLikes_CreatedAt DEFAULT(SYSDATETIME())
                );
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'UX_CommunityLikes_Post_User'
                  AND object_id = OBJECT_ID('dbo.CommunityLikes')
            )
            BEGIN
                ;WITH d AS
                (
                    SELECT Id,
                           ROW_NUMBER() OVER(PARTITION BY PostId,UserId ORDER BY Id) AS rn
                    FROM dbo.CommunityLikes
                )
                DELETE FROM d WHERE rn > 1;

                CREATE UNIQUE INDEX UX_CommunityLikes_Post_User
                ON dbo.CommunityLikes(PostId, UserId);
            END;

            IF OBJECT_ID(N'dbo.CommunityComments', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CommunityComments
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    PostId INT NOT NULL,
                    UserId INT NOT NULL,
                    ParentCommentId INT NULL,
                    Content NVARCHAR(1000) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CommunityComments_CreatedAt DEFAULT(SYSDATETIME())
                );
            END;

            IF OBJECT_ID(N'dbo.CommunityComments', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.CommunityComments', 'UserId') IS NULL
                    ALTER TABLE dbo.CommunityComments ADD UserId INT NULL;

                IF COL_LENGTH('dbo.CommunityComments', 'ParentCommentId') IS NULL
                    ALTER TABLE dbo.CommunityComments ADD ParentCommentId INT NULL;

                IF COL_LENGTH('dbo.CommunityComments', 'Content') IS NULL
                    ALTER TABLE dbo.CommunityComments ADD Content NVARCHAR(1000) NULL;

                IF COL_LENGTH('dbo.CommunityComments', 'AuthorName') IS NULL
                    ALTER TABLE dbo.CommunityComments ADD AuthorName NVARCHAR(150) NULL;

                IF COL_LENGTH('dbo.CommunityComments', 'CreatedAt') IS NULL
                    ALTER TABLE dbo.CommunityComments
                    ADD CreatedAt DATETIME2 NOT NULL
                        CONSTRAINT DF_CommunityComments_CreatedAt_Mobile DEFAULT(SYSDATETIME());

                IF COL_LENGTH('dbo.CommunityComments', 'Body') IS NOT NULL
                    EXEC(N'UPDATE dbo.CommunityComments
                           SET Content = Body
                           WHERE (Content IS NULL OR LTRIM(RTRIM(Content)) = '''')
                             AND Body IS NOT NULL;');
            END;

            IF OBJECT_ID(N'dbo.CommunityLikes', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.CommunityLikes', 'UserId') IS NULL
                    ALTER TABLE dbo.CommunityLikes ADD UserId INT NULL;

                IF COL_LENGTH('dbo.CommunityLikes', 'CreatedAt') IS NULL
                    ALTER TABLE dbo.CommunityLikes
                    ADD CreatedAt DATETIME2 NOT NULL
                        CONSTRAINT DF_CommunityLikes_CreatedAt_Mobile DEFAULT(SYSDATETIME());
            END;

            IF OBJECT_ID(N'dbo.CommunityCommentLikes', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CommunityCommentLikes
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    CommentId INT NOT NULL,
                    UserId INT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_CommunityCommentLikes_CreatedAt DEFAULT(SYSDATETIME())
                );
            END;

            IF NOT EXISTS (
                SELECT 1
                FROM sys.indexes
                WHERE name = 'UX_CommunityCommentLikes_Comment_User'
                  AND object_id = OBJECT_ID('dbo.CommunityCommentLikes')
            )
            BEGIN
                ;WITH d AS
                (
                    SELECT Id,
                           ROW_NUMBER() OVER(PARTITION BY CommentId,UserId ORDER BY Id) AS rn
                    FROM dbo.CommunityCommentLikes
                )
                DELETE FROM d WHERE rn > 1;

                CREATE UNIQUE INDEX UX_CommunityCommentLikes_Comment_User
                ON dbo.CommunityCommentLikes(CommentId, UserId);
            END;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
