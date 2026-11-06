"""Generate an idempotent SQL Server import from catalog-41-sources.csv."""

import csv
import pathlib
import re
import unicodedata


ROOT = pathlib.Path(__file__).resolve().parent
SOURCE = ROOT / "catalog-41-sources.csv"
VARIANT_SOURCE = ROOT / "catalog-41-variant-leads.csv"
OUTPUT = ROOT / "2026-11-05_Catalog41_Upsert.sql"
EXPECTED = {"A": 8, "B": 8, "C": 11, "D": 9, "E": 5}


def sql_string(value: str) -> str:
    return "N'" + value.replace("'", "''") + "'"


def slug(value: str) -> str:
    ascii_text = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    return re.sub(r"[^a-z0-9]+", "-", ascii_text.lower()).strip("-")


with SOURCE.open(encoding="utf-8-sig", newline="") as stream:
    rows = list(csv.DictReader(stream))

codes = [row["code"] for row in rows]
assert len(rows) == 41 and len(set(codes)) == 41, "Expected 41 distinct catalog codes"
assert all(sum(code.startswith(prefix) for code in codes) == count for prefix, count in EXPECTED.items())
assert all(row["model_status"] in ("VERIFIED", "NEEDS_REVIEW") for row in rows)
assert all(row["official_url"].startswith("https://") for row in rows)
assert all(len(row["name_vi"]) <= 150 for row in rows)

with VARIANT_SOURCE.open(encoding="utf-8-sig", newline="") as stream:
    variants = list(csv.DictReader(stream))
assert len({(row["code"], row["variant_key"]) for row in variants}) == len(variants)
assert all(row["code"] in codes and row["source_url"].startswith("https://") for row in variants)

values = []
for row in rows:
    fields = [
        row["code"], row["group"], row["subcategory"], row["brand"],
        row["pet_type"], row["model"], row["name_vi"],
        row["official_url"], row["model_status"], row["review_note"],
        slug(f'{row["brand"]}-{row["model"]}-{row["code"]}'),
    ]
    values.append("    (" + ", ".join(sql_string(value) for value in fields) + ")")

variant_values = []
for row in variants:
    fields = [row["code"], row["variant_key"], row["label"], row["source_url"]]
    variant_values.append("    (" + ", ".join(sql_string(value) for value in fields) + ")")

sql = """USE PetFoodDB;
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
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Catalog_Product_Reviews', N'U') IS NULL
    THROW 51041, 'Run 2026-11-05_CatalogReview.sql first.', 1;

DECLARE @Catalog TABLE
(
    code VARCHAR(3) NOT NULL PRIMARY KEY,
    category_name NVARCHAR(100) NOT NULL,
    subcategory_name NVARCHAR(100) NOT NULL,
    brand NVARCHAR(100) NOT NULL,
    pet_type VARCHAR(20) NOT NULL,
    model_name NVARCHAR(150) NOT NULL,
    product_name NVARCHAR(150) NOT NULL,
    official_url NVARCHAR(1000) NOT NULL,
    model_status VARCHAR(20) NOT NULL,
    review_note NVARCHAR(1000) NULL,
    slug VARCHAR(180) NOT NULL
);

INSERT INTO @Catalog
(code, category_name, subcategory_name, brand, pet_type, model_name,
 product_name, official_url, model_status, review_note, slug)
VALUES
""" + ",\n".join(values) + ";\n\n" + """
IF (SELECT COUNT(*) FROM @Catalog) <> 41
    THROW 51042, 'Catalog must contain exactly 41 products.', 1;

INSERT INTO dbo.Categories(category_name, description, sort_order, is_active)
SELECT source.category_name, N'Danh mục sản phẩm đang được kiểm duyệt',
       CASE source.category_name
           WHEN N'Thức ăn' THEN 10 WHEN N'Đồ chơi' THEN 20
           WHEN N'Chăm sóc và vệ sinh' THEN 30
           WHEN N'Thực phẩm bổ sung' THEN 40 ELSE 50 END,
       1
FROM (SELECT DISTINCT category_name FROM @Catalog) source
WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories existing WHERE existing.category_name = source.category_name);

INSERT INTO dbo.Catalog_Subcategories(category_id, subcategory_name)
SELECT category.category_id, source.subcategory_name
FROM (SELECT DISTINCT category_name, subcategory_name FROM @Catalog) source
JOIN dbo.Categories category ON category.category_name = source.category_name
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Catalog_Subcategories existing
    WHERE existing.category_id = category.category_id
      AND existing.subcategory_name = source.subcategory_name
);

IF EXISTS
(
    SELECT 1 FROM @Catalog source
    JOIN dbo.Products product ON product.sku = 'CAT41-' + source.code
    LEFT JOIN dbo.Catalog_Product_Reviews review ON review.product_id = product.product_id
    WHERE review.product_id IS NULL
)
    THROW 51043, 'A catalog SKU already belongs to an unrelated product.', 1;

INSERT INTO dbo.Products
(category_id, sku, product_name, brand, price, stock_quantity,
 low_stock_threshold, target_pet_type, is_featured, is_active)
SELECT category.category_id, 'CAT41-' + source.code, source.product_name,
       source.brand, 0, 0, 5, source.pet_type, 0, 0
FROM @Catalog source
JOIN dbo.Categories category ON category.category_name = source.category_name
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products product WHERE product.sku = 'CAT41-' + source.code);

INSERT INTO dbo.Catalog_Product_Reviews
(product_id, slug, subcategory_id, model_name, official_url, model_status,
 image_status, price_status, product_status, public_preview, review_note)
SELECT product.product_id, source.slug, subcategory.subcategory_id,
       source.model_name, source.official_url, source.model_status,
       'NEEDS_REVIEW', 'NEEDS_REVIEW', 'NEEDS_REVIEW', 1, NULLIF(source.review_note, N'')
FROM @Catalog source
JOIN dbo.Products product ON product.sku = 'CAT41-' + source.code
JOIN dbo.Categories category ON category.category_name = source.category_name
JOIN dbo.Catalog_Subcategories subcategory
  ON subcategory.category_id = category.category_id
 AND subcategory.subcategory_name = source.subcategory_name
WHERE NOT EXISTS
    (SELECT 1 FROM dbo.Catalog_Product_Reviews review WHERE review.product_id = product.product_id);

DECLARE @Variants TABLE
(
    code VARCHAR(3) NOT NULL,
    variant_key VARCHAR(80) NOT NULL,
    label NVARCHAR(100) NOT NULL,
    source_url NVARCHAR(1000) NOT NULL,
    PRIMARY KEY (code, variant_key)
);
INSERT INTO @Variants(code, variant_key, label, source_url)
VALUES
""" + ",\n".join(variant_values) + ";\n\n" + """
INSERT INTO dbo.Catalog_Product_Variants
(product_id, variant_key, label, price_source_url, price_status)
SELECT product.product_id, source.variant_key, source.label, source.source_url, 'NEEDS_REVIEW'
FROM @Variants source
JOIN dbo.Products product ON product.sku = 'CAT41-' + source.code
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Catalog_Product_Variants existing
    WHERE existing.product_id = product.product_id AND existing.variant_key = source.variant_key
);

COMMIT TRANSACTION;
SELECT LEFT(product.sku, 5) AS catalog_prefix, COUNT(*) AS product_count,
       SUM(CASE WHEN product.is_active = 1 THEN 1 ELSE 0 END) AS active_count
FROM dbo.Products product
WHERE product.sku LIKE 'CAT41-%'
GROUP BY LEFT(product.sku, 5);
GO
"""

OUTPUT.write_text(sql, encoding="utf-8-sig")
print(f"Generated {OUTPUT.name}: {len(rows)} review products")
