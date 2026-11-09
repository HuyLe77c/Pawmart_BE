USE PetFoodDB;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Catalog_Product_Reviews', N'U') IS NULL
    THROW 51051, 'Catalog review schema is missing.', 1;

IF OBJECT_ID(N'dbo.CK_Catalog_Product_Reviews_PriceStatus', N'C') IS NOT NULL
    ALTER TABLE dbo.Catalog_Product_Reviews DROP CONSTRAINT CK_Catalog_Product_Reviews_PriceStatus;
IF OBJECT_ID(N'dbo.CK_Catalog_Product_Reviews_ProductStatus', N'C') IS NOT NULL
    ALTER TABLE dbo.Catalog_Product_Reviews DROP CONSTRAINT CK_Catalog_Product_Reviews_ProductStatus;
ALTER TABLE dbo.Catalog_Product_Reviews WITH CHECK ADD CONSTRAINT CK_Catalog_Product_Reviews_PriceStatus
    CHECK (price_status IN ('VERIFIED','NEEDS_REVIEW','OWNER_SET'));
ALTER TABLE dbo.Catalog_Product_Reviews WITH CHECK ADD CONSTRAINT CK_Catalog_Product_Reviews_ProductStatus
    CHECK (product_status IN ('VERIFIED','NEEDS_REVIEW','OWNER_APPROVED'));

DECLARE @OwnerPrices TABLE
(
    code VARCHAR(3) NOT NULL PRIMARY KEY,
    package_size NVARCHAR(100) NOT NULL,
    price_vnd DECIMAL(18,2) NOT NULL,
    initial_stock INT NOT NULL
);
INSERT INTO @OwnerPrices (code, package_size, price_vnd, initial_stock)
VALUES
    ('A01', N'800 g', 199000, 120),
    ('A02', N'400 g', 139000, 120),
    ('A03', N'480 g', 69000, 120),
    ('A04', N'1.5 kg', 229000, 120),
    ('A05', N'800 g', 219000, 120),
    ('A06', N'1 kg', 269000, 120),
    ('A07', N'400 g', 159000, 120),
    ('A08', N'400 g', 149000, 120),
    ('B01', N'1 chiếc', 189000, 120),
    ('B02', N'1 chiếc', 229000, 120),
    ('B03', N'1 gói', 169000, 120),
    ('B04', N'1 chiếc', 249000, 120),
    ('B05', N'1 chiếc', 209000, 120),
    ('B06', N'1 chiếc', 219000, 120),
    ('B07', N'1 chiếc', 229000, 120),
    ('B08', N'1 chiếc', 229000, 120),
    ('C01', N'1 chiếc', 399000, 120),
    ('C02', N'1 chiếc', 499000, 120),
    ('C03', N'1 tuýp', 239000, 120),
    ('C04', N'1 chiếc', 299000, 120),
    ('C05', N'1 chiếc', 219000, 120),
    ('C06', N'1 chiếc', 649000, 120),
    ('C07', N'1 chiếc', 149000, 120),
    ('C08', N'1 chiếc', 269000, 120),
    ('C09', N'1 chiếc', 289000, 120),
    ('C10', N'1 chiếc', 339000, 120),
    ('C11', N'1 chiếc', 449000, 120),
    ('D01', N'1 lọ', 699000, 120),
    ('D02', N'1 lọ', 749000, 120),
    ('D03', N'1 chai', 599000, 120),
    ('D04', N'1 lọ', 749000, 120),
    ('D05', N'1 lọ', 799000, 120),
    ('D06', N'1 lọ', 729000, 120),
    ('D07', N'1 lọ', 699000, 120),
    ('D08', N'1 lọ', 729000, 120),
    ('D09', N'1 lọ', 849000, 120),
    ('E01', N'1 máy', 799000, 120),
    ('E02', N'1 máy', 1490000, 120),
    ('E03', N'1 máy', 1890000, 120),
    ('E04', N'1 máy', 999000, 120),
    ('E05', N'1 máy', 8990000, 120);

IF (SELECT COUNT(*) FROM dbo.Products WHERE sku LIKE 'CAT41-%') <> 41
    THROW 51052, 'Expected exactly 41 catalog products.', 1;
IF EXISTS
(
    SELECT 1 FROM @OwnerPrices source
    LEFT JOIN dbo.Products product ON product.sku = 'CAT41-' + source.code
    LEFT JOIN dbo.Catalog_Product_Reviews review ON review.product_id = product.product_id
    WHERE product.product_id IS NULL OR review.product_id IS NULL
       OR review.image_status <> 'VERIFIED' OR NULLIF(review.image_source_url, '') IS NULL
       OR NOT EXISTS (SELECT 1 FROM dbo.Product_Images image WHERE image.product_id = product.product_id)
)
    THROW 51053, 'A catalog product or reviewed image is missing.', 1;

UPDATE product
SET product.package_size = source.package_size,
    product.price = source.price_vnd,
    product.stock_quantity = CASE WHEN review.product_status = 'OWNER_APPROVED'
        THEN product.stock_quantity ELSE source.initial_stock END,
    product.is_active = 1,
    product.is_listed = 1,
    product.updated_at = SYSUTCDATETIME()
FROM dbo.Products product
JOIN @OwnerPrices source ON product.sku = 'CAT41-' + source.code
JOIN dbo.Catalog_Product_Reviews review ON review.product_id = product.product_id;

UPDATE review
SET review.price_status = 'OWNER_SET',
    review.product_status = 'OWNER_APPROVED',
    review.public_preview = 0,
    review.reviewed_at = SYSUTCDATETIME()
FROM dbo.Catalog_Product_Reviews review
JOIN dbo.Products product ON product.product_id = review.product_id
JOIN @OwnerPrices source ON product.sku = 'CAT41-' + source.code;

COMMIT TRANSACTION;
SELECT COUNT(*) AS products,
       SUM(CASE WHEN product.is_active = 1 AND product.price > 0 AND product.stock_quantity > 0
                     AND review.price_status = 'OWNER_SET' AND review.product_status = 'OWNER_APPROVED'
                THEN 1 ELSE 0 END) AS ready_to_sell
FROM dbo.Products product
JOIN dbo.Catalog_Product_Reviews review ON review.product_id = product.product_id
WHERE product.sku LIKE 'CAT41-%';
GO
