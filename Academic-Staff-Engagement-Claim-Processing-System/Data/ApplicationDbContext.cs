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
        public DbSet<MarksSubmission> MarksSubmissions => Set<MarksSubmission>();
        // ============================================================
        // CONTRACTS
        // ============================================================

        public DbSet<ContractModel> Contracts => Set<ContractModel>();
        public DbSet<ContractSignature> ContractSignatures => Set<ContractSignature>();

        // ============================================================
        // CLAIMS
        // ============================================================

        public DbSet<ClaimModel> Claims => Set<ClaimModel>();
        public DbSet<ClaimApproval> ClaimApprovals => Set<ClaimApproval>();


        // ============================================================
        // MARKS
        // ============================================================

        public DbSet<MarksSubmission> MarksSubmissions => Set<MarksSubmission>();
        
        // ============================================================
        // TEMPLATES
        // ============================================================

        public DbSet<Template> Templates => Set<Template>();
        public DbSet<MarksSubmission> MarksSubmissions { get; set; } = null!;



        // ============================================================
        // AUDIT LOG
        // ============================================================

        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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
            // MANAGEMENT
            // ========================================================

            // Distinguishes which real office this account represents
            // (HR Officer, DVCAR, Vice Chancellor) — see SignerRole for
            // why these three are modeled as one account type with a
            // Title rather than three separate TPH subclasses.
            modelBuilder.Entity<Management>()
                .Property(m => m.Title)
                .HasConversion<int>()
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
            // CONTRACT SIGNATURE
            // ========================================================

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
                    cs.SequenceOrder
                })
                .IsUnique();

            // ========================================================
            // MARKS SUBMISSION
            // ========================================================

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
                .Property(ms => ms.Semester)
                .HasConversion<int>()
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


            // --------------------------------------------------------
            // UNIQUE SUBMISSION REFERENCE
            // --------------------------------------------------------

            modelBuilder.Entity<MarksSubmission>()
                .HasIndex(ms => ms.SubmissionReference)
                .IsUnique();


            // --------------------------------------------------------
            // LECTURER RELATIONSHIP
            // --------------------------------------------------------

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.Lecturer)
                .WithMany()
                .HasForeignKey(ms => ms.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);


            // --------------------------------------------------------
            // COURSE ASSIGNMENT RELATIONSHIP
            // --------------------------------------------------------

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.CourseAssignment)
                .WithMany()
                .HasForeignKey(ms => ms.CourseAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);


            // --------------------------------------------------------
            // COURSE RELATIONSHIP
            // --------------------------------------------------------

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.Course)
                .WithMany()
                .HasForeignKey(ms => ms.CourseId)
                .OnDelete(DeleteBehavior.Restrict);


            // --------------------------------------------------------
            // MANAGEMENT REVIEWER
            // --------------------------------------------------------

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.ReviewedByManagement)
                .WithMany()
                .HasForeignKey(ms => ms.ReviewedByManagementId)
                .OnDelete(DeleteBehavior.Restrict);


            // --------------------------------------------------------
            // SEARCH INDEX
            // --------------------------------------------------------

            modelBuilder.Entity<MarksSubmission>()
                .HasIndex(ms => new
                {
                    ms.Status,
                    ms.SubmittedAtUtc
                });

            modelBuilder.Entity<MarksSubmission>()
                .HasIndex(ms => new
                {
                    ms.LecturerId,
                    ms.AcademicYear,
                    ms.Semester
                });

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
            // MARKS SUBMISSION
            // ========================================================

            modelBuilder.Entity<MarksSubmission>()
                .ToTable("MarksSubmissions");

            modelBuilder.Entity<MarksSubmission>()
                .Property(m => m.FileName)
                .HasMaxLength(255)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(m => m.FileHash)
                .HasMaxLength(128)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(m => m.FileContent)
                .HasColumnType("varbinary(max)")
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(m => m.Status)
                .HasConversion<int>()
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(m => m.SignatureHashAtReview)
                .HasMaxLength(256);

            modelBuilder.Entity<MarksSubmission>()
                .Property(m => m.DeclineReason)
                .HasMaxLength(1000);

            modelBuilder.Entity<MarksSubmission>()
                .Property(m => m.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(m => m.CourseAssignment)
                .WithMany(ca => ca.MarksSubmissions)
                .HasForeignKey(m => m.CourseAssignmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(m => m.SubmittedByLecturer)
                .WithMany()
                .HasForeignKey(m => m.SubmittedByLecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(m => m.ReviewedByAdminAccount)
                .WithMany()
                .HasForeignKey(m => m.ReviewedByAdminAccountId)
                .OnDelete(DeleteBehavior.Restrict);

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
            // ========================================================
            // MARKS SUBMISSION
            // ========================================================

            modelBuilder.Entity<MarksSubmission>()
                .ToTable("MarksSubmissions");

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.AcademicYear)
                .HasMaxLength(20)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.OriginalFileName)
                .HasMaxLength(255)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.StoredFilePath)
                .HasMaxLength(1000)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.SubmissionReference)
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .Property(ms => ms.Status)
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.Lecturer)
                .WithMany()
                .HasForeignKey(ms => ms.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MarksSubmission>()
                .HasOne(ms => ms.CourseAssignment)
                .WithMany()
                .HasForeignKey(ms => ms.CourseAssignmentId)
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
            // ========================================================
            // AUDIT LOG
            // ========================================================

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

            // Deliberately NOT a RowVersion / concurrency token — this
            // table is insert-only, rows are never updated, so there's
            // nothing to protect against concurrent overwrites.

            // Query pattern is almost always "recent activity" or
            // "activity for this entity" — index both.
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.OccurredAtUtc);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new { a.EntityType, a.EntityId });
        }
    }
}