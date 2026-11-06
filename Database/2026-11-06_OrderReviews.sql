USE PetFoodDB;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.OrderReviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderReviews
    (
        review_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderReviews PRIMARY KEY,
        order_id VARCHAR(50) NOT NULL,
        user_id VARCHAR(50) NOT NULL,
        author_name NVARCHAR(60) NOT NULL,
        rating INT NOT NULL,
        comment NVARCHAR(600) NULL,
        share_publicly BIT NOT NULL CONSTRAINT DF_OrderReviews_SharePublicly DEFAULT (0),
        is_visible BIT NOT NULL CONSTRAINT DF_OrderReviews_IsVisible DEFAULT (1),
        shop_reply NVARCHAR(600) NULL,
        created_at DATETIME2(0) NOT NULL CONSTRAINT DF_OrderReviews_CreatedAt DEFAULT (SYSUTCDATETIME()),
        replied_at DATETIME2(0) NULL,
        CONSTRAINT UQ_OrderReviews_Order UNIQUE (order_id),
        CONSTRAINT FK_OrderReviews_Orders FOREIGN KEY (order_id) REFERENCES dbo.Orders(order_id) ON DELETE CASCADE,
        CONSTRAINT FK_OrderReviews_Users FOREIGN KEY (user_id) REFERENCES dbo.Users(user_id),
        CONSTRAINT CK_OrderReviews_Rating CHECK (rating BETWEEN 1 AND 5),
        CONSTRAINT CK_OrderReviews_Comment CHECK (comment IS NULL OR LEN(comment) BETWEEN 2 AND 600)
    );
    CREATE INDEX IX_OrderReviews_VisibleCreated ON dbo.OrderReviews(is_visible, created_at DESC);
END;

COMMIT TRANSACTION;
GO
