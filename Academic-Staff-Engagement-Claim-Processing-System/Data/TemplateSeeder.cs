using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data
{
    public static class TemplateSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext db)
        {
            var contractTemplate = """
<div class="official-contract">

    <!-- ============================================================
         UNILAK LETTERHEAD
         ============================================================ -->

    <div class="contract-header">

        <img src="/images/PNG_LOGO-_UNILAK-removebg-preview.png"
             alt="UNILAK Logo"
             class="contract-logo" />

        <div class="contract-university-name">
            UNIVERSITY OF LAY ADVENTISTS OF KIGALI
        </div>

        <div class="contract-address">
            PO Box 6392 Kigali, Rwanda
        </div>

        <div class="contract-contact">
            Phone: +250(0)731743439 / +250(0)751743431
        </div>

        <div class="contract-web">
            Website: www.unilak.ac.rw<br />
            E-mail: info@unilak.ac.rw
        </div>

        <div class="contract-header-line"></div>

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

    <div class="contract-title-section">
        <h1>EMPLOYMENT PART-TIME CONTRACT</h1>
    </div>


    <!-- ============================================================
         INTRODUCTION
         ============================================================ -->

    <p>
        Between the undersigned:
    </p>

    <p>
        University of Lay Adventists of Kigali (UNILAK) represented by Vice
        Chancellor <strong>Prof. Jean NGAMIJE</strong> on one hand,
    </p>

    <p>
        And the Employee, <strong>{{LecturerName}}</strong>, having the
        Academic rank of <strong>{{AcademicRank}}</strong> with identity
        card/Passport No: <strong>{{GovernmentId}}</strong>, on the other
        hand;
    </p>

    <p>
        The following has been agreed:
    </p>


    <!-- ============================================================
         ARTICLE 1
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 1</h2>
        <p>
            UNILAK employs <strong>{{LecturerName}}</strong> as
            <strong>{{EmploymentType}}</strong> part time lecturer in the
            faculty of Computing and Information Sciences, Department of
            {{DepartmentOptionsList}}, Intake <strong>{{Intake}}</strong>,
            Session {{SessionOptionsList}}, to teach the course of
            <strong>{{CourseTitle}}</strong>, Academic year
            <strong>{{AcademicYear}}</strong>, semester
            <strong>{{Semester}}</strong>, {{CampusOptionsList}} Campus.
        </p>
    </div>


    <!-- ============================================================
         ARTICLE 2
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 2</h2>
        <p>
            The number of contact hours allocated to the course/module if
            the course is taught through face-to-face mode is
            {{ContactHoursOptionsList}} hours, and this includes the
            theory, practical as well as examinations. The rate per hour
            will be {{RateOptionsList}} (gross).
        </p>
    </div>


    <!-- ============================================================
         ARTICLE 3
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 3</h2>
        <p>
            The number of classes combined if the module/course is taught
            through online teaching mode: <strong>{{NumberOfOnlineClasses}}</strong>,
            and the total number of hours allocated to those combined
            classes taught by one academic staff:
            <strong>{{OnlineHours}}</strong>.
        </p>
    </div>


    <!-- ============================================================
         ARTICLE 4
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 4</h2>
        <p>
            The employee is required to hand into the Deputy Vice
            Chancellor for Academic and Research office his/her application
            letter, CV, notarized copy of the degree, Equivalence if the
            degree is offered from foreign countries, as well as his/her
            nomination papers for his previous academic rank.
        </p>
    </div>


    <!-- ============================================================
         ARTICLE 5
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 5</h2>
        <p>
            The Lecturer is required to submit to the Head of the
            Department the following documents:
        </p>

        <ul>
            <li>
                Course materials such as Handout/syllabuses and other
                supporting documents must be uploaded to UNILAK online
                teaching platform and submitted to the Head of Department
                office before starting the class,
            </li>

            <li>
                Final exam and marking scheme,
            </li>

            <li>
                Continuous assessment papers: assignments/quiz/test.
            </li>
        </ul>
    </div>


    <!-- ============================================================
         ARTICLE 6
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 6</h2>
        <p>
            The sheet of marks properly recorded should be submitted
            within fifteen days dating from the time of exam, in case of
            urgency the institution is entitled to shorten this deadline.
        </p>
    </div>


    <!-- ============================================================
         ARTICLE 7
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 7</h2>
        <p>
            Any teaching staff member is evaluated at the end of the
            course and at the end of the academic year by the hierarchy
            based on:
        </p>

        <ul>
            <li>
                His/her scientific competence (his/her handling of the
                course contents, scientific articles and papers
                publishing);
            </li>

            <li>
                His/her pedagogic competence (methodology techniques, and
                strategies applied in transmitting efficiently the course
                contents);
            </li>

            <li>
                His/her moral aptitudes (punctuality, objectivity, sense
                of responsibility, commitment to students' education,
                etc.);
            </li>

            <li>
                In order to maintain or keep his/her course, a teacher
                must get at least 70% of mark of the evaluation done by
                hierarchy.
            </li>
        </ul>
    </div>


    <!-- ============================================================
         ARTICLE 8
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 8</h2>
        <p>
            A non-informed absence (or late informed) brings prejudice to
            the students in many regards, disturbs the functioning of the
            teaching activities, and seriously spoils the reputation of
            the institution; such conduct cannot be tolerated.
        </p>
    </div>


    <!-- ============================================================
         ARTICLE 9
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 9</h2>
        <p>
            The wage of the part-time employee will be set in accordance
            with his/her Academic rank.
        </p>
    </div>


    <!-- ============================================================
         ARTICLE 10
         ============================================================ -->

    <div class="contract-article">
        <h2>Article 10</h2>
        <p>
            Each party may terminate the appointment by giving to the
            other party 15 days Notice in writing. However, the
            University reserves the right to cancel the present contract
            without prior notice in case the employee seems to be
            inefficient, immoral, or absent without informing the HOD.
        </p>
    </div>


    <!-- ============================================================
         SIGNATURES
         ============================================================ -->

    <div class="paper-signatures">

        <div class="paper-signature-line lecturer-signature-line">

            <span class="paper-signature-name">
                Lecturer’s Name: {{LecturerName}} ................
            </span>

            <span class="paper-signature-field paper-signature-area">
                Signature ................
            </span>

            <span class="paper-signature-field paper-date-area">
                Date ................
            </span>

        </div>

        <div class="paper-signature-line dean-signature-line">

            <span class="paper-signature-name">
                Dean of Faculty: Prof. NYESHEJA M. Enan
            </span>

            <span class="paper-signature-field paper-signature-area">
                Signature ................
            </span>

            <span class="paper-signature-field paper-date-area">
                Date ................
            </span>

        </div>

        <div class="paper-signature-line hr-signature-line">

            <span class="paper-signature-name">
                Human Resource Officer Mr. NTAKIRUTIMANA Elison
            </span>

            <span class="paper-signature-field paper-signature-area">
                Signature ................
            </span>

            <span class="paper-signature-field paper-date-area">
                Date ................
            </span>

        </div>

        <div class="paper-signature-line dvcar-signature-line">

            <span class="paper-signature-name">
                DVCAR Prof. HAKIZIMANA Emmanuel
            </span>

            <span class="paper-signature-field paper-signature-area">
                Signature ................
            </span>

            <span class="paper-signature-field paper-date-area">
                Date ................
            </span>

        </div>

        <div class="paper-signature-line vc-signature-line">

            <span class="paper-signature-name">
                Vice Chancellor Prof. NGAMIJE Jean
            </span>

            <span class="paper-signature-field paper-signature-area">
                Signature ................
            </span>

            <span class="paper-signature-field paper-date-area">
                Date ................
            </span>

        </div>

    </div>


    <!-- ============================================================
         ACCREDITATION
         ============================================================ -->

    <p class="contract-accreditation-note">
        Accredited by Ministerial Order N&deg; 002/09 of 09/04/2009 granting the Definitive Operating Licence.
    </p>

</div>
""";

            var existingTemplates = await db.Templates.ToListAsync();

            if (existingTemplates.Count == 0)
            {
                db.Templates.Add(new Template
                {
                    Contract = contractTemplate,
                    Claim = string.Empty,
                    Letter = string.Empty
                });
            }
            else
            {
                // Keep the stored contract template in sync with the code.
                // Only the Contract column is overwritten; Claim and Letter
                // are left untouched.
                foreach (var existing in existingTemplates)
                {
                    if (existing.Contract != contractTemplate)
                        existing.Contract = contractTemplate;
                }
            }

            await db.SaveChangesAsync();
        }
    }
}