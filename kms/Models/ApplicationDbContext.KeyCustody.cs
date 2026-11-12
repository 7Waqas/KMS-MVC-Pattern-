using kms.Models.KeyCustody;
using Microsoft.EntityFrameworkCore;

namespace kms.Models
{
    // Key Custody Intelligence tables (see Database/001_KeyCustody.sql)
    public partial class ApplicationDbContext
    {
        public virtual DbSet<KcEvent> KcEvents { get; set; }
        public virtual DbSet<KcIncident> KcIncidents { get; set; }
        public virtual DbSet<KcIncidentSuspect> KcIncidentSuspects { get; set; }
        public virtual DbSet<KcProcessedDay> KcProcessedDays { get; set; }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<KcEvent>(e =>
            {
                e.ToTable("KcEvent");
                e.HasKey(x => x.Id);
                e.Property(x => x.ScanTime).HasColumnType("datetime2(0)");
                e.Property(x => x.ProcessedAt).HasColumnType("datetime2(0)");
                e.Property(x => x.KeyName).HasMaxLength(100);
                e.Property(x => x.Direction).HasMaxLength(3);
                e.Property(x => x.Result).HasMaxLength(20);
                e.Property(x => x.FaceName).HasMaxLength(100);
            });

            modelBuilder.Entity<KcIncident>(e =>
            {
                e.ToTable("KcIncident");
                e.HasKey(x => x.Id);
                e.Property(x => x.NaturalKey).HasMaxLength(120);
                e.Property(x => x.Type).HasMaxLength(30);
                e.Property(x => x.KeyName).HasMaxLength(100);
                e.Property(x => x.PersonName).HasMaxLength(100);
                e.Property(x => x.Summary).HasMaxLength(500);
                e.Property(x => x.AuthorizedPersons).HasMaxLength(500);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.ReviewNote).HasMaxLength(500);
                e.Property(x => x.OccurredAt).HasColumnType("datetime2(0)");
                e.Property(x => x.ReviewedAt).HasColumnType("datetime2(0)");
                e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
                e.HasMany(x => x.Suspects)
                 .WithOne()
                 .HasForeignKey(s => s.IncidentId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<KcIncidentSuspect>(e =>
            {
                e.ToTable("KcIncidentSuspect");
                e.HasKey(x => x.Id);
                e.Property(x => x.EmpName).HasMaxLength(100);
                e.Property(x => x.Reason).HasMaxLength(200);
                e.Property(x => x.SeenAt).HasColumnType("datetime2(0)");
            });

            modelBuilder.Entity<KcProcessedDay>(e =>
            {
                e.ToTable("KcProcessedDay");
                e.HasKey(x => x.Day);
                e.Property(x => x.ProcessedAt).HasColumnType("datetime2(0)");
            });
        }
    }
}
