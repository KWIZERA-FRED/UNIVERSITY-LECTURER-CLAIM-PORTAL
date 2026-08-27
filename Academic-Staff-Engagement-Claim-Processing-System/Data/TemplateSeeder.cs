using Academic_Staff_Engagement_Claim_Processing_System.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Data
{
    public static class TemplateSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext db)
        {
            // Don't insert another copy if a contract template already exists.
            if (await db.Templates.AnyAsync())
                return;

            var contractTemplate = """
UNIVERSITY OF LAY ADVENTISTS OF KIGALI

PO. Box 6392, Kigali, Rwanda                                      website: www.unilak.ac.rw

Phone: +250 (0)731743430 / +250 (0)731743431                     E-mail: info@unilak.ac.rw

Kigali, {{ContractDate}}

EMPLOYMENT PART-TIME CONTRACT

Between the undersigned:

University of Lay Adventists of Kigali (UNILAK), represented by the Vice
Chancellor, Prof. Jean NGAMIJE, on one hand,

And the Employee, {{LecturerName}}, having the Academic rank of {{AcademicRank}},
with Identity Card/Passport No. {{GovernmentId}}, on the other hand;

The following has been agreed:

ARTICLE 1

UNILAK employs {{LecturerName}} as an Internal/External part-time
lecturer in the Faculty of Computing and Information Sciences, Department of
{{Department}}, Intake {{Intake}}, {{Session}}, to teach the course of
{{CourseTitle}}, Academic Year {{AcademicYear}}, Semester {{Semester}},
at {{Campus}} Campus.

ARTICLE 2

The number of contact hours allocated to the course/module, if the course
is taught through face-to-face mode, is {{AllocatedHours}} hours. This
includes theory, practical sessions, and examinations.

The rate per hour will be {{HourlyRate}} (gross).

ARTICLE 3

For the total number of classes combined, if the module/course is taught
through online teaching mode: {{NumberOfOnlineClasses}} classes,
and the total number of hours allocated to those combined classes taught by one
academic staff member is {{OnlineHours}} hours.

ARTICLE 4

The employee is required to submit to the Deputy Vice Chancellor for
Academic and Research office his/her application letter, CV, notarized copy of
the degree, equivalence if the degree is obtained from a foreign country,
as well as his/her nomination papers for his/her previous academic rank.

ARTICLE 5

The Lecturer is required to submit to the Head of the Department the
following documents:

- Course materials such as handouts, syllabuses, and other supporting documents
  must be uploaded to the UNILAK online teaching platform and submitted to
  the Head of Department's office before starting the class.

- Final examination and marking scheme.

- Continuous assessment papers, including assignments, quizzes, and tests.

ARTICLE 6

The sheet of marks properly recorded should be submitted within fifteen
days from the time of the examination. In case of urgency, the institution is
entitled to shorten this deadline.

ARTICLE 7

Any teaching staff member is evaluated at the end of the course and at
the end of the academic year by the hierarchy based on:

- Scientific competence: handling of the course contents, scientific articles,
  and paper publishing;

- Pedagogic competence: methodology, techniques, and strategies applied in
  efficiently transmitting the course contents;

- Moral aptitudes: punctuality, objectivity, sense of responsibility,
  commitment to students' education, etc.

In order to maintain or keep his/her course, a teacher must obtain at least
70% of the marks in the evaluation conducted by the hierarchy.

ARTICLE 8

An uninformed absence, or a late-informed absence, causes prejudice to
the students in many regards, disturbs the functioning of teaching activities,
and seriously spoils the reputation of the institution. Such conduct cannot
be tolerated.

ARTICLE 9

The wage of the part-time employee will be set in accordance with his/her
academic rank.

ARTICLE 10

Each party may terminate the appointment by giving the other party 15 days'
notice in writing.

However, the University reserves the right to cancel the present contract
without prior notice in case the employee is found to be inefficient, immoral,
or absent without informing the HOD.


SIGNATURES

LECTURER

Name: {{LecturerName}}

Signature: {{LecturerSignature}}

Date: {{LecturerSignatureDate}}


DEAN OF FACULTY

Name: Prof. NYESHEJA M. Enan

Signature: {{DeanSignature}}

Date: {{DeanSignatureDate}}


HUMAN RESOURCE OFFICER

Name: Mr. NTAKIRUTIMANA Elison

Signature: {{HRSignature}}

Date: {{HRSignatureDate}}


DVCAR

Name: Prof. HAKIZIMANA Emmanuel

Signature: {{DVCARSignature}}

Date: {{DVCARSignatureDate}}


VICE CHANCELLOR

Name: Prof. NGAMIJE Jean

Signature: {{VCSignature}}

Date: {{VCSignatureDate}}
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