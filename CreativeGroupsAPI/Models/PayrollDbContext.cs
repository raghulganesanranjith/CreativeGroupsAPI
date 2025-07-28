using Microsoft.EntityFrameworkCore;

namespace CreativeGroupsAPI.Models
{
    public class PayrollDbContext : DbContext
            
    {
        public PayrollDbContext(DbContextOptions<PayrollDbContext> options) : base(options) { }

        public DbSet<Company> Company { get; set; }
        // public DbSet<Employee> Employees { get; set; }
        public DbSet<Employee> Employee { get; set; }
        public DbSet<Payroll> Payroll { get; set; }
        public DbSet<PayrollMonth> PayrollMonth { get; set; }
        public DbSet<PayrollEntry> PayrollEntry { get; set; }
        public DbSet<PayrollUploadStaging> PayrollUploadStaging { get; set; }
        
        // User Management
        public DbSet<User> User { get; set; }
        public DbSet<Organization> Organization { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Organization relationships
            modelBuilder.Entity<User>()
                .HasOne(u => u.Organization)
                .WithMany(o => o.Users)
                .HasForeignKey(u => u.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Company>()
                .HasOne(c => c.Organization)
                .WithMany(o => o.Companies)
                .HasForeignKey(c => c.OrganizationId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Existing relationships
            modelBuilder.Entity<PayrollMonth>()
                .HasOne(pm => pm.Company)
                .WithMany()
                .HasForeignKey(pm => pm.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Payroll>()
                .HasOne(pm => pm.PayrollMonth)
                .WithMany()
                .HasForeignKey(pm => pm.PayrollMonthid)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PayrollEntry>()
                .HasOne(pe => pe.Employee)
                .WithMany()
                .HasForeignKey(pe => pe.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PayrollEntry>()
                .HasOne(pe => pe.Company)
                .WithMany()
                .HasForeignKey(pe => pe.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PayrollEntry>()
                .HasOne(pe => pe.PayrollMonth)
                .WithMany()
                .HasForeignKey(pe => pe.PayrollMonthId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PayrollUploadStaging>()
                .HasOne(ps => ps.Company)
                .WithMany()
                .HasForeignKey(ps => ps.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PayrollUploadStaging>()
                .HasOne(ps => ps.PayrollMonth)
                .WithMany()
                .HasForeignKey(ps => ps.PayrollMonthId)
                .OnDelete(DeleteBehavior.NoAction);
        }

    }
}
