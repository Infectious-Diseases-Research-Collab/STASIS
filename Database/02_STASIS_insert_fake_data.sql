BEGIN;

-- Seed Identity roles expected by Program.cs and the admin UI.
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
VALUES
    ('00000000-0000-0000-0000-000000000001', 'Admin', 'ADMIN', '10000000-0000-0000-0000-000000000001'),
    ('00000000-0000-0000-0000-000000000002', 'Write', 'WRITE', '10000000-0000-0000-0000-000000000002'),
    ('00000000-0000-0000-0000-000000000003', 'Read', 'READ', '10000000-0000-0000-0000-000000000003');

INSERT INTO "tbl_Studies" ("StudyCode", "StudyName", "PrincipalInvestigator")
VALUES
    ('PRISM', 'PRISM Study', 'Dr. Dorsey'),
    ('SAPPHIRE', 'SAPPHIRE Study', 'Dr. Jane'),
    ('PBO', 'PBO Study', 'Dr. Sam');

INSERT INTO "tbl_SampleTypes" ("TypeName", "IsConsumable", "MaxShippableUnits", "LocalReserveUnits")
VALUES
    ('Plasma', false, 1, 1),
    ('Buffy Coat', false, NULL, NULL),
    ('Whole Blood', false, NULL, NULL),
    ('Filter Paper', true, 2, 2);

INSERT INTO "tbl_Freezers" ("FreezerName", "Description", "Temperature", "LocationInBuilding")
VALUES
    ('-80 Freezer A', 'Main ultra-low freezer', -80, 'Room 101'),
    ('-20 Freezer B', 'Secondary freezer for short-term storage', -20, 'Room 102');

INSERT INTO "tbl_Compartments" ("CompartmentName", "Description", "FreezerID")
VALUES
    ('Top Shelf', 'Upper section of -80 Freezer A', 1),
    ('Middle Shelf', 'Middle section of -80 Freezer A', 1),
    ('Bottom Shelf', 'Lower section of -80 Freezer A', 1),
    ('Main Compartment', NULL, 2);

INSERT INTO "tbl_Racks" ("RackName", "Description", "CompartmentID")
VALUES
    ('Rack 1', NULL, 1),
    ('Rack 2', NULL, 1),
    ('Rack 3', NULL, 2),
    ('Rack 4', NULL, 2),
    ('Shelf 1', 'Single shelf for filter paper binders', 4);

INSERT INTO "tbl_Boxes" ("BoxLabel", "BoxType", "BoxCategory", "RackID")
VALUES
    ('BOX-001', '81-slot', 'Standard', 1),
    ('BOX-002', '81-slot', 'Standard', 1),
    ('BOX-003', '100-slot', 'Standard', 2),
    ('FP-BINDER-01', 'Filter Paper Binder', 'Standard', 5),
    ('SYSTEM-TEMP', '100-slot', 'Temp', NULL),
    ('SYSTEM-TRASH', '100-slot', 'Trash', NULL),
    ('SYSTEM-SHIPPING', '100-slot', 'Shipping', NULL);

-- Plasma: aliquots 1 and 2, with ParticipantIDs
INSERT INTO "tbl_Specimens"
    ("BarcodeID", "StudyID", "SampleTypeID", "CollectionDate", "BoxID", "PositionRow", "PositionCol", "AliquotNumber", "ParticipantID")
SELECT
    'PLASMA-' || lpad(gs::text, 3, '0'),
    1,
    1,
    CURRENT_TIMESTAMP - make_interval(days => gs),
    1,
    ((gs - 1) / 9) + 1,
    ((gs - 1) % 9) + 1,
    CASE WHEN gs % 2 = 1 THEN 1 ELSE 2 END,
    'PC-K-' || lpad(((gs + 1) / 2)::text, 3, '0')
FROM generate_series(1, 50) AS gs;

-- Buffy Coat: no aliquot, no ParticipantID
INSERT INTO "tbl_Specimens"
    ("BarcodeID", "StudyID", "SampleTypeID", "CollectionDate", "BoxID", "PositionRow", "PositionCol")
SELECT
    'BUFFY-' || lpad(gs::text, 3, '0'),
    2,
    2,
    CURRENT_TIMESTAMP - make_interval(days => gs),
    2,
    ((gs - 1) / 9) + 1,
    ((gs - 1) % 9) + 1
FROM generate_series(1, 35) AS gs;

-- Whole Blood
INSERT INTO "tbl_Specimens"
    ("BarcodeID", "StudyID", "SampleTypeID", "CollectionDate", "BoxID", "PositionRow", "PositionCol")
SELECT
    'WB-' || lpad(gs::text, 3, '0'),
    1,
    3,
    CURRENT_TIMESTAMP - make_interval(days => gs),
    3,
    ((gs - 1) / 10) + 1,
    ((gs - 1) % 10) + 1
FROM generate_series(1, 30) AS gs;

-- Filter Paper: RemainingSpots
INSERT INTO "tbl_Specimens"
    ("BarcodeID", "StudyID", "SampleTypeID", "CollectionDate", "BoxID", "PositionRow", "PositionCol", "RemainingSpots")
SELECT
    'FP-' || lpad(gs::text, 3, '0'),
    3,
    4,
    CURRENT_TIMESTAMP - make_interval(days => gs),
    4,
    gs,
    1,
    4
FROM generate_series(1, 25) AS gs;

-- PBMC: aliquots 1, 2, and 3 with CellCount and ParticipantID (tests AliquotNumber = 3 and CellCount)
INSERT INTO "tbl_Specimens"
    ("BarcodeID", "StudyID", "SampleTypeID", "CollectionDate", "BoxID", "PositionRow", "PositionCol", "AliquotNumber", "CellCount", "ParticipantID")
VALUES
    ('PBMC-001', 2, 2, CURRENT_TIMESTAMP - interval '10 days', 2, 5, 1, 1, 5000000, 'BC-001'),
    ('PBMC-002', 2, 2, CURRENT_TIMESTAMP - interval '10 days', 2, 5, 2, 2, 4800000, 'BC-001'),
    ('PBMC-003', 2, 2, CURRENT_TIMESTAMP - interval '10 days', 2, 5, 3, 3, 5200000, 'BC-001'),
    ('PBMC-004', 2, 2, CURRENT_TIMESTAMP - interval '20 days', 2, 5, 4, 1, 6100000, 'BC-002'),
    ('PBMC-005', 2, 2, CURRENT_TIMESTAMP - interval '20 days', 2, 5, 5, 2, 5900000, 'BC-002'),
    ('PBMC-006', 2, 2, CURRENT_TIMESTAMP - interval '20 days', 2, 5, 6, 3, 6300000, 'BC-002');

COMMIT;