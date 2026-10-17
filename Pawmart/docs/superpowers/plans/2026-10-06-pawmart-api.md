# Pawmart API Implementation Plan

> **For agentic workers:** Implement task-by-task in this plan; keep each resource group independently buildable. The user has said not to use GitHub. The current folder has no Git metadata, so do not assume Git commits are available.

**Goal:** Replace the WeatherForecast template with a Swagger-documented ASP.NET Core API for Pawmart's customer and admin store flows.

**Architecture:** ASP.NET Core controllers expose DTO-based REST endpoints. JWT authentication and role policies protect customer-owned and admin operations; EF Core explicitly maps the existing SQL Server schema. Checkout and other multi-row workflows use database transactions, while optional third-party services sit behind provider interfaces and report unavailable until configured.

**Tech Stack:** .NET 10, ASP.NET Core Web API, EF Core SQL Server provider, JWT bearer authentication, built-in `PasswordHasher<TUser>`, Swagger UI (Swashbuckle), SQL Server.

**Spec:** `docs/superpowers/specs/2026-10-06-pawmart-api-design.md`

## Global Constraints

- Target framework remains `net10.0`.
- The supplied SQL Server script remains the schema source of truth; do not automatically create or migrate the database.
- Use CUSTOMER and ADMIN roles; registration creates CUSTOMER accounts only.
- Use DTOs and never return password hashes, FCM tokens, or payment secrets.
- Customer-owned queries must scope by authenticated user ID.
- Recalculate prices, promotions, totals, and stock on the server; never trust those client values.
- Use database transactions for checkout and state changes that span records.
- The SQL trigger writes order status history; do not insert duplicate history rows from the API.
- Return Problem Details for errors, 409 for stock/state conflicts, and 503 for operations that require an unconfigured external provider. A persisted notification broadcast reports optional push failure with `pushAvailable=false` to prevent duplicate retries.
- Swagger UI is enabled in Development and documents bearer auth.
- Do not commit secrets or require GitHub.

## Review Focus

- Concurrent checkouts competing for the last units must not oversell; checkout uses a transaction and conditional stock update.
- A customer changing a route/body ID must not read or mutate another customer's pets, cart, orders, or notifications; all customer queries use the token subject.
- Replayed or invalid payment callbacks must not mark an order paid; provider verification is mandatory and callbacks are idempotent.
- Invalid order transitions and cancellation after shipment must be rejected without changing status.
- Absent provider credentials must return an explicit 503 and must not create fake payment, map, FCM, or AI success records.

---

## File Map

Project paths below are relative to `Pawmart/`.

- `Program.cs`: dependency registration and middleware pipeline.
- `appsettings.json` and `appsettings.Development.json`: non-secret configuration keys and development-only defaults; secrets come from environment/user secrets.
- `Data/PawmartDbContext.cs`, `Data/Configurations/*`: SQL Server connection and explicit entity-to-table/column mappings.
- `Models/Entities/*`: persistence entities that mirror the supplied schema.
- `Models/Contracts/*`: request and response DTOs, validation attributes/rules.
- `Services/*`: business workflows and provider interfaces/implementations.
- `Controllers/*Controller.cs`: HTTP routing, authorization, status codes, and DTO binding.
- `Security/*`: JWT creation, password hashing integration, and authorization constants.
- `Middleware/ExceptionHandlingMiddleware.cs`: consistent Problem Details errors.
- `Database/`: only additive, reviewable SQL if implementation proves the existing script insufficient; otherwise do not add schema changes.
- Remove `WeatherForecast.cs` and `Controllers/WeatherForecastController.cs` when the replacement API foundation is ready.

## Task 1: API foundation, SQL mapping, and Swagger

**Files:** `Pawmart.csproj`, `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Data/PawmartDbContext.cs`, `Data/Configurations/*`, `Models/Entities/*`, `Middleware/ExceptionHandlingMiddleware.cs`, WeatherForecast sample files.

**Interfaces:** Produces configured `PawmartDbContext`, SQL Server connection via `ConnectionStrings:PetFoodDb`, Problem Details responses, and development Swagger UI. Entity mappings must match the supplied table and column names exactly. No automatic database creation/migration.

- [x] Add compatible EF Core SQL Server, JWT bearer, and Swagger UI packages; keep secrets out of committed settings.
- [x] Define persistence entities and explicit mappings for all schema tables needed by later tasks, including string status/role values, computed `OrderItems.line_total`, trigger-managed history (disable SQL `OUTPUT` for `Orders`), and `Product_Images` table naming.
- [x] Register `PawmartDbContext`, authentication placeholders, authorization, controllers, exception middleware, and Swagger bearer security in `Program.cs`.
- [x] Remove the template WeatherForecast class/controller.
- [x] Run `dotnet build Pawmart/Pawmart.csproj`; confirm clean build and Swagger UI route in Development.

## Task 2: Authentication, profiles, and role policies

**Files:** `Security/JwtTokenService.cs`, `Security/AuthorizationPolicies.cs`, `Models/Contracts/Auth/*`, `Services/AuthService.cs`, `Controllers/AuthController.cs`, `Models/Contracts/Profile/*`.

**Interfaces:** `AuthService.RegisterAsync(RegisterRequest) -> AuthResult`, `AuthService.LoginAsync(LoginRequest) -> AuthResult`, `JwtTokenService.CreateToken(User) -> string`. JWT claims include user ID as `NameIdentifier` and role as `Role`.

- [x] Add registration/login DTOs with required-field and length validation.
- [x] Hash passwords with `PasswordHasher<User>`; normalize email; registration always creates CUSTOMER.
- [x] Reject inactive accounts at login; update `last_login_at`; issue signed JWT from environment-configured signing key and expiry.
- [x] Add `[Authorize]` current profile and profile update endpoints; never bind role, password hash, or FCM token from profile DTOs.
- [x] Apply reusable ADMIN and CUSTOMER policies and return Problem Details for duplicate email and invalid credentials.
- [x] Run build and inspect Swagger auth/response documentation.

## Task 3: Product catalog, categories, and pet profiles

**Files:** `Models/Contracts/Catalog/*`, `Services/ProductService.cs`, `Services/CategoryService.cs`, `Services/PetService.cs`, `Controllers/ProductsController.cs`, `Controllers/CategoriesController.cs`, `Controllers/PetsController.cs`.

**Interfaces:** `ProductService.SearchAsync(ProductQuery)`, `GetByIdAsync(int)`, `CreateAsync(ProductWriteRequest)`, `UpdateAsync(int, ProductWriteRequest)`, `SetActiveAsync(int, bool)`; `PetService.ListAsync(userId)`, `CreateAsync(userId, PetWriteRequest)`, `UpdateAsync(userId, petId, PetWriteRequest)`, `DeleteAsync(userId, petId)`.

- [x] Implement public product search with bounded pagination, category/type/brand filters, text query, sort, and active-only defaults.
- [x] Implement product detail with images and public category listing.
- [x] Implement ADMIN product/category CRUD and soft deactivation; validate category references and nonnegative price/stock.
- [x] Implement customer pet CRUD scoped to authenticated user ID; validate pet type, age, and weight to match SQL constraints.
- [x] Run build and review route authorization and DTOs against Swagger output.

## Task 4: Cart and atomic checkout

**Files:** `Models/Contracts/Cart/*`, `Models/Contracts/Orders/CheckoutRequest.cs`, `Services/CartService.cs`, `Services/CheckoutService.cs`, `Controllers/CartController.cs`.

**Interfaces:** `CartService.GetAsync(userId)`, `UpsertItemAsync(userId, productId, quantity)`, `RemoveItemAsync(userId, productId)`; `CheckoutService.CreateOrderAsync(userId, CheckoutRequest) -> OrderSummary`.

- [x] Implement cart read/add/update/remove with owner scoping, positive quantity checks, active product validation, and stock-aware responses.
- [x] Define checkout input for recipient snapshot, delivery address/coordinates, payment method, optional promotion code, and note; do not accept prices or totals.
- [x] In one SQL transaction, reload cart/products, check stock and active state, calculate subtotal/discount/shipping/total using the configured flat fee or configured shipping provider, create order and item price snapshots, decrement stock conditionally, create the single payment record, increment eligible promotion usage, and clear purchased cart rows.
- [x] Roll back and return 409 when an item is unavailable, stock changed, promotion is invalid/expired/used up, or totals violate constraints.
- [x] Use the DB trigger for initial order history; do not insert history manually.
- [x] Run build and inspect checkout Swagger schema and documented conflict responses.

## Task 5: Customer orders and admin order management

**Files:** `Models/Contracts/Orders/*`, `Services/OrderService.cs`, `Controllers/OrdersController.cs`, `Controllers/AdminOrdersController.cs`.

**Interfaces:** `OrderService.ListForCustomerAsync(userId, page, pageSize)`, `GetForCustomerAsync(userId, orderId)`, `CancelAsync(userId, orderId)`, `ListForAdminAsync(query)`, `TransitionStatusAsync(adminUserId, orderId, targetStatus)`.

- [x] Implement customer order list/detail/history scoped by user ID.
- [x] Implement cancellation only while order is pending/confirmed; restore stock and adjust associated pending payment consistently in a transaction.
- [x] Implement ADMIN order search/detail and legal transitions PENDING → CONFIRMED → SHIPPING → DELIVERED, with cancellation allowed only before shipping.
- [x] Update transition timestamps and rely on SQL trigger to create status history.
- [x] Return 409 on invalid transitions and missing/cross-owner data as 404.
- [x] Run build and inspect Swagger authorization and response codes.

## Task 6: Promotions, stores, notifications, and reports

**Files:** `Models/Contracts/Promotions/*`, `Models/Contracts/Stores/*`, `Models/Contracts/Notifications/*`, `Models/Contracts/AdminReports/*`, `Services/PromotionService.cs`, `Services/StoreService.cs`, `Services/NotificationService.cs`, `Services/AdminReportService.cs`, `Controllers/PromotionsController.cs`, `Controllers/StoresController.cs`, `Controllers/NotificationsController.cs`, `Controllers/AdminReportsController.cs`.

**Interfaces:** Services expose public active reads, ADMIN write/report methods, customer notification listing/read methods; all customer methods receive authenticated user ID separately from request DTOs.

- [x] Implement active promotion listing and ADMIN promotion CRUD with date, discount, and usage validation.
- [x] Implement public active store listing and ADMIN store CRUD with valid latitude/longitude.
- [x] Implement customer notification listing/read state and ADMIN notification creation; add an authenticated endpoint to register/update the single MVP FCM token stored on the current user's record, never return that token, and call delivery only through its interface.
- [x] Implement ADMIN totals by period, counts by status, and low-stock reports from existing tables/views; do not add redundant statistics tables.
- [x] Run build and inspect Swagger resource grouping and role requirements.

## Task 7: External provider boundaries and final API documentation

**Files:** `Services/Integrations/IPaymentGateway.cs`, `Services/Integrations/IShippingCalculator.cs`, `Services/Integrations/IPushNotificationSender.cs`, `Services/Integrations/IAiRecommendationService.cs`, provider configuration/options, `Controllers/PaymentCallbacksController.cs`, `Controllers/ChatController.cs`, `Controllers/ShippingController.cs` as required.

**Interfaces:** Each integration has a typed request/result contract and an unconfigured implementation that reports unavailable; no endpoint may claim success without a provider result.

- [x] Add optional provider interfaces and options with secret values sourced only from environment/user secrets.
- [x] Implement payment initiation/callback routes with signature-verification interface, idempotency, and no state update on invalid callback.
- [x] Implement shipping quote route using validated coordinates and an optional distance provider; return unavailable if no provider is configured. Checkout still uses the configured flat-fee fallback and must not claim distance was calculated.
- [x] Implement stateless AI recommendations using pet details and active product context; return unavailable if no AI provider is configured.
- [x] Implement FCM delivery through the provider interface while keeping persisted notification behavior explicit when delivery is unavailable.
- [x] Complete Swagger summaries, bearer requirements, status/error response docs, and sample configuration without real secrets.
- [x] Run `dotnet build Pawmart/Pawmart.csproj` and launch Development API to confirm Swagger UI loads and lists all implemented routes.

## Self-review coverage

- Auth/profile: Task 2.
- Catalog, category, product images/filtering, pet ownership: Task 3.
- Cart, checkout totals/stock/promotion/payment snapshot: Task 4.
- Order ownership, transitions, trigger history: Task 5.
- Promotions, stores/map coordinates, notifications/FCM, reports: Task 6.
- Payment provider callback, distance provider, AI: Task 7.
- Problem Details, SQL mapping, Swagger, configuration: Task 1 and Task 7.
- Database script changes: none anticipated; any demonstrated need requires an additive companion SQL script and user-reviewable rationale.

## Execution Notes

- Do not run the supplied database initializer against any existing database; use a disposable PetFoodDB only if the user later requests database-backed verification.
- Do not add or run automated tests unless the user asks for tests or verification. The defined implementation checks are compilation and a Swagger startup smoke check.
- GitHub is out of scope. Do not publish a repository, open a pull request, or assume this directory supports commits.


