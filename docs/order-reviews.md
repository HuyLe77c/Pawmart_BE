# Đánh giá đơn hàng PawMart

Sau khi đơn ở trạng thái `DELIVERED` **và** thanh toán `SUCCESS`, chủ đơn được gửi một đánh giá 1–5 sao với nhận xét tùy chọn (2–600 ký tự nếu có). Mỗi đơn chỉ được đánh giá một lần. Đơn chờ giao, chờ ghi nhận tiền hoặc đã hủy không được đánh giá. Admin và người dùng khác không thể gửi đánh giá thay chủ đơn.

Khách chọn có chia sẻ công khai hay không; mặc định là **không**. Chỉ đánh giá được chủ đơn cho phép và admin chưa ẩn mới xuất hiện trên trang chủ. Phần công khai chỉ trả tên gọi, sao, nhận xét, phản hồi cửa hàng và thời gian; không trả mã đơn, mã người dùng, số điện thoại hay địa chỉ. Khách luôn thấy đánh giá của mình trong chi tiết đơn, kể cả khi admin ẩn khỏi trang chủ. Admin có thể phản hồi và ẩn/hiện đánh giá đã được chia sẻ; không xóa lịch sử đánh giá.

## API

- `POST /api/orders/{orderId}/review` (khách đăng nhập): `{ "rating": 1..5, "comment": "...", "sharePublicly": false }`.
- `GET /api/orders/{orderId}` và `GET /api/orders`: gồm đánh giá của đơn hoặc số sao đã đánh giá.
- `GET /api/reviews`: chỉ đánh giá được chia sẻ và còn hiển thị, kèm tổng số và điểm trung bình tính từ dữ liệu thật.
- `GET /api/admin/reviews?page=1&pageSize=20`: danh sách đầy đủ cho admin.
- `PUT /api/admin/reviews/{reviewId}/reply`: `{ "reply": "..." }`; chuỗi trống gỡ phản hồi.
- `PUT /api/admin/reviews/{reviewId}/visibility`: `{ "isVisible": false }`.

## Database

Sao lưu database, rồi chạy `Database/migrations/2026-11-06_OrderReviews.sql` sau migration COD/QR. Script có thể chạy lại; tạo `dbo.OrderReviews`, khóa duy nhất theo đơn, khóa ngoại, giới hạn số sao và độ dài nhận xét. Baseline `Database/PetFoodDB_PRM393_Complete_SQLServer.sql` được cập nhật cùng cấu trúc. Bản sao lưu trước khi áp dụng trên máy phát triển: `C:\Users\Public\Documents\PetFoodDB-before-order-reviews-20261106.bak` (`RESTORE VERIFYONLY` đã kiểm tra).

## Kiểm tra ngày 06/11/2026

Đã chạy migration và thử API trong `PetFoodDB_CheckoutQA`: COD/QR, điều kiện giao và ghi nhận tiền, xác thực chủ đơn, sao/nhận xét không hợp lệ, chống gửi trùng, mặc định riêng tư, tự chọn công khai, số liệu công khai, phản hồi, ẩn/hiện, phân quyền admin. Các đơn và đánh giá thử đều nằm trong database QA. Database chính sau migration vẫn có 0 đơn và 0 đánh giá.

Trên Android Emulator đã kiểm tra đặt đơn COD, trạng thái chưa đủ điều kiện đánh giá, chuyển sang đã giao/đã thanh toán, gửi đánh giá 4 sao có chọn chia sẻ, hiển thị đánh giá thật trên trang chủ và danh sách quản trị. Flutter test: 12 bài qua; APK debug đã build, cài và mở với backend chính. Backend chính và QA đều trả HTTP 200 cho API sản phẩm; database chính có 41 sản phẩm đang bán, tất cả tồn kho từ 100 trở lên.
