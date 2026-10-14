using Microsoft.EntityFrameworkCore;

namespace OpenTablo.Api.Data;

public class TabloDbContext(DbContextOptions<TabloDbContext> options) : DbContext(options)
{
    public DbSet<StopArea> StopAreas => Set<StopArea>();
    public DbSet<TransitLine> Lines => Set<TransitLine>();
    public DbSet<VelibStation> VelibStations => Set<VelibStation>();
    public DbSet<ReferentialImport> ReferentialImports => Set<ReferentialImport>();
    public DbSet<PrimQuotaState> PrimQuota => Set<PrimQuotaState>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasPostgresExtension("postgis");

        model.Entity<StopArea>(e =>
        {
            e.Property(s => s.Location).HasColumnType("geography (point, 4326)");
            e.HasIndex(s => s.Location).HasMethod("gist");
            e.HasMany(s => s.Lines).WithMany(l => l.StopAreas).UsingEntity("StopAreaLines");
        });

        model.Entity<VelibStation>(e =>
        {
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.Location).HasColumnType("geography (point, 4326)");
            e.HasIndex(s => s.Location).HasMethod("gist");
        });

        model.Entity<ReferentialImport>().HasKey(r => r.Name);

        model.Entity<PrimQuotaState>(e =>
        {
            e.ToTable("PrimQuota");
            e.Property(q => q.Id).ValueGeneratedNever();
            e.HasData(new PrimQuotaState { Id = PrimQuotaState.SingletonId, Day = DateOnly.MinValue, Used = 0, NextSlot = DateTimeOffset.UnixEpoch });
        });
    }
}
