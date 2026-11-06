-- PawMart demo storefront. Pin is the mapped position of Tan Lap 1 street,
-- not a verified entrance for house number 46/2.
UPDATE dbo.Stores
SET store_name = N'PawMart Pet Store',
    address = N'46/2 Tân Lập 1, phường Tăng Nhơn Phú, Thành phố Hồ Chí Minh, Việt Nam',
    latitude = 10.8483153,
    longitude = 106.7773630,
    updated_at = SYSUTCDATETIME()
WHERE store_type = 'STORE'
  AND (
    (store_name = N'PetFood Main Store' AND address = N'Ho Chi Minh City, Vietnam')
    OR (store_name = N'PawMart Pet Store' AND latitude = 10.8483153
      AND longitude = 106.7773630 AND address LIKE N'46/2 TÃ¢n%')
  );
