using ExcelDash.Models;
using Microsoft.EntityFrameworkCore;

namespace ExcelDash.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Dataset> Datasets => Set<Dataset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Dataset>(entity =>
        {
            entity.Property(x => x.FileName).HasMaxLength(255);
            entity.Property(x => x.SheetName).HasMaxLength(255);
            entity.Property(x => x.HeadersJson).HasColumnType("longtext");
            entity.Property(x => x.RowsJson).HasColumnType("longtext");
        });
    }
}

