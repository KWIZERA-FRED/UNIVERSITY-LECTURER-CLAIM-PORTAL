using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Enums;
using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.EntityFrameworkCore;

using ClaimModel =
Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Claim;

using ContractModel =
Academic_Staff_Engagement_Claim_Processing_System.Data.Models.Contract;

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

        public DbSet<Lecturer> Lecturers => Set<Lecturer>();

        public DbSet<AdminAccount> AdminAccounts => Set<AdminAccount>();
        public DbSet<Hod> Hods => Set<Hod>();
        public DbSet<Dean> Deans => Set<Dean>();
        public DbSet<Management> ManagementAccounts => Set<Management>();

        public DbSet<Course> Courses => Set<Course>();
        public DbSet<CourseAssignment> CourseAssignments => Set<CourseAssignment>();
        public DbSet<MarksSubmission> MarksSubmissions => Set<MarksSubmission>();

        public DbSet<ContractModel> Contracts => Set<ContractModel>();
        public DbSet<ContractSignature> ContractSignatures => Set<ContractSignature>();

        public DbSet<ClaimModel> Claims => Set<ClaimModel>();
        public DbSet<ClaimApproval> ClaimApprovals => Set<ClaimApproval>();
        public DbSet<ClaimAttendance> ClaimAttendances => Set<ClaimAttendance>();
        public DbSet<ClaimAttendanceRecord> ClaimAttendanceRecords => Set<ClaimAttendanceRecord>();

        public DbSet<Template> Templates => Set<Template>();

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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

            modelBuilder.Entity<AdminAccount>()
                .Ignore(a => a.Role);

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

            modelBuilder.Entity<Hod>()
                .Property(h => h.Department)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Management>()
                .Property(m => m.Title)
                .HasConversion<int>()
                .IsRequired();

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
                    plainOrCipher =>
                        _governmentIdProtector.Encrypt(plainOrCipher),

                    cipher =>
                        _governmentIdProtector.Decrypt(cipher))
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

            modelBuilder.Entity<Lecturer>()
                .HasOne(l => l.SignatureCapturedByHod)
                .WithMany()
                .HasForeignKey(l => l.SignatureCapturedByHodId)
                .OnDelete(DeleteBehavior.Restrict);

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

            modelBuilder.Entity<ContractModel>()
                .ToTable("Contracts");

            modelBuilder.Entity<ContractModel>()
                .Property(c => c.Version)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<ContractModel>()
                .Property(c => c.RatePerHour)
                .HasPrecision(10, 2);

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

            modelBuilder.Entity<ContractSignature>()
                .ToTable("ContractSignatures");

            modelBuilder.Entity<ContractSignature>()
                .Property(cs => cs.SignerRole)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<ContractSignature>()
                .Property(cs => cs.Decision)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<ContractSignature>()
                .Property(cs => cs.SignatureHash)
                .HasMaxLength(256);

            modelBuilder.Entity<ContractSignature>()
                .Property(cs => cs.SignatureFilePath)
                .HasMaxLength(500);

            modelBuilder.Entity<ContractSignature>()
                .Property(cs => cs.Comments)
                .HasColumnType("nvarchar(max)");

            modelBuilder.Entity<ContractSignature>()
                .HasOne(cs => cs.Contract)
                .WithMany()
                .HasForeignKey(cs => cs.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContractSignature>()
                .HasOne(cs => cs.SignedByLecturer)
                .WithMany()
                .HasForeignKey(cs => cs.SignedByLecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContractSignature>()
                .HasOne(cs => cs.SignedByAdminAccount)
                .WithMany()
                .HasForeignKey(cs => cs.SignedByAdminAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContractSignature>()
                .HasIndex(cs => new
                {
                    cs.ContractId,
                    cs.SignerRole
                })
                .IsUnique();

            modelBuilder.Entity<ClaimModel>()
                .Property(c => c.HoursClaimed)
                .HasPrecision(6, 2);

            modelBuilder.Entity<MarksSubmission>()
                .ToTable("MarksSubmissions");

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.SubmissionReference)
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.AcademicYear)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.FileName)
                .HasMaxLength(255)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.FilePath)
                .HasMaxLength(500)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.FileHash)
                .HasMaxLength(64)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.ContentType)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.Status)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.ReviewComment)
                .HasMaxLength(1000);

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.Lecturer)
                .WithMany()
                .HasForeignKey(ms => ms.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.CourseAssignment)
                .WithMany(ca => ca.MarksSubmissions)
                .HasForeignKey(ms => ms.CourseAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.Course)
                .WithMany()
                .HasForeignKey(ms => ms.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.ReviewedByManagement)
                .WithMany()
                .HasForeignKey(ms => ms.ReviewedByManagementId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarksSubmission>()
                .HasIndex(ms => ms.SubmissionReference)
                .IsUnique();

            modelBuilder.Entity<MarksSubmission>()
                .HasIndex(ms => new
                {
                    ms.LecturerId,
                    ms.CourseAssignmentId,
                    ms.AcademicYear
                });

            modelBuilder.Entity<AuditLog>()
                .ToTable("AuditLogs");

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Action)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.ActorUsername)
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.ActorRole)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.EntityType)
                .HasMaxLength(50);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Details)
                .HasMaxLength(500);

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.IpAddress)
                .HasMaxLength(45);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.OccurredAtUtc);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new
                {
                    a.EntityType,
                    a.EntityId
                });
            modelBuilder.Entity<ClaimAttendance>()
            .ToTable("ClaimAttendances");

            modelBuilder.Entity<ClaimAttendance>()
            .HasIndex(ca => ca.ClaimId)
            .IsUnique();

            modelBuilder.Entity<ClaimAttendanceRecord>()
            .ToTable("ClaimAttendanceRecord");

            modelBuilder.Entity<ClaimAttendanceRecord>()
            .HasOne(car => car.ClaimAttendance)
            .WithMany(ca => ca.Records)
            .HasForeignKey(car => car.ClaimAttendanceId)
            .OnDelete(DeleteBehavior.Cascade);

        }
    }


}
