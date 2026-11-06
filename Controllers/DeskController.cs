using Microsoft.AspNetCore.Authentication.Cookies;

namespace GymDesk.Controllers;

public abstract class DeskController : Controller
{
    protected GymProfile GymInfo =>
        HttpContext.Items["GymProfile"] as GymProfile ?? new GymProfile();

    protected void Note(string message) => TempData["Message"] = message;

    protected void Warn(string message) => TempData["Error"] = message;

    protected IActionResult Go(string? returnUrl, string action, string controller, object? route = null)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(action, controller, route);
    }
}
