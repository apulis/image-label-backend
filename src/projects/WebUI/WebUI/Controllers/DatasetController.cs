using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Models;
using WebUI.Services;
using WebUI.ViewModels;

namespace WebUI.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableCors("dev-use")]
    [Route("api/projects/{projectId}/datasets")]
    [ApiController]
    public class DatasetController:ControllerBase
    {
        /// <remarks>
        /// 获取当前用户的projectId对应的project下的所属数据集
        /// 返回List,数据集列表
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="page">当前第几页，从1开始递增</param>
        /// <param name="size">每页的数量</param>
        /// <response code="200">返回value字符串</response>
        [HttpGet]
        [ProducesResponseType(typeof(Response), 200)]
        public async Task<IActionResult> GetDatasets(Guid projectId,[FromQuery]int page,[FromQuery]int size)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            List<DatasetViewModel> datasetList = new List<DatasetViewModel>();
            if (role == "admin"|| await AzureService.FindUserIsProjectManager(userId, convertProjectId))
            {
                var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
                var accJson = await accountBlob.DownloadGenericObjectAsync();
                var allAccounts = JsonUtils.GetJToken("dataSets", accJson) as JObject;
                if (allAccounts != null)
                {
                    foreach (var oneAccount in allAccounts)
                    {
                        List<AddLabelViewModel> labels = new List<AddLabelViewModel>();
                        if (oneAccount.Value["labels"] != null)
                        {
                            foreach (var one in oneAccount.Value["labels"] as JArray)
                            {
                                labels.Add(new AddLabelViewModel()
                                {
                                    id = int.Parse(one["id"].ToString()),
                                    name = one["name"].ToString(),
                                    type = one["type"].ToString()
                                });
                            }
                        }
                        datasetList.Add(new DatasetViewModel
                        { dataSetId = oneAccount.Key, 
                            Name = oneAccount.Value["name"].ToString(),Info = oneAccount.Value["info"].ToString(),
                            Type = oneAccount.Value["type"].ToString(),Role = "admin",
                            Labels = labels
                        });
                    }
                }
            }else
            {
                var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", WebUIConfig.membershipFile);
                var json = await configBlob.DownloadGenericObjectAsync();
                var accounts = JsonUtils.GetJToken("dataSets", json) as JObject;
                var datasets = JsonUtils.GetJToken(convertProjectId, accounts) as JArray;
                if (datasets != null)
                {
                    foreach (var datasetId in datasets)
                    {
                        var infoObj =await AzureService.FindDatasetInfo(convertProjectId, datasetId.ToString());
                        if (infoObj!=null)
                        {
                            List<AddLabelViewModel> labels = new List<AddLabelViewModel>();
                            if (infoObj["labels"] != null)
                            {
                                foreach (var one in infoObj["labels"] as JArray)
                                {
                                    labels.Add(new AddLabelViewModel()
                                    {
                                        id = int.Parse(one["id"].ToString()),
                                        name = one["name"].ToString(),
                                        type = one["type"].ToString()
                                    });
                                }
                            }
                            datasetList.Add(new DatasetViewModel
                            {
                                dataSetId = datasetId.ToString(),
                                Name = infoObj["name"].ToString(),
                                Info = infoObj["info"].ToString(),
                                Type = infoObj["type"].ToString(),
                                Role = "labeler",
                                Labels = labels
                            });
                        }
                    }
                }
            }
            var list = PageOps.GetPageRange(datasetList, page, size, datasetList.Count);
            return Ok(new Response().GetJObject("datasets", JToken.FromObject(list),"totalCount", datasetList.Count));
        }
        /// <remarks>
        /// 为project添加数据集,需name、info和type字段，datasetId可选
        /// 关于datasetId字段，如果已存在azure blob上的GUID，则需填写，否则无需填写，将新生成
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="AddDatasetViewModel">字典，包含name\info\type\labels</param>
        [HttpPost]
        public async Task<IActionResult> AddDataset(Guid projectId,[FromBody]AddDatasetViewModel dataSetViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response { Successful = "true", Msg = ModelState.Values.ToString(), Data = null });
            }
            var convertProjectId = projectId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin"&&!await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
            {
                return StatusCode(403);
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var dataSetId = dataSetViewModel.dataSetId.ToString().ToUpper();
            if (dataSetId == "00000000-0000-0000-0000-000000000000")
            {
                dataSetId = Guid.NewGuid().ToString().ToUpper();
            }
            var newObj = new JObject();
            newObj.Add("name", dataSetViewModel.Name);
            newObj.Add("type", dataSetViewModel.Type);
            newObj.Add("info", dataSetViewModel.Info);
            newObj.Add("labels", JToken.FromObject(await AzureService.UpdateLabelInfoToAzure(dataSetViewModel.Labels)));
            if (json == null)
            {
                var obj = new JObject();
                var DataSetObj = new JObject();
                DataSetObj.Add(dataSetId, newObj);
                obj.Add("dataSets", DataSetObj);
                await accountBlob.UploadGenericObjectAsync(obj);
            }
            else
            {
                var res = Json.AddValueToJObject(new []{"dataSets", dataSetId}, json, newObj);
                if (res)
                {
                    await accountBlob.UploadGenericObjectAsync(json);
                }
            }
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 查询一个特定的dataset详细信息
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataSetId的GUid</param>
        [HttpGet("{dataSetId}")]
        public async Task<IActionResult> getDatasetInfo(Guid projectId, Guid dataSetId)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
            {
                var res = await AzureService.CheckUserHasThisDataset(userId, convertProjectId, convertDataSetId);
                if (!res)
                {
                    return StatusCode(403);
                }
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);
            var obj = JsonUtils.GetJToken(convertDataSetId, allAccounts) as JObject;
            return Ok(new Response().GetJObject("info", obj));
        }
        /// <remarks>
        /// 修改一个特定的dataset
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataSetId的GUid</param>
        /// <param name="dataSetViewModel">新的name\info\type\labels字段,json格式</param>
        [HttpPatch("{dataSetId}")]
        public async Task<IActionResult> UpdateDataset(Guid projectId, Guid dataSetId, [FromBody]AddDatasetViewModel dataSetViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response { Successful = "true", Msg = ModelState.Values.ToString(), Data = null });
            }
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
            {
                return StatusCode(403);
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);
            var obj = JsonUtils.GetJToken(convertDataSetId, allAccounts) as JObject;
            if (obj != null)
            {
                obj["name"] = dataSetViewModel.Name;
                obj["info"] = dataSetViewModel.Info;
                obj["type"] = dataSetViewModel.Type;
                obj["labels"] = JToken.FromObject(await AzureService.UpdateLabelInfoToAzure(dataSetViewModel.Labels));
                await accountBlob.UploadGenericObjectAsync(json);
            }
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 为project删除数据集
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">将要删除dataset的GUid</param>
        [HttpDelete]
        public async Task<IActionResult> RemoveDataSet(Guid projectId,[FromBody] Guid dataSetId)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
            {
                return StatusCode(403);
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);
            var AccountArray = allAccounts == null ? null : allAccounts as JObject;
            if (!Object.ReferenceEquals(AccountArray, null))
            {
                foreach (var oneclaim in AccountArray)
                {
                    if (String.Compare(oneclaim.Key, convertDataSetId, true) == 0)
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
                                var accArray = JsonUtils.GetJToken(convertProjectId, dataSetObj) as JArray;
                                if (accArray != null)
                                {
                                    foreach (var one in accArray)
                                    {
                                        if (one.ToString() == convertDataSetId)
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
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 获取project下特定数据集的标注用户列表
        /// 返回用户信息列表
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="page">当前第几页，从1开始递增</param>
        /// <param name="size">每页的数量</param>
        [HttpGet("{datasetId}/users")]
        public async Task<IActionResult> GetDataSetUsers(Guid projectId, Guid dataSetId, [FromQuery]int page, [FromQuery]int size)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
            {
                return StatusCode(403);
            }
            List<JObject> userList = new List<JObject>();
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var datasetObj = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(convertDataSetId, datasetObj) as JObject;
            var userIdList = JsonUtils.GetJToken("users", datasetInfo) as JArray;
            if (userIdList != null)
            {
                foreach (var userId in userIdList)
                {
                    JObject userInfo =await AzureService.FindUserInfo(userId.ToString());
                    if (userInfo == null)
                    {
                        userIdList.Remove(userId);
                        await accountBlob.UploadGenericObjectAsync(json);
                        break;
                    }
                    userList.Add(userInfo);
                }
            }
            var list = PageOps.GetPageRange(userList, page, size, userList.Count);
            return Ok(new Response().GetJObject("users", JToken.FromObject(list), "totalCount", userList.Count));
        }
        /// <remarks>
        /// 为project下特定数据集删除指定的标注用户
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="userNumber">将要删除的用户唯一标识数字</param>
        [HttpDelete("{datasetId}/users")]
        public async Task<IActionResult> RemoveUser(Guid projectId, Guid dataSetId,[FromBody]int userNumber)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
            {
                return StatusCode(403);
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var datasets = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(convertDataSetId, datasets) as JObject;
            var userIdList = JsonUtils.GetJToken("users", datasetInfo) as JArray;
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            if (!Object.ReferenceEquals(userIdList, null))
            {
                foreach (var one in userIdList)
                {
                    if (String.Compare(one.ToString(), userId, true) == 0)
                    {
                        userIdList.Remove(one);
                        await accountBlob.UploadGenericObjectAsync(json);
                        HttpContext.Session.Remove($"user_{userId}_tasks_list");
                        HttpContext.Session.Remove($"user_{userId}_task_{convertDataSetId}_list");
                        HttpContext.Session.Remove($"user_{userId}_task_{convertDataSetId}_permission");
                        break;
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
                    if (one.Key == convertProjectId)
                    {
                        var dataSetArray = one.Value as JArray;
                        foreach (var o in dataSetArray)
                        {
                            if (o.ToString() == convertDataSetId)
                            {
                                dataSetArray.Remove(o);
                                await blob.UploadGenericObjectAsync(userJson);
                                break;
                            }
                        }
                    }
                }
            }
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 为project下特定数据集添加指定number的标注用户
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="userNumber">用户唯一标识数字</param>
        [HttpPost("{datasetId}/users")]
        public async Task<IActionResult> AddUserToDataSet(Guid projectId, Guid dataSetId,[FromBody]int userNumber)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
            {
                return StatusCode(403);
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            if (json == null)
            {
                return Ok(new Response() {Msg = "not dataset"});
            }
            var datasets = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(convertDataSetId, datasets) as JObject;
            if (datasetInfo == null)
            {
                return Ok(new Response() { Msg = "not datasetId" });
            }
            var userIdList = JsonUtils.GetJToken("users", datasetInfo) as JArray;
            if (userIdList == null)
            {
                datasetInfo.Add("users", new JArray() {userId});
                await accountBlob.UploadGenericObjectAsync(json);
            }
            else
            {
                if (!Json.ContainsKey(userId,userIdList))
                {
                    userIdList.Add(userId);
                    await accountBlob.UploadGenericObjectAsync(json);
                }
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            if (Object.ReferenceEquals(userJson, null))
            {
                await blob.UploadGenericObjectAsync(new JObject{{ "dataSets",new JObject{ { convertProjectId, new JArray { convertDataSetId } } }}});
            }
            else
            {
                var res = Json.AddValueToJArray(new string[] {"dataSets", convertProjectId}, userJson, convertDataSetId);
                if (res)
                {
                    await blob.UploadGenericObjectAsync(userJson);
                }
                HttpContext.Session.Remove($"user_{userId}_tasks_list");
                HttpContext.Session.Remove($"user_{userId}_task_{convertDataSetId}_list");
                HttpContext.Session.Remove($"user_{userId}_task_{convertDataSetId}_permission");
            }
            return Ok(new Response() {Msg = "ok"});
        }
        /// <remarks>
        /// 检测指定number的标注用户是否已经存在于project下特定数据集
        /// user already exists!表示用户已经存在于数据集，Cannot find userId!表示找不到该用户number，ok表示可以添加
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="userNumber">用户唯一标识数字</param>
        [HttpGet("{datasetId}/users/{userNumber}")]
        public async Task<IActionResult> CheckUserExists(Guid projectId, Guid dataSetId,int userNumber)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
            {
                return StatusCode(403);
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            if (userId == null)
            {
                return Ok(new Response { Msg = "Cannot find userId!" });
            }
            var datasets = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(convertDataSetId, datasets) as JObject;
            var userIdList = JsonUtils.GetJToken("users", datasetInfo) as JArray;
            if (userIdList != null)
            {
                if (Json.ContainsKey(userId, userIdList))
                {
                    return Ok(new Response { Msg = "user already exists!" });
                }
            }
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 获取数据集的可标注任务列表,包含已修改task+一个锁定的task
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="page">当前第几页，从1开始递增</param>
        /// <param name="size">每页的数量</param>
        [HttpGet("{datasetId}/tasks")]
        public async Task<IActionResult> getTasks(Guid projectId, Guid dataSetId, [FromQuery]int page, [FromQuery]int size)
        {
            var userId = HttpContext.User.Identity.Name;
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
            {
                var res = await AzureService.CheckUserHasThisDataset(userId, convertProjectId, convertDataSetId);
                if (!res)
                {
                    return StatusCode(403);
                }
                var taskList = await AzureService.getDatasetTaskList(userId, convertProjectId, convertDataSetId);
                return Ok(new Response().GetJObject("taskList", JToken.FromObject(taskList)));
            }
            await AzureService.GenerateCommitJsonFile(convertProjectId, convertDataSetId);
            var taskBlob = AzureService.GetBlob("cdn", "private", null, null, $"tasks/{convertDataSetId}", "commit.json");
            var taskJson = await taskBlob.DownloadGenericObjectAsync();
            var lockObj = JsonUtils.GetJToken(convertProjectId, taskJson) as JObject;
            List<JObject> adminTaskList = new List<JObject>();
            if (lockObj != null)
            {
                foreach (var one in lockObj)
                {
                    adminTaskList.Add(new JObject(){{"id",one.Key}, {"status",one.Value["status"]}, { "userId", one.Value["userId"] } });
                }
            }
            var list = PageOps.GetPageRange(adminTaskList, page, size, adminTaskList.Count);
            return Ok(new Response().GetJObject("taskList", JToken.FromObject(list), "totalCount", adminTaskList.Count));
        }
        /// <remarks>
        /// 获取下一个可标注任务
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        [HttpGet("{datasetId}/tasks/next/{taskId}")]
        public async Task<IActionResult> GetNextTask(Guid projectId, Guid dataSetId,string taskId)
        {
            var userId = HttpContext.User.Identity.Name;
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
            {
                var has = await AzureService.CheckLabelerHasThisTask(userId, convertProjectId, convertDataSetId, taskId);
                if (!has)
                {
                    return StatusCode(403);
                }
            }
            JObject nextObj = await AzureService.getDatasetTaskNext(userId, convertProjectId, convertDataSetId, taskId);
            return Ok(new Response().GetJObject("next", JToken.FromObject(nextObj)));
        }
        /// <remarks>
        /// 获取详细标注信息annotations
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="taskId">task的id</param>
        [HttpGet("{datasetId}/tasks/annotations/{taskId}")]
        public async Task<IActionResult> GetOneTask(Guid projectId, Guid dataSetId,string taskId)
        {
            var userId = HttpContext.User.Identity.Name;
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
            {
                var has = await AzureService.CheckLabelerHasThisTask(userId, convertProjectId, convertDataSetId,taskId);
                if (!has)
                {
                    return StatusCode(403);
                }
            }
            var blob = AzureService.GetBlob(null, $"tasks/{convertDataSetId}/annotations", $"{taskId}.json");
            var json = await blob.DownloadGenericObjectAsync();
            var projectObj = JsonUtils.GetJToken(convertProjectId, json) as JObject;
            return Ok(new Response().GetJObject("annotations", projectObj==null?null:JToken.FromObject(projectObj)));
        }
        /// <remarks>
        /// 提交标注信息annotations
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="taskId">task的id</param>
        /// <param name="value">标注信息，json格式</param>
        [HttpPost("{datasetId}/tasks/annotations/{taskId}")]
        public async Task<IActionResult> Post(Guid projectId, Guid dataSetId, string taskId, [FromBody] JObject value)
        {
            var userId = HttpContext.User.Identity.Name;
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
            {
                var has = await AzureService.CheckLabelerHasThisTask(userId, convertProjectId, convertDataSetId,taskId);
                if (!has)
                {
                    return StatusCode(403);
                }
            }
            var blob = AzureService.GetBlob(null, $"tasks/{convertDataSetId}/annotations", $"{taskId}.json");
            var json = await blob.DownloadGenericObjectAsync();
            var res = await AzureService.setTaskStatusToCommited(userId, convertProjectId, convertDataSetId, taskId);
            if (json == null)
            {
                if (res||role=="admin")
                {
                    await blob.UploadGenericObjectAsync(new JObject() { { convertProjectId, value } });
                }
            }
            else
            {
                if (res || role == "admin")
                {
                    json[convertProjectId] = value;
                    await blob.UploadGenericObjectAsync(new JObject() { { convertProjectId, value } });
                }
            }
            return Content(new Response {Msg = "ok"}.JObjectToString());
        }
    }
}
