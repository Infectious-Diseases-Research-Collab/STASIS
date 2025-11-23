-- SQL Server Setup Script for STASIS Database

-- Drop existing tables in reverse order of creation to avoid foreign key constraints
IF OBJECT_ID('tbl_AuditLog', 'U') IS NOT NULL DROP TABLE tbl_AuditLog;
IF OBJECT_ID('tbl_ShipmentContents', 'U') IS NOT NULL DROP TABLE tbl_ShipmentContents;
IF OBJECT_ID('tbl_Shipments', 'U') IS NOT NULL DROP TABLE tbl_Shipments;
IF OBJECT_ID('tbl_ShipmentRequests', 'U') IS NOT NULL DROP TABLE tbl_ShipmentRequests;
IF OBJECT_ID('tbl_Specimens', 'U') IS NOT NULL DROP TABLE tbl_Specimens;
IF OBJECT_ID('tbl_Boxes', 'U') IS NOT NULL DROP TABLE tbl_Boxes;
IF OBJECT_ID('tbl_Racks', 'U') IS NOT NULL DROP TABLE tbl_Racks;
IF OBJECT_ID('tbl_Freezers', 'U') IS NOT NULL DROP TABLE tbl_Freezers;
IF OBJECT_ID('tbl_SampleTypes', 'U') IS NOT NULL DROP TABLE tbl_SampleTypes;
IF OBJECT_ID('tbl_Studies', 'U') IS NOT NULL DROP TABLE tbl_Studies;
IF OBJECT_ID('tbl_Users', 'U') IS NOT NULL DROP TABLE tbl_Users;

-- 1. Reference & Metadata Tables
CREATE TABLE tbl_Studies (
    StudyID INT IDENTITY(1,1) PRIMARY KEY,
    StudyCode NVARCHAR(50) NOT NULL UNIQUE,
    PrincipalInvestigator NVARCHAR(100)
);

CREATE TABLE tbl_SampleTypes (
    SampleTypeID INT IDENTITY(1,1) PRIMARY KEY,
    TypeName NVARCHAR(50) NOT NULL UNIQUE,
    IsConsumable BIT NOT NULL DEFAULT 0
);

CREATE TABLE tbl_Users (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    Role NVARCHAR(50) NOT NULL CHECK (Role IN ('Admin', 'Technician', 'Read-Only'))
);

-- 2. Core Inventory Tables
CREATE TABLE tbl_Freezers (
    FreezerID INT IDENTITY(1,1) PRIMARY KEY,
    FreezerName NVARCHAR(50) NOT NULL UNIQUE,
    Temperature INT,
    LocationInBuilding NVARCHAR(100)
);

CREATE TABLE tbl_Racks (
    RackID INT IDENTITY(1,1) PRIMARY KEY,
    RackName NVARCHAR(50) NOT NULL,
    FreezerID INT FOREIGN KEY REFERENCES tbl_Freezers(FreezerID)
);

CREATE TABLE tbl_Boxes (
    BoxID INT IDENTITY(1,1) PRIMARY KEY,
    BoxLabel NVARCHAR(50) NOT NULL UNIQUE,
    BoxType NVARCHAR(50) NOT NULL CHECK (BoxType IN ('81-slot', '100-slot', 'Filter Paper Binder')),
    RackID INT FOREIGN KEY REFERENCES tbl_Racks(RackID)
);

CREATE TABLE tbl_Specimens (
    SpecimenID INT IDENTITY(1,1) PRIMARY KEY,
    BarcodeID NVARCHAR(100) NOT NULL UNIQUE,
    StudyID INT FOREIGN KEY REFERENCES tbl_Studies(StudyID),
    SampleTypeID INT FOREIGN KEY REFERENCES tbl_SampleTypes(SampleTypeID),
    CollectionDate DATE,
    BoxID INT FOREIGN KEY REFERENCES tbl_Boxes(BoxID),
    PositionRow INT,
    PositionCol INT,
    RemainingSpots INT DEFAULT NULL, -- For Filter Papers
    Status NVARCHAR(50) NOT NULL DEFAULT 'In-Stock' CHECK (Status IN ('In-Stock', 'Shipped', 'Missing', 'Depleted')),
    CONSTRAINT UQ_BoxPosition UNIQUE (BoxID, PositionRow, PositionCol)
);

-- 3. Shipment & Request Tables
CREATE TABLE tbl_ShipmentRequests (
    RequestID INT IDENTITY(1,1) PRIMARY KEY,
    RequestedBarcode NVARCHAR(100) NOT NULL,
    RequestorName NVARCHAR(100),
    RequestDate DATE,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending' CHECK (Status IN ('Pending', 'Staged', 'Shipped', 'Not Found', 'Previously Shipped'))
);

CREATE TABLE tbl_Shipments (
    ShipmentID INT IDENTITY(1,1) PRIMARY KEY,
    ShipmentDate DATE NOT NULL,
    Courier NVARCHAR(100),
    TrackingNumber NVARCHAR(100),
    DestinationAddress NVARCHAR(255)
);

CREATE TABLE tbl_ShipmentContents (
    ShipmentContentID INT IDENTITY(1,1) PRIMARY KEY,
    ShipmentID INT FOREIGN KEY REFERENCES tbl_Shipments(ShipmentID),
    SpecimenID INT FOREIGN KEY REFERENCES tbl_Specimens(SpecimenID),
    ConditionAtShipment NVARCHAR(100),
    SpotsUsed INT
);

-- 4. System & Audit Tables
CREATE TABLE tbl_AuditLog (
    AuditLogID INT IDENTITY(1,1) PRIMARY KEY,
    TableName NVARCHAR(100),
    RecordID INT,
    FieldName NVARCHAR(100),
    OldValue NVARCHAR(MAX),
    NewValue NVARCHAR(MAX),
    ChangedBy INT FOREIGN KEY REFERENCES tbl_Users(UserID),
    Timestamp DATETIME DEFAULT GETDATE()
);

-- =================================================================
-- INSERT FAKE DATA
-- =================================================================

-- Users
INSERT INTO tbl_Users (Username, Role) VALUES ('admin', 'Admin'), ('tech1', 'Technician');

-- Studies
INSERT INTO tbl_Studies (StudyCode, PrincipalInvestigator) VALUES
('FLU-2024', 'Dr. Smith'),
('PED-TB-001', 'Dr. Jones'),
('HIV-VAC-03', 'Dr. Patel');

-- Sample Types
INSERT INTO tbl_SampleTypes (TypeName, IsConsumable) VALUES
('Plasma', 0),
('Buffy Coat', 0),
('Whole Blood', 0),
('Filter Paper', 1);

-- Storage
INSERT INTO tbl_Freezers (FreezerName, Temperature, LocationInBuilding) VALUES
('-80 Freezer A', -80, 'Room 101'),
('-20 Freezer B', -20, 'Room 102');

INSERT INTO tbl_Racks (RackName, FreezerID) VALUES
('Rack 1', 1), ('Rack 2', 1), ('Rack 3', 1), ('Rack 4', 1),
('Shelf 1', 2);

INSERT INTO tbl_Boxes (BoxLabel, BoxType, RackID) VALUES
('BOX-001', '81-slot', 1),
('BOX-002', '81-slot', 1),
('BOX-003', '100-slot', 2),
('FP-BINDER-01', 'Filter Paper Binder', 5);

-- Samples
-- Populate BOX-001 (Plasma)
DECLARE @i INT = 1;
WHILE @i <= 20
BEGIN
    INSERT INTO tbl_Specimens (BarcodeID, StudyID, SampleTypeID, CollectionDate, BoxID, PositionRow, PositionCol)
    VALUES ('PLASMA-' + FORMAT(@i, '000'), 1, 1, DATEADD(day, -@i, GETDATE()), 1, (@i-1)/9 + 1, (@i-1)%9 + 1);
    SET @i = @i + 1;
END

-- Populate BOX-002 (Buffy Coat)
SET @i = 1;
WHILE @i <= 15
BEGIN
    INSERT INTO tbl_Specimens (BarcodeID, StudyID, SampleTypeID, CollectionDate, BoxID, PositionRow, PositionCol)
    VALUES ('BUFFY-' + FORMAT(@i, '000'), 2, 2, DATEADD(day, -@i, GETDATE()), 2, (@i-1)/9 + 1, (@i-1)%9 + 1);
    SET @i = @i + 1;
END

-- Populate BOX-003 (Whole Blood)
SET @i = 1;
WHILE @i <= 10
BEGIN
    INSERT INTO tbl_Specimens (BarcodeID, StudyID, SampleTypeID, CollectionDate, BoxID, PositionRow, PositionCol)
    VALUES ('WB-' + FORMAT(@i, '000'), 1, 3, DATEADD(day, -@i, GETDATE()), 3, (@i-1)/10 + 1, (@i-1)%10 + 1);
    SET @i = @i + 1;
END

-- Populate FP-BINDER-01 (Filter Paper)
SET @i = 1;
WHILE @i <= 5
BEGIN
    INSERT INTO tbl_Specimens (BarcodeID, StudyID, SampleTypeID, CollectionDate, BoxID, PositionRow, PositionCol, RemainingSpots)
    VALUES ('FP-' + FORMAT(@i, '000'), 3, 4, DATEADD(day, -@i, GETDATE()), 4, @i, 1, 4);
    SET @i = @i + 1;
END

-- Shipment Requests
INSERT INTO tbl_ShipmentRequests (RequestedBarcode, RequestorName, RequestDate) VALUES
('PLASMA-001', 'Dr. Evil', GETDATE()),
('BUFFY-003', 'Dr. Evil', GETDATE()),
('FP-002', 'Dr. Good', GETDATE()),
('MISSING-999', 'Dr. Who', GETDATE());

PRINT 'STASIS database schema and sample data created successfully.';
