using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Models;
using WebUI.Services;
using WebUI.ViewModels;

namespace WebUI.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableCors("dev-use")]
    [Route("api/projects")]
    [ApiController]
    public class ProjectController:ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetProjects()
        {
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            List<ProjectViewModel> accounts = new List<ProjectViewModel>();
            List<string> accountList = new List<string>();
            List<string> labelAccountList = new List<string>();
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var allAccounts = await accountBlob.DownloadGenericObjectAsync();
            if (allAccounts != null)
            {
                foreach (var oneAccount in allAccounts)
                {
                    var oneObj = oneAccount.Value as JObject;
                    if (role == "admin")
                    {
                        accounts.Add(new ProjectViewModel
                        { ProjectId = oneAccount.Key, Name = oneObj["name"].ToString(),Info = oneObj["info"].ToString(),Role="admin"});
                    }
                    else
                    {
                        accountList = await AzureService.GetUserAccountIdList(userId);
                        labelAccountList = await AzureService.GetUserLabelAccountIdList(userId);
                        if (accountList != null)
                        {
                            if (accountList.Contains(oneAccount.Key))
                            {
                                accounts.Add(new ProjectViewModel()
                                    { ProjectId = oneAccount.Key, Name = oneObj["name"].ToString(), Info = oneObj["info"].ToString(),Role ="manager"});
                            }
                        }

                        if (labelAccountList.Contains(oneAccount.Key))
                        {
                            accounts.Add(new ProjectViewModel
                            { ProjectId = oneAccount.Key, Name = oneObj["name"].ToString(), Info = oneObj["info"].ToString(), Role = "labeler" });
                        }
                    }

                }
            }
            return Ok(new Response().GetJObject("projects", JToken.FromObject(accounts)));
        }

        [HttpDelete("{projectId}")]
        public async Task<IActionResult> DeleteProject(string projectId)
        {
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return Ok(new Response {Msg = "You don't have access!"});
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
            await blob.DeleteAsync();
            var accblob = AzureService.GetBlob("cdn", "private", null, null, $"account", "index.json");
            var accjson = await accblob.DownloadGenericObjectAsync();
            var accObj = JsonUtils.GetJToken(projectId, accjson) as JObject;
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
                                var oneUserId = user.ToString();
                                var userBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{oneUserId}", "membership.json");
                                var userJson = await userBlob.DownloadGenericObjectAsync();
                                var userArray = JsonUtils.GetJToken("accounts", userJson) as JArray;
                                if (!Object.ReferenceEquals(userArray, null))
                                {
                                    foreach (var o in userArray)
                                    {
                                        if (o.ToString() == projectId)
                                        {
                                            userArray.Remove(o);
                                            break;
                                        }
                                    }
                                }
                                var userObj = JsonUtils.GetJToken("dataSets", userJson) as JObject;
                                if (!Object.ReferenceEquals(userObj, null))
                                {
                                    userObj.Remove(projectId);
                                }
                                await userBlob.UploadGenericObjectAsync(userJson);
                            }
                        }
                    }
                }
            }
            accjson.Remove(projectId);
            await accblob.UploadGenericObjectAsync(accjson);
            HttpContext.Session.Clear();
            return Ok(new Response { Msg = "ok" });
        }
        [HttpPost]
        public async Task<IActionResult> AddProject([FromBody]AddProjectViewModel accountViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response{Successful = "true",Msg=ModelState.Values.ToString(),Data= null });
            }
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var allAccounts = await accountBlob.DownloadGenericObjectAsync();

            if (allAccounts == null)
            {
                var obj = new JObject();
                var accountObj = new JObject
                {
                    {"name", accountViewModel.Name },
                    {"info",accountViewModel.Info }
                };
                obj.Add(Guid.NewGuid().ToString().ToUpper(), accountObj);
                await accountBlob.UploadGenericObjectAsync(obj);
            }
            else
            {
                var Obj = new JObject
                {
                    {"name", accountViewModel.Name },
                    {"info",accountViewModel.Info }
                };
                allAccounts.Add(Guid.NewGuid().ToString().ToUpper(), Obj);
                await accountBlob.UploadGenericObjectAsync(allAccounts);
            }
            return Ok(new Response { Msg = "ok" });
        }
        [HttpPatch("{projectId}")]
        public async Task<IActionResult> UpdateProject(string projectId, AddProjectViewModel accountViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response { Successful = "true", Msg = ModelState.Values.ToString(), Data = null });
            }
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var allAccounts = await accountBlob.DownloadGenericObjectAsync();
            var accountObj = JsonUtils.GetJToken(projectId, allAccounts) as JObject;
            if (accountObj != null)
            {
                accountObj["name"] = accountViewModel.Name;
                accountObj["info"] = accountViewModel.Info;
                await accountBlob.UploadGenericObjectAsync(allAccounts);
            }
            return Ok(new Response { Msg = "ok" });
        }

        [HttpGet("{projectId}/managers")]
        public async Task<IActionResult> GetProjectManagers(string projectId)
        {
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            List<JObject> managerList = new List<JObject>();
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", WebUIConfig.membershipFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken("admin", json);
            if (accounts != null)
            {
                var adminArray = accounts as JArray;
                foreach (var one in adminArray)
                {
                    var obj = await AzureService.FindUserInfo(one.ToString());
                    if (obj != null)
                    {
                        managerList.Add(obj);
                    }
                }
            }
            return Ok(new Response().GetJObject("managers", JToken.FromObject(managerList)));
        }

        [HttpGet("{projectId}/managers/{userNumber}")]
        public async Task<IActionResult> CheckProjectManagerExists(string projectId, int userNumber)
        {
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin")
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            var userId =await AzureService.FindUserIdByNumber(userNumber);
            if (userId == null)
            {
                return Ok(new Response {Msg = "Cannot find userId!"});
            }
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", WebUIConfig.membershipFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken("admin", json) as JArray;
            if (accounts != null)
            {
                foreach (var one in accounts)
                {
                    if (one.ToString() == userId)
                    {
                        return Ok(new Response{Msg = "manager already exists!"});
                    }
                }
            }
            return Ok(new Response {Msg = "ok"});
        }
        [HttpPost("{projectId}/managers")]
        public async Task<IActionResult> AddProjectManager(string projectId,[FromBody]List<int> userNumbers)
        {
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin")
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            List<string> userIdList = new List<string>();
            foreach (var userNumber in userNumbers)
            {
                var userId = await AzureService.FindUserIdByNumber(userNumber);
                if (userId==null)
                {
                    return Ok(new Response { Msg = $"user number {userNumber} wrong!" });
                }
                userIdList.Add(userId);
                var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", WebUIConfig.membershipFile);
                var json = await configBlob.DownloadGenericObjectAsync();
                var accounts = JsonUtils.GetJToken("accounts", json) as JArray;
                if (json == null)
                {
                    await configBlob.UploadGenericObjectAsync(new JObject{{ "accounts", new JArray{ projectId } } });
                }
                else
                {
                    if(accounts == null)
                    {
                        json.Add("accounts", new JArray() {projectId});
                        await configBlob.UploadGenericObjectAsync(json);
                    }
                    else
                    {
                        if (accounts.IndexOf(projectId)!=-1)
                        {
                            accounts.Add(projectId);
                            await configBlob.UploadGenericObjectAsync(json);
                        }
                    }
                }
                
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", WebUIConfig.membershipFile);
            var accJson = await blob.DownloadGenericObjectAsync();
            if (accJson == null)
            {
                await blob.UploadGenericObjectAsync(new JObject() {{ "admin", new JArray{ userIdList } }});
            }
            else
            {
                var accountsList = JsonUtils.GetJToken("admin", accJson) as JArray;
                if (accountsList == null)
                {
                    accJson.Add("admin", new JArray { userIdList });
                }
                else
                {
                    foreach (var one in userIdList)
                    {
                        if (Json.ContainsKey(one, accountsList))
                        {
                            accountsList.Add(one);
                        }
                    }
                }
                await blob.UploadGenericObjectAsync(accJson);
            }
            return Ok(new Response{Msg = "add succeed"});
        }
        [HttpDelete("{projectId}/managers")]
        public async Task<IActionResult> DeleteProjectManager(string projectId,[FromBody] int userNumber)
        {
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin")
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            if (userId == null)
            {
                return Ok(new Response { Msg = $"user number {userNumber} wrong!" });
            }
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", WebUIConfig.membershipFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken("accounts", json) as JArray;
            if (accounts != null)
            {
                foreach (var oneAccount in accounts)
                {
                    if (String.Compare(oneAccount.ToString(), projectId, true) == 0)
                    {
                        accounts.Remove(oneAccount);
                        await configBlob.UploadGenericObjectAsync(json);
                        break;
                    }
                }
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", WebUIConfig.membershipFile);
            var accJson = await blob.DownloadGenericObjectAsync();
            var accountsList = JsonUtils.GetJToken("admin", accJson) as JArray;
            if (accountsList != null)
            {
                foreach (var one in accountsList)
                {
                    if (one.ToString() == userId)
                    {
                        accountsList.Remove(one);
                        await blob.UploadGenericObjectAsync(accJson);
                        break;
                    }
                }
            }
            return Ok(new Response{Msg = "delete success"});
        }
    }
}
