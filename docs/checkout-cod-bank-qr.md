# Đặt hàng PawMart: COD và QR chuyển khoản

## Luồng khách hàng

1. Giỏ hàng lấy giá và tồn kho hiện tại từ BE. Màn xác nhận gọi `POST /api/cart/checkout/quote` để hiện từng dòng, giảm giá, phí giao và tổng tiền.
2. Khách nhập người nhận, số điện thoại, địa chỉ, chọn **COD** hoặc **QR chuyển khoản**. Phương thức khả dụng lấy từ `GET /api/cart/checkout/payment-methods`.
3. `POST /api/cart/checkout` gửi `expectedQuoteFingerprint`. Nếu giỏ, giá hoặc khuyến mãi đổi, BE trả 409 và Flutter yêu cầu xem tổng mới.
4. Đơn và bản ghi thanh toán được tạo trong cùng transaction; tồn kho trừ một lần và giỏ được xóa. Đơn mới là `PENDING`, thanh toán mới là `PENDING` cho cả COD lẫn QR.
5. COD: khách trả khi nhận hàng. Trạng thái giao `DELIVERED` không tự ghi là đã thu tiền. Admin xác nhận đã thu đúng số tiền sau khi giao.
6. QR: khách mở chi tiết đơn, quét mã VietQR hoặc sao chép số tài khoản và nội dung chuyển khoản riêng của đơn. Admin kiểm tra sao kê, nhập số tiền thực nhận và mã giao dịch ngân hàng rồi xác nhận. Đơn QR chưa xác nhận thanh toán không thể chuyển sang `CONFIRMED`.

Ứng dụng không tự đọc số dư ngân hàng và không coi việc hiển thị QR hay khách bấm nút là chứng cứ đã thanh toán. Không nhập thông tin chuyển khoản của khách vào một endpoint công khai để tự đánh dấu đã trả tiền.

## Cấu hình ngân hàng

`BankQr:BankBin`, `BankQr:BankName`, `BankQr:AccountNo`, `BankQr:AccountName` được đọc từ cấu hình BE. Tài khoản thực tế đã được đặt trong .NET User Secrets trên máy phát triển; không ghi số tài khoản vào Git. Trên máy khác, đặt bốn khóa bằng `dotnet user-secrets set` trong Development hoặc biến môi trường `BankQr__BankBin`, `BankQr__BankName`, `BankQr__AccountNo`, `BankQr__AccountName` ở môi trường chạy thật. Nếu thiếu khóa, API báo QR chưa khả dụng và Flutter chỉ cho chọn COD.

Mã QR dùng VietQR Quick Link `img.vietqr.io`. Địa chỉ nhận, số tiền và nội dung chuyển khoản được lưu theo đơn tại thời điểm tạo đơn, nên việc đổi cấu hình về sau không thay đổi thông tin của đơn cũ. Nội dung là `PM` + 20 ký tự đầu mã đơn, dài dưới giới hạn VietQR. Khi tải ảnh QR, VietQR nhận các thông tin cần thiết để tạo ảnh. Không tích hợp API `/v2/generate` đã được VietQR thông báo ngừng hỗ trợ.

## Database và API

Chạy `Database/2026-11-06_BankQrCheckout.sql` sau baseline/import hiện có. Migration thêm `BANK_QR` vào ràng buộc phương thức, bốn cột chụp thông tin tài khoản trong `Payments` và chỉ mục chặn tái sử dụng mã giao dịch ngân hàng. Baseline `Database/PetFoodDB_PRM393_Complete_SQLServer.sql` cũng đã cập nhật. Sao lưu database trước khi chạy migration.

- `GET /api/cart/checkout/payment-methods`: phương thức khả dụng, yêu cầu khách đăng nhập.
- `POST /api/cart/checkout/quote`: báo giá hiện tại.
- `POST /api/cart/checkout`: đặt hàng với COD hoặc BANK_QR.
- `GET /api/orders/{orderId}/payment/qr`: chỉ chủ đơn QR được xem hướng dẫn chuyển khoản; đơn hủy không trả QR.
- `POST /api/admin/orders/{orderId}/payment/confirm`: chỉ admin, yêu cầu đúng tổng tiền; QR phải có mã giao dịch từ sao kê, COD chỉ xác nhận sau giao hàng.

## Kiểm tra ngày 06/11/2026

Đã sao lưu và `RESTORE VERIFYONLY` file `C:\Users\Public\Documents\PetFoodDB-before-checkout-qr-20261106.bak`. Migration chạy trên `PetFoodDB`; database chính sau đó vẫn 0 đơn, 2 người dùng, 1 dòng giỏ, 41 sản phẩm CAT41 còn hoạt động với tồn tối thiểu 120.

Đã sao chép DB sang `PetFoodDB_CheckoutQA` và chạy quy trình thử qua API trên cổng 5166. COD: chặn ghi nhận tiền trước giao, qua đủ ba trạng thái, chỉ chuyển thanh toán thành `SUCCESS` khi admin ghi nhận tiền. QR: đúng tổng tiền/tài khoản/nội dung riêng, ảnh VietQR trả HTTP 200; chặn duyệt đơn trước trả tiền, sai tiền, thiếu/trùng mã ngân hàng, xác nhận lặp; đơn hủy không trả QR và có `payment_status = CANCELLED`. Mọi đơn thử nằm trong DB QA.

Trên Android Emulator, đã chọn QR trong màn xác nhận đơn, đặt một đơn thử ở DB QA và mở chi tiết đơn. Màn hình hiển thị mã QR, tổng tiền và nội dung riêng của đơn; ảnh QR tải thành công. Ứng dụng nhắc khách kiểm tra tên thụ hưởng trong ứng dụng ngân hàng trước khi chuyển khoản.

Chưa có webhook xác minh giao dịch tự động từ ngân hàng hoặc nhà cung cấp đối soát. Với tài khoản nhận hiện tại, admin phải kiểm tra sao kê trước khi xác nhận; không dùng kết quả thử trên DB QA làm chứng cứ chuyển khoản thật.
