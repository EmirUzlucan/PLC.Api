using Microsoft.EntityFrameworkCore;
using PLC.Api.Models;

namespace PLC.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Plc> Plcs => Set<Plc>();

        public DbSet<DataBlock> DataBlocks => Set<DataBlock>();

        public DbSet<DataBlockVariable> DataBlockVariables
            => Set<DataBlockVariable>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Plc>()
                .HasMany(p => p.DataBlocks)
                .WithOne(db => db.Plc)
                .HasForeignKey(db => db.PlcId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DataBlock>()
                .HasIndex(db => new
                {
                    db.PlcId,
                    db.DbNumber
                })
                .IsUnique();

            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(AppDbContext).Assembly);
        }
    }
}