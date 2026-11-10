USE PetFoodDB;
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ChatMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatMessages
    (
        chat_message_id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ChatMessages PRIMARY KEY,
        user_id VARCHAR(50) NOT NULL,
        role VARCHAR(10) NOT NULL,
        content NVARCHAR(MAX) NOT NULL,
        request_content NVARCHAR(2000) NULL,
        products_json NVARCHAR(4000) NULL,
        created_at DATETIME2(0) NOT NULL CONSTRAINT DF_ChatMessages_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_ChatMessages_Users FOREIGN KEY (user_id) REFERENCES dbo.Users(user_id) ON DELETE CASCADE,
        CONSTRAINT CK_ChatMessages_Role CHECK (role IN ('user', 'assistant'))
    );
    CREATE INDEX IX_ChatMessages_User_Message ON dbo.ChatMessages(user_id, chat_message_id);
END;

COMMIT TRANSACTION;
GO
