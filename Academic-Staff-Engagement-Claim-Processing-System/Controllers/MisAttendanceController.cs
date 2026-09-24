using Academic_Staff_Engagement_Claim_Processing_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Academic_Staff_Engagement_Claim_Processing_System.Controllers
{
    [ApiController]
    [Route("api/mis/attendance")]
    public class MisAttendanceController : ControllerBase
    {
        private readonly IMisAttendanceService _attendanceService;

    public MisAttendanceController(
        IMisAttendanceService attendanceService)
        {
            _attendanceService = attendanceService;
        }

        [Authorize(Roles = "Lecturer")]
        [HttpGet("{courseAssignmentId:int}")]
        public async Task<IActionResult> GetAttendance(
            int courseAssignmentId)
        {
            var userId = User.FindFirstValue("UserId");

            if (!int.TryParse(userId, out var lecturerId))
                return Unauthorized();

            if (courseAssignmentId <= 0)
                return BadRequest("Invalid course assignment.");

            var result =
                await _attendanceService.GetAttendanceAsync(
                    lecturerId,
                    courseAssignmentId);

            if (result is null)
                return NotFound(
                    "Attendance could not be retrieved for the selected course.");

            return Ok(result);
        }
    }

}
