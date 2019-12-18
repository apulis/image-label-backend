using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WebUI.Models;
using WebUI.Services;
using WebUI.ViewModels;

namespace WebUI.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ILogger<ExternalLoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public UserInfoViewModel Input { get; set; }

        public string LoginProvider { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public IActionResult OnGetAsync()
        {
            return RedirectToPage("./Login");
        }

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new {ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor : true);
            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return await OnPostConfirmationAsync(info, false, returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                ReturnUrl = returnUrl;
                

                // return Page();
                return await OnPostConfirmationAsync(info, true, ReturnUrl);
            }
        }

        private async Task<IActionResult> postLoginAuthorization(string returnUrl = null)
        {
            return LocalRedirect(returnUrl);
        }

        public async Task<IActionResult> OnPostConfirmationAsync(ExternalLoginInfo info, bool bCreate, string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            LoginProvider = info.LoginProvider;
            if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                var name = info.Principal.FindFirstValue(ClaimTypes.Email);
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                if (name == null)
                {
                    name = email;
                }
                Input = new UserInfoViewModel
                {
                    Email = email,
                    Name = name,
                    Id = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            };
            }
            else if(info.Principal.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
            {
                Input = new UserInfoViewModel
                {
                    Name = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    Id = info.Principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    Email = info.Principal.FindFirstValue(ClaimTypes.Email)
                };
            }
            // Get the information about the user from the external login provider

            var result = new IdentityResult();
            if (ModelState.IsValid)
            {
                var user = new IdentityUser { UserName = Input.Name, Email = Input.Email,Id = Input.Id};

                //if (!String.IsNullOrEmpty(Input.Email))
                {
                    // Find roles 
                    var role = await RoleManager.Current.FindRole(user);
                    //if (String.IsNullOrEmpty(role))
                    //{
                    //    ModelState.AddModelError(Constants.JsontagAuthorization, "The user is unauthorized");
                    //}
                    //else {

                    if (bCreate)
                        {
                            _logger.LogInformation($"Create user: {user}");
                            result = await _userManager.CreateAsync(user);
                            if (result.Succeeded)
                            {
                                _logger.LogInformation($"Add Login: {user}: {info}");
                                result = await _userManager.AddLoginAsync(user, info);
                                if (result.Succeeded)
                                {
                                    _logger.LogInformation($"Add Login succeed ");
                                    await _signInManager.SignInAsync(user, isPersistent: false);
                                    if (user.Email != null)
                                    {
                                        await AzureService.CreateUserId(Input);
                                    }
                                }
                                else
                                {
                                    _logger.LogInformation($"User created an account using {LoginProvider} provider.");
                                }

                            }
                            _logger.LogInformation($"Add role \"{role}\" to {user}");
                            if (role != null)
                            {
                                await _userManager.AddToRoleAsync(user, role);
                            }
                            
                        }
                        _logger.LogInformation($"User role is set to {role}");
                        if (role != null)
                        {
                            HttpContext.Session.SetString(Constants.JsontagRole, role);
                        }
                        

                        return Redirect(returnUrl);
                    //}
                } 
                //else
                //{
                //    ModelState.AddModelError("Identity", "Unable to parse the identity of this user.");
                //}
                 
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            LoginProvider = info.LoginProvider;
            ReturnUrl = returnUrl;
            if ( ModelState.IsValid )
            {
                return Redirect(returnUrl);
            } else
            {
                return Page(); 
            }
            

        }
    }
}
