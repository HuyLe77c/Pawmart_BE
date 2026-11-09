/* Add pet gender without changing existing profiles. Safe to rerun. */
USE PetFoodDB;
GO

IF COL_LENGTH('dbo.Pets', 'gender') IS NULL
BEGIN
    ALTER TABLE dbo.Pets ADD gender VARCHAR(10) NOT NULL
        CONSTRAINT DF_Pets_Gender DEFAULT ('UNKNOWN');
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Pets_Gender')
BEGIN
    ALTER TABLE dbo.Pets ADD CONSTRAINT CK_Pets_Gender
        CHECK (gender IN ('MALE', 'FEMALE', 'UNKNOWN'));
END
GO
