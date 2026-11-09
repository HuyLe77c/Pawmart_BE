/*
====================================================================
 PETFOOD - PRM393
 Complete SQL Server Database Script
 Version: 1.0 - PRM393 5-Week MVP
====================================================================

Target stack:
    Flutter Android
        -> ASP.NET Core Web API
        -> SQL Server (SSMS)

Business roles:
    CUSTOMER
    ADMIN

Core features covered:
    - Register / Login / Role
    - Customer profile
    - Pet profiles
    - Product categories
    - Product CRUD + stock
    - Product multiple images
    - Search / filter support
    - Shopping cart
    - Favorites
    - Delivery address snapshot + Map coordinates
    - Store / warehouse locations
    - Orders + order items
    - Order status tracking
    - Order status history
    - COD / VNPay / MoMo payment records
    - Promotion / voucher
    - Firebase Cloud Messaging token
    - Notification history
    - Admin statistics can be calculated from Orders / OrderItems
    - AI chatbot support through Pets + Products data

Important architecture decisions:
    1. AI chat history is NOT persisted in DB for the 5-week MVP.
       Flutter -> API -> Gemini/OpenAI -> response.
    2. Shipping address is stored as an order snapshot. This preserves the
       address used at purchase time and avoids unnecessary address tables.
    3. A single FCM token is stored on Users for MVP simplicity. If multi-device
       notification support is needed later, create a DeviceTokens table.
    4. Product stock is stored on Products. Multiple-store inventory is outside
       the 5-week MVP; Stores is for map/location management.
    5. Statistics / dashboard values are calculated by SQL queries in the API.
       No redundant Revenue or Statistics tables are created.
    6. Products, Categories, Promotions, Users are intended to be soft-managed
       using IsActive rather than physically deleted in normal application flow.

SQL dialect:
    Microsoft SQL Server / T-SQL

Run this script on a NEW / EMPTY PetFoodDB.
The safety guard below stops execution if core tables already exist.
Do NOT mix this schema with a MySQL version of the project.
====================================================================
*/

/* ================================================================
   0. CREATE DATABASE
   ================================================================ */
IF DB_ID(N'PetFoodDB') IS NULL
BEGIN
    CREATE DATABASE PetFoodDB;
END
GO

USE PetFoodDB;
GO

/* ================================================================
   0.1 SAFETY GUARD
   ================================================================ */
IF EXISTS
(
    SELECT 1
    FROM sys.tables
    WHERE name IN
    (
        N'Users', N'Pets', N'Categories', N'Products', N'Product_Images',
        N'CartItems', N'Favorites', N'Orders', N'OrderItems',
        N'OrderStatusHistory', N'Payments', N'BankQrWebhookEvents', N'Promotions',
        N'Notifications', N'Stores'
    )
)
BEGIN
    THROW 51000,
        'PetFoodDB already contains one or more PetFood tables. Run this script on a new/empty PetFoodDB or remove the old schema intentionally first.',
        1;
END
GO

/* ================================================================
   1. USERS
   ================================================================ */
CREATE TABLE dbo.Users
(
    user_id                 VARCHAR(50)      NOT NULL,
    email                   NVARCHAR(150)    NOT NULL,
    password_hash           NVARCHAR(255)    NOT NULL,
    google_subject          VARCHAR(255)     NULL,
    full_name               NVARCHAR(100)    NOT NULL,
    phone_number            VARCHAR(20)      NULL,
    role                    VARCHAR(20)      NOT NULL
        CONSTRAINT DF_Users_Role DEFAULT ('CUSTOMER'),
    fcm_token               NVARCHAR(1000)   NULL,
    fcm_token_updated_at    DATETIME2(0)     NULL,
    is_active               BIT              NOT NULL
        CONSTRAINT DF_Users_IsActive DEFAULT (1),
    created_at              DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    updated_at              DATETIME2(0)     NULL,
    last_login_at           DATETIME2(0)     NULL,

    CONSTRAINT PK_Users PRIMARY KEY (user_id),
    CONSTRAINT UQ_Users_Email UNIQUE (email),
    CONSTRAINT CK_Users_Role CHECK (role IN ('CUSTOMER', 'ADMIN')),
    CONSTRAINT CK_Users_Email_NotBlank CHECK (LEN(LTRIM(RTRIM(email))) > 0),
    CONSTRAINT CK_Users_FullName_NotBlank CHECK (LEN(LTRIM(RTRIM(full_name))) > 0)
);
GO

/* ================================================================
   2. PETS
   ================================================================ */
CREATE TABLE dbo.Pets
(
    pet_id          INT IDENTITY(1,1) NOT NULL,
    user_id         VARCHAR(50)      NOT NULL,
    pet_name        NVARCHAR(50)     NOT NULL,
    pet_type        VARCHAR(20)      NOT NULL,
    gender          VARCHAR(10)      NOT NULL
        CONSTRAINT DF_Pets_Gender DEFAULT ('UNKNOWN'),
    breed           NVARCHAR(100)    NULL,
    age_months      INT              NULL,
    weight_kg       DECIMAL(5,2)     NULL,
    special_notes   NVARCHAR(MAX)    NULL,
    body_size       VARCHAR(10)      NULL,
    activity_level  VARCHAR(10)      NULL,
    body_condition_score INT         NULL,
    feeding_habits  NVARCHAR(500)    NULL,
    food_preferences NVARCHAR(500)   NULL,
    image_url       NVARCHAR(1000)   NULL,
    created_at      DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Pets_CreatedAt DEFAULT (SYSUTCDATETIME()),
    updated_at      DATETIME2(0)     NULL,

    CONSTRAINT PK_Pets PRIMARY KEY (pet_id),
    CONSTRAINT FK_Pets_Users FOREIGN KEY (user_id)
        REFERENCES dbo.Users(user_id)
        ON DELETE CASCADE,
    CONSTRAINT CK_Pets_Type CHECK (pet_type IN ('DOG', 'CAT', 'OTHER')),
    CONSTRAINT CK_Pets_Gender CHECK (gender IN ('MALE', 'FEMALE', 'UNKNOWN')),
    CONSTRAINT CK_Pets_Age CHECK (age_months IS NULL OR age_months BETWEEN 0 AND 600),
    CONSTRAINT CK_Pets_Weight CHECK (weight_kg IS NULL OR weight_kg > 0),
    CONSTRAINT CK_Pets_BodySize CHECK (body_size IS NULL OR body_size IN ('SMALL', 'MEDIUM', 'LARGE')),
    CONSTRAINT CK_Pets_ActivityLevel CHECK (activity_level IS NULL OR activity_level IN ('LOW', 'MODERATE', 'HIGH')),
    CONSTRAINT CK_Pets_BodyConditionScore CHECK (body_condition_score IS NULL OR body_condition_score BETWEEN 1 AND 9)
);
GO

/* ================================================================
   3. CATEGORIES
   ================================================================ */
CREATE TABLE dbo.Categories
(
    category_id     INT IDENTITY(1,1) NOT NULL,
    category_name   NVARCHAR(100)    NOT NULL,
    image_url       NVARCHAR(1000)   NULL,
    description     NVARCHAR(500)    NULL,
    sort_order      INT              NOT NULL
        CONSTRAINT DF_Categories_SortOrder DEFAULT (0),
    is_active       BIT              NOT NULL
        CONSTRAINT DF_Categories_IsActive DEFAULT (1),
    created_at      DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Categories_CreatedAt DEFAULT (SYSUTCDATETIME()),
    updated_at      DATETIME2(0)     NULL,

    CONSTRAINT PK_Categories PRIMARY KEY (category_id),
    CONSTRAINT UQ_Categories_Name UNIQUE (category_name),
    CONSTRAINT CK_Categories_SortOrder CHECK (sort_order >= 0)
);
GO

/* ================================================================
   4. PRODUCTS
   ================================================================ */
CREATE TABLE dbo.Products
(
    product_id               INT IDENTITY(1,1) NOT NULL,
    category_id              INT              NOT NULL,
    sku                      VARCHAR(50)      NULL,
    product_name             NVARCHAR(150)    NOT NULL,
    brand                    NVARCHAR(100)    NULL,
    description              NVARCHAR(MAX)    NULL,
    ingredients              NVARCHAR(MAX)    NULL,
    nutrition_info           NVARCHAR(MAX)    NULL,
    package_size             NVARCHAR(100)    NULL,
    life_stage               NVARCHAR(100)    NULL,
    feeding_guide            NVARCHAR(MAX)    NULL,
    price                    DECIMAL(18,2)    NOT NULL,
    stock_quantity            INT              NOT NULL
        CONSTRAINT DF_Products_StockQuantity DEFAULT (0),
    low_stock_threshold       INT              NOT NULL
        CONSTRAINT DF_Products_LowStockThreshold DEFAULT (5),
    target_pet_type           VARCHAR(20)      NOT NULL
        CONSTRAINT DF_Products_TargetPetType DEFAULT ('ALL'),
    is_featured               BIT              NOT NULL
        CONSTRAINT DF_Products_IsFeatured DEFAULT (0),
    is_active                 BIT              NOT NULL
        CONSTRAINT DF_Products_IsActive DEFAULT (1),
    is_listed                 BIT              NOT NULL
        CONSTRAINT DF_Products_IsListed DEFAULT (1),
    created_at                DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Products_CreatedAt DEFAULT (SYSUTCDATETIME()),
    updated_at                DATETIME2(0)     NULL,

    CONSTRAINT PK_Products PRIMARY KEY (product_id),
    CONSTRAINT FK_Products_Categories FOREIGN KEY (category_id)
        REFERENCES dbo.Categories(category_id),
    CONSTRAINT CK_Products_Price CHECK (price >= 0),
    CONSTRAINT CK_Products_Stock CHECK (stock_quantity >= 0),
    CONSTRAINT CK_Products_LowStock CHECK (low_stock_threshold >= 0),
    CONSTRAINT CK_Products_TargetPetType CHECK (target_pet_type IN ('DOG', 'CAT', 'ALL', 'OTHER'))
);
GO

/* SKU is optional, but when present it must be unique.
   A filtered unique index avoids SQL Server's single-NULL behavior. */
CREATE UNIQUE INDEX UX_Products_SKU_NotNull
ON dbo.Products(sku)
WHERE sku IS NOT NULL;
GO

/* ================================================================
   5. PRODUCT IMAGES
   ================================================================ */
CREATE TABLE dbo.Product_Images
(
    image_id        INT IDENTITY(1,1) NOT NULL,
    product_id      INT              NOT NULL,
    image_url       NVARCHAR(1000)   NOT NULL,
    is_primary      BIT              NOT NULL
        CONSTRAINT DF_ProductImages_IsPrimary DEFAULT (0),
    display_order   INT              NOT NULL
        CONSTRAINT DF_ProductImages_DisplayOrder DEFAULT (0),
    created_at      DATETIME2(0)     NOT NULL
        CONSTRAINT DF_ProductImages_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_ProductImages PRIMARY KEY (image_id),
    CONSTRAINT FK_ProductImages_Products FOREIGN KEY (product_id)
        REFERENCES dbo.Products(product_id)
        ON DELETE CASCADE,
    CONSTRAINT CK_ProductImages_DisplayOrder CHECK (display_order >= 0)
);
GO

/* Only one primary image per product. */
CREATE UNIQUE INDEX UX_ProductImages_OnePrimary
ON dbo.Product_Images(product_id)
WHERE is_primary = 1;
GO

CREATE UNIQUE INDEX UX_ProductImages_ProductDisplayOrder
ON dbo.Product_Images(product_id, display_order);
GO

IF OBJECT_ID(N'dbo.Catalog_Subcategories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Catalog_Subcategories
    (
        subcategory_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Catalog_Subcategories PRIMARY KEY,
        category_id INT NOT NULL,
        subcategory_name NVARCHAR(100) NOT NULL,
        CONSTRAINT FK_Catalog_Subcategories_Categories FOREIGN KEY (category_id) REFERENCES dbo.Categories(category_id),
        CONSTRAINT UQ_Catalog_Subcategories_Category_Name UNIQUE (category_id, subcategory_name)
    );
END;
GO

IF OBJECT_ID(N'dbo.Catalog_Product_Reviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Catalog_Product_Reviews
    (
        product_id INT NOT NULL CONSTRAINT PK_Catalog_Product_Reviews PRIMARY KEY,
        slug VARCHAR(180) NOT NULL,
        subcategory_id INT NOT NULL,
        model_name NVARCHAR(150) NOT NULL,
        short_description NVARCHAR(500) NULL,
        benefits NVARCHAR(MAX) NULL,
        usage_instructions NVARCHAR(MAX) NULL,
        warnings NVARCHAR(MAX) NULL,
        storage_instructions NVARCHAR(MAX) NULL,
        official_url NVARCHAR(1000) NOT NULL,
        model_status VARCHAR(20) NOT NULL,
        image_source_url NVARCHAR(1000) NULL,
        image_status VARCHAR(20) NOT NULL CONSTRAINT DF_Catalog_Product_Reviews_ImageStatus DEFAULT ('NEEDS_REVIEW'),
        price_source_url NVARCHAR(1000) NULL,
        price_checked_at DATETIME2(0) NULL,
        price_status VARCHAR(20) NOT NULL CONSTRAINT DF_Catalog_Product_Reviews_PriceStatus DEFAULT ('NEEDS_REVIEW'),
        product_status VARCHAR(20) NOT NULL CONSTRAINT DF_Catalog_Product_Reviews_ProductStatus DEFAULT ('NEEDS_REVIEW'),
        public_preview BIT NOT NULL CONSTRAINT DF_Catalog_Product_Reviews_PublicPreview DEFAULT (0),
        review_note NVARCHAR(1000) NULL,
        reviewed_at DATETIME2(0) NULL,
        CONSTRAINT FK_Catalog_Product_Reviews_Products FOREIGN KEY (product_id) REFERENCES dbo.Products(product_id),
        CONSTRAINT FK_Catalog_Product_Reviews_Subcategories FOREIGN KEY (subcategory_id) REFERENCES dbo.Catalog_Subcategories(subcategory_id),
        CONSTRAINT UQ_Catalog_Product_Reviews_Slug UNIQUE (slug),
        CONSTRAINT CK_Catalog_Product_Reviews_ModelStatus CHECK (model_status IN ('VERIFIED','NEEDS_REVIEW')),
        CONSTRAINT CK_Catalog_Product_Reviews_ImageStatus CHECK (image_status IN ('VERIFIED','NEEDS_REVIEW')),
        CONSTRAINT CK_Catalog_Product_Reviews_PriceStatus CHECK (price_status IN ('VERIFIED','NEEDS_REVIEW','OWNER_SET')),
        CONSTRAINT CK_Catalog_Product_Reviews_ProductStatus CHECK (product_status IN ('VERIFIED','NEEDS_REVIEW','OWNER_APPROVED'))
    );
END;
GO

IF OBJECT_ID(N'dbo.Catalog_Product_Variants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Catalog_Product_Variants
    (
        variant_id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Catalog_Product_Variants PRIMARY KEY,
        product_id INT NOT NULL,
        variant_key VARCHAR(80) NOT NULL,
        label NVARCHAR(100) NOT NULL,
        price_vnd DECIMAL(18,2) NULL,
        price_source_url NVARCHAR(1000) NULL,
        price_checked_at DATETIME2(0) NULL,
        price_status VARCHAR(20) NOT NULL CONSTRAINT DF_Catalog_Product_Variants_PriceStatus DEFAULT ('NEEDS_REVIEW'),
        CONSTRAINT FK_Catalog_Product_Variants_Products FOREIGN KEY (product_id) REFERENCES dbo.Products(product_id),
        CONSTRAINT UQ_Catalog_Product_Variants_Product_Key UNIQUE (product_id, variant_key),
        CONSTRAINT CK_Catalog_Product_Variants_Price CHECK (price_vnd IS NULL OR price_vnd > 0),
        CONSTRAINT CK_Catalog_Product_Variants_Status CHECK (price_status IN ('VERIFIED','NEEDS_REVIEW')),
        CONSTRAINT CK_Catalog_Product_Variants_Verification CHECK (price_status <> 'VERIFIED' OR (price_vnd IS NOT NULL AND price_source_url IS NOT NULL AND price_checked_at IS NOT NULL))
    );
END;
GO

/* ================================================================
   6. CART ITEMS
   ================================================================ */
CREATE TABLE dbo.CartItems
(
    cart_item_id    INT IDENTITY(1,1) NOT NULL,
    user_id         VARCHAR(50)      NOT NULL,
    product_id      INT              NOT NULL,
    quantity        INT              NOT NULL
        CONSTRAINT DF_CartItems_Quantity DEFAULT (1),
    created_at      DATETIME2(0)     NOT NULL
        CONSTRAINT DF_CartItems_CreatedAt DEFAULT (SYSUTCDATETIME()),
    updated_at      DATETIME2(0)     NULL,

    CONSTRAINT PK_CartItems PRIMARY KEY (cart_item_id),
    CONSTRAINT FK_CartItems_Users FOREIGN KEY (user_id)
        REFERENCES dbo.Users(user_id)
        ON DELETE CASCADE,
    CONSTRAINT FK_CartItems_Products FOREIGN KEY (product_id)
        REFERENCES dbo.Products(product_id),
    CONSTRAINT UQ_CartItems_UserProduct UNIQUE (user_id, product_id),
    CONSTRAINT CK_CartItems_Quantity CHECK (quantity > 0)
);
GO

/* ================================================================
   7. FAVORITES
   ================================================================ */
CREATE TABLE dbo.Favorites
(
    favorite_id     INT IDENTITY(1,1) NOT NULL,
    user_id         VARCHAR(50)      NOT NULL,
    product_id      INT              NOT NULL,
    created_at      DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Favorites_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_Favorites PRIMARY KEY (favorite_id),
    CONSTRAINT FK_Favorites_Users FOREIGN KEY (user_id)
        REFERENCES dbo.Users(user_id)
        ON DELETE CASCADE,
    CONSTRAINT FK_Favorites_Products FOREIGN KEY (product_id)
        REFERENCES dbo.Products(product_id)
        ON DELETE CASCADE,
    CONSTRAINT UQ_Favorites_UserProduct UNIQUE (user_id, product_id)
);
GO

/* ================================================================
   8. STORES / WAREHOUSES
   ================================================================ */
CREATE TABLE dbo.Stores
(
    store_id        INT IDENTITY(1,1) NOT NULL,
    store_name      NVARCHAR(150)    NOT NULL,
    store_type      VARCHAR(20)      NOT NULL
        CONSTRAINT DF_Stores_StoreType DEFAULT ('STORE'),
    address         NVARCHAR(500)    NOT NULL,
    latitude        DECIMAL(10,7)    NOT NULL,
    longitude       DECIMAL(10,7)    NOT NULL,
    phone_number    VARCHAR(20)      NULL,
    is_active       BIT              NOT NULL
        CONSTRAINT DF_Stores_IsActive DEFAULT (1),
    created_at      DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Stores_CreatedAt DEFAULT (SYSUTCDATETIME()),
    updated_at      DATETIME2(0)     NULL,

    CONSTRAINT PK_Stores PRIMARY KEY (store_id),
    CONSTRAINT CK_Stores_Type CHECK (store_type IN ('STORE', 'WAREHOUSE')),
    CONSTRAINT CK_Stores_Latitude CHECK (latitude BETWEEN -90 AND 90),
    CONSTRAINT CK_Stores_Longitude CHECK (longitude BETWEEN -180 AND 180)
);
GO

/* ================================================================
   9. PROMOTIONS / VOUCHERS
   ================================================================ */
CREATE TABLE dbo.Promotions
(
    promotion_id        INT IDENTITY(1,1) NOT NULL,
    code                 VARCHAR(50)      NOT NULL,
    promotion_name      NVARCHAR(150)    NOT NULL,
    description         NVARCHAR(500)    NULL,
    discount_type       VARCHAR(20)      NOT NULL,
    discount_value      DECIMAL(18,2)    NOT NULL,
    min_order_amount    DECIMAL(18,2)    NOT NULL
        CONSTRAINT DF_Promotions_MinOrderAmount DEFAULT (0),
    max_discount_amount DECIMAL(18,2)    NULL,
    start_at             DATETIME2(0)     NOT NULL,
    end_at               DATETIME2(0)     NOT NULL,
    usage_limit          INT              NULL,
    used_count           INT              NOT NULL
        CONSTRAINT DF_Promotions_UsedCount DEFAULT (0),
    is_active            BIT              NOT NULL
        CONSTRAINT DF_Promotions_IsActive DEFAULT (1),
    created_at           DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Promotions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    updated_at           DATETIME2(0)     NULL,

    CONSTRAINT PK_Promotions PRIMARY KEY (promotion_id),
    CONSTRAINT UQ_Promotions_Code UNIQUE (code),
    CONSTRAINT CK_Promotions_DiscountType CHECK (discount_type IN ('PERCENT', 'FIXED')),
    CONSTRAINT CK_Promotions_DiscountValue CHECK
    (
        (discount_type = 'PERCENT' AND discount_value BETWEEN 0 AND 100)
        OR
        (discount_type = 'FIXED' AND discount_value >= 0)
    ),
    CONSTRAINT CK_Promotions_MinOrder CHECK (min_order_amount >= 0),
    CONSTRAINT CK_Promotions_MaxDiscount CHECK
    (
        max_discount_amount IS NULL OR max_discount_amount >= 0
    ),
    CONSTRAINT CK_Promotions_Dates CHECK (start_at < end_at),
    CONSTRAINT CK_Promotions_UsageLimit CHECK
    (
        usage_limit IS NULL OR usage_limit >= 0
    ),
    CONSTRAINT CK_Promotions_UsedCount CHECK (used_count >= 0)
);
GO

/* ================================================================
   10. ORDERS
   ================================================================ */
CREATE TABLE dbo.Orders
(
    order_id                 VARCHAR(50)      NOT NULL,
    user_id                  VARCHAR(50)      NOT NULL,
    store_id                 INT              NULL,

    subtotal_amount          DECIMAL(18,2)    NOT NULL,
    discount_amount          DECIMAL(18,2)    NOT NULL
        CONSTRAINT DF_Orders_DiscountAmount DEFAULT (0),
    shipping_fee             DECIMAL(18,2)    NOT NULL
        CONSTRAINT DF_Orders_ShippingFee DEFAULT (0),
    grand_total              DECIMAL(18,2)    NOT NULL,

    promotion_id             INT              NULL,
    promotion_code_snapshot  VARCHAR(50)      NULL,

    shipping_address         NVARCHAR(500)    NOT NULL,
    recipient_name           NVARCHAR(100)    NOT NULL,
    recipient_phone          VARCHAR(20)      NOT NULL,
    latitude                 DECIMAL(10,7)    NULL,
    longitude                DECIMAL(10,7)    NULL,
    distance_km              DECIMAL(10,2)    NULL,

    payment_method           VARCHAR(30)      NOT NULL
        CONSTRAINT DF_Orders_PaymentMethod DEFAULT ('COD'),
    order_status             VARCHAR(30)      NOT NULL
        CONSTRAINT DF_Orders_OrderStatus DEFAULT ('PENDING'),

    note                     NVARCHAR(500)    NULL,
    created_at               DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Orders_CreatedAt DEFAULT (SYSUTCDATETIME()),
    updated_at               DATETIME2(0)     NULL,
    confirmed_at             DATETIME2(0)     NULL,
    shipped_at               DATETIME2(0)     NULL,
    delivered_at             DATETIME2(0)     NULL,
    cancelled_at             DATETIME2(0)     NULL,

    CONSTRAINT PK_Orders PRIMARY KEY (order_id),
    CONSTRAINT FK_Orders_Users FOREIGN KEY (user_id)
        REFERENCES dbo.Users(user_id),
    CONSTRAINT FK_Orders_Stores FOREIGN KEY (store_id)
        REFERENCES dbo.Stores(store_id),
    CONSTRAINT FK_Orders_Promotions FOREIGN KEY (promotion_id)
        REFERENCES dbo.Promotions(promotion_id)
        ON DELETE SET NULL,

    CONSTRAINT CK_Orders_Subtotal CHECK (subtotal_amount >= 0),
    CONSTRAINT CK_Orders_Discount CHECK (discount_amount >= 0),
    CONSTRAINT CK_Orders_Shipping CHECK (shipping_fee >= 0),
    CONSTRAINT CK_Orders_Distance CHECK (distance_km IS NULL OR distance_km >= 0),
    CONSTRAINT CK_Orders_GrandTotal CHECK
    (
        grand_total = subtotal_amount - discount_amount + shipping_fee
        AND grand_total >= 0
    ),
    CONSTRAINT CK_Orders_Coordinates CHECK
    (
        (latitude IS NULL AND longitude IS NULL)
        OR
        (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)
    ),
    CONSTRAINT CK_Orders_PaymentMethod CHECK
    (
        payment_method IN ('COD', 'BANK_QR', 'VNPAY', 'MOMO', 'ZALOPAY')
    ),
    CONSTRAINT CK_Orders_Status CHECK
    (
        order_status IN
        (
            'PENDING',
            'CONFIRMED',
            'SHIPPING',
            'DELIVERED',
            'CANCELLED'
        )
    )
);
GO

/* ================================================================
   11. ORDER ITEMS
   ================================================================ */
CREATE TABLE dbo.OrderItems
(
    order_item_id   INT IDENTITY(1,1) NOT NULL,
    order_id        VARCHAR(50)      NOT NULL,
    product_id      INT              NOT NULL,
    quantity        INT              NOT NULL,
    unit_price      DECIMAL(18,2)    NOT NULL,
    line_total      AS (quantity * unit_price) PERSISTED,

    CONSTRAINT PK_OrderItems PRIMARY KEY (order_item_id),
    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (order_id)
        REFERENCES dbo.Orders(order_id)
        ON DELETE CASCADE,
    CONSTRAINT FK_OrderItems_Products FOREIGN KEY (product_id)
        REFERENCES dbo.Products(product_id),
    CONSTRAINT CK_OrderItems_Quantity CHECK (quantity > 0),
    CONSTRAINT CK_OrderItems_UnitPrice CHECK (unit_price >= 0)
);
GO

/* ================================================================
   12. ORDER STATUS HISTORY
   ================================================================ */
CREATE TABLE dbo.OrderStatusHistory
(
    history_id      INT IDENTITY(1,1) NOT NULL,
    order_id        VARCHAR(50)      NOT NULL,
    status          VARCHAR(30)      NOT NULL,
    note            NVARCHAR(500)    NULL,
    changed_by      VARCHAR(50)      NULL,
    created_at      DATETIME2(0)     NOT NULL
        CONSTRAINT DF_OrderStatusHistory_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_OrderStatusHistory PRIMARY KEY (history_id),
    CONSTRAINT FK_OrderStatusHistory_Orders FOREIGN KEY (order_id)
        REFERENCES dbo.Orders(order_id)
        ON DELETE CASCADE,
    CONSTRAINT FK_OrderStatusHistory_Users FOREIGN KEY (changed_by)
        REFERENCES dbo.Users(user_id),
    CONSTRAINT CK_OrderStatusHistory_Status CHECK
    (
        status IN
        (
            'PENDING',
            'CONFIRMED',
            'SHIPPING',
            'DELIVERED',
            'CANCELLED'
        )
    )
);
GO

/* ================================================================
   13. PAYMENTS
   ================================================================ */
CREATE TABLE dbo.Payments
(
    payment_id              VARCHAR(50)      NOT NULL,
    order_id                VARCHAR(50)      NOT NULL,
    payment_method          VARCHAR(30)      NOT NULL,
    payment_status          VARCHAR(20)      NOT NULL
        CONSTRAINT DF_Payments_Status DEFAULT ('PENDING'),
    amount                  DECIMAL(18,2)    NOT NULL,
    transaction_code        VARCHAR(150)     NULL,
    provider_transaction_id VARCHAR(150)     NULL,
    gateway_response        NVARCHAR(MAX)    NULL,
    bank_bin                VARCHAR(6)       NULL,
    bank_name               NVARCHAR(100)    NULL,
    bank_account_no         VARCHAR(30)      NULL,
    bank_account_name       NVARCHAR(100)    NULL,
    paid_at                 DATETIME2(0)     NULL,
    created_at              DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Payments_CreatedAt DEFAULT (SYSUTCDATETIME()),
    updated_at              DATETIME2(0)     NULL,

    CONSTRAINT PK_Payments PRIMARY KEY (payment_id),
    CONSTRAINT UQ_Payments_Order UNIQUE (order_id),
    CONSTRAINT FK_Payments_Orders FOREIGN KEY (order_id)
        REFERENCES dbo.Orders(order_id)
        ON DELETE CASCADE,
    CONSTRAINT CK_Payments_Method CHECK
    (
        payment_method IN ('COD', 'BANK_QR', 'VNPAY', 'MOMO', 'ZALOPAY')
    ),
    CONSTRAINT CK_Payments_Status CHECK
    (
        payment_status IN ('PENDING', 'SUCCESS', 'FAILED', 'CANCELLED')
    ),
    CONSTRAINT CK_Payments_Amount CHECK (amount >= 0),
    CONSTRAINT CK_Payments_BankQrDetails CHECK
    (
        payment_method <> 'BANK_QR' OR
        (bank_bin IS NOT NULL AND bank_name IS NOT NULL AND bank_account_no IS NOT NULL
         AND bank_account_name IS NOT NULL AND transaction_code IS NOT NULL
         AND amount > 0 AND amount = FLOOR(amount))
    )
);
GO
CREATE UNIQUE INDEX UX_Payments_BankQr_BankTransaction
ON dbo.Payments(provider_transaction_id)
WHERE payment_method = 'BANK_QR' AND provider_transaction_id IS NOT NULL;
GO

CREATE TABLE dbo.BankQrWebhookEvents
(
    provider_event_id BIGINT NOT NULL CONSTRAINT PK_BankQrWebhookEvents PRIMARY KEY,
    order_id VARCHAR(50) NULL,
    transfer_code VARCHAR(150) NULL,
    account_number VARCHAR(30) NOT NULL,
    gateway VARCHAR(100) NULL,
    reference_code VARCHAR(150) NULL,
    amount DECIMAL(18,2) NOT NULL,
    status VARCHAR(30) NOT NULL,
    created_at DATETIME2(0) NOT NULL CONSTRAINT DF_BankQrWebhookEvents_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_BankQrWebhookEvents_Amount CHECK (amount > 0),
    CONSTRAINT CK_BankQrWebhookEvents_Status CHECK
    (
        status IN ('UNMATCHED', 'OUTGOING', 'DUPLICATE_REFERENCE', 'ACCOUNT_MISMATCH',
            'CANCELLED_ORDER', 'ALREADY_PAID', 'PAYMENT_CONFLICT', 'PARTIAL', 'APPLIED')
    )
);
GO
CREATE INDEX IX_BankQrWebhookEvents_OrderId ON dbo.BankQrWebhookEvents(order_id);
GO
CREATE INDEX IX_BankQrWebhookEvents_GatewayReference
ON dbo.BankQrWebhookEvents(gateway, reference_code)
WHERE reference_code IS NOT NULL;
GO

/* ================================================================
   14. ORDER REVIEWS
   ================================================================ */
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
GO
CREATE INDEX IX_OrderReviews_VisibleCreated ON dbo.OrderReviews(is_visible, created_at DESC);
GO

/* ================================================================
   15. NOTIFICATIONS
   ================================================================ */
CREATE TABLE dbo.Notifications
(
    notification_id   INT IDENTITY(1,1) NOT NULL,
    user_id           VARCHAR(50)      NOT NULL,
    title             NVARCHAR(150)    NOT NULL,
    message           NVARCHAR(1000)   NOT NULL,
    notification_type VARCHAR(30)      NULL,
    reference_id      VARCHAR(50)      NULL,
    is_read           BIT              NOT NULL
        CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
    created_at        DATETIME2(0)     NOT NULL
        CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_Notifications PRIMARY KEY (notification_id),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (user_id)
        REFERENCES dbo.Users(user_id)
        ON DELETE CASCADE
);
GO

/* ================================================================
   16. ORDER STATUS HISTORY TRIGGER
   Automatically records initial status and every later status change.
   changed_by remains NULL because the database trigger cannot reliably infer
   the authenticated API user without using SQL SESSION_CONTEXT. The API can
   optionally populate history rows itself with changed_by when required.
   ================================================================ */
CREATE TRIGGER dbo.trg_Orders_OrderStatusHistory
ON dbo.Orders
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.OrderStatusHistory
    (
        order_id,
        status,
        note,
        changed_by,
        created_at
    )
    SELECT
        i.order_id,
        i.order_status,
        CASE
            WHEN d.order_id IS NULL THEN N'Order created'
            ELSE N'Order status changed'
        END,
        NULL,
        SYSUTCDATETIME()
    FROM inserted i
    LEFT JOIN deleted d
        ON d.order_id = i.order_id
    WHERE d.order_id IS NULL
       OR ISNULL(i.order_status, '') <> ISNULL(d.order_status, '');
END;
GO

/* ================================================================
   16. INDEXES
   ================================================================ */

/* Users */
CREATE UNIQUE INDEX UX_Users_GoogleSubject
    ON dbo.Users(google_subject)
    WHERE google_subject IS NOT NULL;
GO

CREATE INDEX IX_Users_Role_IsActive
    ON dbo.Users(role, is_active);
GO

/* Pets */
CREATE INDEX IX_Pets_UserId
    ON dbo.Pets(user_id);
GO

/* Categories */
CREATE INDEX IX_Categories_ActiveSort
    ON dbo.Categories(is_active, sort_order);
GO

/* Products */
CREATE INDEX IX_Products_Category_Active
    ON dbo.Products(category_id, is_active);
GO

CREATE INDEX IX_Products_TargetPetType_Active
    ON dbo.Products(target_pet_type, is_active);
GO

CREATE INDEX IX_Products_Brand_Active
    ON dbo.Products(brand, is_active);
GO

CREATE INDEX IX_Products_Featured_Active
    ON dbo.Products(is_featured, is_active);
GO

/* SQL Server Full-Text Search is optional. This normal index supports
   common search/sort operations; API can also use LIKE/CONTAINS where needed. */
CREATE INDEX IX_Products_Name
    ON dbo.Products(product_name);
GO

/* Cart */
CREATE INDEX IX_CartItems_UserId
    ON dbo.CartItems(user_id);
GO

/* Favorites */
CREATE INDEX IX_Favorites_UserId
    ON dbo.Favorites(user_id);
GO

/* Stores */
CREATE INDEX IX_Stores_Type_Active
    ON dbo.Stores(store_type, is_active);
GO

/* Promotions */
CREATE INDEX IX_Promotions_ActiveDates
    ON dbo.Promotions(is_active, start_at, end_at);
GO

/* Orders */
CREATE INDEX IX_Orders_User_CreatedAt
    ON dbo.Orders(user_id, created_at DESC);
GO

CREATE INDEX IX_Orders_Status_CreatedAt
    ON dbo.Orders(order_status, created_at DESC);
GO

CREATE INDEX IX_Orders_StoreId
    ON dbo.Orders(store_id);
GO

/* Order items */
CREATE INDEX IX_OrderItems_OrderId
    ON dbo.OrderItems(order_id);
GO

CREATE INDEX IX_OrderItems_ProductId
    ON dbo.OrderItems(product_id);
GO

/* Order status history */
CREATE INDEX IX_OrderStatusHistory_OrderId_CreatedAt
    ON dbo.OrderStatusHistory(order_id, created_at ASC);
GO

/* Payments */
CREATE INDEX IX_Payments_Status_CreatedAt
    ON dbo.Payments(payment_status, created_at DESC);
GO

CREATE INDEX IX_Payments_TransactionCode
    ON dbo.Payments(transaction_code);
GO

/* Notifications */
CREATE INDEX IX_Notifications_User_Read_CreatedAt
    ON dbo.Notifications(user_id, is_read, created_at DESC);
GO

/* ================================================================
   17. USEFUL VIEWS FOR API / ADMIN DASHBOARD
   ================================================================ */

/* Product stock status */
CREATE VIEW dbo.vw_ProductStockStatus
AS
SELECT
    p.product_id,
    p.sku,
    p.product_name,
    p.stock_quantity,
    p.low_stock_threshold,
    CASE
        WHEN p.stock_quantity = 0 THEN 'OUT_OF_STOCK'
        WHEN p.stock_quantity <= p.low_stock_threshold THEN 'LOW_STOCK'
        ELSE 'IN_STOCK'
    END AS stock_status
FROM dbo.Products p;
GO

/* Simple order totals for admin reporting */
CREATE VIEW dbo.vw_OrderSummary
AS
SELECT
    o.order_id,
    o.user_id,
    u.full_name AS customer_name,
    o.store_id,
    o.subtotal_amount,
    o.discount_amount,
    o.shipping_fee,
    o.grand_total,
    o.payment_method,
    o.order_status,
    o.created_at,
    o.delivered_at
FROM dbo.Orders o
INNER JOIN dbo.Users u ON u.user_id = o.user_id;
GO

/* ================================================================
   18. SAMPLE MASTER DATA
   Safe seed: categories + one store/warehouse.
   No admin account is seeded because a real PasswordHash should come
   from the authentication system / BCrypt implementation in the API.
   ================================================================ */

INSERT INTO dbo.Categories
(
    category_name,
    image_url,
    description,
    sort_order,
    is_active
)
VALUES
(N'Dog Food', NULL, N'Thức ăn dành cho chó', 1, 1),
(N'Cat Food', NULL, N'Thức ăn dành cho mèo', 2, 1),
(N'Treats', NULL, N'Bánh thưởng và đồ ăn vặt cho thú cưng', 3, 1),
(N'Accessories', NULL, N'Phụ kiện chăm sóc thú cưng', 4, 1);
GO

/* Demo storefront: the pin is near Tan Lap 1, not a verified house entrance. */
INSERT INTO dbo.Stores
(
    store_name,
    store_type,
    address,
    latitude,
    longitude,
    phone_number,
    is_active
)
VALUES
(
    N'PawMart Pet Store',
    'STORE',
    N'46/2 Tân Lập 1, phường Tăng Nhơn Phú, Thành phố Hồ Chí Minh, Việt Nam',
    10.8483153,
    106.7773630,
    NULL,
    1
),
(
    N'PetFood Main Warehouse',
    'WAREHOUSE',
    N'Ho Chi Minh City, Vietnam',
    10.8500000,
    106.7000000,
    NULL,
    1
);
GO

/* ================================================================
   19. EXAMPLE ADMIN / STATISTICS QUERIES
   These are examples for API implementation and verification.
   ================================================================ */

/* Total revenue from delivered orders */
-- SELECT SUM(grand_total) AS total_revenue
-- FROM dbo.Orders
-- WHERE order_status = 'DELIVERED';

/* Today's revenue */
-- SELECT SUM(grand_total) AS today_revenue
-- FROM dbo.Orders
-- WHERE order_status = 'DELIVERED'
--   AND CAST(created_at AS DATE) = CAST(SYSUTCDATETIME() AS DATE);

/* Order counts by status */
-- SELECT order_status, COUNT(*) AS order_count
-- FROM dbo.Orders
-- GROUP BY order_status;

/* Low-stock products */
-- SELECT *
-- FROM dbo.vw_ProductStockStatus
-- WHERE stock_status IN ('LOW_STOCK', 'OUT_OF_STOCK')
-- ORDER BY stock_quantity ASC;

/* Best-selling products */
-- SELECT TOP 10
--     oi.product_id,
--     p.product_name,
--     SUM(oi.quantity) AS quantity_sold,
--     SUM(oi.line_total) AS sales_amount
-- FROM dbo.OrderItems oi
-- INNER JOIN dbo.Orders o ON o.order_id = oi.order_id
-- INNER JOIN dbo.Products p ON p.product_id = oi.product_id
-- WHERE o.order_status = 'DELIVERED'
-- GROUP BY oi.product_id, p.product_name
-- ORDER BY quantity_sold DESC;

/* ================================================================
   20. RELATIONSHIP SUMMARY
   ================================================================

   Users 1 --- N Pets
   Users 1 --- N CartItems N --- 1 Products
   Users 1 --- N Favorites N --- 1 Products
   Categories 1 --- N Products
   Products 1 --- N Product_Images
   Users 1 --- N Orders
   Stores 1 --- N Orders
   Promotions 1 --- N Orders (optional)
   Orders 1 --- N OrderItems N --- 1 Products
   Orders 1 --- N OrderStatusHistory
   Orders 1 --- 1 Payments
   Users 1 --- N Notifications

   MAP:
       Stores.latitude/longitude       -> store / warehouse pins
       Orders.latitude/longitude       -> delivery location
       Orders.shipping_address         -> address snapshot
       Orders.distance_km              -> distance snapshot
       Orders.shipping_fee             -> calculated delivery fee snapshot

   PAYMENT:
       Orders.payment_method
       Payments.payment_method
       Payments.payment_status
       Payments.transaction_code

   FCM:
       Users.fcm_token
       Notifications

   AI:
       Pets.pet_type / breed / age / weight / body_size / activity_level
       / body_condition_score / feeding_habits / food_preferences / special_notes
       + Products.target_pet_type / description / ingredients / nutrition_info
       -> API sends context to Gemini/OpenAI
       -> AI returns recommendation

   ADMIN:
       Product CRUD             -> Products + Product_Images
       Category CRUD            -> Categories
       Order management         -> Orders + OrderItems + OrderStatusHistory
       Promotion management     -> Promotions
       Push notification        -> Users.fcm_token + Notifications
       Store map                -> Stores
       Revenue/statistics       -> Orders + OrderItems + Payments

====================================================================
 END OF PETFOOD PRM393 DATABASE SCRIPT
====================================================================
*/
