# Đặt hàng PawMart: COD và QR chuyển khoản

## Luồng khách hàng

1. Giỏ hàng lấy giá và tồn kho hiện tại từ BE. Màn xác nhận gọi `POST /api/cart/checkout/quote` để hiện từng dòng, giảm giá, phí giao và tổng tiền.
2. Khách nhập người nhận, số điện thoại, địa chỉ, chọn **COD** hoặc **QR chuyển khoản**. Phương thức khả dụng lấy từ `GET /api/cart/checkout/payment-methods`.
3. `POST /api/cart/checkout` gửi `expectedQuoteFingerprint`. Nếu giỏ, giá hoặc khuyến mãi đổi, BE trả 409 và Flutter yêu cầu xem tổng mới.
4. Đơn và bản ghi thanh toán được tạo trong cùng transaction; tồn kho trừ một lần và giỏ được xóa. Đơn mới là `PENDING`, thanh toán mới là `PENDING` cho cả COD lẫn QR.
5. COD: khách trả khi nhận hàng. Trạng thái giao `DELIVERED` không tự ghi là đã thu tiền. Admin xác nhận đã thu đúng số tiền sau khi giao.
6. QR: khách mở chi tiết đơn, quét mã VietQR hoặc sao chép số tài khoản và nội dung chuyển khoản riêng của đơn. Khi SePay được cấu hình và báo giao dịch đến, BE tự đối soát. Đủ tiền hợp lệ thì `Payments.payment_status = SUCCESS`; đơn vẫn `PENDING` cho tới khi admin duyệt xử lý. Nếu chưa kết nối SePay, admin tiếp tục kiểm tra sao kê và xác nhận thủ công. Đơn QR chưa thanh toán không thể chuyển sang `CONFIRMED`.

Ứng dụng không tự đọc số dư ngân hàng và không coi việc hiển thị QR hay khách bấm nút là chứng cứ đã thanh toán. Không nhập thông tin chuyển khoản của khách vào một endpoint công khai để tự đánh dấu đã trả tiền.

## Cấu hình ngân hàng

`BankQr:BankBin`, `BankQr:BankName`, `BankQr:AccountNo`, `BankQr:AccountName` được đọc từ cấu hình BE. Tài khoản thực tế đã được đặt trong .NET User Secrets trên máy phát triển; không ghi số tài khoản vào Git. Trên máy khác, đặt bốn khóa bằng `dotnet user-secrets set` trong Development hoặc biến môi trường `BankQr__BankBin`, `BankQr__BankName`, `BankQr__AccountNo`, `BankQr__AccountName` ở môi trường chạy thật. Nếu thiếu khóa, API báo QR chưa khả dụng và Flutter chỉ cho chọn COD.

Mã QR dùng VietQR Quick Link `img.vietqr.io`. Địa chỉ nhận, số tiền và nội dung chuyển khoản được lưu theo đơn tại thời điểm tạo đơn, nên việc đổi cấu hình về sau không thay đổi thông tin của đơn cũ. Nội dung là `PM` + 20 ký tự đầu mã đơn, dài dưới giới hạn VietQR. Khi tải ảnh QR, VietQR nhận các thông tin cần thiết để tạo ảnh. Không tích hợp API `/v2/generate` đã được VietQR thông báo ngừng hỗ trợ.

## Database và API

Chạy `Database/migrations/2026-11-06_BankQrCheckout.sql` sau baseline/import hiện có. Migration thêm `BANK_QR` vào ràng buộc phương thức, bốn cột chụp thông tin tài khoản trong `Payments` và chỉ mục chặn tái sử dụng mã giao dịch ngân hàng. Baseline `Database/PetFoodDB_PRM393_Complete_SQLServer.sql` cũng đã cập nhật. Sao lưu database trước khi chạy migration.

- `GET /api/cart/checkout/payment-methods`: phương thức khả dụng, yêu cầu khách đăng nhập.
- `POST /api/cart/checkout/quote`: báo giá hiện tại.
- `POST /api/cart/checkout`: đặt hàng với COD hoặc BANK_QR.
- `GET /api/orders/{orderId}/payment/qr`: chỉ chủ đơn QR được xem hướng dẫn chuyển khoản; đơn hủy không trả QR.
- `POST /api/admin/orders/{orderId}/payment/confirm`: chỉ admin, yêu cầu đúng tổng tiền; QR phải có mã giao dịch từ sao kê, COD chỉ xác nhận sau giao hàng.

## Kiểm tra ngày 06/11/2026

Đã sao lưu và `RESTORE VERIFYONLY` file `C:\Users\Public\Documents\PetFoodDB-before-checkout-qr-20261106.bak`. Migration chạy trên `PetFoodDB`; database chính sau đó vẫn 0 đơn, 2 người dùng, 1 dòng giỏ, 41 sản phẩm CAT41 còn hoạt động với tồn tối thiểu 120.

Đã sao chép DB sang `PetFoodDB_CheckoutQA` và chạy quy trình thử qua API trên cổng 5166. COD: chặn ghi nhận tiền trước giao, qua đủ ba trạng thái, chỉ chuyển thanh toán thành `SUCCESS` khi admin ghi nhận tiền. QR: đúng tổng tiền/tài khoản/nội dung riêng, ảnh VietQR trả HTTP 200; chặn duyệt đơn trước trả tiền, sai tiền, thiếu/trùng mã ngân hàng, xác nhận lặp; đơn hủy không trả QR và có `payment_status = CANCELLED`. Mọi đơn thử nằm trong DB QA.

Trên Android Emulator, đã chọn QR trong màn xác nhận đơn, đặt một đơn thử ở DB QA và mở chi tiết đơn. Màn hình hiển thị mã QR, tổng tiền và nội dung riêng của đơn; ảnh QR tải thành công. Ứng dụng nhắc khách kiểm tra tên thụ hưởng trong ứng dụng ngân hàng trước khi chuyển khoản.

Phần xác nhận thủ công trên là trạng thái tại thời điểm kiểm tra ngày 06/11. Tích hợp webhook SePay được bổ sung ngày 07/11; xem mục bên dưới. Không dùng kết quả thử trên DB QA làm chứng cứ chuyển khoản thật.

## Webhook SePay tự động (07/11/2026)

1. Sao lưu database, rồi chạy `Database/migrations/2026-11-07_SePayBankQrWebhook.sql` trên `PetFoodDB` sau migration QR ngày 06/11. Script tạo `BankQrWebhookEvents` để giữ ID giao dịch SePay, số tiền và kết quả đối soát, kể cả giao dịch không khớp. Baseline tạo DB mới cũng đã có bảng này.
2. Kết nối đúng tài khoản nhận QR trong SePay. Tạo webhook sự kiện **Có tiền vào**, định dạng **application/json**, chứng thực **API Key**, URL `https://<BE-public-domain>/api/payments/sepay-webhook`. SePay cần gọi được HTTPS công khai; `localhost` và `10.0.2.2` không dùng được cho webhook thật.
3. Tạo khóa ngẫu nhiên ít nhất 24 ký tự và đặt cùng giá trị tại cấu hình webhook SePay và `SePay:WebhookApiKey` của BE. Trên máy phát triển dùng `dotnet user-secrets set "SePay:WebhookApiKey" "<secret>" --project Pawmart/Pawmart.csproj`; trên máy chạy thật dùng biến môi trường `SePay__WebhookApiKey` hoặc kho bí mật. Không đưa khóa vào Git hay Flutter. Khi thiếu khóa, endpoint trả 503; khóa sai trả 401.
4. BE chỉ ghi nhận giao dịch tiền vào với mã `PM` + 20 ký tự của đơn QR, tài khoản nhận đúng với thông tin đã chụp lúc tạo đơn và số tiền hợp lệ. Các lần chuyển thiếu tiền được cộng theo ID giao dịch; QR sau đó hiển thị phần còn thiếu. Đơn đã nhận một phần tiền không thể hủy tự động; cửa hàng cần xử lý hoàn tiền trước. Khi đủ tiền, BE đánh dấu thanh toán `SUCCESS` một lần, lưu ID giao dịch và tạo thông báo cho khách. Webhook gửi lặp, sai tài khoản, đơn hủy hoặc mã không khớp không thể tự đánh dấu đã trả. Chuyển dư được ghi nhận trong `gateway_response` để admin kiểm tra và hoàn tiền phần dư nếu cần.
5. Flutter kiểm tra lại trạng thái mỗi 7 giây khi khách đang mở chi tiết đơn QR và dừng khi rời màn hình. Khách cũng có thể kéo xuống để tải lại. Không coi việc hiển thị QR hoặc khách bấm nút là bằng chứng đã thanh toán.

### Chạy BE cùng ngrok trên máy Windows

1. Cài ngrok bằng `winget install --id Ngrok.Ngrok --exact --source winget`. Lấy authtoken tại dashboard ngrok, rồi chạy `ngrok config add-authtoken <authtoken>` trong terminal trên chính máy chạy BE. Không gửi authtoken qua chat hoặc lưu vào repository. Nếu vừa cài bằng winget mà terminal chưa nhận lệnh `ngrok`, mở terminal mới.
2. Đặt khóa API webhook của SePay vào .NET User Secrets theo bước 3 ở trên, rồi chạy BE lại. Cùng khóa này phải được nhập vào cấu hình webhook trên dashboard SePay. Không dùng authtoken ngrok làm khóa webhook SePay: đây là hai khóa riêng.
3. Từ thư mục gốc BE, chạy `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-pawmart-with-ngrok.ps1`. Script dùng BE đang chạy tại cổng 5165 hoặc tự khởi động BE, kiểm tra `/health/ready`, mở tunnel HTTPS và in đầy đủ URL `https://.../api/payments/sepay-webhook` để dán vào SePay. Giữ terminal này mở khi muốn nhận thanh toán. Nếu dùng cổng BE khác, truyền `-Port <cổng>`.
4. Nếu URL ngrok thay đổi sau lần chạy mới, cập nhật trường URL webhook tại SePay. Chỉ cần sửa URL; đường dẫn `/api/payments/sepay-webhook`, sự kiện **Có tiền vào**, JSON và khóa API giữ nguyên. Kiểm tra webhook bằng chức năng gửi thử của SePay, sau đó dùng một khoản chuyển thật nhỏ với đúng nội dung QR của đơn để xác minh đối soát. Trên Android Emulator, Flutter vẫn gọi BE qua `10.0.2.2:5165`; không cần đổi URL app sang ngrok.

Ngrok phù hợp cho chạy thử trên máy đang mở. Webhook ngừng nhận khi BE, ngrok, máy tính hoặc mạng dừng; lúc đó cần kiểm tra giao dịch chưa đối soát trong SePay và gửi lại webhook khi BE hoạt động. Không coi một lần gọi thử webhook là xác nhận có tiền thật.

SePay gửi `Authorization: Apikey <secret>` và chấp nhận HTTP 201 với `{"success":true}`. Dùng chức năng **Gửi thử** hoặc Test Mode của SePay trước, sau đó kiểm tra một khoản chuyển thật có đúng nội dung và số tiền. Giao dịch không khớp nằm trong `BankQrWebhookEvents` để admin xử lý riêng; không tự gán vào đơn khác. Nếu webhook lỗi tạm thời trả 5xx, cần kiểm tra nhật ký webhook của SePay và gửi lại khi cần.

Kiểm tra local ngày 07/11: đã sao lưu `PetFoodDB` vào `C:\Users\Public\Documents\PetFoodDB-before-sepay-20261107.bak`, chạy `RESTORE VERIFYONLY`, áp dụng migration; sau đó database chính vẫn 0 đơn và 51 sản phẩm. Trên `PetFoodDB_CheckoutQA` (cũng đã sao lưu), gọi API thử cho khóa sai, hai khoản chuyển cộng dồn, webhook lặp, sai tài khoản, đơn hủy, mã không khớp, mã giao dịch ngân hàng lặp và giao dịch tiền ra. Chỉ hai khoản hợp lệ cộng đủ tiền mới đổi payment sang `SUCCESS`. Đây là giao dịch giả lập trên QA, không phải bằng chứng SePay đã kết nối tài khoản thật.

Trên Android Emulator, một đơn QR QA mới hiển thị 314.000đ. Sau webhook giả lập 100.000đ, màn chi tiết tự hiện đã nhận 100.000đ và QR đổi còn 214.000đ. Sau webhook 214.000đ tiếp theo, màn chi tiết tự hiện **Đã thanh toán thành công** mà không cần admin bấm xác nhận. Thử hủy một đơn QA khác đã nhận một phần tiền trả HTTP 409. Môi trường app được chuyển lại về `PetFoodDB` sau kiểm tra.
