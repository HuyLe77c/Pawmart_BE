# PawMart Product Image Upload Implementation Plan

> **For agentic workers:** Implement the approved plan inline. The workspace is not a Git checkout, so do not create commits or push anything.

**Goal:** Support admin product image URLs and uploads from a device, with uploaded files served by the API and references stored in `Product_Images`.

**Architecture:** An ADMIN-only multipart API endpoint validates image bytes, writes a generated filename under `wwwroot/uploads/products`, and saves its relative path in the existing image table. Flutter adds an `image_picker` control, multipart upload, local preview, and resolves relative image paths against the API base URL.

**Tech Stack:** ASP.NET Core, EF Core, SQL Server, Flutter/Dart, `http`, `image_picker`.

**Spec:** `docs/superpowers/specs/2026-10-07-pawmart-product-image-upload-design.md`

## Global Constraints

- Maximum image size is 20 MiB.
- Accept JPEG, PNG, and WebP based on signature validation.
- Keep image binaries in API `wwwroot/uploads/products`; store image references only in SQL Server.
- Keep external absolute HTTP(S) image URLs working.
- Upload requires ADMIN authorization; image GETs are public.
- Do not add DB columns or migrations.

## Review Focus

- Exactly 20 MiB succeeds and anything larger is rejected before persistence.
- Spoofed MIME type, extension, or file name cannot bypass signature validation or control the storage path.
- Relative uploads resolve to the API host on Android emulator while external HTTPS URLs continue to render.
- An upload cannot modify another product or be invoked without ADMIN authorization.
- If DB persistence fails, the newly written file is cleaned up; replaced managed files are deleted only after persistence succeeds.

---

### Task 1: API storage and upload endpoint

**Files:**
- Create `Services/ProductImageStorageService.cs`.
- Modify `Program.cs`, `Controllers/AdminProductsController.cs`, `Services/CatalogServices.cs`, and `Models/Contracts/Catalog/CatalogContracts.cs`.

**Interfaces:** `ProductImageStorageService.UploadPrimaryAsync(int productId, IFormFile file, CancellationToken ct) -> Task<ProductImageResponse>`; endpoint `POST /api/admin/products/{id}/images/upload` with multipart field `file`.

- [x] Add a scoped storage service, safe random filenames, JPEG/PNG/WebP signature validation, 20 MiB checks, public static-file serving, and rollback cleanup.
- [x] Replace the primary image safely and remove replaced API-managed files only after DB commit.
- [x] Allow safe `/uploads/products/...` references and absolute HTTP(S) URLs in product write requests.
- [x] Build API and verify public catalog, API readiness, and upload authorization response.

### Task 2: Flutter picker and upload

**Files:**
- Modify Flutter `pubspec.yaml`, `lib/data/services/api_client.dart`, `lib/ui/core/widgets/product_artwork.dart`, and `lib/ui/features/admin/views/admin_screens.dart`.

**Interfaces:** `ApiClient.postMultipart(String path, {required String fileFieldName, required String filePath}) -> Future<dynamic>`; `ApiClient.resolveMediaUri(String url) -> Uri`.

- [x] Add `image_picker` and authenticated multipart request/error handling.
- [x] Add device selection, local preview, 20 MiB client check, URL input fallback, and upload-on-save behavior for new and existing products.
- [x] Resolve relative media URLs against API base URL in all shared product artwork surfaces.
- [x] Run Flutter analysis/build and launch the app in the Android emulator.

### Task 3: End-to-end verification and documentation

**Files:**
- Modify API and Flutter README files with upload path, limit, supported formats, and local-storage backup note.

- [x] Verify unauthenticated uploads fail and static images are publicly readable.
- [ ] Verify an authenticated upload persists its relative URL and the catalog returns it.
- [ ] Verify invalid signature and oversize requests do not create DB rows or permanent files.
- [ ] Verify selected-image preview/upload and catalog rendering on the Android emulator; record any account prerequisite if no ADMIN token is available.

## Verification record

- API build: succeeded with 0 warnings and 0 errors.
- Swagger: 200; upload route present in generated OpenAPI JSON.
- API health/catalog: database available; catalog returns 10 sample products.
- Authorization: upload request without an ADMIN token returns 401.
- Static files: a temporary PNG returned 200 with `image/png`; the temporary file was removed.
- Flutter: `flutter analyze --no-fatal-infos` exited 0 (32 existing `info` lint messages); debug APK built and installed on Pixel_8.
- Remaining manual check: an authenticated ADMIN upload, database row persistence for its relative URL, the exact 20 MiB boundary, and unsupported-image rejection. No ADMIN credentials were available for that manual end-to-end flow. Sample product image rows remain empty until an admin supplies an external URL or uploads a device image.
