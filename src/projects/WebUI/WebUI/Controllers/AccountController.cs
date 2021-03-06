﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Common.Utils;
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
using AzureService = WebUI.Services.AzureService;
using IdentityUser = Microsoft.AspNetCore.Identity.IdentityUser;

namespace WebUI.Controllers
{
    [Authorize(Roles = "Admin,User")]
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;

        public AccountController(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        // [RequireHttps]
        public async Task<IActionResult> Index()
        {
            List<AccountModel> accounts = new List<AccountModel>();
            List<string> accountList = new List<string>();
            var accountBlob = AzureService.GetBlob("cdn","private",null,null, "account", "index.json");
            var allAccounts = await accountBlob.DownloadGenericObjectAsync();
            if (allAccounts != null)
            {
                foreach (var oneAccount in allAccounts)
                {
                    var oneObj = oneAccount.Value as JObject;
                    if (User.IsInRole("Admin"))
                    {
                        accounts.Add(new AccountModel()
                            { GUid = oneAccount.Key, Name = oneObj["name"].ToString() });
                    }
                    else
                    {
                        accountList = await AzureService.GetUserAccountIdList(await AzureService.FindUserId(await _userManager.GetUserAsync(HttpContext.User)));
                        if (accountList != null)
                        {
                            if (accountList.Contains(oneAccount.Key))
                            {
                                accounts.Add(new AccountModel()
                                    { GUid = oneAccount.Key, Name = oneObj["name"].ToString() });
                            }
                        }
                    }
                    
                }
            }

            return View(accounts);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult AddClaim()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddClaim(AccountModel accountViewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(accountViewModel);
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var allAccounts = await accountBlob.DownloadGenericObjectAsync();

            if (allAccounts == null)
            {
                var obj = new JObject();
                var accountObj = new JObject
                {
                    { "name", accountViewModel.Name }
                };
                obj.Add(Guid.NewGuid().ToString().ToUpper(), accountObj);
                await accountBlob.UploadGenericObjectAsync(obj);
            }
            else
            {
                var Obj = new JObject
                {
                    { "name", accountViewModel.Name }
                };
                allAccounts.Add(Guid.NewGuid().ToString().ToUpper(), Obj);
                await accountBlob.UploadGenericObjectAsync(allAccounts);
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAccount(string id)
        {
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account/{id}", "membership.json");
            await blob.DeleteAsync();

            var accblob = AzureService.GetBlob("cdn", "private", null, null, $"account", "index.json");
            var accjson = await accblob.DownloadGenericObjectAsync();
            var accObj = JsonUtils.GetJToken(id, accjson) as JObject;
            if (!Object.ReferenceEquals(accObj, null))
            {
                foreach (var one in accObj)
                {
                    if (one.Key == "users")
                    {
                        var users = one.Value as JArray;
                        if (users != null)
                        {
                            foreach (var user in users)
                            {
                                var userId = user.ToString();
                                var userBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
                                var userJson = await userBlob.DownloadGenericObjectAsync();
                                var userArray = JsonUtils.GetJToken("accounts", userJson) as JArray;
                                if (!Object.ReferenceEquals(userArray, null))
                                {
                                    foreach (var o in userArray)
                                    {
                                        if (o.ToString() == id)
                                        {
                                            userArray.Remove(o);
                                            break;
                                        }
                                    }
                                }
                                var userObj = JsonUtils.GetJToken("dataSets", userJson) as JObject;
                                if (!Object.ReferenceEquals(userObj, null))
                                {
                                    userObj.Remove(id);
                                }
                                await userBlob.UploadGenericObjectAsync(userJson);
                            }
                        }
                    }
                }
            }

            accjson.Remove(id);
            await accblob.UploadGenericObjectAsync(accjson);
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> ManageAccount(string id)
        {
            List<DataSetModel> dataSets = new List<DataSetModel>();
            var accountViewModel = new AccountModel
            {
                GUid = id,
                DataSets = new List<DataSetModel>()
            };

            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{id}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);
            if (allAccounts == null)
            {
                return View(accountViewModel);
            }

            var accountListArray = allAccounts as JObject;
            foreach (var oneAccount in accountListArray)
            {
                dataSets.Add(new DataSetModel(){dataSetId = oneAccount.Key,Name = oneAccount.Value["name"].ToString()});
            }
            accountViewModel.DataSets = dataSets;
            return View(accountViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveDataSet(string id, string dataSetId)
        {
            List<string> dataSets = new List<string>();
            List<string> dataSetsWait = new List<string>();
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{id}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);

            var AccountArray = allAccounts == null ? null : allAccounts as JObject;
            if (!Object.ReferenceEquals(AccountArray, null))
            {
                foreach (var oneclaim in AccountArray)
                {
                    if (String.Compare(oneclaim.Key, dataSetId, true) == 0)
                    {
                        var obj = oneclaim.Value as JObject;
                        var userArray = JsonUtils.GetJToken("users", obj) as JArray;
                        if (userArray != null)
                        {
                            foreach (var user in userArray)
                            {
                                var blob = AzureService.GetBlob("cdn", "private", null, null, $"user/{user}", "membership.json");
                                var userJson = await blob.DownloadGenericObjectAsync();
                                var dataSetObj = JsonUtils.GetJToken("dataSets", userJson) as JObject;
                                var accArray = JsonUtils.GetJToken(id, dataSetObj) as JArray;
                                if (accArray != null)
                                {
                                    foreach (var one in accArray)
                                    {
                                        if (one.ToString() == dataSetId)
                                        {
                                            accArray.Remove(one);
                                            await blob.UploadGenericObjectAsync(userJson);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        AccountArray.Remove(oneclaim.Key);
                        await accountBlob.UploadGenericObjectAsync(json);
                        break;
                    }
                }
            }

            return RedirectToAction("ManageAccount", new {id});
        }

        public IActionResult AddDataSet(string id)
        {
            var vm = new DataSetModel()
            {
                GUid = id
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> AddDataSet(DataSetModel dataSetViewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(dataSetViewModel);
            }

            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{dataSetViewModel.GUid}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json) as JObject;
            if (allAccounts == null)
            {
                var obj = new JObject();
                var DataSetObj = new JObject();
                var newObj = new JObject();
                DataSetObj.Add(dataSetViewModel.dataSetId??Guid.NewGuid().ToString().ToUpper(), newObj);
                newObj.Add("name",dataSetViewModel.Name);
                newObj.Add("type", dataSetViewModel.dataSetType.ToString());
                obj.Add("dataSets", DataSetObj);
                await accountBlob.UploadGenericObjectAsync(obj);
            }
            else
            {
                var infoObj = JsonUtils.GetJToken(dataSetViewModel.dataSetId, allAccounts);
                if (infoObj == null)
                {
                    var dataSetId = dataSetViewModel.dataSetId ?? Guid.NewGuid().ToString().ToUpper();
                    allAccounts.Add(dataSetId, new JObject
                    {
                        {"name", dataSetViewModel.Name },
                        {"type", dataSetViewModel.dataSetType.ToString()}
                    });
                    await accountBlob.UploadGenericObjectAsync(json);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "DataSet GUid had existed!!!");
                    return View(dataSetViewModel);
                }
            }
            return RedirectToAction("ManageAccount", new {id=dataSetViewModel.GUid});
        }

        public async Task<IActionResult> ManageDataSet(string id ,string dataSetId)
        {
            List<UserEmailViewModel> userList = new List<UserEmailViewModel>();
            var vm = new DataSetModel()
            {
                GUid = id,
                dataSetId = dataSetId,
                Users = new List<UserEmailViewModel>()
            };
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{id}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);

            if (allAccounts == null)
            {
                return View(vm);
            }
            var AccountObj = allAccounts as JObject;

            foreach (var pair in AccountObj)
            {
                if (pair.Key == dataSetId)
                {
                    var obj = pair.Value as JObject;
                    var array = JsonUtils.GetJToken("users", obj) as JArray;
                    if (array!=null)
                    {
                        foreach (var one in array)
                        {
                            var email = await AzureService.FindUserEmail(one.ToString());
                            userList.Add(new UserEmailViewModel { userId = one.ToString(),email = email});
                        }
                    }
                }
            }
            vm.Users = userList;
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveUser(string id, string dataSetId, string userId)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{id}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);

            var AccountObject = allAccounts == null ? null : allAccounts as JObject;

            if (!Object.ReferenceEquals(AccountObject, null))
            {
                foreach (var pair in AccountObject)
                {
                    if (String.Compare(pair.Key, dataSetId, true) == 0)
                    {
                        var obj = pair.Value as JObject;
                        var UserArray = JsonUtils.GetJToken("users", obj) as JArray;
                        foreach (var one in UserArray)
                        {
                            if (String.Compare(one.ToString(), userId, true) == 0)
                            {
                                UserArray.Remove(one);
                                await accountBlob.UploadGenericObjectAsync(json);
                                HttpContext.Session.Remove($"user_{userId}_tasks_list");
                                HttpContext.Session.Remove($"user_{userId}_task_{dataSetId}_list");
                                HttpContext.Session.Remove($"user_{userId}_task_{dataSetId}_permission");
                                break;
                            }
                        }
                    }
                }
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            var dataSets = JsonUtils.GetJToken("dataSets", userJson);
            if (!Object.ReferenceEquals(dataSets, null))
            {
                var dataSetObj = dataSets as JObject;
                foreach (var one in dataSetObj)
                {
                    if (one.Key == id)
                    {
                        var dataSetArray = one.Value as JArray;
                        foreach (var o in dataSetArray)
                        {
                            if (o.ToString() == dataSetId)
                            {
                                dataSetArray.Remove(o);
                                await blob.UploadGenericObjectAsync(userJson);
                                break;
                            }
                        }
                    }
                }
            }
            return RedirectToAction("ManageDataSet", new { id =id, dataSetId = dataSetId });
        }
        public async Task<IActionResult> AddUserToDataSet(string id,string dataSetId)
        {
            List<UserEmailViewModel> userList = new List<UserEmailViewModel>();
            List<string> userIdList = new List<string>();
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{id}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);
            var AccountObject = allAccounts == null ? null : allAccounts as JObject;

            var userBlob = AzureService.GetBlob("cdn", "private", null, null, $"user", "list.json");
            var userJson = await userBlob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(userJson, null))
            {
                foreach (var pair in userJson)
                {
                    var oneUser = pair.Value as JObject;
                    userList.Add(new UserEmailViewModel{ email = oneUser["email"].ToString(),userId = pair.Key});
                    userIdList.Add(pair.Key);
                }
            }
               

            if (!Object.ReferenceEquals(AccountObject, null))
            {
                foreach (var pair in AccountObject)
                {
                    if (pair.Key == dataSetId)
                    {
                        var obj = pair.Value as JObject;
                        var array = JsonUtils.GetJToken("users", obj) as JArray;
                        if (!Object.ReferenceEquals(array, null))
                        {
                            foreach (var o in array)
                            {
                                var index = userIdList.IndexOf(o.ToString());
                                userIdList.RemoveAt(index);
                                userList.RemoveAt(index);
                            }
                        }
                        
                    }
                }
            }

            var vm = new DataSetModel()
            {
                GUid = id,
                dataSetId = dataSetId,
                Users = userList
            };
            return View(vm);

        }

        [HttpPost]
        public async Task<IActionResult> AddUserToDataSet(DataSetModel dataSetViewModel)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{dataSetViewModel.GUid}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);
            var AccountObject = allAccounts == null ? null : allAccounts as JObject;
            bool flag = false;

            foreach (var pair in AccountObject)
            {
                if (String.Compare(pair.Key, dataSetViewModel.dataSetId, true) == 0)
                {
                    var obj = pair.Value as JObject;
                    var array = JsonUtils.GetJToken("users", obj) as JArray;
                    if (!Object.ReferenceEquals(array, null))
                    {
                        foreach (var one in array)
                        {
                            if (String.Compare(one.ToString(), dataSetViewModel.AddUser, true) == 0)
                            {
                                flag = true;
                                break;
                            }
                        }
                    }
                }
            }
            if (!flag)
            {
                var dataSetObj = JsonUtils.GetJToken(dataSetViewModel.dataSetId, AccountObject) as JObject;
                var array = JsonUtils.GetJToken("users", dataSetObj) as JArray;
                if (!Object.ReferenceEquals(array, null))
                {
                    array.Add(dataSetViewModel.AddUser);
                }
                else
                {
                    var newArray = new JArray
                    {
                        dataSetViewModel.AddUser
                    };
                    dataSetObj.Add("users", newArray);
                }
                await accountBlob.UploadGenericObjectAsync(json);

                var blob = AzureService.GetBlob("cdn", "private", null, null, $"user/{dataSetViewModel.AddUser}", "membership.json");
                var userJson = await blob.DownloadGenericObjectAsync();
                if(Object.ReferenceEquals(userJson, null))
                {
                    var obj = new JObject
                        {
                            { "dataSets",new JObject{{ dataSetViewModel.GUid, new JArray { dataSetViewModel.dataSetId } }} }
                        };
                    await blob.UploadGenericObjectAsync(obj);
                }
                else
                {
                    var dataSets = JsonUtils.GetJToken("dataSets", userJson) as JObject;
                    bool userFlag = false;
                    if (!Object.ReferenceEquals(dataSets, null))
                    {
                        var dataSetArray = JsonUtils.GetJToken(dataSetViewModel.GUid, dataSets) as JArray;
                        if (!Object.ReferenceEquals(dataSetArray, null))
                        {
                            foreach (var o in dataSetArray)
                            {
                                if (o.ToString() == dataSetViewModel.dataSetId)
                                {
                                    userFlag = true;
                                }
                            }
                            if (!userFlag)
                            {
                                dataSetArray.Add(dataSetViewModel.dataSetId);
                                await blob.UploadGenericObjectAsync(userJson);
                            }
                        }
                        else
                        {
                            dataSets.Add(dataSetViewModel.GUid, new JArray { dataSetViewModel.dataSetId });
                            await blob.UploadGenericObjectAsync(userJson);
                        }
                    }
                    else
                    {
                        var obj = new JObject
                        {
                            { dataSetViewModel.GUid, new JArray { dataSetViewModel.dataSetId } }
                        };
                        userJson.Add("dataSets", obj);
                        await blob.UploadGenericObjectAsync(userJson);
                    }
                }
                
                HttpContext.Session.Remove($"user_{dataSetViewModel.AddUser}_tasks_list");
                HttpContext.Session.Remove($"user_{dataSetViewModel.AddUser}_task_{dataSetViewModel.dataSetId}_list");
                HttpContext.Session.Remove($"user_{dataSetViewModel.AddUser}_task_{dataSetViewModel.dataSetId}_permission");
            }

            return RedirectToAction("ManageDataSet", new { id = dataSetViewModel.GUid, dataSetId = dataSetViewModel.dataSetId });

        }

    }
}
