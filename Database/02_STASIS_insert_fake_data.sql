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

INSERT INTO "tbl_Freezers" ("FreezerName", "Temperature", "LocationInBuilding")
VALUES
    ('-80 Freezer A', -80, 'Room 101'),
    ('-20 Freezer B', -20, 'Room 102');

INSERT INTO "tbl_Racks" ("RackName", "FreezerID")
VALUES
    ('Rack 1', 1),
    ('Rack 2', 1),
    ('Rack 3', 1),
    ('Rack 4', 1),
    ('Shelf 1', 2);

INSERT INTO "tbl_Boxes" ("BoxLabel", "BoxType", "BoxCategory", "RackID")
VALUES
    ('BOX-001', '81-slot', 'Standard', 1),
    ('BOX-002', '81-slot', 'Standard', 1),
    ('BOX-003', '100-slot', 'Standard', 2),
    ('FP-BINDER-01', 'Filter Paper Binder', 'Standard', 5),
    ('SYSTEM-TEMP', '100-slot', 'Temp', NULL),
    ('SYSTEM-TRASH', '100-slot', 'Trash', NULL),
    ('SYSTEM-SHIPPING', '100-slot', 'Shipping', NULL);

INSERT INTO "tbl_Specimens"
    ("BarcodeID", "StudyID", "SampleTypeID", "CollectionDate", "BoxID", "PositionRow", "PositionCol", "AliquotNumber")
SELECT
    'PLASMA-' || lpad(gs::text, 3, '0'),
    1,
    1,
    CURRENT_TIMESTAMP - make_interval(days => gs),
    1,
    ((gs - 1) / 9) + 1,
    ((gs - 1) % 9) + 1,
    CASE WHEN gs % 2 = 1 THEN 1 ELSE 2 END
FROM generate_series(1, 50) AS gs;

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

COMMIT;