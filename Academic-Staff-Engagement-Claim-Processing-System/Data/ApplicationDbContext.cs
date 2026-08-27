using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.EntityFrameworkCore;

using ClaimModel = Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Claim;
using ContractModel = Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Contract;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly GovernmentIdProtector _governmentIdProtector;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            GovernmentIdProtector governmentIdProtector)
            : base(options)
        {
            _governmentIdProtector = governmentIdProtector;
        }

        // ============================================================
        // LECTURER
        // ============================================================

        public DbSet<Lecturer> Lecturers => Set<Lecturer>();

        // ============================================================
        // ADMIN ACCOUNTS
        // ============================================================

        public DbSet<AdminAccount> AdminAccounts => Set<AdminAccount>();
        public DbSet<Hod> Hods => Set<Hod>();
        public DbSet<Dean> Deans => Set<Dean>();
        public DbSet<Management> ManagementAccounts => Set<Management>();

        // ============================================================
        // ACADEMIC
        // ============================================================

        public DbSet<Course> Courses => Set<Course>();
        public DbSet<CourseAssignment> CourseAssignments => Set<CourseAssignment>();

        // ============================================================
        // CONTRACTS
        // ============================================================

        public DbSet<ContractModel> Contracts => Set<ContractModel>();

        // ============================================================
        // CLAIMS
        // ============================================================

        public DbSet<ClaimModel> Claims => Set<ClaimModel>();
        public DbSet<ClaimApproval> ClaimApprovals => Set<ClaimApproval>();

        // ============================================================
        // TEMPLATES
        // ============================================================

        public DbSet<Template> Templates => Set<Template>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ========================================================
            // ADMIN ACCOUNT - TPH INHERITANCE
            // ========================================================

            modelBuilder.Entity<AdminAccount>()
                .ToTable("AdminAccounts");

            modelBuilder.Entity<Hod>()
                .HasBaseType<AdminAccount>();

            modelBuilder.Entity<Dean>()
                .HasBaseType<AdminAccount>();

            modelBuilder.Entity<Management>()
                .HasBaseType<AdminAccount>();

            modelBuilder.Entity<AdminAccount>()
                .HasDiscriminator<ApprovalRole>("AccountType")
                .HasValue<Hod>(ApprovalRole.HOD)
                .HasValue<Dean>(ApprovalRole.Dean)
                .HasValue<Management>(ApprovalRole.Management);

            // Do not create a separate database column for the
            // abstract Role property. The discriminator represents it.
            modelBuilder.Entity<AdminAccount>()
                .Ignore(a => a.Role);

            // ========================================================
            // ADMIN ACCOUNT
            // ========================================================

            modelBuilder.Entity<AdminAccount>()
                .Property(a => a.UserName)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<AdminAccount>()
                .Property(a => a.Email)
                .HasMaxLength(150)
                .IsRequired();

            modelBuilder.Entity<AdminAccount>()
                .Property(a => a.PasswordHash)
                .IsRequired();

            modelBuilder.Entity<AdminAccount>()
                .Property(a => a.SignatureFilePath)
                .HasMaxLength(500);

            modelBuilder.Entity<AdminAccount>()
                .Property(a => a.SignatureFileHash)
                .HasMaxLength(256);

            modelBuilder.Entity<AdminAccount>()
                .Property(a => a.SignatureStatus)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<AdminAccount>()
                .Property(a => a.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<AdminAccount>()
                .HasIndex(a => a.UserName)
                .IsUnique();

            modelBuilder.Entity<AdminAccount>()
                .HasIndex(a => a.Email)
                .IsUnique();

            // ========================================================
            // HOD
            // ========================================================

            modelBuilder.Entity<Hod>()
                .Property(h => h.Department)
                .HasMaxLength(100)
                .IsRequired();

            // ========================================================
            // LECTURER
            // ========================================================

            modelBuilder.Entity<Lecturer>()
                .ToTable("Lecturers");

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.UserName)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.Email)
                .HasMaxLength(150)
                .IsRequired();

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.PhoneNumber)
                .HasMaxLength(20);

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.PasswordHash)
                .IsRequired();

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.GovernmentIdEncrypted)
                .HasConversion(
                    plainOrCipher => _governmentIdProtector.Encrypt(plainOrCipher),
                    cipher => _governmentIdProtector.Decrypt(cipher))
                .IsRequired();

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.SignatureFilePath)
                .HasMaxLength(500);

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.SignatureFileHash)
                .HasMaxLength(256);

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.Type)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.Rank)
                .HasConversion<int?>();

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.SignatureStatus)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<Lecturer>()
                .Property(l => l.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<Lecturer>()
                .HasIndex(l => l.UserName)
                .IsUnique();

            modelBuilder.Entity<Lecturer>()
                .HasIndex(l => l.Email)
                .IsUnique();

            // Lecturer signature captured by HOD
            modelBuilder.Entity<Lecturer>()
                .HasOne(l => l.SignatureCapturedByHod)
                .WithMany()
                .HasForeignKey(l => l.SignatureCapturedByHodId)
                .OnDelete(DeleteBehavior.Restrict);

            // ========================================================
            // COURSE
            // ========================================================

            modelBuilder.Entity<Course>()
                .ToTable("Courses");

            modelBuilder.Entity<Course>()
                .Property(c => c.Code)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<Course>()
                .Property(c => c.Title)
                .HasMaxLength(200)
                .IsRequired();

            modelBuilder.Entity<Course>()
                .Property(c => c.Department)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Course>()
                .Property(c => c.CreditHours)
                .HasPrecision(5, 2);

            modelBuilder.Entity<Course>()
                .Property(c => c.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<Course>()
                .HasIndex(c => c.Code)
                .IsUnique();

            // ========================================================
            // COURSE ASSIGNMENT
            // ========================================================

            modelBuilder.Entity<CourseAssignment>()
                .ToTable("CourseAssignments");

            modelBuilder.Entity<CourseAssignment>()
                .Property(ca => ca.AcademicYear)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<CourseAssignment>()
                .Property(ca => ca.Semester)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<CourseAssignment>()
                .Property(ca => ca.Session)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<CourseAssignment>()
                .Property(ca => ca.Campus)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<CourseAssignment>()
                .Property(ca => ca.AllocatedHours)
                .HasPrecision(6, 2);

            modelBuilder.Entity<CourseAssignment>()
                .Property(ca => ca.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<CourseAssignment>()
                .HasOne(ca => ca.Lecturer)
                .WithMany(l => l.CourseAssignments)
                .HasForeignKey(ca => ca.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CourseAssignment>()
                .HasOne(ca => ca.Course)
                .WithMany(c => c.CourseAssignments)
                .HasForeignKey(ca => ca.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CourseAssignment>()
                .HasOne(ca => ca.ApprovedByHod)
                .WithMany()
                .HasForeignKey(ca => ca.ApprovedByHodId)
                .OnDelete(DeleteBehavior.Restrict);

            // ========================================================
            // CONTRACT
            // ========================================================

            modelBuilder.Entity<ContractModel>()
                .ToTable("Contracts");

            modelBuilder.Entity<ContractModel>()
                .Property(c => c.Version)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<ContractModel>()
                .Property(c => c.Content)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            modelBuilder.Entity<ContractModel>()
                .Property(c => c.Status)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<ContractModel>()
                .Property(c => c.SignatureHashAtSigning)
                .HasMaxLength(256);

            modelBuilder.Entity<ContractModel>()
                .Property(c => c.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<ContractModel>()
                .HasOne(c => c.Lecturer)
                .WithMany(l => l.Contracts)
                .HasForeignKey(c => c.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContractModel>()
                .HasOne(c => c.CourseAssignment)
                .WithMany()
                .HasForeignKey(c => c.CourseAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ========================================================
            // CLAIM
            // ========================================================

            modelBuilder.Entity<ClaimModel>()
                .ToTable("Claims");

            modelBuilder.Entity<ClaimModel>()
                .Property(c => c.HoursClaimed)
                .HasPrecision(6, 2);

            modelBuilder.Entity<ClaimModel>()
                .Property(c => c.Status)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<ClaimModel>()
                .Property(c => c.QrCodeToken)
                .HasMaxLength(64)
                .IsRequired();

            modelBuilder.Entity<ClaimModel>()
                .Property(c => c.Description)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            modelBuilder.Entity<ClaimModel>()
                .Property(c => c.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<ClaimModel>()
                .HasOne(c => c.CourseAssignment)
                .WithMany(ca => ca.Claims)
                .HasForeignKey(c => c.CourseAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClaimModel>()
                .HasOne(c => c.Contract)
                .WithMany()
                .HasForeignKey(c => c.ContractId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClaimModel>()
                .HasIndex(c => c.QrCodeToken)
                .IsUnique();

            // ========================================================
            // CLAIM APPROVAL
            // ========================================================

            modelBuilder.Entity<ClaimApproval>()
                .ToTable("ClaimApprovals");

            modelBuilder.Entity<ClaimApproval>()
                .Property(ca => ca.ApprovalRole)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<ClaimApproval>()
                .Property(ca => ca.Decision)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<ClaimApproval>()
                .Property(ca => ca.SignatureHashAtApproval)
                .HasMaxLength(256);

            modelBuilder.Entity<ClaimApproval>()
                .Property(ca => ca.Comments)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<ClaimApproval>()
                .HasOne(ca => ca.Claim)
                .WithMany(c => c.Approvals)
                .HasForeignKey(ca => ca.ClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ClaimApproval>()
                .HasOne(ca => ca.ApprovedByAdminAccount)
                .WithMany()
                .HasForeignKey(ca => ca.ApprovedByAdminAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ClaimApproval>()
                .HasIndex(ca => new
                {
                    ca.ClaimId,
                    ca.SequenceOrder
                })
                .IsUnique();

            // ========================================================
            // TEMPLATE
            // ========================================================

            modelBuilder.Entity<Template>()
                .ToTable("Templates");

            modelBuilder.Entity<Template>()
                .Property(t => t.Contract)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            modelBuilder.Entity<Template>()
                .Property(t => t.Claim)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            modelBuilder.Entity<Template>()
                .Property(t => t.Letter)
                .HasColumnType("nvarchar(max)")
                .IsRequired();
        }
    }
}