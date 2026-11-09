USE PetFoodDB;
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
