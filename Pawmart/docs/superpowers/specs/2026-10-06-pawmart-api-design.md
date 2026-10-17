# Pawmart API Design

**Date:** 2026-10-06  
**Status:** Approved by user  
**Project:** PRM393 Pet Food Store

## Goal

Replace the default ASP.NET Core WeatherForecast template with a documented API for the Pawmart Flutter client. The API will use the supplied SQL Server schema, expose Swagger UI for development, and enforce the CUSTOMER and ADMIN roles.

The first implementation should provide the store's core flows and clean integration boundaries. Real payment gateway, map provider, Firebase, and AI credentials are external setup and are not assumed to exist. The API should make their configuration status explicit instead of reporting success for an unconfigured provider.

## Existing context

- The repository contains a single `net10.0` ASP.NET Core Web API project, currently using the WeatherForecast sample controller.
- The project currently references only `Microsoft.AspNetCore.OpenApi`.
- The supplied SQL Server script defines Users, Pets, Categories, Products, Product_Images, CartItems, Favorites, Stores, Promotions, Orders, OrderItems, OrderStatusHistory, Payments, and Notifications, plus useful indexes and views.
- The SQL script intentionally does not seed an administrator or persist AI chat history.
- The repository checkout does not currently contain Git metadata, so this design document cannot be committed from the present directory.

## Scope

### Included in the first API implementation

1. **API foundation:** configuration validation, SQL Server access, consistent error responses, DTO validation, Swagger UI, and bearer-token documentation.
2. **Authentication and authorization:** customer registration, login, current-user profile, role-based protection, and password hashing. Registration must always create CUSTOMER accounts; ADMIN accounts are provisioned out of band.
3. **Catalog:** public product listing, search/filter/pagination, product details and images, category listing, and ADMIN product/category CRUD with stock fields.
4. **Pet profiles:** authenticated customer CRUD limited to the owner's pets.
5. **Cart and checkout:** authenticated cart read/upsert/remove operations and order creation from the cart. Checkout recalculates all prices on the server, checks active products and inventory, records immutable order item price snapshots, decrements stock, applies an eligible promotion, records payment intent/status, clears purchased cart rows, and commits atomically.
6. **Orders:** customer order listing/details/status history and cancellation where allowed; ADMIN order listing/details/status transition. The SQL trigger records status changes; API code must not duplicate trigger history rows.
7. **Promotions, stores, notifications, and reporting:** public active promotion/store reads; ADMIN promotion/store management, notification creation, and basic sales/order/low-stock reporting; customer notification listing/read state.
8. **Integration boundaries:** provider interfaces and configuration for payment initiation/callback verification, shipping distance/fee calculation, FCM delivery, and AI recommendations. Shipping checkout supports an explicitly configured flat-fee fallback; distance quotes require a configured distance provider. Until credentials/configuration exist, other provider operations return an explicit unavailable response. AI recommendations are grounded only in active catalog products and may use a supplied pet profile; chat history remains stateless.

### Excluded from the first implementation

- Production payment settlement, refunds, and reconciliation for real merchant accounts.
- A full map UI or map SDK; the mobile app owns map interaction, while the API accepts validated coordinates and can use an optional distance provider.
- Persisted AI conversation history, multi-device FCM token management, multi-warehouse inventory, and advanced analytics.
- Database migrations or destructive schema recreation. The supplied SQL remains the schema source of truth; any necessary change must be additive, justified, and accompanied by a revised SQL script.

## API architecture

- Keep controllers thin and group routes by resource: `auth`, `products`, `categories`, `pets`, `cart`, `orders`, `promotions`, `stores`, `notifications`, `admin/reports`, and `chat/recommendations`.
- Use request/response DTOs; never serialize database entities directly. Never return password hashes, FCM tokens, or raw payment gateway secrets.
- Use ASP.NET Core password hashing (`PasswordHasher<TUser>`) and signed JWT bearer tokens. Read signing key and connection string from configuration/environment; do not commit secrets. Tokens include user ID and role claims.
- Map SQL tables explicitly through EF Core SQL Server configuration. Do not run automatic schema creation or migrations against the supplied database.
- Use database transactions for checkout and status/payment updates where multiple rows must remain consistent. Use conditional stock updates or suitable transaction isolation to prevent overselling.
- Return Problem Details for validation, missing resources, authorization, conflicts, and provider-unavailable errors. Use 409 for insufficient inventory/state conflicts and 503 when an operation requires an unconfigured external provider. If notification records are persisted but optional FCM delivery is unavailable, return the persisted result with `pushAvailable: false` so clients do not retry the broadcast as though it failed.
- Keep checkout available with a configured flat shipping fee when no distance provider is set; do not claim distance was calculated in that case.
- Enable Swagger UI only in Development by default. Document bearer authentication and response/error shapes. Expose health/readiness information without leaking connection strings or secrets.

## Authorization outline

| Capability | Anonymous | CUSTOMER | ADMIN |
|---|---:|---:|---:|
| Browse catalog, categories, stores, active promotions | Yes | Yes | Yes |
| Register/login | Yes | Yes | Yes |
| Manage own profile, pets, cart, orders, notifications | No | Own data only | No, except explicit admin management endpoints |
| Manage products, categories, promotions, stores, order statuses, reports, broadcast notifications | No | No | Yes |
| AI recommendation | No | Yes | Optional admin diagnostics only |

Every customer-owned query must filter by the authenticated user ID, not by a client-supplied owner ID.

## Database review

The supplied schema covers the stated MVP without an immediate structural change. API implementation must respect these existing details:

- `Orders.grand_total` is constrained to `subtotal_amount - discount_amount + shipping_fee`.
- `OrderStatusHistory` is populated by a trigger for initial and changed order status.
- EF Core's SQL Server mapping for `Orders` must disable the SQL `OUTPUT` clause because the table has that enabled trigger.
- `Payments` permits one row per order.
- Product/category/user deactivation uses `is_active`; normal API flows should soft-disable these records.
- AI conversation persistence and per-device FCM token storage are deliberately omitted for this MVP.

Before coding, verify entity mappings against the complete SQL script. If implementation exposes a required missing field or constraint, propose an additive SQL change in a separate reviewable script rather than silently changing the existing database initializer.

## Error and security behavior

- Validate lengths, enum values, nonnegative monetary values, quantities, and coordinate ranges at the API boundary as well as relying on database constraints.
- Never trust client-sent product prices, discount amounts, totals, user IDs, or order status.
- Normalize email and promotion codes consistently before lookup; store passwords only as one-way hashes.
- Payment callbacks must verify provider signatures before changing payment/order state; callback handlers must be idempotent.
- Log operational failures without logging passwords, bearer tokens, signing keys, FCM tokens, or unredacted payment payloads.
- Use pagination with bounded page size for catalog, orders, and notifications.

## Verification approach

- Build the API and verify OpenAPI/Swagger generation.
- Add focused automated tests for role/ownership rules, DTO validation, checkout totals/stock conflicts, order transition rules, and provider-unavailable behavior.
- Exercise SQL-backed flows against a disposable SQL Server database initialized from the supplied script; do not run the destructive initializer against a user's existing database.
- Verify Swagger exposes bearer auth and representative endpoints for each resource group.

## Rollout sequence

1. API foundation and configuration.
2. Authentication and role policies.
3. Catalog and pet profiles.
4. Cart, atomic checkout, orders, and status history.
5. Promotions, stores, notifications, and reports.
6. Optional integration adapters and Swagger documentation.

The written implementation plan should preserve these boundaries and identify which external integrations can only be validated after credentials are supplied.

