# PawMart catalog 41: implementation plan

**Goal:** Replace the public demo catalog with verifiable pet products while preserving existing users, carts, favorites, and orders.

**Current state (2026-11-05):** SQL Server `PetFoodDB` has 10 demo products, four categories, one cart item, and no order items. The UI checkout requires a numeric price and stock. Product images are stored as URLs in `Product_Images`; local uploads are served from `wwwroot/uploads`.

## Data policy

- Keep stable SKU keys for idempotent upserts. Never delete product rows as part of the import.
- Source supplied product names are candidates until an exact manufacturer model page is checked.
- Keep candidate price, image, and product verification independent. No inferred VNĐ price, dummy stock, or substitute image.
- Only expose a product for sale when exact model, image use, VNĐ price for its variant, and stock are confirmed.
- Keep the 10 existing records for cart/order history; hide them from public lists once the 41 clearly labeled previews are visible.
- Record reference URL, checked URL, review status, and verification date for every requested item.

## Work

1. Audit all 41 model references on official sites; resolve changed or missing URLs. Select exact FURminator size/coat variants and identify unavailable models.
2. Match each sellable variant to a current Vietnam retailer listing, capturing price, size, URL, and check time. Inspect image source and reuse rights; store verified media in the existing upload directory only when permitted.
3. Add an idempotent, additive SQL import with five top-level categories and populated subcategories. Preserve existing IDs and relationships. Synchronize the baseline SQL and migration.
4. Extend API response and Flutter screens only where needed for verified metadata, variant selection, price status, and clear review state. Retain the current PawMart design.
5. Back up live SQL Server before applying SQL. Run import twice to prove idempotency, then verify counts, duplicate SKUs, references, images, filters, cart, and Android rendering.
6. Report per-item product, image, and price statuses, along with remaining review work. Do not publish repositories unless separately requested.

## 2026-11-06 update

The customer catalog now shows all 41 products as non-purchasable previews. The 10 demo records remain in SQL Server but are hidden from public lists. Product photos, variant prices, full descriptions, and stock still require verification before sale. See `docs/catalog-41-status.md` for current test evidence and restore instructions.
