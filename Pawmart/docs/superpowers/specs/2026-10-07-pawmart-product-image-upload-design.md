# PawMart Product Image Upload Design

## Goal

Allow an administrator to keep using external product image URLs or select an image from an Android device and upload it to PawMart. The customer catalog must display uploaded images from the API while the database stores the image reference.

## Decisions

- Keep `dbo.Product_Images`; do not store image bytes in SQL Server.
- Save uploaded files under the API content root at `wwwroot/uploads/products` with generated names. Never use a client supplied filename as a storage path.
- Store a relative path such as `/uploads/products/{generated-name}.jpg` in `Product_Images.image_url`. Continue accepting absolute `http` and `https` URLs for externally hosted images.
- Limit an image to 20 MiB. Accept JPEG, PNG, and WebP after checking file signatures, not only filename or browser MIME type.
- Upload is ADMIN-only. Static image reads are public so catalog users do not need a token. Flutter resolves relative image paths against its configured API base URL (`10.0.2.2` on Android emulator).
- The admin product form supports one primary image, entered by URL or selected from the device. A picked file takes precedence; existing image remains until an upload succeeds. Replaced PawMart-managed files are deleted after the database update succeeds; external URLs are never deleted.
- If a product is created with a selected file, create the product first and upload the file using its returned product ID. Report separately if product save succeeds but image upload fails.

## API and storage

- Add an authenticated multipart endpoint `POST /api/admin/products/{id}/images/upload`, with a `file` field.
- Store the file with an unpredictable name and serve it through ASP.NET Core static files.
- Validate the product exists, enforce the 20 MiB limit at the request and file layers, permit only supported image signatures, and return the saved `ProductImageResponse`.
- Use the existing `Product_Images` table and make image URL validation accept only absolute HTTP(S) URLs or safe app-relative product-upload paths.
- Do not add database columns or migrations.

## Flutter

- Add the official `image_picker` package.
- Add an authenticated multipart method to `ApiClient` that preserves the existing JWT header and parses API errors.
- Extend the admin product form with device selection, local preview, size check, and a clear URL-versus-upload choice.
- Resolve relative server image paths in `ProductArtwork` using the configured API base URI; preserve external image URL behavior and the existing fallback.

## Verification

- Build the API and run Flutter analysis/build.
- Check unauthenticated upload is rejected, the public catalog keeps working, and saved image paths serve bytes.
- Run the app in the Android emulator and confirm uploaded image URLs render through `10.0.2.2`.
- Verify 20 MiB boundary, unsupported signatures, and external URL support.

## Operational note

The upload folder is local to the API host. Copy `wwwroot/uploads/products` together with the SQL database when moving the API or restoring a backup.
