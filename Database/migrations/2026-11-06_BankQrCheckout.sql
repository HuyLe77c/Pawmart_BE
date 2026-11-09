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
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Payments', 'bank_bin') IS NULL
    ALTER TABLE dbo.Payments ADD bank_bin VARCHAR(6) NULL;
IF COL_LENGTH('dbo.Payments', 'bank_name') IS NULL
    ALTER TABLE dbo.Payments ADD bank_name NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.Payments', 'bank_account_no') IS NULL
    ALTER TABLE dbo.Payments ADD bank_account_no VARCHAR(30) NULL;
IF COL_LENGTH('dbo.Payments', 'bank_account_name') IS NULL
    ALTER TABLE dbo.Payments ADD bank_account_name NVARCHAR(100) NULL;
GO

IF OBJECT_ID(N'dbo.CK_Orders_PaymentMethod', N'C') IS NOT NULL
    ALTER TABLE dbo.Orders DROP CONSTRAINT CK_Orders_PaymentMethod;
ALTER TABLE dbo.Orders WITH CHECK ADD CONSTRAINT CK_Orders_PaymentMethod
    CHECK (payment_method IN ('COD', 'BANK_QR', 'VNPAY', 'MOMO', 'ZALOPAY'));

IF OBJECT_ID(N'dbo.CK_Payments_Method', N'C') IS NOT NULL
    ALTER TABLE dbo.Payments DROP CONSTRAINT CK_Payments_Method;
ALTER TABLE dbo.Payments WITH CHECK ADD CONSTRAINT CK_Payments_Method
    CHECK (payment_method IN ('COD', 'BANK_QR', 'VNPAY', 'MOMO', 'ZALOPAY'));

IF OBJECT_ID(N'dbo.CK_Payments_BankQrDetails', N'C') IS NOT NULL
    ALTER TABLE dbo.Payments DROP CONSTRAINT CK_Payments_BankQrDetails;
ALTER TABLE dbo.Payments WITH CHECK ADD CONSTRAINT CK_Payments_BankQrDetails CHECK
(
    payment_method <> 'BANK_QR' OR
    (bank_bin IS NOT NULL AND bank_name IS NOT NULL AND bank_account_no IS NOT NULL
     AND bank_account_name IS NOT NULL AND transaction_code IS NOT NULL
     AND amount > 0 AND amount = FLOOR(amount))
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Payments')
    AND name = N'UX_Payments_BankQr_BankTransaction')
    CREATE UNIQUE INDEX UX_Payments_BankQr_BankTransaction
        ON dbo.Payments(provider_transaction_id)
        WHERE payment_method = 'BANK_QR' AND provider_transaction_id IS NOT NULL;

COMMIT TRANSACTION;
GO
