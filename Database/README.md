# Cơ sở dữ liệu PawMart

Thư mục này chỉ giữ **một file SQL ở cấp chính**: `PetFoodDB_PRM393_Complete_SQLServer.sql`. File này tạo cấu trúc mới nhất cho database `PetFoodDB` mới hoặc trống. Nó không tự nạp 41 sản phẩm catalog. Không chạy nó đè lên database đang có dữ liệu.

| Thư mục | Mục đích |
| --- | --- |
| `migrations/` | Các bản cập nhật SQL cho database đã tồn tại và các bước nhập catalog. |
| `catalog/` | Dữ liệu nguồn, công cụ tạo SQL và ảnh đối chiếu của 41 sản phẩm. |
| `backup/` | Bản sao lưu `PetFoodDB-20261018.bak` ngày 18/10/2026; bản này cũ hơn catalog và các tính năng thanh toán hiện tại. |

## Tạo database mới có 41 sản phẩm

1. Chạy `PetFoodDB_PRM393_Complete_SQLServer.sql` trong SSMS trên SQL Server. Chỉ làm bước này khi chưa có `PetFoodDB` hoặc database đang trống.
2. Từ thư mục gốc repo, chạy lần lượt năm file sau bằng SSMS hoặc `sqlcmd -S localhost\SQLEXPRESS -d PetFoodDB -E -b -f 65001 -i <đường-dẫn-file>`:
   1. `Database/migrations/2026-11-05_CatalogReview.sql`
   2. `Database/migrations/2026-11-05_Catalog41_Upsert.sql`
   3. `Database/migrations/2026-11-06_CatalogPublicPreview.sql`
   4. `Database/migrations/2026-11-06_Catalog41_Enrichment.sql`
   5. `Database/migrations/2026-11-06_Catalog41_OwnerPricing.sql`
3. Giữ nguyên `Pawmart/wwwroot/uploads/products/` khi chuyển project sang máy khác; SQL chỉ lưu đường dẫn ảnh. Giá và tồn kho ban đầu là số chủ dự án đặt cho catalog.

Các bảng/cột của những migration còn lại đã có trong file tạo database mới. Chỉ chọn migration riêng khi cập nhật một database cũ; sao lưu database trước khi cập nhật. Xem tài liệu tương ứng trong `docs/` để biết thứ tự và điều kiện của từng tính năng. Không chạy file tạo database mới sau khi khôi phục `.bak`.

Các file Python trong `catalog/` chỉ dùng khi cần tạo lại SQL catalog từ dữ liệu nguồn. Chúng ghi kết quả vào `migrations/`; không cần chạy chúng để khởi động BE hoặc dùng app.
