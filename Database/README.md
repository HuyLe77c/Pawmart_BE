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

Để bật đăng nhập Google trên database cũ, chạy `migrations/2026-11-08_GoogleSignIn.sql` sau khi sao lưu; xem `docs/google-sign-in.md` để thiết lập OAuth.

Để lưu lịch sử tư vấn AI trên database cũ, chạy `migrations/2026-11-09_ChatHistory.sql` trước khi khởi động backend mới.

Để thêm giới tính cho hồ sơ thú cưng trên database cũ, chạy `migrations/2026-11-09_PetGender.sql` trước khi khởi động backend mới. Hồ sơ hiện có sẽ để trống giới tính cho đến khi được cập nhật.

Các file Python trong `catalog/` chỉ dùng khi cần tạo lại SQL catalog từ dữ liệu nguồn. Chúng ghi kết quả vào `migrations/`; không cần chạy chúng để khởi động BE hoặc dùng app.

## Nạp dữ liệu mẫu để thử ứng dụng

Sau khi tạo schema bằng `PetFoodDB_PRM393_Complete_SQLServer.sql`, chạy `PetFoodDB_Demo_Seed_SQLServer.sql` trong SSMS (hoặc `sqlcmd -S <server> -d PetFoodDB -E -b -f 65001 -i Database/PetFoodDB_Demo_Seed_SQLServer.sql`). File này **xóa toàn bộ dữ liệu hiện có** trong các bảng PawMart rồi tạo lại 3 tài khoản, 3 thú cưng, 10 sản phẩm có URL ảnh công khai, giỏ hàng, yêu thích, khuyến mãi, 4 đơn hàng ở các trạng thái khác nhau, thanh toán, đánh giá và thông báo. Chỉ chạy trên database dùng để demo; thao tác được bọc trong một transaction.

Tài khoản mẫu: `admin.demo@pawmart.test`, `lan.demo@pawmart.test`, `minh.demo@pawmart.test`. Mật khẩu chung: `PawMartDemo@2026`. Đổi mật khẩu hoặc xóa dữ liệu mẫu trước khi đưa database ra môi trường thật. Ảnh sản phẩm dùng URL từ `catalog/catalog-41-image-sources.csv`; các trang nguồn có thể thay đổi đường dẫn hoặc chặn tải trực tiếp trong tương lai.
