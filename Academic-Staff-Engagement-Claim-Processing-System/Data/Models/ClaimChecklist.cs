using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data.Models
{
    public class ClaimChecklist
    {
        [Key]
        public int Id { get; private set; }

        [Required]
        [ForeignKey(nameof(Claim))]
        public int ClaimId { get; set; }
        public Claim Claim { get; set; } = null!;

        public bool NotesUploadedToELearning { get; set; }
        public bool IndividualGroupWorkOnELearning { get; set; }
        public bool MarksAvailableInMIS { get; set; }
        public bool MarksApprovedByHOD { get; set; }
        public bool HardCopySubmittedToHodAndRegistrar { get; set; }
        public bool ExamAndMarkingSchemeAvailable { get; set; }
        public bool ClassAttendanceListAvailable { get; set; }
        public bool ExamScriptsReturned { get; set; }

        [Required]
        [ForeignKey(nameof(ConfirmedByHod))]
        public int ConfirmedByHodId { get; set; }
        public Hod ConfirmedByHod { get; set; } = null!;

        public DateTime ConfirmedAtUtc { get; private set; } = DateTime.UtcNow;

        public ClaimChecklist(int id, int claimId, int confirmedByHodId)
        {
            Id = id;
            ClaimId = claimId;
            ConfirmedByHodId = confirmedByHodId;
        }
    }
}