namespace Neolution.AzureSqlFederatedIdentity.Sample.Database
{
    using Microsoft.EntityFrameworkCore;
    using Neolution.AzureSqlFederatedIdentity.Sample.Entities;

    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> TestTable { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestEntity>(entity =>
            {
                entity.ToTable("TestTable");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Value).HasColumnName("Value");
            });
        }
    }
}
