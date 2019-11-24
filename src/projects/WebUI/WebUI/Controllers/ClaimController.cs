using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Azure;
using WebUI.Models;
using WebUI.Utils;
using WebUI.ViewModels;
using IdentityUser = Microsoft.AspNetCore.Identity.IdentityUser;

namespace WebUI.Controllers
{
    [Authorize(Roles = "Admin")]
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
            List<string> claimList = new List<string>();
            var userClaimViewModel = new UserClaimViewModel
            {
                user_id = id,
                claims = new List<string>()
            };
            byte[] encodedClaimListFromSession = HttpContext.Session.Get("user_claim_list");
            if (encodedClaimListFromSession != null)
            {
                string deserializedString = Encoding.UTF8.GetString(encodedClaimListFromSession);
                claimList = JsonConvert.DeserializeObject<List<string>>(deserializedString);
            }
            else
            {
                var user = await _userManager.FindByIdAsync(id);
                var configAuthorization = Config.App.GetJToken(Constants.JsontagClaim) as JObject;
                var container = CloudStorage.GetContainer(null);
                var dirpath = container.GetDirectoryReference("index");
                var configBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
                var json = await configBlob.DownloadGenericObjectAsync();
                var addClaim = JsonUtils.GetJToken(Constants.JsontagClaim, json);
                var addClaimObj = addClaim == null ? null : addClaim as JObject;
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
                                claimList.Add(oneclaim.ToString());
                            }
                        }

                    }
                }
                var serializedString = JsonConvert.SerializeObject(claimList);
                byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
                HttpContext.Session.Set("user_claim_list", encodedUserList);
            }
            userClaimViewModel.claims = claimList;
            return View(userClaimViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveClaim(string id, string claim)
        {
            List<string> claimList = new List<string>();
            List<string> claims = new List<string>();
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
                                byte[] encodedUserListFromSession = HttpContext.Session.Get("user_claim_list");
                                if (encodedUserListFromSession != null)
                                {
                                    string deserializedString = Encoding.UTF8.GetString(encodedUserListFromSession);
                                    claimList = JsonConvert.DeserializeObject<List<string>>(deserializedString);
                                    claimList.Remove(oneclaim.ToString());
                                    var serializedString = JsonConvert.SerializeObject(claimList);
                                    byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
                                    HttpContext.Session.Set("user_claim_list", encodedUserList);
                                }
                                byte[] encodedAllClaimListFromSession = HttpContext.Session.Get("all_claim_list");
                                if (encodedAllClaimListFromSession != null)
                                {
                                    string deserializedString = Encoding.UTF8.GetString(encodedAllClaimListFromSession);
                                    claims = JsonConvert.DeserializeObject<List<string>>(deserializedString);
                                    claims.Add(claim);
                                    var serializedString = JsonConvert.SerializeObject(claims);
                                    byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
                                    HttpContext.Session.Set("all_claim_list", encodedUserList);
                                }
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
            List<string> claims = new List<string>();
            if (!flag)
            {
                addClaimObj.Add(addClaimViewModel.ClaimName);
                await configBlob.UploadGenericObjectAsync(json);
                byte[] encodedUserListFromSession = HttpContext.Session.Get("all_claim_list");
                if (encodedUserListFromSession != null)
                {
                    string deserializedString = Encoding.UTF8.GetString(encodedUserListFromSession);
                    claims = JsonConvert.DeserializeObject<List<string>>(deserializedString);
                    claims.Add(addClaimViewModel.ClaimName);
                    var serializedString = JsonConvert.SerializeObject(claims);
                    byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
                    HttpContext.Session.Set("role_user_list_only_blob", encodedUserList);
                }
            }

            return RedirectToAction("Index");
        }
        public async Task<IActionResult> AddClaimTouser(string id)
        {
            List<string> claims = new List<string>();
            byte[] encodedUserListFromSession = HttpContext.Session.Get("all_claim_list");
            if (encodedUserListFromSession != null)
            {
                string deserializedString = Encoding.UTF8.GetString(encodedUserListFromSession);
                claims = JsonConvert.DeserializeObject<List<string>>(deserializedString);
            }
            else
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
                var serializedString = JsonConvert.SerializeObject(claims);
                byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
                HttpContext.Session.Set("all_claim_list", encodedUserList);
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
            List<string> claims = new List<string>();
            List<string> claimList = new List<string>();
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
                            byte[] encodedAllClaimListFromSession = HttpContext.Session.Get("all_claim_list");
                            byte[] encodedUserClaimListFromSession = HttpContext.Session.Get("user_claim_list");
                            if (encodedAllClaimListFromSession != null)
                            {
                                string deserializedString = Encoding.UTF8.GetString(encodedAllClaimListFromSession);
                                claims = JsonConvert.DeserializeObject<List<string>>(deserializedString);
                                claims.Remove(addClaimViewModel.ClaimName);
                                var serializedString = JsonConvert.SerializeObject(claims);
                                byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
                                HttpContext.Session.Set("all_claim_list", encodedUserList);
                            }
                            if (encodedUserClaimListFromSession != null)
                            {
                                string deserializedString = Encoding.UTF8.GetString(encodedUserClaimListFromSession);
                                claimList = JsonConvert.DeserializeObject<List<string>>(deserializedString);
                                claimList.Add(addClaimViewModel.ClaimName);
                                var serializedString = JsonConvert.SerializeObject(claimList);
                                byte[] encodedUserList = Encoding.UTF8.GetBytes(serializedString);
                                HttpContext.Session.Set("user_claim_list", encodedUserList);
                            }
                            break;
                        }
                    }
                }
            }
            
            

            return RedirectToAction("ManageClaims", new { id = addClaimViewModel.user_id });

        }
    }
}
