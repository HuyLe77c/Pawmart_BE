# Catalog PawMart: 41 mẫu có ảnh và mô tả

Cập nhật 2026-11-06. Danh sách tên tiếng Việt, model và URL nguồn nằm trong `Database/catalog/catalog-41-sources.csv`. Trạng thái `VERIFIED` của model chỉ xác nhận danh tính mẫu; không xác nhận quyền dùng ảnh, giá VNĐ, tồn kho hoặc toàn bộ nội dung. Giá bán và tồn kho ban đầu trong `Database/catalog/catalog-41-owner-prices.csv` do chủ dự án cho phép PawMart tự đặt; đây không phải giá thị trường đã xác minh hay kiểm kê hàng thật.

| Nhóm | Số mẫu |
| --- | ---: |
| Thức ăn | 8 |
| Đồ chơi | 8 |
| Chăm sóc và vệ sinh | 11 |
| Thực phẩm bổ sung | 9 |
| Phụ kiện và thiết bị | 5 |

38 model có nguồn hãng phù hợp. A01 chưa tìm được trang hãng Việt Nam; A04 cần xác minh tên công thức “Lamb & Rice” trên nhãn; E05 dùng trang PETKIT Đức của PURAMAX 2 thay cho URL hãng cũ đã chuyển sang model khác. Bộ dữ liệu ZIP do người dùng cung cấp có 41 mô tả ngắn tiếng Việt nhưng không chứa ảnh hay giá; mô tả hiển thị được bổ sung từ `Database/catalog/catalog-41-curated-details.vi.tsv` dựa trên trang sản phẩm tương ứng. Đã lấy 41 ảnh, đối chiếu bằng bảng ảnh và lưu trong `Pawmart/wwwroot/uploads/products`; bảng URL và SHA-256 ở `Database/catalog/catalog-41-image-sources.csv`. Trạng thái ảnh `VERIFIED` trong database chỉ có nghĩa đã đối chiếu ảnh với model. Quyền sử dụng ảnh cần được xác nhận riêng trước khi phát hành công khai. Có 18 quy cách tham khảo trong `Database/catalog/catalog-41-variant-leads.csv`, tất cả `price_vnd = NULL`.

## Trạng thái trên app

41 mẫu mới đã mở bán trong trang chủ, tìm kiếm và chi tiết, có ảnh, mô tả, giá VNĐ và nút mua. Mỗi SKU có một quy cách bán trong `Products.package_size`, giá trong `Products.price` và số tồn ban đầu 120. Các quy cách tham khảo trong `Catalog_Product_Variants` chưa tham gia giỏ hàng, chưa có giá và không được hiển thị như các lựa chọn mua. Năm nhóm catalog mới xuất hiện trong bộ lọc. Chưa có thành phần và hướng dẫn dùng đầy đủ cho tất cả mẫu, và tồn kho 120 là số do chủ dự án chỉ định cho app.

Mười sản phẩm demo có `is_listed = 0`, nên không còn trong danh sách khách hàng. Bản ghi và `is_active` được giữ nguyên để không làm mất dòng giỏ hàng cũ hoặc phá quan hệ đơn hàng.

Backend cho kích hoạt khi ảnh đã được đối chiếu, có ảnh lưu, giá dương, tồn kho dương và một trong hai luồng duyệt: nguồn giá/model/nội dung được xác minh; hoặc giá `OWNER_SET` và sản phẩm `OWNER_APPROVED` theo quyết định chủ dự án. Ba model A01/A04/E05 vẫn giữ `NEEDS_REVIEW`; không đánh dấu đã xác minh giả. Giá chủ dự án đặt không có `price_source_url` hay `price_checked_at`. Trạng thái xem trước `public_preview` đã tắt khi kích hoạt.

## Database và chạy lại

Bản sao lưu trước khi công khai bản xem trước: `C:\Users\Public\Documents\PetFoodDB-before-public-preview-20261106.bak`; đã chạy `RESTORE VERIFYONLY`.

- Database hiện có chưa có catalog: chạy `Database/migrations/2026-11-05_CatalogReview.sql`, `Database/migrations/2026-11-05_Catalog41_Upsert.sql`, `Database/migrations/2026-11-06_CatalogPublicPreview.sql`, `Database/migrations/2026-11-06_Catalog41_Enrichment.sql`, rồi `Database/migrations/2026-11-06_Catalog41_OwnerPricing.sql`.
- Database đã nhập catalog và ảnh: chạy `Database/migrations/2026-11-06_Catalog41_OwnerPricing.sql`.
- Database mới: baseline `Database/PetFoodDB_PRM393_Complete_SQLServer.sql` đã có schema; chạy import, enrichment và owner pricing. Sao chép cả `Pawmart/wwwroot/uploads/products` khi chuyển sang máy khác.
- Mỗi file SQL chạy bằng `sqlcmd -S localhost\SQLEXPRESS -d PetFoodDB -E -b -f 65001 -i <tệp>`. Khi sửa bảng giá CSV, chạy `python Database/catalog/build_catalog_41_owner_pricing.py`; migration không nạp lại tồn kho của sản phẩm đã duyệt bán khi chạy lại. Bảng CSV là nguồn giá chủ dự án tự đặt, không phải chứng từ nhập kho.

## Kiểm tra đã thực hiện

- Import và migration giữ đúng 41 SKU, 19 danh mục con, 18 quy cách tham khảo, không có SKU trùng. Database có 51 sản phẩm tổng cộng: 41 sản phẩm đang bán và 10 bản demo được giữ lại. Hai người dùng, một dòng giỏ hàng và 10 ảnh cũ còn nguyên.
- Sao lưu trước khi nhập ảnh và mô tả: `C:\Users\Public\Documents\PetFoodDB-before-41-enrichment-20261106.bak`; `RESTORE VERIFYONLY` thành công. API trả 41 mô tả và 41 ảnh; cả 41 URL ảnh trả HTTP 200 với kiểu `image/jpeg`. Chạy lại enrichment không tạo thêm ảnh trùng.
- Backend build: 0 lỗi, 0 cảnh báo. API khách hàng trả 41 mẫu mới, 5 danh mục, không trả demo trong danh sách. Tìm “KONG” trả 8 mẫu; lọc chó trả 33; lọc Thức ăn trả 8. Sau migration giá, API danh sách và chi tiết đều trả giá 199.000 đ, tồn 120 và quy cách 800 g cho CAT41-A01; cả 41 mẫu có giá dương, đang bán và `OWNER_APPROVED`.
- Sao lưu trước khi mở bán: `C:\Users\Public\Documents\PetFoodDB-before-owner-pricing-20261106.bak`; `RESTORE VERIFYONLY` thành công. So sánh API với CSV từng dòng: 41/41 đúng giá, quy cách, tồn kho, ảnh và trạng thái bán. Số người dùng vẫn là 2, giỏ hàng vẫn 1 dòng.
- Flutter build/cài/mở trên Pixel 8 Android 17 (API 37). Đã kiểm tra màn danh sách 41 sản phẩm có ảnh, bộ lọc Thức ăn và chi tiết Royal Canin Mini Adult có ảnh/mô tả. Ảnh kiểm tra: `C:\Users\cubin\AppData\Local\Temp\pawmart-final-41-photos.png`, `C:\Users\cubin\AppData\Local\Temp\pawmart-food-filter.png`, `C:\Users\cubin\AppData\Local\Temp\pawmart-final-41-detail.png`.
- Widget test thẻ sản phẩm ở cỡ chữ 1.3 và test view model catalog đạt. Flutter analyze các file sửa: không có vấn đề. Màn admin chưa được kiểm thử bằng tài khoản đăng nhập.
- Bản Android mới đã cài và mở trên `emulator-5554`: trang chủ hiện giá và nút mua; chi tiết A01 hiện 199.000 đ, tồn 120, quy cách 800 g và nút thêm giỏ. Ảnh kiểm tra: `C:\Users\cubin\AppData\Local\Temp\pawmart-owner-pricing-android.png` và `C:\Users\cubin\AppData\Local\Temp\pawmart-owner-pricing-detail.png`. Khi bấm thêm giỏ, app chuyển tới đăng nhập do phiên cũ hết hạn sau khi khởi động lại BE; chưa kiểm thử đơn hàng bằng tài khoản đăng nhập.

## Giới hạn khi sử dụng ngoài bản demo

Xác nhận quyền sử dụng ảnh, đơn vị đóng gói và tồn kho vật lý trước khi nhận đơn thực tế. Giá và tồn kho hiện là cấu hình chủ dự án chấp thuận cho bản app; không có nguồn giá thị trường hay số liệu kho vật lý. Các sản phẩm bổ sung không được mô tả như thuốc.
