USE PetFoodDB;
GO
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.BankQrWebhookEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BankQrWebhookEvents
    (
        provider_event_id BIGINT NOT NULL CONSTRAINT PK_BankQrWebhookEvents PRIMARY KEY,
        order_id VARCHAR(50) NULL,
        transfer_code VARCHAR(150) NULL,
        account_number VARCHAR(30) NOT NULL,
        gateway VARCHAR(100) NULL,
        reference_code VARCHAR(150) NULL,
        amount DECIMAL(18,2) NOT NULL,
        status VARCHAR(30) NOT NULL,
        created_at DATETIME2(0) NOT NULL CONSTRAINT DF_BankQrWebhookEvents_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_BankQrWebhookEvents_Amount CHECK (amount > 0),
        CONSTRAINT CK_BankQrWebhookEvents_Status CHECK
        (
            status IN ('UNMATCHED', 'OUTGOING', 'DUPLICATE_REFERENCE', 'ACCOUNT_MISMATCH',
                'CANCELLED_ORDER', 'ALREADY_PAID', 'PAYMENT_CONFLICT', 'PARTIAL', 'APPLIED')
        )
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BankQrWebhookEvents')
    AND name = N'IX_BankQrWebhookEvents_OrderId')
    CREATE INDEX IX_BankQrWebhookEvents_OrderId ON dbo.BankQrWebhookEvents(order_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BankQrWebhookEvents')
    AND name = N'IX_BankQrWebhookEvents_GatewayReference')
    CREATE INDEX IX_BankQrWebhookEvents_GatewayReference
        ON dbo.BankQrWebhookEvents(gateway, reference_code)
        WHERE reference_code IS NOT NULL;
GO
