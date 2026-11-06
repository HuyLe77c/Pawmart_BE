import csv
from pathlib import Path


root = Path(__file__).parent
source = root / 'catalog-41-owner-prices.csv'
output = root / '2026-11-06_Catalog41_OwnerPricing.sql'
with source.open(encoding='utf-8-sig', newline='') as handle:
    rows = list(csv.DictReader(handle))

codes = [row['code'] for row in rows]
assert len(rows) == 41 and len(set(codes)) == 41
assert set(codes) == {f'A{i:02}' for i in range(1, 9)} | {f'B{i:02}' for i in range(1, 9)} | {f'C{i:02}' for i in range(1, 12)} | {f'D{i:02}' for i in range(1, 10)} | {f'E{i:02}' for i in range(1, 6)}
assert all(row['package_size'].strip() and int(row['price_vnd']) > 0 and int(row['stock_quantity']) >= 100 for row in rows)


def literal(value):
    return "N'" + value.replace("'", "''") + "'"


values = ',\n'.join(
    f"    ('{row['code']}', {literal(row['package_size'])}, {int(row['price_vnd'])}, {int(row['stock_quantity'])})"
    for row in rows
)

sql = f"""USE PetFoodDB;
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
{values};

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
"""
output.write_text(sql, encoding='utf-8')
print(output)
