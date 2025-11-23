using Microsoft.EntityFrameworkCore;
using STASIS.Models;

namespace STASIS.Data;

public class StasisDbContext : DbContext
{
    public StasisDbContext(DbContextOptions<StasisDbContext> options) : base(options)
    {
    }

    public DbSet<Study> Studies { get; set; }
    public DbSet<SampleType> SampleTypes { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Freezer> Freezers { get; set; }
    public DbSet<Rack> Racks { get; set; }
    public DbSet<Box> Boxes { get; set; }
    public DbSet<Specimen> Specimens { get; set; }
    public DbSet<ShipmentRequest> ShipmentRequests { get; set; }
    public DbSet<Shipment> Shipments { get; set; }
    public DbSet<ShipmentContent> ShipmentContents { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Study>().ToTable("tbl_Studies");
        modelBuilder.Entity<SampleType>().ToTable("tbl_SampleTypes");
        modelBuilder.Entity<User>().ToTable("tbl_Users");
        modelBuilder.Entity<Freezer>().ToTable("tbl_Freezers");
        modelBuilder.Entity<Rack>().ToTable("tbl_Racks");
        modelBuilder.Entity<Box>().ToTable("tbl_Boxes");
        modelBuilder.Entity<Specimen>().ToTable("tbl_Specimens");
        modelBuilder.Entity<ShipmentRequest>().ToTable("tbl_ShipmentRequests");
        modelBuilder.Entity<Shipment>().ToTable("tbl_Shipments");
        modelBuilder.Entity<ShipmentContent>().ToTable("tbl_ShipmentContents");
        modelBuilder.Entity<AuditLog>().ToTable("tbl_AuditLog");
    }
}
