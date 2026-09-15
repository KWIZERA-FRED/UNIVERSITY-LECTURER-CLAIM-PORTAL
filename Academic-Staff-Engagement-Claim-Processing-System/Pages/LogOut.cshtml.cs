using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Academic_Staff_Engagement_Claim_Processing_System.Pages;

public class LogoutModel : PageModel
{
    // ============================================================
    // GET
    // ============================================================
    //
    // A GET request must never sign the user out. A bookmark, a
    // prefetch, or a crawler hitting /Logout would end the session
    // silently. So we only redirect to the login page.
    //

    public IActionResult OnGet()
    {
        return RedirectToPage("/Login");
    }

    // ============================================================
    // POST — HARDENED SIGN-OUT
    // ============================================================

    public async Task<IActionResult> OnPostAsync()
    {
        // --------------------------------------------------------
        // 1. Sign out the cookie authentication scheme.
        //    Your Program.cs uses:
        //      AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        //        .AddCookie(...)
        //    which is the "Cookies" scheme.
        // --------------------------------------------------------

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        // --------------------------------------------------------
        // 2. Clear the ASP.NET Core session.
        //    Even though we will also delete the session cookie
        //    below, clearing the server-side data first makes any
        //    stale reference in the memory cache unusable.
        // --------------------------------------------------------

        try
        {
            HttpContext.Session.Clear();
        }
        catch (InvalidOperationException)
        {
            // Session middleware not enabled — nothing to clear.
        }

        // --------------------------------------------------------
        // 3. Delete every cookie the app has ever set.
        //
        //    We delete both the auth cookie and the session cookie
        //    explicitly (with the exact same options they were
        //    issued with, otherwise the browser will refuse to
        //    overwrite them), plus a defensive sweep of any other
        //    cookie whose name starts with the app prefix.
        // --------------------------------------------------------

        DeleteCookie(
            HttpContext,
            ".StaffPortal.Auth",
            isEssential: true);

        DeleteCookie(
            HttpContext,
            ".StaffPortal.Session",
            isEssential: true);

        // Defensive: remove any cookie the app has issued whose
        // name starts with the shared prefix. This catches future
        // cookies that a developer adds without updating this file.
        foreach (var cookie in HttpContext.Request.Cookies.Keys)
        {
            if (cookie.StartsWith(
                    ".StaffPortal",
                    StringComparison.OrdinalIgnoreCase))
            {
                DeleteCookie(
                    HttpContext,
                    cookie,
                    isEssential: true);
            }
        }

        // --------------------------------------------------------
        // 4. Force the browser to never cache the response, and
        //    never reuse a stale authenticated page for the
        //    current URL. This is what stops "Back button returns
        //    to a page rendered before logout".
        // --------------------------------------------------------

        Response.Headers["Cache-Control"] =
            "no-cache, no-store, must-revalidate, max-age=0";

        Response.Headers["Pragma"] = "no-cache";

        Response.Headers["Expires"] = "0";

        // --------------------------------------------------------
        // 5. Redirect to the login page.
        //
        //    RedirectToPage is a server-side 302 — the browser
        //    will navigate to /Login without ever re-rendering
        //    the page the user was on.
        // --------------------------------------------------------

        return RedirectToPage("/Login");
    }

    // ============================================================
    // COOKIE DELETION HELPER
    // ============================================================
    //
    // A cookie is only removed by the browser if the deletion
    // response carries the exact same Path, Domain, SameSite, and
    // Secure attributes the cookie was issued with.
    //
    // We deliberately match the options used in Program.cs so the
    // deletion always wins, regardless of the browser.
    //

    private static void DeleteCookie(
        HttpContext httpContext,
        string name,
        bool isEssential)
    {
        var options = new CookieOptions
        {
            Path = "/",

            HttpOnly = true,

            // Must mirror Program.cs:
            //   options.Cookie.SameSite = SameSiteMode.Strict;
            //   options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            SameSite = SameSiteMode.Strict,

            Secure = true,

            IsEssential = isEssential,

            // Expire in the past — instruct the browser to drop it.
            Expires = DateTimeOffset.UnixEpoch
        };

        httpContext.Response.Cookies.Delete(name, options);

        // Second pass: also write an empty cookie with the same
        // attributes and a past expiry. Some browsers only drop
        // a cookie when they see this explicit overwrite.
        httpContext.Response.Cookies.Append(
            name,
            string.Empty,
            options);
    }
}