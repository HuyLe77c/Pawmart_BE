USE PetFoodDB;
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.Products', N'is_listed') IS NULL
    ALTER TABLE dbo.Products ADD is_listed BIT NOT NULL
        CONSTRAINT DF_Products_IsListed DEFAULT (1) WITH VALUES;
GO

IF COL_LENGTH(N'dbo.Catalog_Product_Reviews', N'public_preview') IS NULL
    ALTER TABLE dbo.Catalog_Product_Reviews ADD public_preview BIT NOT NULL
        CONSTRAINT DF_Catalog_Product_Reviews_PublicPreview DEFAULT (0) WITH VALUES;
GO

BEGIN TRANSACTION;

IF (SELECT COUNT(*) FROM dbo.Products WHERE sku LIKE 'CAT41-%') <> 41
    THROW 51044, 'Import 41 catalog products before publishing previews.', 1;

UPDATE dbo.Products
SET is_listed = 0
WHERE sku IN
(
    'DOG-FOOD-001', 'DOG-FOOD-002', 'DOG-FOOD-003',
    'CAT-FOOD-001', 'CAT-FOOD-002', 'CAT-FOOD-003',
    'DOG-TREAT-001', 'CAT-TREAT-001',
    'DOG-ACCESSORY-001', 'CAT-ACCESSORY-001'
)
AND is_listed = 1;

UPDATE dbo.Products
SET is_listed = 1
WHERE sku LIKE 'CAT41-%' AND is_listed = 0;

UPDATE review
SET public_preview = 1
FROM dbo.Catalog_Product_Reviews review
JOIN dbo.Products product ON product.product_id = review.product_id
WHERE product.sku LIKE 'CAT41-%' AND review.public_preview = 0;

UPDATE dbo.Categories
SET is_active = 1
WHERE category_name IN
(
    N'Thức ăn', N'Đồ chơi', N'Chăm sóc và vệ sinh',
    N'Thực phẩm bổ sung', N'Phụ kiện và thiết bị'
)
AND is_active = 0;

COMMIT TRANSACTION;
SELECT COUNT(*) AS preview_count
FROM dbo.Catalog_Product_Reviews WHERE public_preview = 1;
GO
