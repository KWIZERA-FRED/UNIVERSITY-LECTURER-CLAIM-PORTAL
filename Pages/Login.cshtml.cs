using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class LoginModel : PageModel
{
    [BindProperty]
    public string Username { get; set; }

    [BindProperty]
    public string Password { get; set; }

    public string ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your username and password.";
            return Page();
        }

        if (Username == "Ali" && Password == "1234")
        {
            return RedirectToPage("/Lecturer/Index");
        }

        if (Username == "Malaz" && Password == "1234")
        {
            return RedirectToPage("/HOD/Index");
        }

        if (Username == "Fred" && Password == "1234")
        {
            return RedirectToPage("/DEAN/Index");
        }

        if (Username == "management" && Password == "1234")
        {
            return RedirectToPage("/ManagementDashboard");
        }
        if (Username == "Reem" && Password == "1234")
        {
            return RedirectToPage("/Lecturer/Part/Index");
        }

        ErrorMessage = "Invalid username or password.";
        return Page();
    }
}