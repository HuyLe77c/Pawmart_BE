# Pawmart API

ASP.NET Core 10 Web API for the PetFoodDB SQL Server schema. Swagger UI is available in Development at `http://localhost:5165/swagger` or `https://localhost:7222/swagger` when running with the supplied launch profile. The HTTP profile binds to `0.0.0.0:5165` so the Android Emulator can reach it.

## Local setup

1. Run `../Database/PetFoodDB_PRM393_Complete_SQLServer.sql` in SSMS only against a new/empty `PetFoodDB` database, or restore the snapshot in `../Database/PetFoodDB-20261018.bak` when you need its saved data.
2. Confirm the SQL Server `SQLEXPRESS` instance is running; the checked-in connection string uses `localhost\SQLEXPRESS` with Windows authentication. Change `ConnectionStrings:PetFoodDb` if your database is on another instance.
3. From this folder, run:

   ```powershell
   dotnet restore
   dotnet run
   ```

4. Open `/swagger` and use **Authorize** after logging in through `POST /api/auth/login`.

## Product images

- Product images are referenced by `dbo.Product_Images.image_url`. Product administrators can keep using external HTTP(S) image URLs or upload one primary image using `POST /api/admin/products/{id}/images/upload` with a multipart `file` field and an ADMIN bearer token.
- Uploads are limited to 20 MiB and accept JPEG, PNG, and WebP image signatures. The API generates the stored filename and serves uploaded files publicly from `/uploads/products/{filename}` so customer catalog images need no token.
- Uploaded files live in `wwwroot/uploads/products`; SQL Server stores only their relative URL. Back up that folder alongside the database. When moving the API, copy the folder and keep the relative URLs unchanged.

The Development host creates a random temporary JWT key when `Jwt:SigningKey` is absent. Tokens from that host stop working when the process restarts. Set `Jwt__SigningKey` to a random value of at least 32 bytes when you need stable local tokens. Outside Development, startup requires this key and does not generate one.

The fixed checkout shipping fee is `25,000 VND` in `Shipping:FlatFee` (`appsettings.json`). `Shipping__FlatFee` can override it for a deployment. This is a flat fee; no distance is claimed unless a distance provider is configured.

## Create the first administrator

Public registration always creates a CUSTOMER. Register the trusted administrator account through the API, then promote that account once through SSMS using its email:

```sql
DECLARE @Email NVARCHAR(150) = N'admin@example.com';

UPDATE dbo.Users
SET role = 'ADMIN', updated_at = SYSUTCDATETIME()
WHERE email = UPPER(LTRIM(RTRIM(@Email)))
  AND is_active = 1;

IF @@ROWCOUNT <> 1
    THROW 51001, 'Expected to promote exactly one active user.', 1;
```

Log in again after promotion so the JWT contains the ADMIN role. Keep database write access restricted to trusted operators.

## External providers

Payment initiation/callback verification, distance quoting, and Firebase push delivery remain behind optional provider interfaces. Checkout works with the configured flat shipping fee and COD.

## Chat AI with Omniroute

PawMart's `POST /api/chat/messages` answers free-form questions and adds current, read-only catalog and customer-owned data when relevant. `POST /api/chat/recommendations` uses the same Omniroute connection for pet-specific advice. Both routes require a CUSTOMER bearer token. Chat history is kept in Flutter memory only; it is not saved in SQL Server.

Chat message responses include a `products` array with product ID, current name, and price when the answer references verified catalog items. The app renders these as cards that open product detail. The pet-specific recommendation path uses its already-filtered product list; free-form answers only link active, stocked products present in the catalog context given to AI. Clients should treat this array as the source for navigation rather than extracting product IDs from answer text.

The API uses `http://127.0.0.1:20128/v1` and model `cloudflare-ai/@cf/meta/llama-3.3-70b-instruct-fp8-fast` by default. Start the locally installed gateway with `omniroute serve --daemon --no-open` and ensure it is bound to loopback (`OMNIROUTE_SERVER_HOST=127.0.0.1`). A loopback-only gateway can be used without an API key if its local configuration permits this. For any remote Omniroute URL, set `Omniroute__BaseUrl`, `Omniroute__Model`, and `Omniroute__ApiKey` in the backend environment or secret store; the API rejects remote connections without a key. Never put the key in Flutter or commit it.

This development machine's Omniroute key was saved in .NET User Secrets for the project. User Secrets are outside the repository and will not accompany a push or clone. On another machine, set `Omniroute__ApiKey` in the API process environment or run `dotnet user-secrets set 'Omniroute:ApiKey' '<your Omniroute key>' --project Pawmart.csproj` from this folder. Use a model available to that gateway; override `Omniroute__Model` when needed.

For example, in PowerShell before starting the API:

```powershell
$env:Omniroute__BaseUrl = 'http://127.0.0.1:20128/v1'
$env:Omniroute__Model = 'cloudflare-ai/@cf/meta/llama-3.3-70b-instruct-fp8-fast'
$env:OMNIROUTE_SERVER_HOST = '127.0.0.1'
omniroute serve --daemon --no-open
dotnet run --launch-profile http
```

When Omniroute or its selected model is unavailable, the API responds with a safe 503/502 and Flutter offers retry. General answers are model-generated and can be wrong; product, price, stock and account facts are provided from current PawMart database reads. The assistant cannot change orders, cart or profiles.

Pet food advice is checked by the API before it is shown: it uses only active, stocked food/treat products for the pet type, removes clearly age-incompatible products, and excludes ingredients explicitly listed as allergens in the pet notes. A missing allergen in the stored ingredient list is **not** an allergy-safe certification; customers should check the package for cross-contact. For urgent symptoms and conditions requiring a veterinary diet, the API returns no product recommendation and directs the customer to a veterinarian. Chat AI applies the same safeguards to explicit health questions and, when there is exactly one pet profile, to food questions about that pet. These rules are conservative and cannot diagnose disease or replace veterinary care.

For an existing `PetFoodDB`, apply `Database/2026-10-28_PetsNutritionProfile.sql` once before starting the updated API. It adds optional size, activity, 1–9 body condition score, feeding habits, and food preferences to `Pets`; it is safe to rerun. The baseline `Database/PetFoodDB_PRM393_Complete_SQLServer.sql` includes the same columns for a fresh database. Existing pet profiles remain valid with null values. The Flutter pet form can edit these fields, and the recommendation endpoint returns source URLs alongside its advice.

For an existing database, also run `Database/2026-10-30_ProductShoppingDetails.sql` before starting this API version. It adds optional `package_size`, `life_stage`, and `feeding_guide` product fields. The baseline script contains the same columns. Existing products keep null values; admins should enter only verified package or shop information. The product form now edits description, ingredients, nutrition, package size, life stage, and feeding guide.

Checkout uses `POST /api/cart/checkout/quote` with an optional `promotionCode` to return current items, subtotal, discount, configured shipping fee, total, and a fingerprint. The Flutter checkout shows this quote before enabling order submission and sends its fingerprint as `expectedQuoteFingerprint` to `POST /api/cart/checkout`. A changed cart, price, shipping fee, or promotion returns HTTP 409 so the customer can review a new quote. The order endpoint still recalculates and validates stock in its transaction. The configured flat shipping fee is 25,000 VND.

Nutrition guidance was reviewed on 2026-10-28 against [WSAVA Global Nutrition Guidelines](https://wsava.org/Global-Guidelines/Global-Nutrition-Guidelines/), [FDA Complete and Balanced Pet Food](https://www.fda.gov/animal-veterinary/animal-health-literacy/complete-and-balanced-pet-food), and [Merck Veterinary Manual nutritional requirements](https://www.merckvetmanual.com/management-and-nutrition/nutrition-small-animals/nutritional-requirements-of-small-animals). This is a curated local reference, not live web search. The app does not compute grams/day without verified product energy density and veterinary context; breed, size, and taste preferences alone cannot establish medical suitability. Refresh the reviewed sources when changing the guidance.

The recommendation service filters explicit allergens and disliked ingredients, then favors matching preferred ingredients when a main food exists. If Omniroute returns no usable product IDs or an invalid/unavailable response, the API labels a fallback list built from the same filtered catalog. It identifies treat-only results as treats rather than presenting them as a complete meal. Chat AI asks which pet to use when a food question is ambiguous across multiple profiles and answers exact product stock, price, and ingredients directly from the database.

## API groups

- `api/auth`: registration, login, and own profile.
- `api/products`, `api/categories`, `api/pets`: catalog and pet profiles.
- `api/admin/products`: admin catalog listing and product image uploads.
- `api/cart`, `api/orders`, `api/admin/orders`: cart, checkout, tracking, and order management.
- `api/promotions`, `api/stores`, `api/notifications`: promotion/store data and customer notifications.
- `api/admin/reports`, `api/admin/notifications`: dashboard summaries and notification broadcasts.
- `api/payments`, `api/shipping`, `api/chat`: optional provider-backed operations.
- `/health` is process liveness; `/health/ready` checks SQL Server connectivity without returning connection details.

Revenue reports accept an optional paired `from` and `to` UTC date range (maximum 366 days). Without a range, the report covers the current UTC month; today's delivered revenue/count are returned separately.
