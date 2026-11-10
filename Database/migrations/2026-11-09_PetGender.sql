USE PetFoodDB;
GO
SET XACT_ABORT ON;
IF @@TRANCOUNT > 0
    THROW 50000, 'An existing transaction is open. Roll it back or commit it before running this migration.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Pets', N'U') IS NULL
        THROW 50001, 'The Pets table does not exist.', 1;

    IF COL_LENGTH(N'dbo.Pets', N'gender') IS NULL
        ALTER TABLE dbo.Pets ADD gender VARCHAR(10) NULL;

    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Pets') AND name = N'CK_Pets_Gender'
    )
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Pets ADD CONSTRAINT CK_Pets_Gender
            CHECK (gender IS NULL OR gender IN (''MALE'', ''FEMALE''))';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
