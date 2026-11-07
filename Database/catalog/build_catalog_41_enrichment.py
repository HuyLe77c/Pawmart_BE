"""Generate repeatable catalog text/photo import from supplied data and reviewed source photos."""

import csv
import hashlib
import json
import shutil
import uuid
from pathlib import Path


ROOT = Path(__file__).resolve().parent
API_ROOT = ROOT.parent.parent / "Pawmart"
MEDIA_ROOT = API_ROOT / "wwwroot" / "uploads" / "products"
MEDIA_ROOT.mkdir(parents=True, exist_ok=True)
products = json.loads((ROOT / "catalog-41-supplied.vi.json").read_text(encoding="utf-8"))["products"]
with (ROOT / "catalog-41-curated-details.vi.tsv").open(encoding="utf-8", newline="") as stream:
    curated = {row["code"]: row["description_vi"].strip() for row in csv.DictReader(stream, delimiter="\t")}
images = json.loads((ROOT / "catalog-41-image-candidates.json").read_text(encoding="utf-8"))
previous_manifest_path = ROOT / "catalog-41-image-sources.csv"
previous_hashes = {}
if previous_manifest_path.exists():
    with previous_manifest_path.open(encoding="utf-8-sig", newline="") as stream:
        previous_hashes = {row["code"]: row["sha256"] for row in csv.DictReader(stream)}
assert len(products) == len(images) == 41
assert {product["id"] for product in products} == {image["code"] for image in images}
assert {product["id"] for product in products} == set(curated)
assert all(image["result"] == "CANDIDATE" for image in images)
image_by_code = {image["code"]: image for image in images}


def literal(value):
    return "N'" + value.replace("'", "''") + "'"


source_rows = []
sql_values = []
for product in products:
    code = product["id"]
    image = image_by_code[code]
    filename = uuid.uuid5(uuid.NAMESPACE_URL, f"https://pawmart.local/catalog-41/{code}").hex + ".jpg"
    target = MEDIA_ROOT / filename
    original = ROOT / image["path"]
    if not original.is_file():
        original = target
    assert original.is_file()
    data = original.read_bytes()
    if target.exists():
        current_hash = hashlib.sha256(target.read_bytes()).hexdigest()
        assert current_hash in (hashlib.sha256(data).hexdigest(), previous_hashes.get(code)), f"Existing photo differs: {target}"
    if original != target:
        shutil.copyfile(original, target)
    media_url = f"/uploads/products/{filename}"
    seed_description = product["description_vi"].strip()
    description = curated[code]
    short_description = product["short_description_vi"].strip()
    assert description and seed_description and short_description and len(short_description) <= 500
    sql_values.append("    (" + ", ".join(literal(value) for value in (
        code, description, seed_description, short_description, media_url, image["image_url"], image["source_url"]
    )) + ")")
    source_rows.append({
        "code": code, "model": product["model"], "source_page": image["source_url"],
        "source_image": image["image_url"], "local_image": media_url,
        "sha256": hashlib.sha256(data).hexdigest(), "visual_review": "MATCHED_MODEL",
    })

with (ROOT / "catalog-41-image-sources.csv").open("w", encoding="utf-8-sig", newline="") as stream:
    writer = csv.DictWriter(stream, fieldnames=list(source_rows[0]))
    writer.writeheader()
    writer.writerows(source_rows)

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
BEGIN TRANSACTION;

DECLARE @Enrichment TABLE
(
    code VARCHAR(3) NOT NULL PRIMARY KEY,
    description NVARCHAR(MAX) NOT NULL,
    seed_description NVARCHAR(MAX) NOT NULL,
    short_description NVARCHAR(500) NOT NULL,
    media_url NVARCHAR(1000) NOT NULL,
    image_source_url NVARCHAR(1000) NOT NULL,
    source_page NVARCHAR(1000) NOT NULL
);
INSERT INTO @Enrichment (code, description, seed_description, short_description, media_url, image_source_url, source_page)
VALUES
""" + ",\n".join(sql_values) + ";\n" + """
IF (SELECT COUNT(*) FROM @Enrichment) <> 41
    THROW 51045, 'Expected 41 catalog enrichment rows.', 1;
IF (SELECT COUNT(*) FROM dbo.Products WHERE sku LIKE 'CAT41-%') <> 41
    THROW 51046, 'Run catalog 41 upsert before enrichment.', 1;

UPDATE product
SET description = source.description, updated_at = SYSUTCDATETIME()
FROM dbo.Products product
JOIN @Enrichment source ON product.sku = 'CAT41-' + source.code
WHERE NULLIF(LTRIM(RTRIM(product.description)), N'') IS NULL
   OR product.description = source.seed_description;

UPDATE review
SET short_description = CASE WHEN NULLIF(LTRIM(RTRIM(review.short_description)), N'') IS NULL
                             THEN source.short_description ELSE review.short_description END,
    image_source_url = COALESCE(NULLIF(review.image_source_url, N''), source.image_source_url),
    image_status = CASE WHEN review.image_status = 'NEEDS_REVIEW' AND review.image_source_url IS NULL
                        THEN 'VERIFIED' ELSE review.image_status END,
    official_url = CASE WHEN source.code = 'E05'
                             AND review.official_url = N'https://www.petkit.com/products/puramax-2-ultimate-bundles'
                        THEN source.source_page ELSE review.official_url END,
    reviewed_at = COALESCE(review.reviewed_at, SYSUTCDATETIME())
FROM dbo.Catalog_Product_Reviews review
JOIN dbo.Products product ON product.product_id = review.product_id
JOIN @Enrichment source ON product.sku = 'CAT41-' + source.code;

INSERT INTO dbo.Product_Images (product_id, image_url, is_primary, display_order)
SELECT product.product_id, source.media_url, 1, 0
FROM @Enrichment source
JOIN dbo.Products product ON product.sku = 'CAT41-' + source.code
WHERE NOT EXISTS (SELECT 1 FROM dbo.Product_Images image WHERE image.product_id = product.product_id AND image.is_primary = 1);

COMMIT TRANSACTION;
SELECT COUNT(*) AS catalog_count,
       SUM(CASE WHEN product.description IS NOT NULL THEN 1 ELSE 0 END) AS description_count,
       SUM(CASE WHEN review.image_status = 'VERIFIED' THEN 1 ELSE 0 END) AS reviewed_image_count
FROM dbo.Products product
JOIN dbo.Catalog_Product_Reviews review ON review.product_id = product.product_id
WHERE product.sku LIKE 'CAT41-%';
GO
"""
(ROOT.parent / "migrations" / "2026-11-06_Catalog41_Enrichment.sql").write_text(sql, encoding="utf-8-sig")
print("Generated 41 descriptions and 41 product photos")
