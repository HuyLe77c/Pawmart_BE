/* Add optional nutrition-profile fields to an existing PetFoodDB. Safe to rerun. */
USE PetFoodDB;
GO

IF COL_LENGTH('dbo.Pets', 'body_size') IS NULL
    ALTER TABLE dbo.Pets ADD body_size VARCHAR(10) NULL;
IF COL_LENGTH('dbo.Pets', 'activity_level') IS NULL
    ALTER TABLE dbo.Pets ADD activity_level VARCHAR(10) NULL;
IF COL_LENGTH('dbo.Pets', 'body_condition_score') IS NULL
    ALTER TABLE dbo.Pets ADD body_condition_score INT NULL;
IF COL_LENGTH('dbo.Pets', 'feeding_habits') IS NULL
    ALTER TABLE dbo.Pets ADD feeding_habits NVARCHAR(500) NULL;
IF COL_LENGTH('dbo.Pets', 'food_preferences') IS NULL
    ALTER TABLE dbo.Pets ADD food_preferences NVARCHAR(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Pets_BodySize')
    ALTER TABLE dbo.Pets ADD CONSTRAINT CK_Pets_BodySize
        CHECK (body_size IS NULL OR body_size IN ('SMALL', 'MEDIUM', 'LARGE'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Pets_ActivityLevel')
    ALTER TABLE dbo.Pets ADD CONSTRAINT CK_Pets_ActivityLevel
        CHECK (activity_level IS NULL OR activity_level IN ('LOW', 'MODERATE', 'HIGH'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Pets_BodyConditionScore')
    ALTER TABLE dbo.Pets ADD CONSTRAINT CK_Pets_BodyConditionScore
        CHECK (body_condition_score IS NULL OR body_condition_score BETWEEN 1 AND 9);
GO
