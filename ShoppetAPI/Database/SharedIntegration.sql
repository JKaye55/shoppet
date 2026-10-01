SET XACT_ABORT ON;
IF OBJECT_ID('dbo.MarketplaceOrders','U') IS NOT NULL AND COL_LENGTH('dbo.MarketplaceOrders','BuyerUserId') IS NULL
BEGIN
    ALTER TABLE dbo.MarketplaceOrders ADD BuyerUserId INT NULL,SellerUserId INT NULL,Reference NVARCHAR(100) NULL,PaymentMethod NVARCHAR(50) NULL,Subtotal DECIMAL(10,2) NULL,VoucherDiscount DECIMAL(10,2) NOT NULL DEFAULT(0),Total DECIMAL(10,2) NULL,CreatedAt DATETIME2 NULL,CompletedAt DATETIME2 NULL;
    ALTER TABLE dbo.MarketplaceOrders ALTER COLUMN UserId INT NULL;
    ALTER TABLE dbo.MarketplaceOrders ALTER COLUMN TotalAmount DECIMAL(10,2) NULL;
END;
GO
IF OBJECT_ID('dbo.MarketplaceOrderItems','U') IS NOT NULL AND COL_LENGTH('dbo.MarketplaceOrderItems','ListingId') IS NULL
BEGIN
    ALTER TABLE dbo.MarketplaceOrderItems ADD ListingId INT NULL,ListingTitle NVARCHAR(200) NULL,Price DECIMAL(10,2) NULL;
    ALTER TABLE dbo.MarketplaceOrderItems ALTER COLUMN MarketplaceListingId INT NULL;
    ALTER TABLE dbo.MarketplaceOrderItems ALTER COLUMN Quantity INT NULL;
    ALTER TABLE dbo.MarketplaceOrderItems ALTER COLUMN UnitPrice DECIMAL(10,2) NULL;
END;
GO
IF COL_LENGTH('dbo.MarketplaceOrderItems','MarketplaceListingId') IS NOT NULL
 EXEC(N'UPDATE oi SET ListingId=MarketplaceListingId,ListingTitle=COALESCE(m.Title,''Marketplace item''),Price=UnitPrice FROM dbo.MarketplaceOrderItems oi LEFT JOIN dbo.MarketplaceListings m ON m.Id=oi.MarketplaceListingId WHERE ListingId IS NULL');
IF COL_LENGTH('dbo.MarketplaceOrders','UserId') IS NOT NULL
 EXEC(N'UPDATE o SET BuyerUserId=UserId,SellerUserId=(SELECT TOP(1) m.SellerUserId FROM dbo.MarketplaceOrderItems i JOIN dbo.MarketplaceListings m ON m.Id=i.ListingId WHERE i.OrderId=o.Id),Reference=CONCAT(''MOCK-LEGACY-'',Id),PaymentMethod=''GCash Mock'',Subtotal=TotalAmount,Total=TotalAmount,CreatedAt=OrderedAt,CompletedAt=OrderedAt,Status=''Completed'' FROM dbo.MarketplaceOrders o WHERE BuyerUserId IS NULL');
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

IF OBJECT_ID('dbo.MarketplaceCartItems','U') IS NOT NULL UPDATE dbo.MarketplaceCartItems SET Quantity=1 WHERE Quantity<>1;
GO
