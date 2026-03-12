# STASIS Phase 7: Manual Testing & Production Readiness

**Created:** March 12, 2026
**Purpose:** Guide for manual testing of items that cannot be verified through code alone, plus production readiness checklists.

---

## 1. Barcode Scanner Testing (NFR-USE-01)

### What was coded
- All barcode/label input fields have `autofocus` attribute (Add, Move, Place, Search, Rebox, Discard)
- Standard HTML forms submit on Enter key without extra JS
- Rebox page has dedicated Enter-key handler for client-side scan-add workflow
- After successful operations (redirect), autofocus returns cursor to input field

### Manual test checklist

- [ ] **Samples/Add** — Connect a USB barcode scanner. Scan a barcode → verify it populates the BarcodeID field. Fill remaining fields and press Enter → verify form submits. After redirect, verify cursor is back in BarcodeID field.
- [ ] **Boxes/Move** — Scan a barcode → verify lookup triggers on Enter. After specimen appears, select destination and submit. After redirect, verify cursor returns to barcode field.
- [ ] **Boxes/Place** — Scan a box label → verify lookup triggers on Enter.
- [ ] **Boxes/Rebox** — Scan a barcode → verify specimen is added to the list on Enter (no button click needed). Scan another → verify auto-increment of row/col. Continue scanning until box is full.
- [ ] **Boxes/Search** — Type or scan a box label → verify search triggers on Enter.
- [ ] **Samples/Discard** — Paste multiple barcodes (one per line) → click Look Up → verify results.
- [ ] **General** — Verify no page requires a mouse click to initiate lookup/submit when using a scanner. The scanner typically sends characters followed by Enter.

### Notes
- If the scanner sends a Tab instead of Enter after the barcode, you may need to configure the scanner to send Enter (CR/LF). This is a scanner configuration issue, not an app issue.
- If scan speed is too fast and characters are dropped, check the scanner's inter-character delay setting.

---

## 2. Color-Coded Box Occupancy Grid (NFR-USE-02)

### What was coded
- `Boxes/Search` renders a color-coded grid with legend
- Colors: Green (occupied), Grey (empty), Yellow (staged), Blue (temp)
- Grid size adapts to box type (9x9 for 81-slot, 10x10 for 100-slot)
- Each cell shows last 6 chars of barcode with full tooltip

### Manual test checklist

- [ ] Navigate to `Boxes/Search` and select a box with specimens → verify green cells appear in correct positions
- [ ] Move a specimen to Temp → verify the cell turns blue on the grid
- [ ] Verify empty positions show grey "—" cells
- [ ] Verify the occupancy count (e.g., "Occupied: 5 / 81") is accurate
- [ ] Verify tooltip on specimen cells shows full barcode, sample type, and status
- [ ] Test with different box types (81-slot, 100-slot) → verify grid dimensions match
- [ ] Verify grid is readable on both desktop and tablet-sized screens

---

## 3. Bulk Import Performance (NFR-PER-01)

### What was coded
- `SampleService.ImportSpecimensFromCsv` handles CSV parsing with validation
- Pre-loads lookup data (studies, sample types, boxes, existing barcodes) before the loop

### Manual test procedure

1. **Generate a test CSV** with 1000+ rows. Use this format:
   ```
   BarcodeID,LegacyID,StudyCode,SampleType,CollectionDate,BoxLabel,PositionRow,PositionCol
   TEST-0001,,STUDY1,Plasma,2025-01-01,BOX-001,1,1
   TEST-0002,,STUDY1,Plasma,2025-01-01,BOX-001,1,2
   ...
   ```
   Ensure referenced studies, sample types, and boxes exist in the database.

2. **Navigate to Samples/Import** and upload the CSV.

3. **Measure:**
   - [ ] Preview step completes in < 10 seconds for 1000 rows
   - [ ] Commit step completes in < 30 seconds for 1000 rows
   - [ ] No browser timeout or server error

4. **If too slow:**
   - Check if the per-row `AnyAsync` position check is the bottleneck (line ~264 in SampleService.cs). Consider pre-loading all occupied positions for referenced boxes.
   - Consider wrapping the commit in a single transaction with `AddRange` instead of individual adds.

---

## 4. Search Performance (NFR-PER-02)

### What was coded
- Added database indexes on `Specimen.Status`, `Specimen.StudyID`, and `Specimen.SampleTypeID` (migration: `AddSearchPerformanceIndexes`)
- Existing indexes on `BarcodeID` (unique) and `BoxID/PositionRow/PositionCol` (unique)

### Manual test procedure

1. **Seed the database** with 1000+ specimen records (or use the bulk import to create them).

2. **Test search scenarios:**
   - [ ] Search by barcode substring → results in < 2 seconds
   - [ ] Filter by study → results in < 2 seconds
   - [ ] Filter by sample type → results in < 2 seconds
   - [ ] Combined filters (barcode + study + type) → results in < 2 seconds
   - [ ] Pagination: click Next/Previous through all pages → each page loads in < 2 seconds

3. **If too slow:**
   - Run `EXPLAIN ANALYZE` on the PostgreSQL query to check index usage:
     ```sql
     EXPLAIN ANALYZE SELECT * FROM "tbl_Specimens"
     WHERE "BarcodeID" LIKE '%TEST%' AND "StudyID" = 1
     ORDER BY "SpecimenID" LIMIT 25 OFFSET 0;
     ```
   - Verify the new indexes are applied: `dotnet ef database update`
   - Consider adding a covering index if LIKE queries are slow: partial text indexes won't help with `LIKE '%substring%'`, but `LIKE 'prefix%'` will use the btree index.

---

## 5. Backup and Restore Procedures (NFR-SYS-02)

### Recommended backup strategy

#### Daily backup script
```bash
#!/bin/bash
# Save as /opt/stasis/backup.sh and add to crontab
BACKUP_DIR="/var/backups/stasis"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
DB_NAME="stasis"
DB_USER="stasis_app"

mkdir -p "$BACKUP_DIR"

# Full database dump (custom format for selective restore)
pg_dump -U "$DB_USER" -d "$DB_NAME" -Fc -f "$BACKUP_DIR/stasis_$TIMESTAMP.dump"

# Keep only last 30 days of backups
find "$BACKUP_DIR" -name "stasis_*.dump" -mtime +30 -delete

echo "Backup completed: stasis_$TIMESTAMP.dump"
```

#### Crontab entry (daily at 2 AM)
```
0 2 * * * /opt/stasis/backup.sh >> /var/log/stasis-backup.log 2>&1
```

#### Restore procedure
```bash
# List contents of a backup
pg_restore -l /var/backups/stasis/stasis_20260312_020000.dump

# Full restore (WARNING: drops and recreates all objects)
pg_restore -U stasis_app -d stasis --clean --if-exists \
  /var/backups/stasis/stasis_20260312_020000.dump

# Restore specific table only
pg_restore -U stasis_app -d stasis --table=tbl_Specimens \
  /var/backups/stasis/stasis_20260312_020000.dump
```

### Manual test checklist

- [ ] Run the backup script → verify a `.dump` file is created
- [ ] Create a test database, restore from the dump → verify data integrity
- [ ] Verify the application can connect to the restored database
- [ ] Document the backup location and retention policy in your operations runbook

---

## 6. Deployment Checklist (Production)

### Pre-deployment

- [ ] **Database:** PostgreSQL 17 installed and running
- [ ] **Database user:** `stasis_app` role created with appropriate permissions
- [ ] **Database:** `stasis` database created, owned by `stasis_app`
- [ ] **Migrations applied:** `dotnet ef database update --project STASIS.csproj`
- [ ] **Connection string:** Configured via environment variable or user secrets (never in `appsettings.json`)
  ```
  ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=stasis;Username=stasis_app;Password=SECURE_PASSWORD
  ```
- [ ] **Admin seed password:** Set via environment variable or config
  ```
  AdminSeedPassword=YOUR_SECURE_ADMIN_PASSWORD
  ```
- [ ] **HTTPS certificate:** Configured for production domain
- [ ] **Email settings:** (optional) Configure SMTP in `appsettings.json` or environment variables if email notifications are needed

### Application deployment

- [ ] **Publish:** `dotnet publish STASIS.csproj -c Release -o /opt/stasis/app`
- [ ] **Runtime:** .NET 10 runtime installed on server
- [ ] **Reverse proxy:** Configure Nginx or Apache as reverse proxy to Kestrel
  ```nginx
  server {
      listen 443 ssl;
      server_name stasis.yourdomain.com;

      location / {
          proxy_pass http://localhost:5000;
          proxy_http_version 1.1;
          proxy_set_header Upgrade $http_upgrade;
          proxy_set_header Connection keep-alive;
          proxy_set_header Host $host;
          proxy_cache_bypass $http_upgrade;
          proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
          proxy_set_header X-Forwarded-Proto $scheme;
      }
  }
  ```
- [ ] **Service file:** Create systemd service for auto-start
  ```ini
  [Unit]
  Description=STASIS Application
  After=network.target postgresql.service

  [Service]
  WorkingDirectory=/opt/stasis/app
  ExecStart=/usr/bin/dotnet /opt/stasis/app/STASIS.dll
  Restart=always
  RestartSec=10
  Environment=ASPNETCORE_ENVIRONMENT=Production
  Environment=ASPNETCORE_URLS=http://localhost:5000
  Environment=ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=stasis;Username=stasis_app;Password=SECURE_PASSWORD
  Environment=AdminSeedPassword=YOUR_SECURE_ADMIN_PASSWORD

  [Install]
  WantedBy=multi-user.target
  ```
- [ ] **Firewall:** Only expose ports 443 (HTTPS) and 22 (SSH) externally. Kestrel port 5000 should be internal only.

### Post-deployment verification

- [ ] Navigate to the application URL → login page loads
- [ ] Log in as `admin@stasis.com` → dashboard loads
- [ ] Change admin password on first login (forced by `MustChangePassword`)
- [ ] Create a test user with Read role → verify they can search but not add
- [ ] Create a test user with Write role → verify they can add specimens
- [ ] Add a specimen → verify it appears in search
- [ ] Run through one complete workflow: Add specimen → Move → Ship → verify audit trail

### IIS deployment alternative

If deploying to Windows/IIS instead of Linux:

- [ ] Install the .NET 10 Hosting Bundle
- [ ] Publish to IIS site folder
- [ ] Configure Application Pool (No Managed Code, per-site)
- [ ] Set environment variables in `web.config` `<environmentVariables>` section
- [ ] Configure HTTPS binding with SSL certificate

---

## 7. Automated Tests (Future Work)

### What is NOT coded yet

An xUnit test project does not exist. When you're ready to add tests, here's the recommended approach:

#### Create test project
```bash
dotnet new xunit -n STASIS.Tests
dotnet sln STASIS.sln add STASIS.Tests/STASIS.Tests.csproj
cd STASIS.Tests
dotnet add reference ../STASIS.csproj
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

#### Priority test scenarios

| Service | Test | What to verify |
|---------|------|----------------|
| SampleService | `AddSpecimen_WithDuplicateBarcode_ThrowsException` | Barcode uniqueness enforced |
| SampleService | `ImportSpecimensFromCsv_ValidFile_ImportsAllRows` | CSV import happy path |
| SampleService | `ImportSpecimensFromCsv_DuplicateBarcodes_ReportsErrors` | Intra-file duplicate detection |
| SampleService | `RequestDiscardAsync_CreatesApprovalRecord` | Discard workflow initiation |
| SampleService | `ExecuteDiscardAsync_SetsStatusToDiscarded` | Discard execution |
| StorageService | `MoveSpecimenAsync_UpdatesPosition` | Basic move |
| StorageService | `MoveSpecimenAsync_ToOccupiedPosition_ThrowsException` | Position conflict |
| StorageService | `MoveToTempAsync_SetsStatusToTemp` | Temp move |
| StorageService | `CheckAndUnassignEmptyBoxAsync_ClearsRackId` | Empty box cleanup |
| ShipmentService | `ImportBatchFromCsvAsync_MatchesSpecimens` | Auto-matching |
| ShipmentService | `ShipBatchAsync_Plasma2_ThrowsValidationError` | Plasma-2 block |
| ShipmentService | `ShipBatchAsync_FilterPaper_DecrementsSpots` | Spot tracking |
| ShipmentService | `ShipBatchAsync_ExceedsInternationalLimit_ThrowsError` | International limit |
| ShipmentService | `ValidateShipmentAsync_AllValid_ReturnsNoErrors` | Happy path validation |

#### Test pattern (using in-memory DB)
```csharp
public class SampleServiceTests
{
    private StasisDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<StasisDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new StasisDbContext(options);
    }

    [Fact]
    public async Task AddSpecimen_ValidSpecimen_Succeeds()
    {
        using var context = CreateContext();
        var auditService = new AuditService(context);
        var service = new SampleService(context, auditService);

        var specimen = new Specimen { BarcodeID = "TEST-001", Status = "In-Stock" };
        await service.AddSpecimen(specimen);

        var found = await service.GetSpecimenByBarcode("TEST-001");
        Assert.NotNull(found);
        Assert.Equal("In-Stock", found.Status);
    }
}
```

---

## Summary: What Was Coded vs What Needs Manual Testing

| Item | Coded? | Manual Testing Required? |
|------|--------|--------------------------|
| 7.1 Barcode scanner autofocus + Enter | Yes | Yes — test with physical scanner |
| 7.2 Color-coded box grid | Already existed (Phase 3) | Yes — verify visual correctness |
| 7.3 Bulk import performance | Code exists | Yes — test with 1000+ row CSV |
| 7.4 Search performance indexes | Yes (migration added) | Yes — benchmark with 1000+ records |
| 7.5 Backup/restore | Documented above | Yes — test backup + restore cycle |
| 7.6 Automated tests | Not coded (guide above) | N/A — create test project when ready |
| 7.7 Deployment checklist | Documented above | Yes — follow checklist during deployment |
