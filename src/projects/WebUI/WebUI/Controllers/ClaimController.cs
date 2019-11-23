using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Azure;
using WebUI.Models;
using WebUI.Utils;
using WebUI.ViewModels;
using IdentityUser = Microsoft.AspNetCore.Identity.IdentityUser;

namespace WebUI.Controllers
{
    //[Authorize(Roles = "Administrators")]
    [AllowAnonymous]
    public class ClaimController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public ClaimController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        // [RequireHttps]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index");
                }

                ModelState.AddModelError(string.Empty, "删除用户时发生错误");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "用户找不到");
            }

            return View("Index", await _userManager.Users.ToListAsync());
        }

        public async Task<IActionResult> ManageClaims(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            var configAuthorization = Config.App.GetJToken(Constants.JsontagClaim) as JObject;
            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference("index");
            var configBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var addClaim = JsonUtils.GetJToken(Constants.JsontagClaim, json);
            var addClaimObj = addClaim == null ? null : addClaim as JObject;
            var userClaimViewModel = new UserClaimViewModel
            {
                user_id = id,
                claims = new List<string>()
            };

            if (!Object.ReferenceEquals(addClaimObj, null))
            {
                addClaimObj.Merge(configAuthorization, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union
                });
            }
            if (!Object.ReferenceEquals(addClaimObj, null))
            {
                foreach (var pair in addClaimObj)
                {
                    if (pair.Key == user.Email)
                    {
                        var claimsArray = pair.Value as JArray;
                        foreach (var oneclaim in claimsArray)
                        {
                            userClaimViewModel.claims.Add(oneclaim.ToString());
                        }
                    }

                }
            }

            return View(userClaimViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveClaim(string id, string claim)
        {
            var user = await _userManager.FindByIdAsync(id);
            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference("index");
            var configBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var addClaim = JsonUtils.GetJToken(Constants.JsontagClaim, json);
            var addClaimObj = addClaim == null ? null : addClaim as JObject;
            var userClaimViewModel = new UserClaimViewModel
            {
                user_id = id,
                claims = new List<string>()
            };
            if (!Object.ReferenceEquals(addClaimObj, null))
            {
                foreach (var pair in addClaimObj)
                {
                    if (pair.Key == user.Email)
                    {
                        var claimsArray = pair.Value as JArray;
                        foreach (var oneclaim in claimsArray)
                        {
                            if (String.Compare(oneclaim.ToString(), claim, true) == 0)
                            {
                                claimsArray.Remove(oneclaim);
                                await configBlob.UploadGenericObjectAsync(json);
                                break;
                            }
                        }
                    }

                }
            }
            return RedirectToAction("ManageClaims", new { id });
        }
        public IActionResult AddClaim()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddClaim(AddClaimViewModel addClaimViewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(addClaimViewModel);
            }

            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference("index");
            var configBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var addClaimList = JsonUtils.GetJToken(Constants.JsontagClaimAllList, json);
            if (addClaimList == null)
            {
                json.Add(Constants.JsontagClaimAllList, new JArray());
                addClaimList = JsonUtils.GetJToken(Constants.JsontagClaimAllList, json);
            }
            var addClaimObj = addClaimList == null ? new JArray() : addClaimList as JArray;
            bool flag = false;
            if (!Object.ReferenceEquals(addClaimObj, null))
            {
                foreach (var claim in addClaimObj)
                {
                    if (String.Compare(claim.ToString(), addClaimViewModel.ClaimName, true) == 0)
                    {
                        flag = true;
                    }

                }
            }

            if (!flag)
            {
                addClaimObj.Add(addClaimViewModel.ClaimName);
                await configBlob.UploadGenericObjectAsync(json);
            }

            return RedirectToAction("Index");
        }
        public async Task<IActionResult> AddClaimTouser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference("index");
            var authBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
            var json = await authBlob.DownloadGenericObjectAsync();
            var addClaim = JsonUtils.GetJToken(Constants.JsontagClaim, json);
            var addClaimList = JsonUtils.GetJToken(Constants.JsontagClaimAllList, json);
            var addClaimObj = addClaim == null ? null : addClaim as JObject;
            var addClaimListObj = addClaimList == null ? null : addClaimList as JArray;
            List<string> claims = new List<string>();
            foreach (var one in addClaimListObj)
            {
                claims.Add(one.ToString());
            }
            if (!Object.ReferenceEquals(addClaimObj, null))
            {
                foreach (var pair in addClaimObj)
                {
                    if (pair.Key == user.Email)
                    {
                        var claimList = pair.Value as JArray;
                        foreach (var one in claimList)
                        {
                            if (claims.Contains(one.ToString()))
                            {
                                claims.Remove(one.ToString());
                            }
                        }
                    }
                }
            }
            var vm = new AddClaimViewModel
            {
                user_id = id,
                claims = claims
            };
            return View(vm);

        }

        [HttpPost]
        public async Task<IActionResult> AddClaimTouser(AddClaimViewModel addClaimViewModel)
        {
            var user = await _userManager.FindByIdAsync(addClaimViewModel.user_id);
            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference("index");
            var configBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var addAuth = JsonUtils.GetJToken(Constants.JsontagClaim, json);
            if (addAuth == null)
            {
                json.Add(Constants.JsontagClaim, new JObject());
                addAuth = JsonUtils.GetJToken(Constants.JsontagClaim, json);
                var addObj = addAuth as JObject;
                addObj.Add(user.Email, new JArray(addClaimViewModel.ClaimName));
                await configBlob.UploadGenericObjectAsync(json);
            }
            else
            {
                var addAuthObj = addAuth == null ? null : addAuth as JObject;
                if (!Object.ReferenceEquals(addAuthObj, null))
                {
                    foreach (var pair in addAuthObj)
                    {
                        if (pair.Key == user.Email)
                        {
                            var peopleArray = pair.Value as JArray;
                            peopleArray.Add(addClaimViewModel.ClaimName);
                            await configBlob.UploadGenericObjectAsync(json);
                        }
                    }
                }
            }
            
            

            return RedirectToAction("ManageClaims", new { id = addClaimViewModel.user_id });

        }
    }
}
