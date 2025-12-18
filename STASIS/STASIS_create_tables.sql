/*******************************************************************************
 * STASIS Database Creation Script
 * 
 * Specimen Tracking And Storage Information System
 * 
 * Purpose: Create all database tables from scratch including ASP.NET Identity
 * Related Documents: system_requirements_updated.md, table_descriptions_final.md
 * 
 * USAGE:
 *   1. Create a database named 'STASIS' in SQL Server
 *   2. Run this entire script
 *   3. Configure your ASP.NET application to use the existing Identity tables
 * 
 * TABLE CREATION ORDER (respects foreign key dependencies):
 *   1. ASP.NET Identity tables
 *   2. tbl_UserProfiles (extends Identity)
 *   3. tbl_Freezers
 *   4. tbl_Racks
 *   5. tbl_Boxes
 *   6. tbl_Studies
 *   7. tbl_SampleTypes
 *   8. tbl_Approvals
 *   9. tbl_Specimens
 *  10. tbl_ShipmentBatches
 *  11. tbl_ShipmentRequests
 *  12. tbl_Shipments
 *  13. tbl_ShipmentContents
 *  14. tbl_AuditLog
 ******************************************************************************/

USE [STASIS]
GO


/*******************************************************************************
 * DROP EXISTING TABLES
 * 
 * Drop in reverse order of creation to avoid foreign key constraint errors
 ******************************************************************************/

-- STASIS Tables
IF OBJECT_ID('tbl_AuditLog', 'U') IS NOT NULL DROP TABLE tbl_AuditLog;
IF OBJECT_ID('tbl_ShipmentContents', 'U') IS NOT NULL DROP TABLE tbl_ShipmentContents;
IF OBJECT_ID('tbl_Shipments', 'U') IS NOT NULL DROP TABLE tbl_Shipments;
IF OBJECT_ID('tbl_ShipmentRequests', 'U') IS NOT NULL DROP TABLE tbl_ShipmentRequests;
IF OBJECT_ID('tbl_ShipmentBatches', 'U') IS NOT NULL DROP TABLE tbl_ShipmentBatches;
IF OBJECT_ID('tbl_Specimens', 'U') IS NOT NULL DROP TABLE tbl_Specimens;
IF OBJECT_ID('tbl_Approvals', 'U') IS NOT NULL DROP TABLE tbl_Approvals;
IF OBJECT_ID('tbl_SampleTypes', 'U') IS NOT NULL DROP TABLE tbl_SampleTypes;
IF OBJECT_ID('tbl_Studies', 'U') IS NOT NULL DROP TABLE tbl_Studies;
IF OBJECT_ID('tbl_Boxes', 'U') IS NOT NULL DROP TABLE tbl_Boxes;
IF OBJECT_ID('tbl_Racks', 'U') IS NOT NULL DROP TABLE tbl_Racks;
IF OBJECT_ID('tbl_Freezers', 'U') IS NOT NULL DROP TABLE tbl_Freezers;
IF OBJECT_ID('tbl_UserProfiles', 'U') IS NOT NULL DROP TABLE tbl_UserProfiles;

-- ASP.NET Identity Tables
IF OBJECT_ID('AspNetUserTokens', 'U') IS NOT NULL DROP TABLE AspNetUserTokens;
IF OBJECT_ID('AspNetUserLogins', 'U') IS NOT NULL DROP TABLE AspNetUserLogins;
IF OBJECT_ID('AspNetUserClaims', 'U') IS NOT NULL DROP TABLE AspNetUserClaims;
IF OBJECT_ID('AspNetRoleClaims', 'U') IS NOT NULL DROP TABLE AspNetRoleClaims;
IF OBJECT_ID('AspNetUserRoles', 'U') IS NOT NULL DROP TABLE AspNetUserRoles;
IF OBJECT_ID('AspNetRoles', 'U') IS NOT NULL DROP TABLE AspNetRoles;
IF OBJECT_ID('AspNetUsers', 'U') IS NOT NULL DROP TABLE AspNetUsers;

-- Legacy table (if exists from old schema)
IF OBJECT_ID('tbl_Users', 'U') IS NOT NULL DROP TABLE tbl_Users;

PRINT 'Dropped existing tables'
PRINT ''
GO


/*******************************************************************************
 * ASP.NET IDENTITY TABLES
 * 
 * These tables match the schema created by ASP.NET Core Identity
 * Your application will use these existing tables instead of creating new ones
 ******************************************************************************/

-- AspNetUsers: Core user accounts
CREATE TABLE [dbo].[AspNetUsers](
    [Id]                    [nvarchar](450) NOT NULL,
    [UserName]              [nvarchar](256) NULL,
    [NormalizedUserName]    [nvarchar](256) NULL,
    [Email]                 [nvarchar](256) NULL,
    [NormalizedEmail]       [nvarchar](256) NULL,
    [EmailConfirmed]        [bit] NOT NULL,
    [PasswordHash]          [nvarchar](max) NULL,
    [SecurityStamp]         [nvarchar](max) NULL,
    [ConcurrencyStamp]      [nvarchar](max) NULL,
    [PhoneNumber]           [nvarchar](max) NULL,
    [PhoneNumberConfirmed]  [bit] NOT NULL,
    [TwoFactorEnabled]      [bit] NOT NULL,
    [LockoutEnd]            [datetimeoffset](7) NULL,
    [LockoutEnabled]        [bit] NOT NULL,
    [AccessFailedCount]     [int] NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail] ASC)
GO

CREATE UNIQUE NONCLUSTERED INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName] ASC) 
WHERE ([NormalizedUserName] IS NOT NULL)
GO

PRINT 'Created AspNetUsers'
GO


-- AspNetRoles: Role definitions
CREATE TABLE [dbo].[AspNetRoles](
    [Id]                    [nvarchar](450) NOT NULL,
    [Name]                  [nvarchar](256) NULL,
    [NormalizedName]        [nvarchar](256) NULL,
    [ConcurrencyStamp]      [nvarchar](max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE UNIQUE NONCLUSTERED INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName] ASC)
WHERE ([NormalizedName] IS NOT NULL)
GO

PRINT 'Created AspNetRoles'
GO


-- AspNetUserRoles: User-to-Role mapping
CREATE TABLE [dbo].[AspNetUserRoles](
    [UserId]                [nvarchar](450) NOT NULL,
    [RoleId]                [nvarchar](450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED ([UserId] ASC, [RoleId] ASC),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) 
        REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
) ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId] ASC)
GO

PRINT 'Created AspNetUserRoles'
GO


-- AspNetUserClaims: User claims
CREATE TABLE [dbo].[AspNetUserClaims](
    [Id]                    [int] IDENTITY(1,1) NOT NULL,
    [UserId]                [nvarchar](450) NOT NULL,
    [ClaimType]             [nvarchar](max) NULL,
    [ClaimValue]            [nvarchar](max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId] ASC)
GO

PRINT 'Created AspNetUserClaims'
GO


-- AspNetRoleClaims: Role claims
CREATE TABLE [dbo].[AspNetRoleClaims](
    [Id]                    [int] IDENTITY(1,1) NOT NULL,
    [RoleId]                [nvarchar](450) NOT NULL,
    [ClaimType]             [nvarchar](max) NULL,
    [ClaimValue]            [nvarchar](max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) 
        REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId] ASC)
GO

PRINT 'Created AspNetRoleClaims'
GO


-- AspNetUserLogins: External login providers
CREATE TABLE [dbo].[AspNetUserLogins](
    [LoginProvider]         [nvarchar](450) NOT NULL,
    [ProviderKey]           [nvarchar](450) NOT NULL,
    [ProviderDisplayName]   [nvarchar](max) NULL,
    [UserId]                [nvarchar](450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY CLUSTERED ([LoginProvider] ASC, [ProviderKey] ASC),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId] ASC)
GO

PRINT 'Created AspNetUserLogins'
GO


-- AspNetUserTokens: Authentication tokens
CREATE TABLE [dbo].[AspNetUserTokens](
    [UserId]                [nvarchar](450) NOT NULL,
    [LoginProvider]         [nvarchar](450) NOT NULL,
    [Name]                  [nvarchar](450) NOT NULL,
    [Value]                 [nvarchar](max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY CLUSTERED ([UserId] ASC, [LoginProvider] ASC, [Name] ASC),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

PRINT 'Created AspNetUserTokens'
GO


/*******************************************************************************
 * TABLE: tbl_UserProfiles
 * 
 * Purpose: Extends ASP.NET Identity with STASIS-specific user attributes
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_UserProfiles](
    -- Primary Key
    [UserProfileID]         [int] IDENTITY(1,1) NOT NULL,
    
    -- Identity Link
    [AspNetUserId]          [nvarchar](450) NOT NULL,
    
    -- STASIS-specific attributes
    [Department]            [nvarchar](100) NULL,
    [CanApproveShipments]   [bit] NOT NULL DEFAULT 0,
    [CanApproveDiscards]    [bit] NOT NULL DEFAULT 0,
    [MustChangePassword]    [bit] NOT NULL DEFAULT 0,
    
    -- Constraints
    CONSTRAINT [PK_UserProfiles] PRIMARY KEY CLUSTERED ([UserProfileID] ASC),
    CONSTRAINT [UQ_UserProfiles_AspNetUserId] UNIQUE ([AspNetUserId]),
    CONSTRAINT [FK_UserProfiles_AspNetUsers] FOREIGN KEY ([AspNetUserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id])
) ON [PRIMARY]
GO

PRINT 'Created tbl_UserProfiles'
GO


/*******************************************************************************
 * TABLE: tbl_Freezers
 * 
 * Purpose: Top-level storage units (freezers, cold rooms)
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_Freezers](
    -- Primary Key
    [FreezerID]             [int] IDENTITY(1,1) NOT NULL,
    
    -- Freezer Information
    [FreezerName]           [nvarchar](50) NOT NULL,
    [Temperature]           [int] NULL,
    [LocationInBuilding]    [nvarchar](100) NULL,
    
    -- Constraints
    CONSTRAINT [PK_Freezers] PRIMARY KEY CLUSTERED ([FreezerID] ASC),
    CONSTRAINT [UQ_Freezers_FreezerName] UNIQUE ([FreezerName])
) ON [PRIMARY]
GO

PRINT 'Created tbl_Freezers'
GO


/*******************************************************************************
 * TABLE: tbl_Racks
 * 
 * Purpose: Subdivisions within a freezer (shelves, racks, compartments)
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_Racks](
    -- Primary Key
    [RackID]                [int] IDENTITY(1,1) NOT NULL,
    
    -- Rack Information
    [RackName]              [nvarchar](50) NOT NULL,
    
    -- Location (Parent)
    [FreezerID]             [int] NULL,
    
    -- Constraints
    CONSTRAINT [PK_Racks] PRIMARY KEY CLUSTERED ([RackID] ASC),
    CONSTRAINT [FK_Racks_Freezers] FOREIGN KEY ([FreezerID]) 
        REFERENCES [dbo].[tbl_Freezers] ([FreezerID])
) ON [PRIMARY]
GO

PRINT 'Created tbl_Racks'
GO


/*******************************************************************************
 * TABLE: tbl_Boxes
 * 
 * Purpose: Physical containers holding samples (cryo boxes, binders)
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_Boxes](
    -- Primary Key
    [BoxID]                 [int] IDENTITY(1,1) NOT NULL,
    
    -- Box Information
    [BoxLabel]              [nvarchar](50) NOT NULL,
    [BoxType]               [nvarchar](50) NOT NULL,
    [BoxCategory]           [nvarchar](50) NOT NULL DEFAULT 'Standard',
    
    -- Location (Parent)
    [RackID]                [int] NULL,
    
    -- Constraints
    CONSTRAINT [PK_Boxes] PRIMARY KEY CLUSTERED ([BoxID] ASC),
    CONSTRAINT [UQ_Boxes_BoxLabel] UNIQUE ([BoxLabel]),
    CONSTRAINT [CK_Boxes_BoxType] CHECK (
        [BoxType] IN ('81-slot', '100-slot', 'Filter Paper Binder')
    ),
    CONSTRAINT [CK_Boxes_BoxCategory] CHECK (
        [BoxCategory] IN ('Standard', 'Temp', 'Trash', 'Shipping')
    ),
    CONSTRAINT [FK_Boxes_Racks] FOREIGN KEY ([RackID]) 
        REFERENCES [dbo].[tbl_Racks] ([RackID])
) ON [PRIMARY]
GO

PRINT 'Created tbl_Boxes'
GO


/*******************************************************************************
 * TABLE: tbl_Studies
 * 
 * Purpose: Registry of research projects submitting samples
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_Studies](
    -- Primary Key
    [StudyID]               [int] IDENTITY(1,1) NOT NULL,
    
    -- Study Information
    [StudyCode]             [nvarchar](50) NOT NULL,
    [StudyName]             [nvarchar](200) NULL,
    [PrincipalInvestigator] [nvarchar](100) NULL,
    
    -- Constraints
    CONSTRAINT [PK_Studies] PRIMARY KEY CLUSTERED ([StudyID] ASC),
    CONSTRAINT [UQ_Studies_StudyCode] UNIQUE ([StudyCode])
) ON [PRIMARY]
GO

PRINT 'Created tbl_Studies'
GO


/*******************************************************************************
 * TABLE: tbl_SampleTypes
 * 
 * Purpose: Defines valid sample types with shipping restrictions
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_SampleTypes](
    -- Primary Key
    [SampleTypeID]          [int] IDENTITY(1,1) NOT NULL,
    
    -- Sample Type Information
    [TypeName]              [nvarchar](50) NOT NULL,
    [IsConsumable]          [bit] NOT NULL DEFAULT 0,
    
    -- Shipping Restrictions
    [MaxShippableUnits]     [int] NULL,
    [LocalReserveUnits]     [int] NULL,
    
    -- Constraints
    CONSTRAINT [PK_SampleTypes] PRIMARY KEY CLUSTERED ([SampleTypeID] ASC),
    CONSTRAINT [UQ_SampleTypes_TypeName] UNIQUE ([TypeName])
) ON [PRIMARY]
GO

PRINT 'Created tbl_SampleTypes'
GO


/*******************************************************************************
 * TABLE: tbl_Approvals
 * 
 * Purpose: Records approval decisions for shipments and sample disposal
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_Approvals](
    -- Primary Key
    [ApprovalID]                    [int] IDENTITY(1,1) NOT NULL,
    
    -- Request Information
    [ApprovalType]                  [nvarchar](50) NOT NULL,
    [RequestedByUserId]             [nvarchar](450) NULL,
    [RequestedDate]                 [datetime] NOT NULL DEFAULT GETDATE(),
    
    -- ED Approval
    [EDApproverUserId]              [nvarchar](450) NULL,
    [EDApprovalDate]                [datetime] NULL,
    [EDApprovalStatus]              [nvarchar](20) NULL,
    [EDComments]                    [nvarchar](500) NULL,
    
    -- Regulatory Approval
    [RegulatoryApproverUserId]      [nvarchar](450) NULL,
    [RegulatoryApprovalDate]        [datetime] NULL,
    [RegulatoryApprovalStatus]      [nvarchar](20) NULL,
    [RegulatoryComments]            [nvarchar](500) NULL,
    
    -- PI Approval (for discards)
    [PIApproverUserId]              [nvarchar](450) NULL,
    [PIApprovalDate]                [datetime] NULL,
    [PIApprovalStatus]              [nvarchar](20) NULL,
    [PIComments]                    [nvarchar](500) NULL,
    
    -- Overall Status
    [OverallStatus]                 [nvarchar](20) NOT NULL DEFAULT 'Pending',
    
    -- Constraints
    CONSTRAINT [PK_Approvals] PRIMARY KEY CLUSTERED ([ApprovalID] ASC),
    CONSTRAINT [CK_Approvals_ApprovalType] CHECK (
        [ApprovalType] IN ('Shipment', 'Discard', 'SingleAliquotException')
    ),
    CONSTRAINT [CK_Approvals_EDApprovalStatus] CHECK (
        [EDApprovalStatus] IS NULL OR [EDApprovalStatus] IN ('Pending', 'Approved', 'Rejected')
    ),
    CONSTRAINT [CK_Approvals_RegulatoryApprovalStatus] CHECK (
        [RegulatoryApprovalStatus] IS NULL OR [RegulatoryApprovalStatus] IN ('Pending', 'Approved', 'Rejected')
    ),
    CONSTRAINT [CK_Approvals_PIApprovalStatus] CHECK (
        [PIApprovalStatus] IS NULL OR [PIApprovalStatus] IN ('Pending', 'Approved', 'Rejected')
    ),
    CONSTRAINT [CK_Approvals_OverallStatus] CHECK (
        [OverallStatus] IN ('Pending', 'Approved', 'Rejected')
    ),
    CONSTRAINT [FK_Approvals_RequestedBy] FOREIGN KEY ([RequestedByUserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id]),
    CONSTRAINT [FK_Approvals_EDApprover] FOREIGN KEY ([EDApproverUserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id]),
    CONSTRAINT [FK_Approvals_RegulatoryApprover] FOREIGN KEY ([RegulatoryApproverUserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id]),
    CONSTRAINT [FK_Approvals_PIApprover] FOREIGN KEY ([PIApproverUserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id])
) ON [PRIMARY]
GO

PRINT 'Created tbl_Approvals'
GO


/*******************************************************************************
 * TABLE: tbl_Specimens
 * 
 * Purpose: Core table - each row represents one physical sample
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_Specimens](
    -- Primary Key
    [SpecimenID]                    [int] IDENTITY(1,1) NOT NULL,
    
    -- Identifiers
    [BarcodeID]                     [nvarchar](100) NOT NULL,
    [LegacyID]                      [nvarchar](100) NULL,
    
    -- Source Information
    [StudyID]                       [int] NULL,
    [SampleTypeID]                  [int] NULL,
    [CollectionDate]                [date] NULL,
    
    -- Current Location
    [BoxID]                         [int] NULL,
    [PositionRow]                   [int] NULL,
    [PositionCol]                   [int] NULL,
    
    -- Status
    [Status]                        [nvarchar](50) NOT NULL DEFAULT 'In-Stock',
    
    -- Filter Paper Specific
    [RemainingSpots]                [int] NULL,
    [SpotsShippedInternational]     [int] NOT NULL DEFAULT 0,
    [SpotsReservedLocal]            [int] NOT NULL DEFAULT 0,
    
    -- Plasma Specific
    [AliquotNumber]                 [int] NULL,
    
    -- Discard Workflow
    [DiscardApprovalID]             [int] NULL,
    
    -- Constraints
    CONSTRAINT [PK_Specimens] PRIMARY KEY CLUSTERED ([SpecimenID] ASC),
    CONSTRAINT [UQ_Specimens_BarcodeID] UNIQUE ([BarcodeID]),
    CONSTRAINT [UQ_Specimens_BoxPosition] UNIQUE ([BoxID], [PositionRow], [PositionCol]),
    CONSTRAINT [CK_Specimens_Status] CHECK (
        [Status] IN ('In-Stock', 'Staged', 'Shipped', 'Missing', 'Depleted', 'Discarded', 'Temp')
    ),
    CONSTRAINT [CK_Specimens_AliquotNumber] CHECK (
        [AliquotNumber] IS NULL OR [AliquotNumber] IN (1, 2)
    ),
    CONSTRAINT [FK_Specimens_Studies] FOREIGN KEY ([StudyID]) 
        REFERENCES [dbo].[tbl_Studies] ([StudyID]),
    CONSTRAINT [FK_Specimens_SampleTypes] FOREIGN KEY ([SampleTypeID]) 
        REFERENCES [dbo].[tbl_SampleTypes] ([SampleTypeID]),
    CONSTRAINT [FK_Specimens_Boxes] FOREIGN KEY ([BoxID]) 
        REFERENCES [dbo].[tbl_Boxes] ([BoxID]),
    CONSTRAINT [FK_Specimens_DiscardApproval] FOREIGN KEY ([DiscardApprovalID]) 
        REFERENCES [dbo].[tbl_Approvals] ([ApprovalID])
) ON [PRIMARY]
GO

PRINT 'Created tbl_Specimens'
GO


/*******************************************************************************
 * TABLE: tbl_ShipmentBatches
 * 
 * Purpose: Groups shipment requests imported together for approval workflow
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_ShipmentBatches](
    -- Primary Key
    [BatchID]                       [int] IDENTITY(1,1) NOT NULL,
    
    -- Import Information
    [ImportDate]                    [datetime] NOT NULL DEFAULT GETDATE(),
    [ImportedByUserId]              [nvarchar](450) NULL,
    
    -- Requestor Information
    [RequestorName]                 [nvarchar](100) NULL,
    [RequestorEmail]                [nvarchar](100) NULL,
    
    -- Request Counts
    [TotalRequested]                [int] NOT NULL DEFAULT 0,
    [TotalAvailable]                [int] NOT NULL DEFAULT 0,
    [TotalNotFound]                 [int] NOT NULL DEFAULT 0,
    [TotalPreviouslyShipped]        [int] NOT NULL DEFAULT 0,
    [TotalDiscarded]                [int] NOT NULL DEFAULT 0,
    [TotalNotYetReceived]           [int] NOT NULL DEFAULT 0,
    
    -- Approval
    [ApprovalID]                    [int] NULL,
    
    -- Status
    [Status]                        [nvarchar](50) NOT NULL DEFAULT 'Pending Approval',
    
    -- Constraints
    CONSTRAINT [PK_ShipmentBatches] PRIMARY KEY CLUSTERED ([BatchID] ASC),
    CONSTRAINT [CK_ShipmentBatches_Status] CHECK (
        [Status] IN ('Pending Approval', 'Approved', 'Rejected', 'Processing', 'Shipped')
    ),
    CONSTRAINT [FK_ShipmentBatches_ImportedBy] FOREIGN KEY ([ImportedByUserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id]),
    CONSTRAINT [FK_ShipmentBatches_Approval] FOREIGN KEY ([ApprovalID]) 
        REFERENCES [dbo].[tbl_Approvals] ([ApprovalID])
) ON [PRIMARY]
GO

PRINT 'Created tbl_ShipmentBatches'
GO


/*******************************************************************************
 * TABLE: tbl_ShipmentRequests
 * 
 * Purpose: Individual line items from imported shipment request files
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_ShipmentRequests](
    -- Primary Key
    [RequestID]                     [int] IDENTITY(1,1) NOT NULL,
    
    -- Batch Link
    [BatchID]                       [int] NULL,
    
    -- Request Information
    [RequestedBarcode]              [nvarchar](100) NOT NULL,
    [RequestedSampleTypeID]         [int] NULL,
    [RequestorName]                 [nvarchar](100) NULL,
    [RequestDate]                   [date] NULL,
    
    -- Matching
    [MatchedSpecimenID]             [int] NULL,
    
    -- Status
    [Status]                        [nvarchar](50) NOT NULL DEFAULT 'Pending',
    
    -- Constraints
    CONSTRAINT [PK_ShipmentRequests] PRIMARY KEY CLUSTERED ([RequestID] ASC),
    CONSTRAINT [CK_ShipmentRequests_Status] CHECK (
        [Status] IN ('Pending', 'Staged', 'Shipped', 'Not Found', 'Previously Shipped', 'Discarded', 'Not Yet Received')
    ),
    CONSTRAINT [FK_ShipmentRequests_Batch] FOREIGN KEY ([BatchID]) 
        REFERENCES [dbo].[tbl_ShipmentBatches] ([BatchID]),
    CONSTRAINT [FK_ShipmentRequests_SampleType] FOREIGN KEY ([RequestedSampleTypeID]) 
        REFERENCES [dbo].[tbl_SampleTypes] ([SampleTypeID]),
    CONSTRAINT [FK_ShipmentRequests_Specimen] FOREIGN KEY ([MatchedSpecimenID]) 
        REFERENCES [dbo].[tbl_Specimens] ([SpecimenID])
) ON [PRIMARY]
GO

PRINT 'Created tbl_ShipmentRequests'
GO


/*******************************************************************************
 * TABLE: tbl_Shipments
 * 
 * Purpose: Represents physical outgoing packages (manifests)
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_Shipments](
    -- Primary Key
    [ShipmentID]                    [int] IDENTITY(1,1) NOT NULL,
    
    -- Batch Link
    [BatchID]                       [int] NULL,
    
    -- Shipment Information
    [ShipmentDate]                  [date] NOT NULL,
    [Courier]                       [nvarchar](100) NULL,
    [TrackingNumber]                [nvarchar](100) NULL,
    [DestinationAddress]            [nvarchar](255) NULL,
    
    -- User who processed shipment
    [ShippedByUserId]               [nvarchar](450) NULL,
    
    -- Entire Box Shipment
    [IsEntireBox]                   [bit] NOT NULL DEFAULT 0,
    [ShippedBoxID]                  [int] NULL,
    
    -- Constraints
    CONSTRAINT [PK_Shipments] PRIMARY KEY CLUSTERED ([ShipmentID] ASC),
    CONSTRAINT [FK_Shipments_Batch] FOREIGN KEY ([BatchID]) 
        REFERENCES [dbo].[tbl_ShipmentBatches] ([BatchID]),
    CONSTRAINT [FK_Shipments_ShippedBy] FOREIGN KEY ([ShippedByUserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id]),
    CONSTRAINT [FK_Shipments_ShippedBox] FOREIGN KEY ([ShippedBoxID]) 
        REFERENCES [dbo].[tbl_Boxes] ([BoxID])
) ON [PRIMARY]
GO

PRINT 'Created tbl_Shipments'
GO


/*******************************************************************************
 * TABLE: tbl_ShipmentContents
 * 
 * Purpose: Junction table linking specimens to shipments
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_ShipmentContents](
    -- Primary Key
    [ShipmentContentID]             [int] IDENTITY(1,1) NOT NULL,
    
    -- Links
    [ShipmentID]                    [int] NOT NULL,
    [SpecimenID]                    [int] NOT NULL,
    
    -- Shipment Details
    [ConditionAtShipment]           [nvarchar](100) NULL,
    [ShippingBoxPosition]           [nvarchar](20) NULL,
    
    -- Filter Paper Specific
    [SpotsUsed]                     [int] NULL,
    
    -- Constraints
    CONSTRAINT [PK_ShipmentContents] PRIMARY KEY CLUSTERED ([ShipmentContentID] ASC),
    CONSTRAINT [FK_ShipmentContents_Shipment] FOREIGN KEY ([ShipmentID]) 
        REFERENCES [dbo].[tbl_Shipments] ([ShipmentID]),
    CONSTRAINT [FK_ShipmentContents_Specimen] FOREIGN KEY ([SpecimenID]) 
        REFERENCES [dbo].[tbl_Specimens] ([SpecimenID])
) ON [PRIMARY]
GO

PRINT 'Created tbl_ShipmentContents'
GO


/*******************************************************************************
 * TABLE: tbl_AuditLog
 * 
 * Purpose: Tracks all changes to specimen and box records
 ******************************************************************************/

CREATE TABLE [dbo].[tbl_AuditLog](
    -- Primary Key
    [AuditLogID]                    [int] IDENTITY(1,1) NOT NULL,
    
    -- What Changed
    [TableName]                     [nvarchar](100) NOT NULL,
    [RecordID]                      [int] NOT NULL,
    [FieldName]                     [nvarchar](100) NOT NULL,
    [OldValue]                      [nvarchar](max) NULL,
    [NewValue]                      [nvarchar](max) NULL,
    
    -- Who and When
    [ChangedByUserId]               [nvarchar](450) NULL,
    [Timestamp]                     [datetime] NOT NULL DEFAULT GETDATE(),
    
    -- Constraints
    CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([AuditLogID] ASC),
    CONSTRAINT [FK_AuditLog_ChangedBy] FOREIGN KEY ([ChangedByUserId]) 
        REFERENCES [dbo].[AspNetUsers] ([Id])
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_AuditLog_TableRecord] 
ON [dbo].[tbl_AuditLog] ([TableName], [RecordID])
GO

CREATE NONCLUSTERED INDEX [IX_AuditLog_Timestamp] 
ON [dbo].[tbl_AuditLog] ([Timestamp] DESC)
GO

PRINT 'Created tbl_AuditLog'
GO


/*******************************************************************************
 * SEED DATA: ASP.NET Identity Roles
 ******************************************************************************/

INSERT INTO [dbo].[AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
VALUES 
    (NEWID(), 'Admin', 'ADMIN', NEWID()),
    (NEWID(), 'Write', 'WRITE', NEWID()),
    (NEWID(), 'Read', 'READ', NEWID());

PRINT 'Created user roles (Admin, Write, Read)'
GO


/*******************************************************************************
 * SEED DATA: Studies
 ******************************************************************************/

INSERT INTO tbl_Studies (StudyCode, StudyName, PrincipalInvestigator) VALUES
    ('PRISM', 'PRISM Study', 'Dr. Dorsey'),
    ('SAPPHIRE', 'SAPPHIRE Study', 'Dr. Jane'),
    ('PBO', 'PBO Study', 'Dr. Sam');

PRINT 'Created studies'
GO


/*******************************************************************************
 * SEED DATA: Sample Types
 ******************************************************************************/

INSERT INTO tbl_SampleTypes (TypeName, IsConsumable, MaxShippableUnits, LocalReserveUnits) VALUES
    ('Plasma', 0, 1, 1),
    ('Buffy Coat', 0, NULL, NULL),
    ('Whole Blood', 0, NULL, NULL),
    ('Filter Paper', 1, 2, 2);

PRINT 'Created sample types'
GO


/*******************************************************************************
 * SEED DATA: Storage Infrastructure
 ******************************************************************************/

-- Freezers
INSERT INTO tbl_Freezers (FreezerName, Temperature, LocationInBuilding) VALUES
    ('-80 Freezer A', -80, 'Room 101'),
    ('-20 Freezer B', -20, 'Room 102');

-- Racks
INSERT INTO tbl_Racks (RackName, FreezerID) VALUES
    ('Rack 1', 1), 
    ('Rack 2', 1), 
    ('Rack 3', 1), 
    ('Rack 4', 1),
    ('Shelf 1', 2);

-- Boxes (Standard storage boxes)
INSERT INTO tbl_Boxes (BoxLabel, BoxType, BoxCategory, RackID) VALUES
    ('BOX-001', '81-slot', 'Standard', 1),
    ('BOX-002', '81-slot', 'Standard', 1),
    ('BOX-003', '100-slot', 'Standard', 2),
    ('FP-BINDER-01', 'Filter Paper Binder', 'Standard', 5);

-- System Boxes (no rack assignment)
INSERT INTO tbl_Boxes (BoxLabel, BoxType, BoxCategory, RackID) VALUES
    ('SYSTEM-TEMP', '100-slot', 'Temp', NULL),
    ('SYSTEM-TRASH', '100-slot', 'Trash', NULL);

PRINT 'Created storage infrastructure (freezers, racks, boxes)'
GO


/*******************************************************************************
 * SEED DATA: Sample Specimens
 ******************************************************************************/

-- Populate BOX-001 with Plasma samples (50 samples)
DECLARE @i INT = 1;
WHILE @i <= 50
BEGIN
    INSERT INTO tbl_Specimens (BarcodeID, StudyID, SampleTypeID, CollectionDate, BoxID, PositionRow, PositionCol, AliquotNumber)
    VALUES (
        'PLASMA-' + FORMAT(@i, '000'), 
        1,  -- PRISM study
        1,  -- Plasma
        DATEADD(day, -@i, GETDATE()), 
        1,  -- BOX-001
        (@i-1)/9 + 1, 
        (@i-1)%9 + 1,
        CASE WHEN @i % 2 = 1 THEN 1 ELSE 2 END  -- Alternate aliquot numbers
    );
    SET @i = @i + 1;
END

PRINT 'Created 50 Plasma samples in BOX-001'


-- Populate BOX-002 with Buffy Coat samples (35 samples)
SET @i = 1;
WHILE @i <= 35
BEGIN
    INSERT INTO tbl_Specimens (BarcodeID, StudyID, SampleTypeID, CollectionDate, BoxID, PositionRow, PositionCol)
    VALUES (
        'BUFFY-' + FORMAT(@i, '000'), 
        2,  -- SAPPHIRE study
        2,  -- Buffy Coat
        DATEADD(day, -@i, GETDATE()), 
        2,  -- BOX-002
        (@i-1)/9 + 1, 
        (@i-1)%9 + 1
    );
    SET @i = @i + 1;
END

PRINT 'Created 35 Buffy Coat samples in BOX-002'


-- Populate BOX-003 with Whole Blood samples (30 samples)
SET @i = 1;
WHILE @i <= 30
BEGIN
    INSERT INTO tbl_Specimens (BarcodeID, StudyID, SampleTypeID, CollectionDate, BoxID, PositionRow, PositionCol)
    VALUES (
        'WB-' + FORMAT(@i, '000'), 
        1,  -- PRISM study
        3,  -- Whole Blood
        DATEADD(day, -@i, GETDATE()), 
        3,  -- BOX-003
        (@i-1)/10 + 1, 
        (@i-1)%10 + 1
    );
    SET @i = @i + 1;
END

PRINT 'Created 30 Whole Blood samples in BOX-003'


-- Populate FP-BINDER-01 with Filter Paper samples (25 samples)
SET @i = 1;
WHILE @i <= 25
BEGIN
    INSERT INTO tbl_Specimens (BarcodeID, StudyID, SampleTypeID, CollectionDate, BoxID, PositionRow, PositionCol, RemainingSpots)
    VALUES (
        'FP-' + FORMAT(@i, '000'), 
        3,  -- PBO study
        4,  -- Filter Paper
        DATEADD(day, -@i, GETDATE()), 
        4,  -- FP-BINDER-01
        @i, 
        1,
        4   -- All 4 spots available
    );
    SET @i = @i + 1;
END

PRINT 'Created 25 Filter Paper samples in FP-BINDER-01'
GO


/*******************************************************************************
 * SUMMARY
 ******************************************************************************/

PRINT ''
PRINT '============================================================'
PRINT 'STASIS Database Creation Complete'
PRINT '============================================================'
PRINT ''
PRINT 'ASP.NET IDENTITY TABLES:'
PRINT '  - AspNetUsers'
PRINT '  - AspNetRoles'
PRINT '  - AspNetUserRoles'
PRINT '  - AspNetUserClaims'
PRINT '  - AspNetRoleClaims'
PRINT '  - AspNetUserLogins'
PRINT '  - AspNetUserTokens'
PRINT ''
PRINT 'STASIS TABLES:'
PRINT '  - tbl_UserProfiles'
PRINT '  - tbl_Freezers'
PRINT '  - tbl_Racks'
PRINT '  - tbl_Boxes'
PRINT '  - tbl_Studies'
PRINT '  - tbl_SampleTypes'
PRINT '  - tbl_Approvals'
PRINT '  - tbl_Specimens'
PRINT '  - tbl_ShipmentBatches'
PRINT '  - tbl_ShipmentRequests'
PRINT '  - tbl_Shipments'
PRINT '  - tbl_ShipmentContents'
PRINT '  - tbl_AuditLog'
PRINT ''
PRINT 'SEED DATA:'
PRINT '  - 3 User roles (Admin, Write, Read)'
PRINT '  - 3 Studies (PRISM, SAPPHIRE, PBO)'
PRINT '  - 4 Sample types (Plasma, Buffy Coat, Whole Blood, Filter Paper)'
PRINT '  - 2 Freezers, 5 Racks, 6 Boxes (including SYSTEM-TEMP and SYSTEM-TRASH)'
PRINT '  - 140 Specimen samples'
PRINT ''
PRINT 'NEXT STEPS:'
PRINT '  1. Configure ASP.NET app connection string to STASIS database'
PRINT '  2. In Program.cs/Startup.cs, configure Identity to use existing tables'
PRINT '  3. Create admin user account via application registration'
PRINT ''
PRINT '============================================================'
GO
