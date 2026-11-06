# Bản đồ và địa chỉ PawMart

Khách mở **Ghé PawMart** từ trang chủ hoặc danh mục để xem địa chỉ cơ sở, bản đồ, sao chép địa chỉ và mở chỉ đường. Dữ liệu lấy từ `GET /api/stores`; màn khách chỉ lấy các điểm `STORE` đang hoạt động. Kho hàng không hiển thị như một địa điểm mua trực tiếp.

Địa chỉ demo do chủ dự án chọn: **46/2 Tân Lập 1, phường Tăng Nhơn Phú, Thành phố Hồ Chí Minh, Việt Nam**. Tọa độ `10.8483153, 106.7773630` là vị trí gần đường Tân Lập 1 tra được trên bản đồ, **chưa xác nhận đúng cổng số nhà 46/2**. App ghi rõ độ chính xác này và nút chỉ đường tìm bằng địa chỉ chữ. Admin có thể cập nhật tọa độ chính xác trong mục **Quản lý → Điểm bán**.

Ở checkout, khách có thể nhập địa chỉ thủ công; nút **Chọn trên bản đồ** cho phép tìm địa chỉ đã nhập, dùng GPS theo yêu cầu, hoặc chạm chọn ghim. Kết quả địa chỉ tự điền từ dịch vụ geocoding của Android có thể thiếu số nhà; khách phải kiểm tra và bổ sung trước khi xác nhận. Nếu dịch vụ định vị hoặc geocoding lỗi, khách vẫn nhập địa chỉ thủ công và đặt hàng. Đơn có tọa độ sẽ hiển thị lại ghim trong chi tiết đơn. Đây là điểm nhận hàng, không phải vị trí theo dõi shipper theo thời gian thực.

Áp dụng dữ liệu trên máy đã có database: sao lưu database rồi chạy `sqlcmd -S localhost\SQLEXPRESS -E -d PetFoodDB -f 65001 -i Database/2026-11-06_StoreLocation.sql -b`. Script chỉ thay bản ghi cửa hàng mẫu, có thể chạy lại. File tạo database mới `Database/PetFoodDB_PRM393_Complete_SQLServer.sql` đã cập nhật seed tương ứng. Tùy chọn `-f 65001` cần thiết để giữ nguyên dấu tiếng Việt khi chạy bằng SQLCMD.

Bản sao lưu trước cập nhật ngày 06/11/2026: `C:\Users\Public\Documents\PetFoodDB-before-store-map-20261106.bak`; `RESTORE VERIFYONLY` đã thành công.
