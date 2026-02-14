using Microsoft.EntityFrameworkCore;

namespace kms.Models
{
    public partial class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<EmployeeMaster> EmployeeMasters { get; set; }
        public virtual DbSet<KeyAuthorization> KeyAuthorizations { get; set; }
        public virtual DbSet<KeyMaster> KeyMasters { get; set; }
        public virtual DbSet<KeyReportData> KeyReportData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseCollation("Chinese_PRC_CI_AS");

            modelBuilder.Entity<EmployeeMaster>(entity =>
            {
                entity.HasKey(e => e.EmpId);
                entity.ToTable("EmployeeMaster");
                entity.Property(e => e.EmpId).HasColumnName("EmpID");
                entity.Property(e => e.Department).HasMaxLength(100);
                entity.Property(e => e.FullName).HasMaxLength(100);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<KeyAuthorization>(entity =>
            {
                entity.HasKey(e => e.AuthId);
                entity.ToTable("KeyAuthorization");
                entity.Property(e => e.AuthId).HasColumnName("AuthID");
                entity.Property(e => e.AssignedDate).HasDefaultValueSql("(getdate())");
            });

            modelBuilder.Entity<KeyMaster>(entity =>
            {
                entity.HasKey(e => e.KeyId);
                entity.ToTable("KeyMaster");
                entity.Property(e => e.KeyId).HasColumnName("KeyID");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.KeyLocation).HasMaxLength(100);
                entity.Property(e => e.KeyName).HasMaxLength(100);
            });

            modelBuilder.Entity<KeyReportData>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("ID");
                entity.Property(e => e.AlertStatus).HasMaxLength(50);
                entity.Property(e => e.AuthStatus).HasMaxLength(20);
                entity.Property(e => e.AuthorizedPersons).HasMaxLength(500);
                entity.Property(e => e.Direction).HasMaxLength(20);
                entity.Property(e => e.Employee).HasMaxLength(100);
                entity.Property(e => e.KeyName).HasMaxLength(100);
                entity.Property(e => e.ScanTime).HasMaxLength(10);
                entity.Property(e => e.Status).HasMaxLength(50);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}