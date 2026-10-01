SET XACT_ABORT ON;
-- Check columns individually: existing databases have several order formats.
IF OBJECT_ID('dbo.MarketplaceOrders','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.MarketplaceOrders','BuyerUserId') IS NULL
        ALTER TABLE dbo.MarketplaceOrders ADD BuyerUserId INT NULL;
    IF COL_LENGTH('dbo.MarketplaceOrders','SellerUserId') IS NULL
        ALTER TABLE dbo.MarketplaceOrders ADD SellerUserId INT NULL;
    IF COL_LENGTH('dbo.MarketplaceOrders','Reference') IS NULL
        ALTER TABLE dbo.MarketplaceOrders ADD Reference NVARCHAR(100) NULL;
    IF COL_LENGTH('dbo.MarketplaceOrders','PaymentMethod') IS NULL
        ALTER TABLE dbo.MarketplaceOrders ADD PaymentMethod NVARCHAR(50) NULL;
    IF COL_LENGTH('dbo.MarketplaceOrders','Subtotal') IS NULL
        ALTER TABLE dbo.MarketplaceOrders ADD Subtotal DECIMAL(10,2) NULL;
    IF COL_LENGTH('dbo.MarketplaceOrders','VoucherDiscount') IS NULL
        ALTER TABLE dbo.MarketplaceOrders ADD VoucherDiscount DECIMAL(10,2) NOT NULL DEFAULT(0);
    IF COL_LENGTH('dbo.MarketplaceOrders','Total') IS NULL
        ALTER TABLE dbo.MarketplaceOrders ADD Total DECIMAL(10,2) NULL;
    IF COL_LENGTH('dbo.MarketplaceOrders','CreatedAt') IS NULL
        ALTER TABLE dbo.MarketplaceOrders ADD CreatedAt DATETIME2 NULL;
    IF COL_LENGTH('dbo.MarketplaceOrders','CompletedAt') IS NULL
        ALTER TABLE dbo.MarketplaceOrders ADD CompletedAt DATETIME2 NULL;
END;
GO
IF OBJECT_ID('dbo.MarketplaceOrderItems','U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.MarketplaceOrderItems','ListingId') IS NULL
        ALTER TABLE dbo.MarketplaceOrderItems ADD ListingId INT NULL;
    IF COL_LENGTH('dbo.MarketplaceOrderItems','ListingTitle') IS NULL
        ALTER TABLE dbo.MarketplaceOrderItems ADD ListingTitle NVARCHAR(200) NULL;
    IF COL_LENGTH('dbo.MarketplaceOrderItems','Price') IS NULL
        ALTER TABLE dbo.MarketplaceOrderItems ADD Price DECIMAL(10,2) NULL;
END;
GO
IF OBJECT_ID('dbo.MarketplaceOrders', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MarketplaceOrders
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BuyerUserId INT NOT NULL,
        SellerUserId INT NOT NULL,
        Reference NVARCHAR(80) NOT NULL,
        PaymentMethod NVARCHAR(50) NOT NULL,
        Status NVARCHAR(40) NOT NULL CONSTRAINT DF_MarketplaceOrders_Status DEFAULT ('Purchase Requested'),
        Subtotal DECIMAL(10,2) NOT NULL,
        VoucherDiscount DECIMAL(10,2) NOT NULL CONSTRAINT DF_MarketplaceOrders_Discount DEFAULT (0),
        Total DECIMAL(10,2) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MarketplaceOrders_CreatedAt DEFAULT (SYSDATETIME()),
        CompletedAt DATETIME2 NULL,
        CONSTRAINT FK_MarketplaceOrders_Buyer FOREIGN KEY (BuyerUserId) REFERENCES dbo.UserAccounts(Id),
        CONSTRAINT FK_MarketplaceOrders_Seller FOREIGN KEY (SellerUserId) REFERENCES dbo.UserAccounts(Id)
    );
END;
GO

IF OBJECT_ID('dbo.MarketplaceOrderItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MarketplaceOrderItems
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderId INT NOT NULL,
        ListingId INT NOT NULL,
        ListingTitle NVARCHAR(150) NOT NULL,
        Price DECIMAL(10,2) NOT NULL,
        CONSTRAINT FK_MarketplaceOrderItems_Order FOREIGN KEY (OrderId) REFERENCES dbo.MarketplaceOrders(Id),
        CONSTRAINT FK_MarketplaceOrderItems_Listing FOREIGN KEY (ListingId) REFERENCES dbo.MarketplaceListings(Id)
    );
END;
GO

-- New checkout stores lines in MarketplaceOrderItems. Obsolete required fields
-- must allow NULL so both clients can insert the canonical order format.
DECLARE @relaxSql NVARCHAR(MAX)=N'';
SELECT @relaxSql=@relaxSql+N'ALTER TABLE dbo.'+QUOTENAME(t.name)+N' ALTER COLUMN '+QUOTENAME(c.name)+N' '+QUOTENAME(ty.name)+
    CASE WHEN ty.name IN ('nvarchar','nchar') THEN N'('+CASE WHEN c.max_length=-1 THEN N'MAX' ELSE CONVERT(NVARCHAR(10),c.max_length/2) END+N')'
         WHEN ty.name IN ('varchar','char','varbinary','binary') THEN N'('+CASE WHEN c.max_length=-1 THEN N'MAX' ELSE CONVERT(NVARCHAR(10),c.max_length) END+N')'
         WHEN ty.name IN ('decimal','numeric') THEN N'('+CONVERT(NVARCHAR(10),c.precision)+N','+CONVERT(NVARCHAR(10),c.scale)+N')'
         WHEN ty.name IN ('datetime2','datetimeoffset','time') THEN N'('+CONVERT(NVARCHAR(10),c.scale)+N')'
         ELSE N'' END+N' NULL;'
FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE t.schema_id=SCHEMA_ID('dbo') AND c.is_nullable=0 AND c.is_computed=0 AND c.is_identity=0
AND ((t.name='MarketplaceOrders' AND c.name IN ('UserId','TotalAmount','ListingId','ItemTitle','Amount','PaymentStatus','IsSimulation','OrderedAt'))
  OR (t.name='MarketplaceOrderItems' AND c.name IN ('MarketplaceListingId','Quantity','UnitPrice')));
IF LEN(@relaxSql)>0 EXEC sys.sp_executesql @relaxSql;
GO
-- Preserve legacy values; do not mark unpaid or pending orders completed.
IF COL_LENGTH('dbo.MarketplaceOrderItems','MarketplaceListingId') IS NOT NULL
    EXEC(N'UPDATE oi SET ListingId=COALESCE(oi.ListingId,oi.MarketplaceListingId), ListingTitle=COALESCE(oi.ListingTitle,m.Title,''Marketplace item'') FROM dbo.MarketplaceOrderItems oi LEFT JOIN dbo.MarketplaceListings m ON m.Id=oi.MarketplaceListingId');
IF COL_LENGTH('dbo.MarketplaceOrderItems','UnitPrice') IS NOT NULL
    EXEC(N'UPDATE dbo.MarketplaceOrderItems SET Price=COALESCE(Price,UnitPrice)');
IF COL_LENGTH('dbo.MarketplaceOrders','UserId') IS NOT NULL
    EXEC(N'UPDATE dbo.MarketplaceOrders SET BuyerUserId=COALESCE(BuyerUserId,UserId)');
IF COL_LENGTH('dbo.MarketplaceOrders','TotalAmount') IS NOT NULL
    EXEC(N'UPDATE dbo.MarketplaceOrders SET Subtotal=COALESCE(Subtotal,TotalAmount),Total=COALESCE(Total,TotalAmount)');
IF COL_LENGTH('dbo.MarketplaceOrders','Amount') IS NOT NULL
    EXEC(N'UPDATE dbo.MarketplaceOrders SET Subtotal=COALESCE(Subtotal,Amount),Total=COALESCE(Total,Amount)');
IF COL_LENGTH('dbo.MarketplaceOrders','OrderedAt') IS NOT NULL
    EXEC(N'UPDATE dbo.MarketplaceOrders SET CreatedAt=COALESCE(CreatedAt,OrderedAt)');
GO
UPDATE dbo.MarketplaceOrders SET Reference=CONCAT('LEGACY-',Id) WHERE Reference IS NULL;
UPDATE o SET SellerUserId=(SELECT TOP(1) m.SellerUserId FROM dbo.MarketplaceOrderItems i JOIN dbo.MarketplaceListings m ON m.Id=i.ListingId WHERE i.OrderId=o.Id ORDER BY i.Id)
FROM dbo.MarketplaceOrders o WHERE o.SellerUserId IS NULL;
GO
-- Older single-listing orders stored their line on the order itself.
IF COL_LENGTH('dbo.MarketplaceOrders','ListingId') IS NOT NULL
   AND COL_LENGTH('dbo.MarketplaceOrders','ItemTitle') IS NOT NULL
   AND COL_LENGTH('dbo.MarketplaceOrders','Amount') IS NOT NULL
    EXEC(N'INSERT INTO dbo.MarketplaceOrderItems(OrderId,ListingId,ListingTitle,Price)
        SELECT o.Id,o.ListingId,COALESCE(o.ItemTitle,m.Title,''Marketplace item''),o.Amount
        FROM dbo.MarketplaceOrders o JOIN dbo.MarketplaceListings m ON m.Id=o.ListingId
        WHERE NOT EXISTS(SELECT 1 FROM dbo.MarketplaceOrderItems i WHERE i.OrderId=o.Id)');
GO
IF OBJECT_ID('dbo.MarketplaceReviews', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MarketplaceReviews
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OrderItemId INT NOT NULL,
        ListingId INT NOT NULL,
        BuyerUserId INT NOT NULL,
        Rating INT NOT NULL,
        Comment NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MarketplaceReviews_CreatedAt DEFAULT (SYSDATETIME()),
        RewardIssued BIT NOT NULL CONSTRAINT DF_MarketplaceReviews_RewardIssued DEFAULT (0),
        CONSTRAINT CK_MarketplaceReviews_Rating CHECK (Rating BETWEEN 1 AND 5),
        CONSTRAINT FK_MarketplaceReviews_OrderItem FOREIGN KEY (OrderItemId) REFERENCES dbo.MarketplaceOrderItems(Id),
        CONSTRAINT FK_MarketplaceReviews_Listing FOREIGN KEY (ListingId) REFERENCES dbo.MarketplaceListings(Id),
        CONSTRAINT FK_MarketplaceReviews_Buyer FOREIGN KEY (BuyerUserId) REFERENCES dbo.UserAccounts(Id)
    );
END;
GO

IF OBJECT_ID('dbo.MarketplaceVouchers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MarketplaceVouchers
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NOT NULL,
        Code NVARCHAR(60) NOT NULL,
        Amount DECIMAL(10,2) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_MarketplaceVouchers_Status DEFAULT ('Active'),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MarketplaceVouchers_CreatedAt DEFAULT (SYSDATETIME()),
        ExpiresAt DATETIME2 NOT NULL,
        UsedAt DATETIME2 NULL,
        UsedOrderId INT NULL,
        CONSTRAINT FK_MarketplaceVouchers_User FOREIGN KEY (UserId) REFERENCES dbo.UserAccounts(Id),
        CONSTRAINT FK_MarketplaceVouchers_Order FOREIGN KEY (UsedOrderId) REFERENCES dbo.MarketplaceOrders(Id)
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_MarketplaceOrders_Reference'
      AND object_id = OBJECT_ID('dbo.MarketplaceOrders')
)
    CREATE UNIQUE INDEX UX_MarketplaceOrders_Reference ON dbo.MarketplaceOrders(Reference);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_MarketplaceReviews_OrderItem_Buyer'
      AND object_id = OBJECT_ID('dbo.MarketplaceReviews')
)
    CREATE UNIQUE INDEX UX_MarketplaceReviews_OrderItem_Buyer ON dbo.MarketplaceReviews(OrderItemId, BuyerUserId);
GO

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_MarketplaceVouchers_Code'
      AND object_id = OBJECT_ID('dbo.MarketplaceVouchers')
)
    CREATE UNIQUE INDEX UX_MarketplaceVouchers_Code ON dbo.MarketplaceVouchers(Code);
GO


IF COL_LENGTH('dbo.UserAccounts','ApiToken') IS NULL ALTER TABLE dbo.UserAccounts ADD ApiToken NVARCHAR(250) NULL;
IF COL_LENGTH('dbo.UserAccounts','ApiTokenExpiresAt') IS NULL ALTER TABLE dbo.UserAccounts ADD ApiTokenExpiresAt DATETIME2 NULL;
IF COL_LENGTH('dbo.MarketplaceListings','UpdatedAt') IS NULL ALTER TABLE dbo.MarketplaceListings ADD UpdatedAt DATETIME2 NULL;
IF OBJECT_ID('dbo.VetVisitReminders','U') IS NULL
CREATE TABLE dbo.VetVisitReminders(Id INT IDENTITY PRIMARY KEY,UserId INT NOT NULL,PetId INT NOT NULL,ClinicName NVARCHAR(150) NULL,VisitAt DATETIME2 NOT NULL,Purpose NVARCHAR(150) NULL,Notes NVARCHAR(1000) NULL,Completed BIT NOT NULL DEFAULT(0));
GO

IF COL_LENGTH('dbo.UserAccounts','IsDisabled') IS NULL ALTER TABLE dbo.UserAccounts ADD IsDisabled BIT NOT NULL DEFAULT(0);
GO

IF OBJECT_ID('dbo.MarketplaceCartItems','U') IS NOT NULL
   AND COL_LENGTH('dbo.MarketplaceCartItems','Quantity') IS NULL
    ALTER TABLE dbo.MarketplaceCartItems ADD Quantity INT NOT NULL DEFAULT(1);
GO
IF OBJECT_ID('dbo.MarketplaceCartItems','U') IS NOT NULL
    EXEC(N'UPDATE dbo.MarketplaceCartItems SET Quantity=1 WHERE Quantity IS NULL OR Quantity<>1');
GO
