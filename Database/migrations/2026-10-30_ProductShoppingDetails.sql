-- Optional product details shown on the store product page.
-- Existing products remain valid; populate values only from verified packaging or shop records.
IF COL_LENGTH('dbo.Products', 'package_size') IS NULL
    ALTER TABLE dbo.Products ADD package_size NVARCHAR(100) NULL;
GO
IF COL_LENGTH('dbo.Products', 'life_stage') IS NULL
    ALTER TABLE dbo.Products ADD life_stage NVARCHAR(100) NULL;
GO
IF COL_LENGTH('dbo.Products', 'feeding_guide') IS NULL
    ALTER TABLE dbo.Products ADD feeding_guide NVARCHAR(MAX) NULL;
GO
