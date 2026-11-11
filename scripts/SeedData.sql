/*
    Optional manual setup for a local SQL Server instance.

    The API does not need this script. On startup it creates EfCompiledQueries
    and seeds a few hundred orders. Run this script only when you want the small
    hand-checked sample instead, and only when that database has no customers yet.

    Windows authentication. No password is stored in this file.
    The shape matches AppDbContext (names, lengths, indexes, and delete behavior
    were taken from a database created by EF Core).

    Sample, if the tables are empty:
      Order 1  Ava Patel   41.00  Paid
      Order 2  Ava Patel   16.00  Shipped
      Order 3  Noah Kim    48.00  Pending
      Order 4  Maya Chen   24.00  Cancelled
      Leo Martins has no orders.
*/

IF DB_ID(N'EfCompiledQueries') IS NULL
    CREATE DATABASE EfCompiledQueries;
GO

USE EfCompiledQueries;
GO

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
        Name nvarchar(200) NOT NULL,
        Email nvarchar(320) NOT NULL,
        City nvarchar(100) NOT NULL
    );

    CREATE UNIQUE INDEX IX_Customers_Email ON dbo.Customers(Email);
END
GO

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
        Sku nvarchar(32) NOT NULL,
        Name nvarchar(200) NOT NULL,
        UnitPrice decimal(18,2) NOT NULL
    );

    CREATE UNIQUE INDEX IX_Products_Sku ON dbo.Products(Sku);
END
GO

IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
        CustomerId int NOT NULL,
        OrderedAtUtc datetime2(7) NOT NULL,
        Status nvarchar(20) NOT NULL,
        TotalAmount decimal(18,2) NOT NULL,
        CONSTRAINT FK_Orders_Customers_CustomerId
            FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id) ON DELETE NO ACTION
    );

    CREATE INDEX IX_Orders_CustomerId ON dbo.Orders(CustomerId);
    CREATE INDEX IX_Orders_Status_OrderedAtUtc ON dbo.Orders(Status, OrderedAtUtc);
END
GO

IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderItems
    (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
        OrderId int NOT NULL,
        ProductId int NOT NULL,
        Quantity int NOT NULL,
        UnitPrice decimal(18,2) NOT NULL,
        CONSTRAINT FK_OrderItems_Orders_OrderId
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_OrderItems_Products_ProductId
            FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE NO ACTION
    );

    CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);
    CREATE INDEX IX_OrderItems_ProductId ON dbo.OrderItems(ProductId);
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Customers)
BEGIN
    SET IDENTITY_INSERT dbo.Customers ON;
    INSERT INTO dbo.Customers (Id, Name, Email, City) VALUES
        (1, N'Ava Patel', N'ava.patel@example.test', N'Seattle'),
        (2, N'Noah Kim', N'noah.kim@example.test', N'Austin'),
        (3, N'Maya Chen', N'maya.chen@example.test', N'Denver'),
        (4, N'Leo Martins', N'leo.martins@example.test', N'Boston');
    SET IDENTITY_INSERT dbo.Customers OFF;

    SET IDENTITY_INSERT dbo.Products ON;
    INSERT INTO dbo.Products (Id, Sku, Name, UnitPrice) VALUES
        (1, N'NB-100', N'Field Notes Notebook', 8.50),
        (2, N'LP-200', N'Cedar Desk Lamp', 24.00),
        (3, N'CB-300', N'Northwind Coffee Beans', 16.00);
    SET IDENTITY_INSERT dbo.Products OFF;

    SET IDENTITY_INSERT dbo.Orders ON;
    INSERT INTO dbo.Orders (Id, CustomerId, OrderedAtUtc, Status, TotalAmount) VALUES
        (1, 1, '2026-03-02T15:00:00', N'Paid', 41.00),
        (2, 1, '2026-04-11T15:00:00', N'Shipped', 16.00),
        (3, 2, '2026-05-20T15:00:00', N'Pending', 48.00),
        (4, 3, '2026-06-01T15:00:00', N'Cancelled', 24.00);
    SET IDENTITY_INSERT dbo.Orders OFF;

    SET IDENTITY_INSERT dbo.OrderItems ON;
    INSERT INTO dbo.OrderItems (Id, OrderId, ProductId, Quantity, UnitPrice) VALUES
        (1, 1, 1, 2, 8.50),
        (2, 1, 2, 1, 24.00),
        (3, 2, 3, 1, 16.00),
        (4, 3, 3, 3, 16.00),
        (5, 4, 2, 1, 24.00);
    SET IDENTITY_INSERT dbo.OrderItems OFF;
END
GO
