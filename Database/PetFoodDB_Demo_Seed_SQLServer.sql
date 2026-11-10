/*
 PawMart demo seed for PetFoodDB (SQL Server).
 Run AFTER PetFoodDB_PRM393_Complete_SQLServer.sql, never on production data.
 This replaces ALL rows in the PawMart tables listed below. The transaction rolls
 back on error. Product images are external URLs and require internet access.
 Demo login password for all three accounts: PawMartDemo@2026
*/
USE PetFoodDB;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
    THROW 51050, 'Create PetFoodDB schema before running the demo seed.', 1;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    /* Child tables first. Do not disable constraints or triggers. */
    DELETE FROM dbo.Notifications;
    DELETE FROM dbo.OrderReviews;
    DELETE FROM dbo.BankQrWebhookEvents;
    DELETE FROM dbo.Payments;
    DELETE FROM dbo.OrderStatusHistory;
    DELETE FROM dbo.OrderItems;
    DELETE FROM dbo.Orders;
    DELETE FROM dbo.CartItems;
    DELETE FROM dbo.Favorites;
    DELETE FROM dbo.Catalog_Product_Variants;
    DELETE FROM dbo.Catalog_Product_Reviews;
    DELETE FROM dbo.Catalog_Subcategories;
    DELETE FROM dbo.Product_Images;
    DELETE FROM dbo.Products;
    DELETE FROM dbo.Pets;
    DELETE FROM dbo.Promotions;
    DELETE FROM dbo.Stores;
    DELETE FROM dbo.Categories;
    DELETE FROM dbo.Users;

    /* ASP.NET Core Identity PasswordHasher V3 / PBKDF2-SHA256, 100,000 rounds. */
    DECLARE @PasswordHash NVARCHAR(255) =
        N'AQAAAAEAAYagAAAAENSOoclCuvVcSOBeBlPBqcZ8MHuDjwP+QeGaizB8f0Iq8rczwvsk1fHOhY58toWPHw==';

    INSERT INTO dbo.Users (user_id, email, password_hash, full_name, phone_number, role)
    VALUES
        ('demo-admin', N'admin.demo@pawmart.test', @PasswordHash, N'Quản trị PawMart', '0900000001', 'ADMIN'),
        ('demo-lan', N'lan.demo@pawmart.test', @PasswordHash, N'Nguyễn Ngọc Lan', '0900000002', 'CUSTOMER'),
        ('demo-minh', N'minh.demo@pawmart.test', @PasswordHash, N'Trần Văn Minh', '0900000003', 'CUSTOMER');

    INSERT INTO dbo.Pets
        (user_id, pet_name, pet_type, breed, age_months, weight_kg, special_notes,
         body_size, activity_level, body_condition_score, feeding_habits, food_preferences)
    VALUES
        ('demo-lan', N'Milu', 'DOG', N'Poodle', 30, 5.20, N'Không ăn được thịt bò.',
         'SMALL', 'MODERATE', 5, N'Ăn hai bữa mỗi ngày.', N'Thích thức ăn hạt.'),
        ('demo-lan', N'Bông', 'CAT', N'Mèo ta', 18, 3.80, NULL,
         'SMALL', 'LOW', 5, N'Ăn ít mỗi lần.', N'Thích vị cá ngừ.'),
        ('demo-minh', N'Lucky', 'DOG', N'Golden Retriever', 48, 25.40, NULL,
         'LARGE', 'HIGH', 5, N'Ăn hai bữa mỗi ngày.', N'Thích đồ chơi nhai.');

    INSERT INTO dbo.Categories (category_name, image_url, description, sort_order, is_active)
    VALUES
        (N'Dog Food', N'https://www.petmart.vn/wp-content/uploads/2021/06/thuc-an-cho-cho-truong-thanh-co-nho-royal-canin-mini-adult1.jpg', N'Thức ăn dành cho chó', 1, 1),
        (N'Cat Food', N'https://marspetcareaprimocdn.petcare.global/6e85bfb2-2a80-4d09-800d-b3aa00a4ebfc/6e85bfb2-2a80-4d09-800d-b3aa00a4ebfc_DownloadAsJpg.jpg', N'Thức ăn dành cho mèo', 2, 1),
        (N'Treats', NULL, N'Bánh thưởng và đồ ăn vặt', 3, 1),
        (N'Accessories', N'https://cdn11.bigcommerce.com/s-k3kxaf4rop/products/2003/images/17372/d6942459-9ca0-4890-a0e2-d3e723874e63_medium__34276.1793893580.386.513.jpg?c=1', N'Đồ chơi và phụ kiện', 4, 1);

    INSERT INTO dbo.Stores (store_name, store_type, address, latitude, longitude, is_active)
    VALUES
        (N'PawMart Pet Store', 'STORE', N'46/2 Tân Lập 1, phường Tăng Nhơn Phú, TP. Hồ Chí Minh', 10.8483153, 106.7773630, 1),
        (N'PetFood Main Warehouse', 'WAREHOUSE', N'TP. Hồ Chí Minh, Việt Nam', 10.8500000, 106.7000000, 1);

    DECLARE @Dog INT = (SELECT category_id FROM dbo.Categories WHERE category_name = N'Dog Food');
    DECLARE @Cat INT = (SELECT category_id FROM dbo.Categories WHERE category_name = N'Cat Food');
    DECLARE @Accessories INT = (SELECT category_id FROM dbo.Categories WHERE category_name = N'Accessories');
    DECLARE @Store INT = (SELECT store_id FROM dbo.Stores WHERE store_name = N'PawMart Pet Store');

    INSERT INTO dbo.Products
        (category_id, sku, product_name, brand, description, package_size, life_stage,
         price, stock_quantity, low_stock_threshold, target_pet_type, is_featured)
    VALUES
        (@Dog, 'DEMO-A01', N'Royal Canin Mini Adult', N'Royal Canin', N'Thức ăn hạt cho chó trưởng thành giống nhỏ.', N'800 g', N'Trưởng thành', 185000, 32, 5, 'DOG', 1),
        (@Dog, 'DEMO-A05', N'Royal Canin Mini Puppy', N'Royal Canin', N'Thức ăn hạt cho chó con giống nhỏ.', N'800 g', N'Chó con', 215000, 18, 5, 'DOG', 1),
        (@Dog, 'DEMO-A06', N'Royal Canin Maxi Adult', N'Royal Canin', N'Thức ăn hạt cho chó trưởng thành giống lớn.', N'1 kg', N'Trưởng thành', 245000, 4, 5, 'DOG', 0),
        (@Cat, 'DEMO-A02', N'Royal Canin Indoor 27', N'Royal Canin', N'Thức ăn hạt cho mèo trưởng thành nuôi trong nhà.', N'400 g', N'Trưởng thành', 155000, 30, 5, 'CAT', 1),
        (@Cat, 'DEMO-A03', N'Whiskas Adult Tuna', N'Whiskas', N'Thức ăn hạt vị cá ngừ cho mèo trưởng thành.', N'1.1 kg', N'Trưởng thành', 115000, 40, 5, 'CAT', 0),
        (@Cat, 'DEMO-A07', N'Royal Canin Kitten', N'Royal Canin', N'Thức ăn hạt cho mèo con.', N'400 g', N'Mèo con', 170000, 0, 5, 'CAT', 0),
        (@Cat, 'DEMO-A08', N'Royal Canin Sterilised 37', N'Royal Canin', N'Thức ăn hạt cho mèo đã triệt sản.', N'400 g', N'Trưởng thành', 165000, 12, 5, 'CAT', 0),
        (@Accessories, 'DEMO-B01', N'KONG Classic', N'KONG', N'Đồ chơi cao su có thể nhét bánh thưởng cho chó.', N'S', N'Mọi lứa tuổi', 199000, 15, 5, 'DOG', 1),
        (@Accessories, 'DEMO-B02', N'KONG Extreme', N'KONG', N'Đồ chơi cao su cho chó nhai mạnh.', N'M', N'Mọi lứa tuổi', 259000, 9, 5, 'DOG', 0),
        (@Accessories, 'DEMO-B03', N'KONG SqueakAir Balls', N'KONG', N'Bóng phát tiếng kêu cho chó.', N'Gói 3 bóng', N'Mọi lứa tuổi', 145000, 22, 5, 'DOG', 0);

    /* Images sourced from Database/catalog/catalog-41-image-sources.csv. */
    INSERT INTO dbo.Product_Images (product_id, image_url, is_primary, display_order)
    VALUES
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-A01'), N'https://www.petmart.vn/wp-content/uploads/2021/06/thuc-an-cho-cho-truong-thanh-co-nho-royal-canin-mini-adult1.jpg', 1, 0),
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-A05'), N'https://marspetcareaprimocdn.petcare.global/c2698e01-24b8-4434-b780-b2490009a85b/c2698e01-24b8-4434-b780-b2490009a85b_DownloadAsJpg.jpg', 1, 0),
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-A06'), N'https://marspetcareaprimocdn.petcare.global/174a77ba-09ae-4328-acbb-b1ef00132462/174a77ba-09ae-4328-acbb-b1ef00132462_DownloadAsJpg.jpg', 1, 0),
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-A02'), N'https://marspetcareaprimocdn.petcare.global/6e85bfb2-2a80-4d09-800d-b3aa00a4ebfc/6e85bfb2-2a80-4d09-800d-b3aa00a4ebfc_DownloadAsJpg.jpg', 1, 0),
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-A03'), N'https://www.whiskas.com.vn/cdn-cgi/image/width=600,height=600,f=auto,quality=90/sites/g/files/fnmzdf8261/files/2025-07/9310022866203.png', 1, 0),
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-A07'), N'https://marspetcareaprimocdn.petcare.global/fa1241ac-7528-4e00-b045-b2490009a695/fa1241ac-7528-4e00-b045-b2490009a695_DownloadAsJpg.jpg', 1, 0),
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-A08'), N'https://cdn.royalcanin-weshare-online.io/SCEua2QBaxEApS7Lk_vW/v176/16-sterilised-37-b1-ru', 1, 0),
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-B01'), N'https://cdn11.bigcommerce.com/s-k3kxaf4rop/products/2003/images/17372/d6942459-9ca0-4890-a0e2-d3e723874e63_medium__34276.1793893580.386.513.jpg?c=1', 1, 0),
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-B02'), N'https://cdn11.bigcommerce.com/s-k3kxaf4rop/products/2000/images/15239/cccae0aa-2144-4383-ab38-6d07932ad5ba_medium__46782.1793893766.386.513.jpg?c=1', 1, 0),
        ((SELECT product_id FROM dbo.Products WHERE sku='DEMO-B03'), N'https://cdn11.bigcommerce.com/s-k3kxaf4rop/products/1949/images/14786/d49c96d1-360e-4cac-bede-fe366eae68ad_medium__45898.1793892967.386.513.jpg?c=1', 1, 0);

    INSERT INTO dbo.CartItems (user_id, product_id, quantity)
    VALUES
        ('demo-lan', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-A01'), 2),
        ('demo-lan', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-B01'), 1),
        ('demo-minh', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-A02'), 1);

    INSERT INTO dbo.Favorites (user_id, product_id)
    VALUES
        ('demo-lan', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-A02')),
        ('demo-lan', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-B01')),
        ('demo-minh', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-A06'));

    INSERT INTO dbo.Promotions
        (code, promotion_name, description, discount_type, discount_value,
         min_order_amount, max_discount_amount, start_at, end_at, usage_limit, used_count)
    VALUES
        ('DEMO10', N'Giảm 10% đơn mẫu', N'Áp dụng cho đơn từ 200.000đ.', 'PERCENT', 10, 200000, 50000,
         DATEADD(DAY,-30,SYSUTCDATETIME()), DATEADD(DAY,365,SYSUTCDATETIME()), 100, 1),
        ('DEMO30K', N'Giảm 30.000đ đơn mẫu', N'Áp dụng cho đơn từ 300.000đ.', 'FIXED', 30000, 300000, NULL,
         DATEADD(DAY,-30,SYSUTCDATETIME()), DATEADD(DAY,365,SYSUTCDATETIME()), 100, 0);

    DECLARE @Promo INT = (SELECT promotion_id FROM dbo.Promotions WHERE code='DEMO10');
    /* The schema trigger records one initial status history row per order. */
    INSERT INTO dbo.Orders
        (order_id, user_id, store_id, subtotal_amount, discount_amount, shipping_fee,
         grand_total, promotion_id, promotion_code_snapshot, shipping_address,
         recipient_name, recipient_phone, payment_method, order_status, created_at,
         confirmed_at, shipped_at, delivered_at, cancelled_at)
    VALUES
        ('DEMO-ORD-001', 'demo-lan', @Store, 340000, 34000, 20000, 326000, @Promo, 'DEMO10',
         N'12 Võ Văn Ngân, TP. Thủ Đức, TP. Hồ Chí Minh', N'Nguyễn Ngọc Lan', '0900000002', 'COD', 'DELIVERED',
         DATEADD(DAY,-12,SYSUTCDATETIME()), DATEADD(DAY,-12,SYSUTCDATETIME()), DATEADD(DAY,-11,SYSUTCDATETIME()), DATEADD(DAY,-10,SYSUTCDATETIME()), NULL),
        ('DEMO-ORD-002', 'demo-minh', @Store, 444000, 0, 20000, 464000, NULL, NULL,
         N'25 Nguyễn Văn Bá, TP. Thủ Đức, TP. Hồ Chí Minh', N'Trần Văn Minh', '0900000003', 'BANK_QR', 'SHIPPING',
         DATEADD(DAY,-3,SYSUTCDATETIME()), DATEADD(DAY,-2,SYSUTCDATETIME()), DATEADD(DAY,-1,SYSUTCDATETIME()), NULL, NULL),
        ('DEMO-ORD-003', 'demo-lan', @Store, 155000, 0, 20000, 175000, NULL, NULL,
         N'12 Võ Văn Ngân, TP. Thủ Đức, TP. Hồ Chí Minh', N'Nguyễn Ngọc Lan', '0900000002', 'COD', 'PENDING',
         DATEADD(HOUR,-4,SYSUTCDATETIME()), NULL, NULL, NULL, NULL),
        ('DEMO-ORD-004', 'demo-minh', @Store, 259000, 0, 20000, 279000, NULL, NULL,
         N'25 Nguyễn Văn Bá, TP. Thủ Đức, TP. Hồ Chí Minh', N'Trần Văn Minh', '0900000003', 'COD', 'CANCELLED',
         DATEADD(DAY,-7,SYSUTCDATETIME()), NULL, NULL, NULL, DATEADD(DAY,-6,SYSUTCDATETIME()));

    INSERT INTO dbo.OrderItems (order_id, product_id, quantity, unit_price)
    VALUES
        ('DEMO-ORD-001', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-A01'), 1, 185000),
        ('DEMO-ORD-001', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-A02'), 1, 155000),
        ('DEMO-ORD-002', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-A06'), 1, 245000),
        ('DEMO-ORD-002', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-B01'), 1, 199000),
        ('DEMO-ORD-003', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-A02'), 1, 155000),
        ('DEMO-ORD-004', (SELECT product_id FROM dbo.Products WHERE sku='DEMO-B02'), 1, 259000);

    INSERT INTO dbo.Payments
        (payment_id, order_id, payment_method, payment_status, amount,
         transaction_code, bank_bin, bank_name, bank_account_no, bank_account_name, paid_at)
    VALUES
        ('DEMO-PAY-001', 'DEMO-ORD-001', 'COD', 'SUCCESS', 326000, NULL, NULL, NULL, NULL, NULL,
         DATEADD(DAY,-10,SYSUTCDATETIME())),
        ('DEMO-PAY-002', 'DEMO-ORD-002', 'BANK_QR', 'SUCCESS', 464000, 'PAWMART-DEMO-ORD-002',
         '970422', N'MB Bank', '0000000000', N'PAWMART DEMO', DATEADD(DAY,-3,SYSUTCDATETIME())),
        ('DEMO-PAY-003', 'DEMO-ORD-003', 'COD', 'PENDING', 175000, NULL, NULL, NULL, NULL, NULL, NULL),
        ('DEMO-PAY-004', 'DEMO-ORD-004', 'COD', 'CANCELLED', 279000, NULL, NULL, NULL, NULL, NULL, NULL);

    INSERT INTO dbo.OrderReviews
        (order_id, user_id, author_name, rating, comment, share_publicly, is_visible)
    VALUES
        ('DEMO-ORD-001', 'demo-lan', N'Nguyễn Ngọc Lan', 5, N'Đóng gói cẩn thận, giao hàng đúng hẹn.', 1, 1);

    INSERT INTO dbo.Notifications (user_id, title, message, notification_type, reference_id, is_read)
    VALUES
        ('demo-lan', N'Đơn hàng đã giao', N'Đơn DEMO-ORD-001 đã được giao thành công.', 'ORDER', 'DEMO-ORD-001', 1),
        ('demo-lan', N'Đơn hàng mới', N'Đơn DEMO-ORD-003 đang chờ xác nhận.', 'ORDER', 'DEMO-ORD-003', 0),
        ('demo-minh', N'Đơn hàng đang giao', N'Đơn DEMO-ORD-002 đang trên đường giao.', 'ORDER', 'DEMO-ORD-002', 0);

    IF EXISTS
    (
        SELECT 1 FROM dbo.Orders AS o
        OUTER APPLY (SELECT SUM(oi.line_total) AS total FROM dbo.OrderItems AS oi WHERE oi.order_id=o.order_id) AS x
        WHERE o.subtotal_amount <> x.total
    )
        THROW 51051, 'Demo order totals do not match order items.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT N'Users' AS table_name, COUNT(*) AS row_count FROM dbo.Users
UNION ALL SELECT N'Pets', COUNT(*) FROM dbo.Pets
UNION ALL SELECT N'Categories', COUNT(*) FROM dbo.Categories
UNION ALL SELECT N'Products', COUNT(*) FROM dbo.Products
UNION ALL SELECT N'Product_Images', COUNT(*) FROM dbo.Product_Images
UNION ALL SELECT N'Orders', COUNT(*) FROM dbo.Orders
UNION ALL SELECT N'Payments', COUNT(*) FROM dbo.Payments;
