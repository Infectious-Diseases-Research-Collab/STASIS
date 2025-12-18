USE [STASIS]
GO

ALTER TABLE [dbo].[tbl_UserProfiles]
ADD [MustChangePassword] [bit] NOT NULL DEFAULT 0;
GO

PRINT 'Added MustChangePassword column to tbl_UserProfiles'
GO
