using Microsoft.AspNetCore.Mvc;

namespace TaskManagement.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Dashboard");

        return RedirectToAction("Login", "Account");
    }

    [Route("Home/Error")]
    public IActionResult Error()
    {
        ViewBag.Code = 500;
        ViewBag.Message = "Something went wrong on our side. Try again, and tell an administrator if it keeps happening.";
        return View("Error");
    }

    [Route("Home/StatusCode")]
    public IActionResult StatusCodeHandler(int code)
    {
        ViewBag.Code = code;
        ViewBag.Message = code switch
        {
            403 => "You do not have access to that page.",
            404 => "That page does not exist.",
            401 => "Sign in to continue.",
            _ => "The request could not be completed."
        };
        return View("Error");
    }
}
