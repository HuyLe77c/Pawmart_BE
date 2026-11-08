# Đăng nhập Google trên Android

BE xác minh Google ID token tại `POST /api/auth/google`, kiểm tra chữ ký, thời hạn, email đã xác minh và `aud` khớp Web OAuth Client ID. Sau đó BE cấp JWT PawMart. Google ID token không được lưu trong database.

## Thiết lập lần đầu

1. Trong Google Cloud Console, tạo OAuth consent screen và hai OAuth client cùng một project: **Android** (package name `com.example.pawmart`, SHA-1 của khóa ký APK) và **Web application**. Thêm tài khoản thử nghiệm vào consent screen nếu app còn ở chế độ Testing. Khi đổi package name hoặc khóa ký bản release, tạo/cập nhật Android client tương ứng.
2. Lấy **Web client ID** dạng `...apps.googleusercontent.com`, đặt trên BE bằng `dotnet user-secrets set "GoogleAuth:WebClientId" "<WEB_CLIENT_ID>" --project .\Pawmart\Pawmart.csproj` trong môi trường Development. Trên server dùng biến môi trường `GoogleAuth__WebClientId`. Đây là client ID công khai, không phải client secret.
3. Chạy migration `Database/migrations/2026-11-08_GoogleSignIn.sql` **một lần** trên database đang dùng. Database mới tạo từ script chính đã có cột và index tương ứng.
4. Giá trị Web Client ID phải trùng với BE. PawMart hiện đã có Client ID mặc định trong Flutter; khi đổi Google Cloud project, ghi đè lúc build/chạy bằng `--dart-define=GOOGLE_WEB_CLIENT_ID=<WEB_CLIENT_ID>`. Ví dụ: `flutter run -d emulator-5554 --dart-define=GOOGLE_WEB_CLIENT_ID=<WEB_CLIENT_ID>`.

Để xem SHA-1 bản debug, chạy `cd android` rồi `./gradlew signingReport` trong PowerShell, hoặc dùng `keytool -list -v -keystore "$env:USERPROFILE\.android\debug.keystore" -alias androiddebugkey -storepass android -keypass android`.

Tài khoản Google mới được tạo với vai trò CUSTOMER. Nếu email đã có tài khoản PawMart bằng mật khẩu, app yêu cầu đăng nhập bằng mật khẩu; hệ thống không tự liên kết để tránh chiếm quyền tài khoản. Admin chỉ đăng nhập bằng phương thức đã được cấp trước đó.
