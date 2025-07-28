-- Script to mark the initial migration as applied
-- This should be run on the database to fix the migration history

USE CreativeGroupsDb;

-- Check if the migration history table exists
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') AND type in (N'U'))
BEGIN
    -- Create the migration history table if it doesn't exist
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END

-- Insert the migration record to mark it as applied
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250725035703_InitialCreate')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250725035703_InitialCreate', '9.0.7');
    
    PRINT 'Migration 20250725035703_InitialCreate marked as applied.';
END
ELSE
BEGIN
    PRINT 'Migration 20250725035703_InitialCreate is already marked as applied.';
END
