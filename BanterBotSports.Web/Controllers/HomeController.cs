using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BanterBotSports.Web.Models;

namespace BanterBotSports.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        // Authenticated users go directly to the tournament list
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Torneo");

        // Anonymous users see the branded landing page
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        var model = new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = statusCode ?? StatusCodes.Status500InternalServerError
        };
        return View(model);
    }
}
