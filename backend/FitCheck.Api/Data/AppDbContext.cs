using FitCheck.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FitCheck.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Analysis> Analyses => Set<Analysis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<Analysis>(e =>
        {
            e.ToTable("analyses");
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).HasMaxLength(200).IsRequired();
            e.Property(a => a.Company).HasMaxLength(200);
            e.Property(a => a.FileName).HasMaxLength(255).IsRequired();
            e.Property(a => a.CvText).IsRequired();
            e.Property(a => a.JobDescription).IsRequired();
            e.Property(a => a.Summary).IsRequired();
            e.Property(a => a.DetailsJson).HasColumnType("jsonb").IsRequired();
            e.Property(a => a.Model).HasMaxLength(100);
            e.HasOne(a => a.User)
                .WithMany(u => u.Analyses)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(a => new { a.UserId, a.CreatedAt });
        });
    }
}

/// <summary>Used by `dotnet ef` so migrations can be created without real secrets configured.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=fitcheck;Username=postgres;Password=postgres")
            .Options);
}
