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
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Catalog_Product_Reviews', N'U') IS NULL
    THROW 51041, 'Run 2026-11-05_CatalogReview.sql first.', 1;

DECLARE @Catalog TABLE
(
    code VARCHAR(3) NOT NULL PRIMARY KEY,
    category_name NVARCHAR(100) NOT NULL,
    subcategory_name NVARCHAR(100) NOT NULL,
    brand NVARCHAR(100) NOT NULL,
    pet_type VARCHAR(20) NOT NULL,
    model_name NVARCHAR(150) NOT NULL,
    product_name NVARCHAR(150) NOT NULL,
    official_url NVARCHAR(1000) NOT NULL,
    model_status VARCHAR(20) NOT NULL,
    review_note NVARCHAR(1000) NULL,
    slug VARCHAR(180) NOT NULL
);

INSERT INTO @Catalog
(code, category_name, subcategory_name, brand, pet_type, model_name,
 product_name, official_url, model_status, review_note, slug)
VALUES
    (N'A01', N'Thức ăn', N'Thức ăn cho chó', N'Royal Canin', N'DOG', N'Mini Adult', N'Royal Canin Mini Adult – Thức ăn hạt cho chó trưởng thành giống nhỏ', N'https://www.petmart.vn/thuc-an-cho-cho-truong-thanh-co-nho-royal-canin-mini-adult', N'NEEDS_REVIEW', N'Đã thấy trang nhà bán lẻ; chưa tìm được trang model trên website chính hãng Việt Nam', N'royal-canin-mini-adult-a01'),
    (N'A02', N'Thức ăn', N'Thức ăn cho mèo', N'Royal Canin', N'CAT', N'Indoor 27', N'Royal Canin Indoor 27 – Thức ăn hạt cho mèo nuôi trong nhà', N'https://www.royalcanin.com/vn/cats/products/retail-products/indoor-27-2529', N'VERIFIED', N'', N'royal-canin-indoor-27-a02'),
    (N'A03', N'Thức ăn', N'Thức ăn cho mèo', N'Whiskas', N'CAT', N'Adult Tuna', N'Whiskas Adult Tuna – Thức ăn hạt vị cá ngừ cho mèo trưởng thành', N'https://www.whiskas.com.vn/thuc-an-cho-meo-san-pham/thuc-an-kho/thuc-an-kho-whiskas-vi-ca-ngu-cho-meo-truong-thanh-tu-1-tuoi', N'VERIFIED', N'Phiên bản Việt Nam từ 1 tuổi', N'whiskas-adult-tuna-a03'),
    (N'A04', N'Thức ăn', N'Thức ăn cho mèo', N'Reflex Plus', N'CAT', N'Adult Cat Lamb', N'Reflex Plus Adult Cat Lamb – Thức ăn hạt vị cừu cho mèo trưởng thành', N'https://www.reflexmama.com/reflex-plus-adult-cat-food-with-lamb', N'NEEDS_REVIEW', N'Tên gạo trong yêu cầu cần xác minh theo nhãn trước khi mô tả thành phần', N'reflex-plus-adult-cat-lamb-a04'),
    (N'A05', N'Thức ăn', N'Thức ăn cho chó', N'Royal Canin', N'DOG', N'Mini Puppy', N'Royal Canin Mini Puppy – Thức ăn hạt cho chó con giống nhỏ', N'https://www.royalcanin.com/vn/dogs/products/retail-products/puppy---mini-3000', N'VERIFIED', N'', N'royal-canin-mini-puppy-a05'),
    (N'A06', N'Thức ăn', N'Thức ăn cho chó', N'Royal Canin', N'DOG', N'Maxi Adult', N'Royal Canin Maxi Adult – Thức ăn hạt cho chó trưởng thành giống lớn', N'https://www.royalcanin.com/vn/dogs/products/retail-products/maxi-adult-3007', N'VERIFIED', N'', N'royal-canin-maxi-adult-a06'),
    (N'A07', N'Thức ăn', N'Thức ăn cho mèo', N'Royal Canin', N'CAT', N'Kitten', N'Royal Canin Kitten – Thức ăn hạt cho mèo con', N'https://www.royalcanin.com/vn/cats/products/retail-products/kitten-2522', N'VERIFIED', N'', N'royal-canin-kitten-a07'),
    (N'A08', N'Thức ăn', N'Thức ăn cho mèo', N'Royal Canin', N'CAT', N'Sterilised 37', N'Royal Canin Sterilised 37 – Thức ăn hạt cho mèo đã triệt sản', N'https://www.royalcanin.com/vn/cats/products/retail-products/sterilised-37-2537', N'VERIFIED', N'', N'royal-canin-sterilised-37-a08'),
    (N'B01', N'Đồ chơi', N'Đồ chơi nhai gặm', N'KONG', N'DOG', N'Classic', N'KONG Classic – Đồ chơi cao su nhét bánh thưởng cho chó', N'https://www.kongcompany.com/kong-classic/', N'VERIFIED', N'Kích cỡ cần xác định khi nhập giá', N'kong-classic-b01'),
    (N'B02', N'Đồ chơi', N'Đồ chơi nhai gặm', N'KONG', N'DOG', N'Extreme', N'KONG Extreme – Đồ chơi cao su cho chó nhai mạnh', N'https://www.kongcompany.com/kong-extreme/', N'VERIFIED', N'Kích cỡ cần xác định khi nhập giá', N'kong-extreme-b02'),
    (N'B03', N'Đồ chơi', N'Bóng và đồ chơi ném nhặt', N'KONG', N'DOG', N'SqueakAir Balls', N'KONG SqueakAir Balls – Bóng phát tiếng kêu cho chó', N'https://www.kongcompany.com/squeakair-balls/', N'VERIFIED', N'Kích cỡ và số lượng bóng trong gói cần xác định', N'kong-squeakair-balls-b03'),
    (N'B04', N'Đồ chơi', N'Đồ chơi tương tác', N'KONG', N'DOG', N'Wild Knots Bear', N'KONG Wild Knots Bear – Gấu bông đồ chơi cho chó', N'https://www.kongcompany.com/wild-knots-bear/', N'VERIFIED', N'Kích cỡ cần xác định khi nhập giá', N'kong-wild-knots-bear-b04'),
    (N'B05', N'Đồ chơi', N'Đồ chơi tương tác', N'KONG', N'DOG', N'Wubba', N'KONG Wubba – Đồ chơi kéo giật cho chó', N'https://www.kongcompany.com/wubba/', N'VERIFIED', N'Kích cỡ và màu sắc cần xác định', N'kong-wubba-b05'),
    (N'B06', N'Đồ chơi', N'Đồ chơi tương tác', N'KONG', N'DOG', N'Tugga Wubba', N'KONG Tugga Wubba – Đồ chơi dây thừng kéo co cho chó', N'https://www.kongcompany.com/tugga-wubba/', N'VERIFIED', N'Kích cỡ cần xác định', N'kong-tugga-wubba-b06'),
    (N'B07', N'Đồ chơi', N'Bóng và đồ chơi ném nhặt', N'KONG', N'DOG', N'Wubba Sport Basketball', N'KONG Wubba Sport Basketball – Đồ chơi hình bóng rổ cho chó', N'https://www.kongcompany.com/wubba-sport-basketball/', N'VERIFIED', N'Kích cỡ cần xác định', N'kong-wubba-sport-basketball-b07'),
    (N'B08', N'Đồ chơi', N'Bóng và đồ chơi ném nhặt', N'KONG', N'DOG', N'Wubba Sport Soccer Ball', N'KONG Wubba Sport Soccer Ball – Đồ chơi hình bóng đá cho chó', N'https://www.kongcompany.com/wubba-sport-soccer-ball/', N'VERIFIED', N'Kích cỡ cần xác định', N'kong-wubba-sport-soccer-ball-b08'),
    (N'C01', N'Chăm sóc và vệ sinh', N'Chăm sóc lông', N'FURminator', N'CAT', N'Small Cat Short Hair', N'FURminator Small Cat Short Hair – Lược loại bỏ lông rụng cho mèo nhỏ lông ngắn', N'https://www.furminator.com/products/tools/deshedding-tools/undercoat-deshedding-tool-small-cat-short-hair', N'VERIFIED', N'Model P-92917', N'furminator-small-cat-short-hair-c01'),
    (N'C02', N'Chăm sóc và vệ sinh', N'Chăm sóc lông', N'FURminator', N'DOG', N'Medium Dog Long Hair', N'FURminator Medium Dog Long Hair – Lược loại bỏ lông rụng cho chó cỡ vừa lông dài', N'https://www.furminator.com/products/tools/deshedding-tools/undercoat-deshedding-tool-medium-dog-long-hair', N'VERIFIED', N'Model P-92914', N'furminator-medium-dog-long-hair-c02'),
    (N'C03', N'Chăm sóc và vệ sinh', N'Vệ sinh răng miệng', N'Virbac', N'ALL', N'C.E.T. Enzymatic Toothpaste', N'Virbac C.E.T. Enzymatic Toothpaste – Kem đánh răng enzyme cho chó mèo', N'https://vet-us.virbac.com/home/products/dental/cet-enzymatic-toothpaste.html', N'VERIFIED', N'Hương vị cụ thể cần xác định trước khi nhập giá và ảnh', N'virbac-c-e-t-enzymatic-toothpaste-c03'),
    (N'C04', N'Chăm sóc và vệ sinh', N'Chăm sóc móng', N'FURminator', N'ALL', N'Nail Clippers', N'FURminator Nail Clippers – Kềm cắt móng cho chó mèo', N'https://www.furminator.com/products/tools/nail-care/nail-clippers', N'VERIFIED', N'Model P-104014-1', N'furminator-nail-clippers-c04'),
    (N'C05', N'Chăm sóc và vệ sinh', N'Dụng cụ tắm', N'FURminator', N'ALL', N'Bathing Brush', N'FURminator Bathing Brush – Bàn chải tắm cho chó mèo', N'https://www.furminator.com/products/coat-care/shampoos-and-conditioners/bathing-brush', N'VERIFIED', N'Model P-92932', N'furminator-bathing-brush-c05'),
    (N'C06', N'Chăm sóc và vệ sinh', N'Chăm sóc móng', N'FURminator', N'ALL', N'Nail Grinder', N'FURminator Nail Grinder – Máy mài móng cho chó mèo', N'https://www.furminator.com/products/tools/nail-care/nail-grinder', N'VERIFIED', N'Model P-104005-2', N'furminator-nail-grinder-c06'),
    (N'C07', N'Chăm sóc và vệ sinh', N'Vệ sinh răng miệng', N'Virbac', N'ALL', N'C.E.T. Dual-Ended Toothbrush', N'Virbac C.E.T. Dual-Ended Toothbrush – Bàn chải đánh răng hai đầu cho chó mèo', N'https://us.virbac.com/home/our-products/pagecontent/product-selector/cet-brand-toothbrushes.html', N'VERIFIED', N'Trang chính hãng có nhiều mẫu bàn chải; chọn đúng mẫu hai đầu', N'virbac-c-e-t-dual-ended-toothbrush-c07'),
    (N'C08', N'Chăm sóc và vệ sinh', N'Chăm sóc lông', N'FURminator', N'ALL', N'Dual Grooming Brush', N'FURminator Dual Grooming Brush – Bàn chải chải lông hai mặt cho thú cưng', N'https://www.furminator.com/products/brush/brushes-and-rakes/dual-grooming-brush', N'VERIFIED', N'Model P-92929', N'furminator-dual-grooming-brush-c08'),
    (N'C09', N'Chăm sóc và vệ sinh', N'Chăm sóc lông', N'FURminator', N'ALL', N'Grooming Rake', N'FURminator Grooming Rake – Lược răng xoay gỡ rối lông thú cưng', N'https://www.furminator.com/products/brush/brushes-and-rakes/grooming-rake', N'VERIFIED', N'Model P-92930', N'furminator-grooming-rake-c09'),
    (N'C10', N'Chăm sóc và vệ sinh', N'Dụng cụ gỡ lông rối', N'FURminator', N'ALL', N'Adjustable Dematting Tool', N'FURminator Adjustable Dematting Tool – Dụng cụ gỡ lông rối có thể điều chỉnh', N'https://www.furminator.com/products/tools/dematting-tools/adjustable-dematting-tool', N'VERIFIED', N'Model P-92922-1', N'furminator-adjustable-dematting-tool-c10'),
    (N'C11', N'Chăm sóc và vệ sinh', N'Chăm sóc lông', N'FURminator', N'CAT', N'Medium Large Cat Long Hair', N'FURminator Medium Large Cat Long Hair – Lược loại bỏ lông rụng cho mèo lông dài', N'https://www.furminator.com/products/tools/deshedding-tools/undercoat-deshedding-tool-medium-large-cat-long-hair', N'VERIFIED', N'Khác C01 và C02; kích cỡ mèo vừa và lớn', N'furminator-medium-large-cat-long-hair-c11'),
    (N'D01', N'Thực phẩm bổ sung', N'Hỗ trợ tiêu hóa', N'Zesty Paws', N'DOG', N'Probiotic Bites', N'Zesty Paws Probiotic Bites – Viên nhai bổ sung lợi khuẩn cho chó', N'https://zestypaws.com/products/probiotic-bites', N'VERIFIED', N'Phiên bản và số viên cần xác định', N'zesty-paws-probiotic-bites-d01'),
    (N'D02', N'Thực phẩm bổ sung', N'Vitamin', N'Zesty Paws', N'DOG', N'8-in-1 Bites', N'Zesty Paws 8-in-1 Bites – Viên nhai đa vitamin cho chó', N'https://zestypaws.com/products/8-in-1-bites', N'VERIFIED', N'Phiên bản và số viên cần xác định', N'zesty-paws-8-in-1-bites-d02'),
    (N'D03', N'Thực phẩm bổ sung', N'Hỗ trợ da và lông', N'Zesty Paws', N'ALL', N'Wild Alaskan Omega-3 Blend', N'Zesty Paws Wild Alaskan Omega-3 Blend – Dầu cá bổ sung cho chó mèo', N'https://zestypaws.com/products/wild-alaskan-omega-3-blend-dogs-cats', N'VERIFIED', N'Dung tích cần xác định', N'zesty-paws-wild-alaskan-omega-3-blend-d03'),
    (N'D04', N'Thực phẩm bổ sung', N'Hỗ trợ xương khớp', N'Zesty Paws', N'DOG', N'Mobility Bites', N'Zesty Paws Mobility Bites – Viên nhai hỗ trợ xương khớp cho chó', N'https://zestypaws.com/products/mobility-bites', N'VERIFIED', N'Tên trên website hiện là Mobility Bites', N'zesty-paws-mobility-bites-d04'),
    (N'D05', N'Thực phẩm bổ sung', N'Hỗ trợ tiêu hóa', N'Zesty Paws', N'DOG', N'Ancient Elements Probiotic Bites', N'Zesty Paws Ancient Elements Probiotic Bites – Viên nhai lợi khuẩn cho chó', N'https://zestypaws.com/products/ancient-elements-probiotic-bites', N'VERIFIED', N'Bison 90 viên trên trang hãng; kiểm tra lại biến thể phân phối ở Việt Nam', N'zesty-paws-ancient-elements-probiotic-bites-d05'),
    (N'D06', N'Thực phẩm bổ sung', N'Hỗ trợ miễn dịch', N'Zesty Paws', N'DOG', N'Allergy & Immune Bites', N'Zesty Paws Allergy & Immune Bites – Viên nhai hỗ trợ miễn dịch cho chó', N'https://zestypaws.com/products/allergy-immune-soft-chews-for-dogs', N'VERIFIED', N'Phiên bản và số viên cần xác định', N'zesty-paws-allergy-immune-bites-d06'),
    (N'D07', N'Thực phẩm bổ sung', N'Hỗ trợ da và lông', N'Zesty Paws', N'DOG', N'Skin & Coat Bites', N'Zesty Paws Skin & Coat Bites – Viên nhai bổ sung cho da và lông chó', N'https://zestypaws.com/collections/our-collection/products/skin-coat-bites-for-dogs', N'VERIFIED', N'Phiên bản và số viên cần xác định', N'zesty-paws-skin-coat-bites-d07'),
    (N'D08', N'Thực phẩm bổ sung', N'Hỗ trợ thư giãn', N'Zesty Paws', N'DOG', N'Calming Bites', N'Zesty Paws Calming Bites – Viên nhai hỗ trợ thư giãn cho chó', N'https://zestypaws.com/products/calming-soft-chews-for-dogs', N'VERIFIED', N'Phiên bản và số viên cần xác định', N'zesty-paws-calming-bites-d08'),
    (N'D09', N'Thực phẩm bổ sung', N'Vitamin', N'Zesty Paws', N'DOG', N'Senior Advanced 11-in-1', N'Zesty Paws Senior Advanced 11-in-1 – Viên nhai đa vitamin cho chó lớn tuổi', N'https://zestypaws.com/products/senior-multifunctional-bites', N'VERIFIED', N'Phiên bản và số viên cần xác định', N'zesty-paws-senior-advanced-11-in-1-d09'),
    (N'E01', N'Phụ kiện và thiết bị', N'Máy nước uống', N'PETKIT', N'ALL', N'EVERSWEET Solo SE', N'PETKIT EVERSWEET Solo SE – Máy nước uống tuần hoàn cho chó mèo', N'https://www.petkit.com/products/eversweet-solo-se', N'VERIFIED', N'Model và điện áp bán tại Việt Nam cần xác định', N'petkit-eversweet-solo-se-e01'),
    (N'E02', N'Phụ kiện và thiết bị', N'Máy cho ăn', N'PETKIT', N'ALL', N'Fresh Element Solo', N'PETKIT Fresh Element Solo – Máy cho thú cưng ăn tự động', N'https://www.petkit.com/products/fresh-element-solo', N'VERIFIED', N'Model và điện áp bán tại Việt Nam cần xác định', N'petkit-fresh-element-solo-e02'),
    (N'E03', N'Phụ kiện và thiết bị', N'Máy nước uống', N'PETKIT', N'ALL', N'EVERSWEET Max', N'PETKIT EVERSWEET Max – Máy nước uống không dây cho chó mèo', N'https://www.petkit.com/products/petkit-eversweet-max-cordless', N'VERIFIED', N'Phân biệt với EVERSWEET Max 2', N'petkit-eversweet-max-e03'),
    (N'E04', N'Phụ kiện và thiết bị', N'Máy nước uống', N'PETKIT', N'ALL', N'EVERSWEET Solo 2', N'PETKIT EVERSWEET Solo 2 – Máy nước uống tuần hoàn cho chó mèo', N'https://www.petkit.com/products/eversweet-solo-2', N'VERIFIED', N'Model P4114', N'petkit-eversweet-solo-2-e04'),
    (N'E05', N'Phụ kiện và thiết bị', N'Nhà vệ sinh', N'PETKIT', N'CAT', N'PURAMAX 2', N'PETKIT PURAMAX 2 – Nhà vệ sinh tự động cho mèo', N'https://www.petkit.com/products/puramax-2-ultimate-bundles', N'NEEDS_REVIEW', N'Trang hãng là bộ sản phẩm bundle; model P9902 xác nhận từ sách hướng dẫn hãng nhưng cần xác minh gói bán riêng tại Việt Nam', N'petkit-puramax-2-e05');


IF (SELECT COUNT(*) FROM @Catalog) <> 41
    THROW 51042, 'Catalog must contain exactly 41 products.', 1;

INSERT INTO dbo.Categories(category_name, description, sort_order, is_active)
SELECT source.category_name, N'Danh mục sản phẩm đang được kiểm duyệt',
       CASE source.category_name
           WHEN N'Thức ăn' THEN 10 WHEN N'Đồ chơi' THEN 20
           WHEN N'Chăm sóc và vệ sinh' THEN 30
           WHEN N'Thực phẩm bổ sung' THEN 40 ELSE 50 END,
       1
FROM (SELECT DISTINCT category_name FROM @Catalog) source
WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories existing WHERE existing.category_name = source.category_name);

INSERT INTO dbo.Catalog_Subcategories(category_id, subcategory_name)
SELECT category.category_id, source.subcategory_name
FROM (SELECT DISTINCT category_name, subcategory_name FROM @Catalog) source
JOIN dbo.Categories category ON category.category_name = source.category_name
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Catalog_Subcategories existing
    WHERE existing.category_id = category.category_id
      AND existing.subcategory_name = source.subcategory_name
);

IF EXISTS
(
    SELECT 1 FROM @Catalog source
    JOIN dbo.Products product ON product.sku = 'CAT41-' + source.code
    LEFT JOIN dbo.Catalog_Product_Reviews review ON review.product_id = product.product_id
    WHERE review.product_id IS NULL
)
    THROW 51043, 'A catalog SKU already belongs to an unrelated product.', 1;

INSERT INTO dbo.Products
(category_id, sku, product_name, brand, price, stock_quantity,
 low_stock_threshold, target_pet_type, is_featured, is_active)
SELECT category.category_id, 'CAT41-' + source.code, source.product_name,
       source.brand, 0, 0, 5, source.pet_type, 0, 0
FROM @Catalog source
JOIN dbo.Categories category ON category.category_name = source.category_name
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products product WHERE product.sku = 'CAT41-' + source.code);

INSERT INTO dbo.Catalog_Product_Reviews
(product_id, slug, subcategory_id, model_name, official_url, model_status,
 image_status, price_status, product_status, public_preview, review_note)
SELECT product.product_id, source.slug, subcategory.subcategory_id,
       source.model_name, source.official_url, source.model_status,
       'NEEDS_REVIEW', 'NEEDS_REVIEW', 'NEEDS_REVIEW', 1, NULLIF(source.review_note, N'')
FROM @Catalog source
JOIN dbo.Products product ON product.sku = 'CAT41-' + source.code
JOIN dbo.Categories category ON category.category_name = source.category_name
JOIN dbo.Catalog_Subcategories subcategory
  ON subcategory.category_id = category.category_id
 AND subcategory.subcategory_name = source.subcategory_name
WHERE NOT EXISTS
    (SELECT 1 FROM dbo.Catalog_Product_Reviews review WHERE review.product_id = product.product_id);

DECLARE @Variants TABLE
(
    code VARCHAR(3) NOT NULL,
    variant_key VARCHAR(80) NOT NULL,
    label NVARCHAR(100) NOT NULL,
    source_url NVARCHAR(1000) NOT NULL,
    PRIMARY KEY (code, variant_key)
);
INSERT INTO @Variants(code, variant_key, label, source_url)
VALUES
    (N'A01', N'800g', N'800 g', N'https://www.petmart.vn/thuc-an-cho-cho-truong-thanh-co-nho-royal-canin-mini-adult'),
    (N'A01', N'2kg', N'2 kg', N'https://www.petmart.vn/thuc-an-cho-cho-truong-thanh-co-nho-royal-canin-mini-adult'),
    (N'A01', N'8kg', N'8 kg', N'https://www.petmart.vn/thuc-an-cho-cho-truong-thanh-co-nho-royal-canin-mini-adult'),
    (N'A02', N'400g', N'400 g', N'https://www.petmart.vn/thuc-an-cho-meo-truong-thanh-royal-canin-indoor-27'),
    (N'A02', N'2kg', N'2 kg', N'https://www.petmart.vn/thuc-an-cho-meo-truong-thanh-royal-canin-indoor-27'),
    (N'A02', N'10kg', N'10 kg', N'https://www.petmart.vn/thuc-an-cho-meo-truong-thanh-royal-canin-indoor-27'),
    (N'A03', N'480g', N'480 g', N'https://www.whiskas.com.vn/thuc-an-cho-meo-san-pham/thuc-an-kho/thuc-an-kho-whiskas-vi-ca-ngu-cho-meo-truong-thanh-tu-1-tuoi'),
    (N'A03', N'1.2kg', N'1.2 kg', N'https://www.whiskas.com.vn/thuc-an-cho-meo-san-pham/thuc-an-kho/thuc-an-kho-whiskas-vi-ca-ngu-cho-meo-truong-thanh-tu-1-tuoi'),
    (N'A03', N'3kg', N'3 kg', N'https://www.whiskas.com.vn/thuc-an-cho-meo-san-pham/thuc-an-kho/thuc-an-kho-whiskas-vi-ca-ngu-cho-meo-truong-thanh-tu-1-tuoi'),
    (N'A03', N'7kg', N'7 kg', N'https://www.whiskas.com.vn/thuc-an-cho-meo-san-pham/thuc-an-kho/thuc-an-kho-whiskas-vi-ca-ngu-cho-meo-truong-thanh-tu-1-tuoi'),
    (N'A05', N'800g', N'800 g', N'https://www.petmart.vn/thuc-an-cho-cho-con-co-nho-royal-canin-mini-puppy'),
    (N'A05', N'2kg', N'2 kg', N'https://www.petmart.vn/thuc-an-cho-cho-con-co-nho-royal-canin-mini-puppy'),
    (N'A05', N'8kg', N'8 kg', N'https://www.petmart.vn/thuc-an-cho-cho-con-co-nho-royal-canin-mini-puppy'),
    (N'A06', N'1kg', N'1 kg', N'https://www.petmart.vn/thuc-an-cho-cho-truong-thanh-co-lon-royal-canin-maxi-adult'),
    (N'A06', N'10kg', N'10 kg', N'https://www.petmart.vn/thuc-an-cho-cho-truong-thanh-co-lon-royal-canin-maxi-adult'),
    (N'A07', N'400g', N'400 g', N'https://www.petmart.vn/thuc-an-cho-meo-con-royal-canin-kitten'),
    (N'A07', N'2kg', N'2 kg', N'https://www.petmart.vn/thuc-an-cho-meo-con-royal-canin-kitten'),
    (N'A07', N'10kg', N'10 kg', N'https://www.petmart.vn/thuc-an-cho-meo-con-royal-canin-kitten');


INSERT INTO dbo.Catalog_Product_Variants
(product_id, variant_key, label, price_source_url, price_status)
SELECT product.product_id, source.variant_key, source.label, source.source_url, 'NEEDS_REVIEW'
FROM @Variants source
JOIN dbo.Products product ON product.sku = 'CAT41-' + source.code
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Catalog_Product_Variants existing
    WHERE existing.product_id = product.product_id AND existing.variant_key = source.variant_key
);

COMMIT TRANSACTION;
SELECT LEFT(product.sku, 5) AS catalog_prefix, COUNT(*) AS product_count,
       SUM(CASE WHEN product.is_active = 1 THEN 1 ELSE 0 END) AS active_count
FROM dbo.Products product
WHERE product.sku LIKE 'CAT41-%'
GROUP BY LEFT(product.sku, 5);
GO
