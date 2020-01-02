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
        /// <remarks>
        /// 获取当前用户的所属project列表
        /// 返回List
        /// </remarks>
        /// <param name="page">当前第几页，从1开始递增</param>
        /// <param name="size">每页的数量</param>
        [HttpGet]
        public async Task<IActionResult> GetProjects([FromQuery]int page, [FromQuery]int size)
        {
            var userId = HttpContext.User.Identity.Name;
            List<ProjectViewModel> accounts = await AzureService.FindUserRoleDetail(userId);
            var list = PageOps.GetPageRange(accounts, page, size, accounts.Count);
            return Ok(new Response().GetJObject("datasets", JToken.FromObject(list), "totalCount", accounts.Count));
        }
        /// <remarks>
        /// 删除一个project
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        [HttpDelete("{projectId}")]
        public async Task<IActionResult> DeleteProject(Guid projectId)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            var accblob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var accjson = await accblob.DownloadGenericObjectAsync();
            var accObj = JsonUtils.GetJToken(convertProjectId, accjson) as JObject;
            if (Object.ReferenceEquals(accObj, null))
            {
                return Ok(new Response {Msg = "The project doesn't exists!"});
            }
            accjson.Remove(convertProjectId);
            await accblob.UploadGenericObjectAsync(accjson);
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await blob.DownloadGenericObjectAsync();
            var adminList = JsonUtils.GetJToken("admin", json) as JArray;
            var datasets = JsonUtils.GetJToken("dataSets", json) as JObject;
            var waitUserIdList = new JArray();
            if (datasets != null)
            {
                foreach (var pair in datasets)
                {
                    var userList = JsonUtils.GetJToken("users", pair.Value as JObject) as JArray;
                    if (userList != null)
                    {
                        waitUserIdList.Merge(userList, new JsonMergeSettings
                        {
                            MergeArrayHandling = MergeArrayHandling.Union
                        });
                    }
                }
            }
            if (adminList != null)
            {
                waitUserIdList.Merge(adminList, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union
                });
            }
            foreach (var user in waitUserIdList)
            {
                var oneUserId = user.ToString();
                var userBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{oneUserId}", "membership.json");
                var userJson = await userBlob.DownloadGenericObjectAsync();
                var userArray = JsonUtils.GetJToken("accounts", userJson) as JArray;
                if (!Object.ReferenceEquals(userArray, null))
                {
                    foreach (var o in userArray)
                    {
                        if (o.ToString() == convertProjectId)
                        {
                            userArray.Remove(o);
                            await userBlob.UploadGenericObjectAsync(userJson);
                            break;
                        }
                    }
                }
                var userObj = JsonUtils.GetJToken("dataSets", userJson) as JObject;
                if (!Object.ReferenceEquals(userObj, null))
                {
                    userObj.Remove(convertProjectId);
                }
                await userBlob.UploadGenericObjectAsync(userJson);
            }
            await blob.DeleteAsync();
            HttpContext.Session.Clear();
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 添加一个project
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="accountViewModel">需name和info字段</param>
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
                return StatusCode(403);
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
        /// <remarks>
        /// 修改一个特定的project
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="accountViewModel">新的name和info字段</param>
        [HttpPatch("{projectId}")]
        public async Task<IActionResult> UpdateProject(Guid projectId, AddProjectViewModel accountViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response { Successful = "true", Msg = ModelState.Values.ToString(), Data = null });
            }
            var convertProjectId = projectId.ToString().ToUpper();
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var allAccounts = await accountBlob.DownloadGenericObjectAsync();
            var accountObj = JsonUtils.GetJToken(convertProjectId, allAccounts) as JObject;
            if (accountObj != null)
            {
                accountObj["name"] = accountViewModel.Name;
                accountObj["info"] = accountViewModel.Info;
                await accountBlob.UploadGenericObjectAsync(allAccounts);
            }
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 获取指定的project的项目管理员列表
        /// 返回List用户信息列表
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        [HttpGet("{projectId}/managers")]
        public async Task<IActionResult> GetProjectManagers(Guid projectId)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            List<JObject> managerList = new List<JObject>();
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", WebUIConfig.membershipFile);
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
        /// <remarks>
        /// 检测用户是否已经是该project的项目管理员
        /// manager already exists!表示用户已经存在于数据集，Cannot find userId!表示找不到该用户number，ok表示可以添加
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        ///<param name = "userNumber" > 用户唯一标识数字 </param>
        [HttpGet("{projectId}/managers/{userNumber}")]
        public async Task<IActionResult> CheckProjectManagerExists(Guid projectId, int userNumber)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            var userId =await AzureService.FindUserIdByNumber(userNumber);
            if (userId == null)
            {
                return Ok(new Response {Msg = "Cannot find userId!"});
            }
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", WebUIConfig.membershipFile);
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
        /// <remarks>
        /// 为指定的project添加项目管理员
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="userNumbers">用户的唯一标识数字列表</param>
        [HttpPost("{projectId}/managers")]
        public async Task<IActionResult> AddProjectManager(Guid projectId,[FromBody]List<int> userNumbers)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin")
            {
                return StatusCode(403);
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
                    await configBlob.UploadGenericObjectAsync(new JObject{{ "accounts", new JArray{ convertProjectId } } });
                }
                else
                {
                    if(accounts == null)
                    {
                        json.Add("accounts", new JArray() { convertProjectId });
                        await configBlob.UploadGenericObjectAsync(json);
                    }
                    else
                    {
                        if (!Json.ContainsKey(convertProjectId, accounts))
                        {
                            accounts.Add(convertProjectId);
                            await configBlob.UploadGenericObjectAsync(json);
                        }
                    }
                }
                
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", WebUIConfig.membershipFile);
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
                        if (!Json.ContainsKey(one, accountsList))
                        {
                            accountsList.Add(one);
                        }
                    }
                }
                await blob.UploadGenericObjectAsync(accJson);
            }
            return Ok(new Response{Msg = "add succeed"});
        }
        /// <remarks>
        /// 为指定的project删除项目管理员
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="userNumber">用户唯一标识数字</param>
        [HttpDelete("{projectId}/managers")]
        public async Task<IActionResult> DeleteProjectManager(Guid projectId,[FromBody] int userNumber)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin")
            {
                return StatusCode(403);
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
                    if (String.Compare(oneAccount.ToString(), convertProjectId, true) == 0)
                    {
                        accounts.Remove(oneAccount);
                        await configBlob.UploadGenericObjectAsync(json);
                        break;
                    }
                }
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", WebUIConfig.membershipFile);
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
