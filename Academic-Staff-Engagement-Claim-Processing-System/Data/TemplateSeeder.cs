using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data
{
    public static class TemplateSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext db)
        {
            // Do not create another template if one already exists.
            if (await db.Templates.AnyAsync())
                return;

            var contractTemplate = """
<div class="unilak-contract">

    <!-- ============================================================
         UNILAK LETTERHEAD
         ============================================================ -->

    <div class="contract-header">

        <div class="header-main">
            <div class="header-logo">
                <img src="/images/PNG_LOGO-_UNILAK-removebg-preview.png"
                     alt="UNILAK Logo" />
            </div>

            <div class="header-university">
                <div class="university-name">
                    UNIVERSITY OF LAY ADVENTISTS OF KIGALI
                </div>
            </div>
        </div>

        <div class="header-contact">
            <div>
                <span>P.O. Box 6392 Kigali, Rwanda</span>
                <span>Website: www.unilak.ac.rw</span>
            </div>

            <div>
                <span>Phone: +250(0)731743439 / +250(0)751743431</span>
                <span>E-mail: info@unilak.ac.rw</span>
            </div>
        </div>

        <div class="header-line"></div>

    </div>


    <!-- ============================================================
         CONTRACT DATE
         ============================================================ -->

    <p class="contract-date">
        Kigali, {{ContractDate}}
    </p>


    <!-- ============================================================
         CONTRACT TITLE
         ============================================================ -->

    <h2 class="contract-title">
        EMPLOYMENT PART-TIME CONTRACT
    </h2>


    <!-- ============================================================
         INTRODUCTION
         ============================================================ -->

    <p>
        Between the undersigned:
    </p>

    <p>
        University of Lay Adventists of Kigali (UNILAK), represented by
        Vice Chancellor <strong>Prof. Jean NGAMIJE</strong> on one hand,
    </p>

    <p>
        And the Employee,
        <strong>{{LecturerName}}</strong>,
        having the Academic rank of
        <strong>{{AcademicRank}}</strong>,
        with Identity Card/Passport No:
        <strong>{{GovernmentId}}</strong>,
        on the other hand;
    </p>

    <p>
        The following has been agreed:
    </p>


    <!-- ============================================================
         ARTICLE 1
         ============================================================ -->

    <p>
        <strong>Article 1:</strong>
        UNILAK employs
        <strong>{{LecturerName}}</strong>
        as an
        <strong>{{EmploymentType}}</strong>
        part-time lecturer in the Faculty of Computing and Information
        Sciences, Department of
        <strong>{{Department}}</strong>,
        Intake <strong>{{Intake}}</strong>,
        <strong>{{Session}}</strong>,
        to teach the course of
        <strong>{{CourseTitle}}</strong>,
        Academic year <strong>{{AcademicYear}}</strong>,
        Semester <strong>{{Semester}}</strong>,
        <strong>{{Campus}}</strong> Campus.
    </p>


    <!-- ============================================================
         ARTICLE 2
         ============================================================ -->

    <p>
        <strong>Article 2:</strong>
        The number of contact hours allocated to the course/module,
        if the course is taught through face-to-face mode,
        is <strong>{{AllocatedHours}}</strong> hours.
        This includes the theory, practical sessions as well as
        examinations.
    </p>

    <p>
        The rate per hour will be
        <strong>{{HourlyRate}}</strong>
        (gross).
    </p>


    <!-- ============================================================
         ARTICLE 3
         ============================================================ -->

    <p>
        <strong>Article 3:</strong>
        The number of classes combined if the module/course is taught
        through online teaching mode is
        <strong>{{NumberOfOnlineClasses}}</strong>
        classes, and the total number of hours allocated to those
        combined classes taught by one academic staff member is
        <strong>{{OnlineHours}}</strong> hours.
    </p>


    <!-- ============================================================
         ARTICLE 4
         ============================================================ -->

    <p>
        <strong>Article 4:</strong>
        The employee is required to hand into the Deputy Vice Chancellor
        for Academic and Research office his/her application letter,
        CV, notarized copy of the degree, equivalence if the degree is
        obtained from a foreign country, as well as his/her nomination
        papers for his/her previous academic rank.
    </p>


    <!-- ============================================================
         ARTICLE 5
         ============================================================ -->

    <p>
        <strong>Article 5:</strong>
        The Lecturer is required to submit to the Head of the Department
        the following documents:
    </p>

    <ul>
        <li>
            Course materials such as handouts, syllabuses and other
            supporting documents must be uploaded to the UNILAK online
            teaching platform and submitted to the Head of Department's
            office before starting the class.
        </li>

        <li>
            Final examination and marking scheme.
        </li>

        <li>
            Continuous assessment papers including assignments, quizzes
            and tests.
        </li>
    </ul>


    <!-- ============================================================
         ARTICLE 6
         ============================================================ -->

    <p>
        <strong>Article 6:</strong>
        The sheet of marks properly recorded should be submitted within
        fifteen days dating from the time of examination. In case of
        urgency, the institution is entitled to shorten this deadline.
    </p>


    <!-- ============================================================
         ARTICLE 7
         ============================================================ -->

    <p>
        <strong>Article 7:</strong>
        Any teaching staff member is evaluated at the end of the course
        and at the end of the academic year by the hierarchy based on:
    </p>

    <ul>
        <li>
            <strong>Scientific competence:</strong>
            handling of the course contents, scientific articles and
            paper publishing.
        </li>

        <li>
            <strong>Pedagogic competence:</strong>
            methodology, techniques and strategies applied in
            transmitting efficiently the course contents.
        </li>

        <li>
            <strong>Moral aptitudes:</strong>
            punctuality, objectivity, sense of responsibility,
            commitment to students' education, etc.
        </li>
    </ul>

    <p>
        In order to maintain or keep his/her course, a teacher must
        obtain at least <strong>70%</strong> of the marks in the
        evaluation conducted by the hierarchy.
    </p>


    <!-- ============================================================
         ARTICLE 8
         ============================================================ -->

    <p>
        <strong>Article 8:</strong>
        An uninformed absence, or a late-informed absence, brings
        prejudice to the students in many regards, disturbs the
        functioning of teaching activities and seriously spoils the
        reputation of the institution. Such conduct cannot be tolerated.
    </p>


    <!-- ============================================================
         ARTICLE 9
         ============================================================ -->

    <p>
        <strong>Article 9:</strong>
        The wage of the part-time employee will be set in accordance
        with his/her academic rank.
    </p>


    <!-- ============================================================
         ARTICLE 10
         ============================================================ -->

    <p>
        <strong>Article 10:</strong>
        Each party may terminate the appointment by giving to the other
        party 15 days' notice in writing.
    </p>

    <p>
        However, the University reserves the right to cancel the present
        contract without prior notice in case the employee is found to be
        inefficient, immoral, or absent without informing the HOD.
    </p>


    <!-- ============================================================
         SIGNATURES
         ============================================================ -->

    <div class="signature-section">

        <h3>SIGNATURES</h3>

        <table class="signature-table">

            <tr>
                <td class="signature-role">
                    <strong>Lecturer</strong>
                </td>

                <td>
                    Name:
                    <strong>{{LecturerName}}</strong>
                </td>

                <td>
                    Signature:
                    {{LecturerSignature}}
                </td>

                <td>
                    Date:
                    {{LecturerSignatureDate}}
                </td>
            </tr>


            <tr>
                <td class="signature-role">
                    <strong>Dean of Faculty</strong>
                </td>

                <td>
                    Name:
                    Prof. NYESHEJA M. Enan
                </td>

                <td>
                    Signature:
                    {{DeanSignature}}
                </td>

                <td>
                    Date:
                    {{DeanSignatureDate}}
                </td>
            </tr>


            <tr>
                <td class="signature-role">
                    <strong>Human Resource Officer</strong>
                </td>

                <td>
                    Name:
                    Mr. NTAKIRUTIMANA Elison
                </td>

                <td>
                    Signature:
                    {{HRSignature}}
                </td>

                <td>
                    Date:
                    {{HRSignatureDate}}
                </td>
            </tr>


            <tr>
                <td class="signature-role">
                    <strong>DVCAR</strong>
                </td>

                <td>
                    Name:
                    Prof. HAKIZIMANA Emmanuel
                </td>

                <td>
                    Signature:
                    {{DVCARSignature}}
                </td>

                <td>
                    Date:
                    {{DVCARSignatureDate}}
                </td>
            </tr>


            <tr>
                <td class="signature-role">
                    <strong>Vice Chancellor</strong>
                </td>

                <td>
                    Name:
                    Prof. NGAMIJE Jean
                </td>

                <td>
                    Signature:
                    {{VCSignature}}
                </td>

                <td>
                    Date:
                    {{VCSignatureDate}}
                </td>
            </tr>

        </table>

    </div>

</div>
""";

            var template = new Template
            {
                Contract = contractTemplate,
                Claim = string.Empty,
                Letter = string.Empty
            };

            db.Templates.Add(template);

            await db.SaveChangesAsync();
        }
    }
}