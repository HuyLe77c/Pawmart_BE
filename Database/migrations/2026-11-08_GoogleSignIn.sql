IF COL_LENGTH('dbo.Users', 'google_subject') IS NULL
    ALTER TABLE dbo.Users ADD google_subject VARCHAR(255) NULL;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'UX_Users_GoogleSubject')
    CREATE UNIQUE INDEX UX_Users_GoogleSubject ON dbo.Users(google_subject)
    WHERE google_subject IS NOT NULL;
GO
