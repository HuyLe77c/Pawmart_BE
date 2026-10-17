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

Set `Shipping__FlatFee` to the store's fixed fee in the database's currency units. The checked-in default is zero; no distance is claimed unless a distance provider is configured.

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

Payment initiation/callback verification, distance quoting, Firebase push delivery, and AI recommendations are behind provider interfaces. The default implementations return an explicit unavailable response until a provider and its secrets are configured. Checkout still works with the configured flat shipping fee and COD. AI conversation history is not stored.

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
