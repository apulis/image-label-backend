using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace WebUI.Controllers
{
    // [Authorize(Roles = "Admin")]
    [Authorize(Roles = "Admin,User")] // Allow either Admin or User role to access. 
    public class RemoteSensingController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        public RemoteSensingController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;

        }

        private async Task GetRole()
        {

            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user != null)
            {
                var role = await _userManager.GetRolesAsync(user);
                if (role.Count > 0)
                {
                    ViewData["Role"] = "(" + String.Join(",", role) + ")";
                }

            }

        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Upload()
        {
            return View();
        }

        
        public async Task<IActionResult> Browse()
        {
            return View();
        }

    }
}