using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebUI.Models;
using Microsoft.AspNetCore.Http;

namespace WebUI.Controllers
{
   
    public class HomeController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        public HomeController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
           
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
                    HttpContext.Session.SetString(Constants.JsontagRole, roleInfo);
                    return; 
                }
            }
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
