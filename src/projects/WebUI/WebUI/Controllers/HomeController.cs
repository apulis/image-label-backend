using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebUI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace WebUI.Controllers
{
   
    public class HomeController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<HomeController> _logger;
        public HomeController(UserManager<IdentityUser> userManager, ILogger<HomeController> logger)
        {
            _userManager = userManager;
            _logger = logger; 
        }

        private async Task GetRole( )
        {

            var user = await _userManager.GetUserAsync(HttpContext.User);
            if ( user != null )
            { 
                var role = await _userManager.GetRolesAsync(user);
                if (role.Count > 0)
                {
                    var roleInfo = String.Join(",", role);
                    _logger.LogInformation($"HomeController: User role is set to {roleInfo}");
                    HttpContext.Session.SetString(Constants.JsontagRole, roleInfo);
                    return; 
                }
            }
            _logger.LogInformation($"User is null, role removed...");
            HttpContext.Session.Remove(Constants.JsontagRole);

        }
        public async Task<IActionResult> Index()
        {
            await GetRole(); 
            return View();
        }

        public async Task<IActionResult> About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public async Task<IActionResult> Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public async Task<IActionResult> Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
