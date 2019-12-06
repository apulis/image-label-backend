using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebUI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WebUI.Services;

namespace WebUI.Controllers
{
   
    [Authorize]
    public class HomeController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(UserManager<IdentityUser> userManager, ILogger<HomeController> logger, IConfiguration _configuration)
        {
            _userManager = userManager;
            _logger = logger;
            this._configuration = _configuration;
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
        public async Task<IActionResult> Token()
        {
            var userId = await AzureService.FindUserId(await _userManager.GetUserAsync(HttpContext.User));
            if (userId==null)
            {
                return Redirect("/Identity/Account/Manage");
            }
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["SecurityKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, userId)
            };
            var token = new JwtSecurityToken(
                issuer: "apulis-china-infra01.sigsus.cn",
                audience: "apulis-china-infra01.sigsus.cn",
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds);

            var tokenGenerate = new JwtSecurityTokenHandler().WriteToken(token);
            return Redirect($"{_configuration["FontEndUrl"]}/?token={tokenGenerate}");
        }
    }
}
