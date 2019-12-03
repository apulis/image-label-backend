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
using Common.Utils;
using Microsoft.AspNetCore.Cors;
using WebUI.ViewModels;
using AzureService = WebUI.Services.AzureService;
using IdentityUser = Microsoft.AspNetCore.Identity.IdentityUser;

namespace WebUI.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public UserController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }
        // [RequireHttps]
        public async Task<IActionResult> Index()
        {
            List<UserEmailViewModel> userList = new List<UserEmailViewModel>();
            var userBlob = AzureService.GetBlob("cdn", "private", null, null, $"user", "list.json");
            var userJson = await userBlob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(userJson, null))
            {
                foreach (var pair in userJson)
                {
                    var oneUser = pair.Value as JObject;
                    userList.Add(new UserEmailViewModel { email = oneUser["email"].ToString(), userId = pair.Key });
                }
            }
            return View(userList);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var userBlob = AzureService.GetBlob("cdn", "private", null, null, $"user", "list.json");
            var userJson = await userBlob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(userJson, null))
            {
                foreach (var pair in userJson)
                {
                    if (pair.Key == id)
                    {
                        userJson.Remove(id);
                    }
                }
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> ManageClaims(string id)
        {
            List<UserClaimViewModel> claimList = new List<UserClaimViewModel>();
            var userClaimViewModel = new UserClaimViewModel
            {
                user_id = id,
                claims = new List<UserClaimViewModel>()
            };
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{id}", WebUIConfig.membershipFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken(Constants.JsontagAccount, json);
            if (accounts == null)
            {
                return View(userClaimViewModel);
            }
            var AccountObj = accounts as JArray;

            foreach (var account in AccountObj)
            {
                var name = await AzureService.FindAccountName(account.ToString());
                if (name != null)
                {
                    claimList.Add(new UserClaimViewModel() { Name = name , GUid = account.ToString() });
                }
            }
            userClaimViewModel.claims = claimList;
            return View(userClaimViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveClaim(string id, string accountId)
        {
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{id}", WebUIConfig.membershipFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken(Constants.JsontagAccount, json);

            var AccountArray = accounts == null ? null : accounts as JArray;

            if (!Object.ReferenceEquals(AccountArray, null))
            {
                foreach (var oneAccount in AccountArray)
                {
                    if (String.Compare(oneAccount.ToString(), accountId, true) == 0)
                    {
                        AccountArray.Remove(oneAccount);
                        await configBlob.UploadGenericObjectAsync(json);
                        break;
                    }
                }
            }
            var accounts2 = JsonUtils.GetJToken("dataSets", json) as JObject;
            if (!Object.ReferenceEquals(accounts2, null))
            {
                foreach (var one in accounts2)
                {
                    if (one.Key == accountId)
                    {
                        accounts2.Remove(one.Key);
                        await configBlob.UploadGenericObjectAsync(json);
                        break;
                    }
                }
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account", "index.json");
            var accjson = await blob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(accjson, null))
            {
                foreach (var pair in accjson)
                {
                    if (pair.Key == accountId)
                    {
                        var obj = pair.Value as JObject;
                        var users = JsonUtils.GetJToken("users", obj) as JArray;
                        if (!Object.ReferenceEquals(users, null))
                        {
                            foreach (var one in users)
                            {
                                if (one.ToString() == id)
                                {
                                    users.Remove(one);
                                    break;
                                }
                            }
                        }
                        await blob.UploadGenericObjectAsync(accjson);
                    }
                }
            }
            return RedirectToAction("ManageClaims", new { id });
        }
        
        public async Task<IActionResult> AddClaimToUser(string id)
        {
            List<UserClaimViewModel> claims = new List<UserClaimViewModel>();
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{id}", WebUIConfig.membershipFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken(Constants.JsontagAccount, json);

            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var accountJson = await accountBlob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(accountJson, null))
            {
                foreach (var one in accountJson)
                {
                    claims.Add(new UserClaimViewModel(){GUid = one.Key,Name = one.Value["name"].ToString()});
                }
            }
            var AccountArray = accounts == null ? null : accounts as JArray;
            if (!Object.ReferenceEquals(AccountArray, null))
            {
                foreach (var account in AccountArray)
                {
                    foreach (UserClaimViewModel o in claims)
                    {
                        if (o.GUid == account.ToString())
                        {
                            claims.Remove(o);
                            break;
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
        public async Task<IActionResult> AddClaimToUser(AddClaimViewModel addClaimViewModel)
        {
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{addClaimViewModel.user_id}", WebUIConfig.membershipFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken(Constants.JsontagAccount, json);
            if (accounts == null)
            {
                var obj = new JObject();
                var accountArray = new JArray();
                accountArray.Add(addClaimViewModel.accountId);
                obj.Add(Constants.JsontagAccount, accountArray);
                await configBlob.UploadGenericObjectAsync(obj);
            }
            else
            {
                var accountArray = accounts as JArray;
                accountArray.Add(addClaimViewModel.accountId);
                await configBlob.UploadGenericObjectAsync(json);
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account", "index.json");
            var accJson = await blob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(accJson, null))
            {
                foreach (var pair in accJson)
                {
                    if (pair.Key == addClaimViewModel.accountId)
                    {
                        var obj = pair.Value as JObject;
                        var users = JsonUtils.GetJToken("users", obj) as JArray;
                        if (Object.ReferenceEquals(users, null))
                        {
                            obj.Add("users", new JArray { addClaimViewModel.user_id });
                        }
                        else
                        {
                            users.Add(addClaimViewModel.user_id);
                        }
                        await blob.UploadGenericObjectAsync(accJson);
                    }
                }
            }
            return RedirectToAction("ManageClaims", new { id = addClaimViewModel.user_id });

        }
        
    }
}
