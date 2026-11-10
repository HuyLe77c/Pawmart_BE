/* Add pet gender without changing existing profiles. Safe to rerun. */
USE PetFoodDB;
GO

IF COL_LENGTH('dbo.Pets', 'gender') IS NULL
BEGIN
    ALTER TABLE dbo.Pets ADD gender VARCHAR(10) NOT NULL
        CONSTRAINT DF_Pets_Gender DEFAULT ('UNKNOWN');
END
GO

/* Existing databases may have a nullable gender column and an older check
   constraint that rejects UNKNOWN. Repair both in one transaction. */
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID('dbo.Pets') AND name = 'CK_Pets_Gender'
    )
        ALTER TABLE dbo.Pets DROP CONSTRAINT CK_Pets_Gender;

    UPDATE dbo.Pets SET gender = 'UNKNOWN' WHERE gender IS NULL;

    IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('dbo.Pets') AND name = 'gender' AND is_nullable = 1
    )
    BEGIN
        DECLARE @defaultConstraint sysname;
        SELECT @defaultConstraint = d.name
        FROM sys.default_constraints AS d
        INNER JOIN sys.columns AS c
            ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
        WHERE c.object_id = OBJECT_ID('dbo.Pets') AND c.name = 'gender';

        IF @defaultConstraint IS NOT NULL
        BEGIN
            DECLARE @dropDefaultSql nvarchar(max) =
                N'ALTER TABLE dbo.Pets DROP CONSTRAINT ' + QUOTENAME(@defaultConstraint);
            EXEC sys.sp_executesql @dropDefaultSql;
        END

        ALTER TABLE dbo.Pets ALTER COLUMN gender VARCHAR(10) NOT NULL;
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.default_constraints AS d
        INNER JOIN sys.columns AS c
            ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
        WHERE c.object_id = OBJECT_ID('dbo.Pets') AND c.name = 'gender'
    )
        ALTER TABLE dbo.Pets ADD CONSTRAINT DF_Pets_Gender DEFAULT ('UNKNOWN') FOR gender;

    ALTER TABLE dbo.Pets WITH CHECK ADD CONSTRAINT CK_Pets_Gender
        CHECK (gender IN ('MALE', 'FEMALE', 'UNKNOWN'));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
