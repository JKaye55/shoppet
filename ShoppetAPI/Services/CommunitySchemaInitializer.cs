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
                CREATE UNIQUE INDEX UX_CommunityLikes_Post_User
                ON dbo.CommunityLikes(PostId, UserId);

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
                CREATE UNIQUE INDEX UX_CommunityCommentLikes_Comment_User
                ON dbo.CommunityCommentLikes(CommentId, UserId);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
