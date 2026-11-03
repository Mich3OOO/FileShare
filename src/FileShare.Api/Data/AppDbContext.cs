using FileShare.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FileShare.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<AccessCode> AccessCodes => Set<AccessCode>();
    public DbSet<FileItem> Files => Set<FileItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(64);
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
        });

        modelBuilder.Entity<AccessCode>(entity =>
        {
            entity.ToTable("access_codes");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(32);
            entity.Property(e => e.Note).HasMaxLength(200);
        });

        modelBuilder.Entity<FileItem>(entity =>
        {
            entity.ToTable("files");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.OriginalName).HasMaxLength(512);
            entity.Property(e => e.StoragePath).HasMaxLength(1024);
            entity.Property(e => e.Token).HasMaxLength(64);
            entity.HasOne(e => e.AccessCode)
                .WithMany()
                .HasForeignKey(e => e.AccessCodeId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}