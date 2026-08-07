using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using WildBerriesAnalyzer.Data.Properties;
using WildBerriesAnalyzer.Domain.Enums;
using WildBerriesAnalyzer.Domain.Models.DataBase;

namespace WildBerriesAnalyzer.Data
{
    public class WbDataBase : DbContext
    {
        public DbSet<WbProduct> Products { get; set; }
        public DbSet<WbPrice> PricesHistory { get; set; }
        public DbSet<WbUser> Users { get; set; }
        public DbSet<WbCategory> Categories { get; set; }
        public DbSet<WbFilter> Filters { get; set; }
        public DbSet<WbFilterCategory> CategoryFilters { get; set; }
        public DbSet<WbFilterBag> FilterBags { get; set; }
        public DbSet<PriceUpdateJob> PriceUpdateJobs { get; set; }
        public DbSet<WbActualDiscont> ActualDisconts { get; set; }
        public DbSet<VkLinkCode> VkLinkCodes { get; set; }
        public DbSet<DiscontNotification> DiscontNotifications { get; set; }

        public WbDataBase()
        {

        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
            {
                return;
            }

            //optionsBuilder.UseNpgsql(ResolveConnectionString());
            optionsBuilder.UseNpgsql("User ID=WbAdmin;Password=W3s4m1p3g3h1c8z0.;Host=62.233.35.144;Port=5432;Database=WildBerriesAnalyzerDb;");
        }

        /// <summary>
        /// Порядок: ConnectionStrings__DefaultConnection → ConnectionStrings__MyDb →
        /// WB_CONNECTION_STRING → Resources.BotLocalConnectionString (локальная разработка).
        /// </summary>
        public static string ResolveConnectionString()
        {
            return FirstNonEmpty(
                       Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
                       Environment.GetEnvironmentVariable("ConnectionStrings__MyDb"),
                       Environment.GetEnvironmentVariable("WB_CONNECTION_STRING"))
                   ?? Resources.BotLocalConnectionString;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region Users

            modelBuilder.Entity<WbUser>(entity =>
            {
                entity.Property(u => u.Login)
                      .HasMaxLength(100);

                entity.Property(u => u.Password)
                      .HasMaxLength(500);

                entity.Property(u => u.VkId)
                      .HasMaxLength(64);

                entity.Property(u => u.AccessToken)
                      .HasMaxLength(2000);

                entity.Property(u => u.RefreshToken)
                      .HasMaxLength(2000);

                entity.HasIndex(u => u.Login)
                      .IsUnique();

                entity.HasIndex(u => u.VkId)
                      .IsUnique();

                entity.HasIndex(u => u.AccessToken)
                      .IsUnique();

                entity.HasIndex(u => u.RefreshToken)
                      .IsUnique();
            });

            #endregion

            #region Products

            modelBuilder.Entity<WbProduct>()
                        .HasIndex(prod => prod.IdInMarket)
                        .IsUnique();

            modelBuilder.Entity<WbProduct>()
                        .HasOne(p => p.Category)
                        .WithMany(c => c.Products)
                        .HasForeignKey(p => p.CategoryId);

            modelBuilder.Entity<WbPrice>()
                        .HasOne(pr => pr.Product)
                        .WithMany(p => p.PricesHistory)
                        .HasForeignKey(pr => pr.ProductId);

            #endregion

            #region Filters

            modelBuilder.Entity<WbFilter>()
                        .HasOne(f => f.User)
                        .WithOne(u => u.Filter);

            modelBuilder.Entity<WbFilter>()
                        .Property(f => f.ReferencePriceStrartegies)
                        .HasConversion(v => v != null && v.Any() ? v.Select(x => x.ToString()).ToArray()
                                                                 : null,

                                       v => v != null && v.Length > 0 ? v.Select(x => (ReferencePriceStrategy)Enum.Parse(typeof(ReferencePriceStrategy), x))
                                                                         .ToList()
                                                                      : null)
                        .HasColumnType("text[]");

            modelBuilder.Entity<WbFilterCategory>()
                        .HasOne(fc => fc.Category)
                        .WithMany(c => c.FiltersCategory)
                        .HasForeignKey(fc => fc.CategoryId);

            modelBuilder.Entity<WbFilterCategory>()
                        .HasOne(fc => fc.Filter)
                        .WithMany(f => f.FilterCategories)
                        .HasForeignKey(fc => fc.FilterId);

            modelBuilder.Entity<WbFilterBag>()
                        .HasOne(fb => fb.Product)
                        .WithMany(p => p.Bags)
                        .HasForeignKey(fb => fb.ProductId);

            modelBuilder.Entity<WbFilterBag>()
                        .HasOne(fb => fb.Filter)
                        .WithMany(f => f.BagProducts)
                        .HasForeignKey(fb => fb.FilterId);

            #endregion

            #region PriceUpdateJobs

            modelBuilder.Entity<PriceUpdateJob>(entity =>
            {
                entity.ToTable("PriceUpdateJobs");

                entity.Property(j => j.Status)
                      .HasConversion<int>();

                entity.Property(j => j.LockedBy)
                      .HasMaxLength(200);

                entity.Property(j => j.LastError)
                      .HasMaxLength(2000);

                entity.HasIndex(j => new { j.Status, j.CompletedAt })
                      .HasName("IX_PriceUpdateJobs_Status_CompletedAt");

                entity.HasIndex(j => j.LockedAt)
                      .HasName("IX_PriceUpdateJobs_LockedAt");
            });

            #endregion

            #region ActualDisconts

            modelBuilder.Entity<WbActualDiscont>(entity =>
            {
                entity.ToTable("ActualDisconts");

                entity.Property(d => d.ReferencePriceStrategy)
                      .HasConversion<int>();

                entity.HasOne(d => d.Product)
                      .WithMany()
                      .HasForeignKey(d => d.ProductId);

                entity.HasOne(d => d.PriceUpdateJob)
                      .WithMany()
                      .HasForeignKey(d => d.PriceUpdateJobId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(d => new { d.ProductId, d.ReferencePriceStrategy })
                      .HasName("IX_ActualDisconts_Product_Strategy");

                entity.HasIndex(d => d.DiscontPercent)
                      .HasName("IX_ActualDisconts_DiscontPercent");
            });

            #endregion

            #region VkLinkCodes

            modelBuilder.Entity<VkLinkCode>(entity =>
            {
                entity.ToTable("VkLinkCodes");

                entity.Property(c => c.Code)
                      .HasMaxLength(16)
                      .IsRequired();

                entity.HasIndex(c => c.Code)
                      .IsUnique()
                      .HasName("IX_VkLinkCodes_Code");

                entity.HasIndex(c => c.UserId)
                      .HasName("IX_VkLinkCodes_UserId");

                entity.HasOne(c => c.User)
                      .WithMany()
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            #endregion

            #region DiscontNotifications

            modelBuilder.Entity<DiscontNotification>(entity =>
            {
                entity.ToTable("DiscontNotifications");

                entity.Property(n => n.ReferencePriceStrategy)
                      .HasConversion<int>();

                entity.HasIndex(n => new { n.UserId, n.ProductId, n.ReferencePriceStrategy })
                      .IsUnique()
                      .HasName("IX_DiscontNotifications_User_Product_Strategy");

                entity.HasOne(n => n.User)
                      .WithMany()
                      .HasForeignKey(n => n.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.Product)
                      .WithMany()
                      .HasForeignKey(n => n.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            #endregion
        }
    }
}
