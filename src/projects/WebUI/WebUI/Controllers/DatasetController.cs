﻿using System;
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
    [Route("api/projects/{projectId}/datasets")]
    [ApiController]
    public class DatasetController:ControllerBase
    {
        /// <remarks>
        /// 获取当前用户的projectId对应的project下的所属数据集
        /// 返回List,数据集列表
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        [HttpGet]
        public async Task<IActionResult> GetDatasets(string projectId)
        {
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            List<DatasetViewModel> datasetList = new List<DatasetViewModel>();
            if (role == "admin"|| await AzureService.FindUserIsProjectManager(userId, projectId))
            {
                var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
                var accJson = await accountBlob.DownloadGenericObjectAsync();
                var allAccounts = JsonUtils.GetJToken("dataSets", accJson) as JObject;
                if (allAccounts != null)
                {
                    foreach (var oneAccount in allAccounts)
                    {
                        datasetList.Add(new DatasetViewModel
                        { dataSetId = oneAccount.Key, 
                            Name = oneAccount.Value["name"].ToString(),Info = oneAccount.Value["info"].ToString(),
                            Type = oneAccount.Value["type"].ToString(),Role = "admin"
                        });
                    }
                }
            }else
            {
                var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", WebUIConfig.membershipFile);
                var json = await configBlob.DownloadGenericObjectAsync();
                var accounts = JsonUtils.GetJToken("dataSets", json) as JObject;
                var datasets = JsonUtils.GetJToken(projectId, accounts) as JArray;
                if (datasets != null)
                {
                    foreach (var datasetId in datasets)
                    {
                        var infoObj =await AzureService.FindDatasetInfo(projectId, datasetId.ToString());
                        datasetList.Add(new DatasetViewModel
                        {
                            dataSetId = datasetId.ToString(),
                            Name = infoObj["name"].ToString(),
                            Info = infoObj["info"].ToString(),
                            Type = infoObj["type"].ToString(),
                            Role = "user"});
                    }
                }
            }
            return Ok(new Response().GetJObject("datasets", JToken.FromObject(datasetList)));
        }
        /// <remarks>
        /// 为project添加数据集,需name、info和type字段，datasetId可选
        /// 关于datasetId字段，如果已存在azure blob上的GUID，则需填写，否则无需填写，将新生成
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        [HttpPost]
        public async Task<IActionResult> AddDataset(string projectId,[FromBody]AddDatasetViewModel dataSetViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response { Successful = "true", Msg = ModelState.Values.ToString(), Data = null });
            }
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin"&&!await AzureService.FindUserIsProjectManager(currentUserId, projectId))
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var dataSetId = dataSetViewModel.dataSetId ?? Guid.NewGuid().ToString().ToUpper();
            var newObj = new JObject();
            newObj.Add("name", dataSetViewModel.Name);
            newObj.Add("type", dataSetViewModel.Type);
            newObj.Add("info", dataSetViewModel.Info);
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
                var allDatasets = JsonUtils.GetJToken("dataSets", json) as JObject;
                if (allDatasets == null)
                {
                    json.Add("dataSets", new JObject() {{dataSetId, newObj}});
                    await accountBlob.UploadGenericObjectAsync(json);
                }
                else
                {
                    var infoObj = JsonUtils.GetJToken(dataSetId, allDatasets);
                    if (infoObj == null)
                    {
                        allDatasets.Add(dataSetId, newObj);
                        await accountBlob.UploadGenericObjectAsync(json);
                    }
                }
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
        public async Task<IActionResult> RemoveDataSet(string projectId,[FromBody] string dataSetId)
        {
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, projectId))
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
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
                                var accArray = JsonUtils.GetJToken(projectId, dataSetObj) as JArray;
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
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 获取project下特定数据集的标注用户列表
        /// 返回用户信息列表
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        [HttpGet("{datasetId}/users")]
        public async Task<IActionResult> GetDataSetUsers(string projectId, string dataSetId)
        {
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, projectId))
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            List<JObject> userList = new List<JObject>();
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var datasetObj = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(dataSetId, datasetObj) as JObject;
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

            var a = JToken.FromObject(userList);
            var b = new JArray(userList);
            return Ok(new Response().GetJObject("users", JToken.FromObject(userList)));
        }
        /// <remarks>
        /// 为project下特定数据集删除指定的标注用户
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="userNumber">将要删除的用户唯一标识数字</param>
        [HttpDelete("{datasetId}/users")]
        public async Task<IActionResult> RemoveUser(string projectId, string dataSetId,[FromBody]int userNumber)
        {
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, projectId))
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var datasets = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(dataSetId, datasets) as JObject;
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
                        HttpContext.Session.Remove($"user_{userId}_task_{dataSetId}_list");
                        HttpContext.Session.Remove($"user_{userId}_task_{dataSetId}_permission");
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
                    if (one.Key == projectId)
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
        public async Task<IActionResult> AddUserToDataSet(string projectId, string dataSetId,[FromBody]int userNumber)
        {
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, projectId))
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            if (json == null)
            {
                return Ok(new Response() {Msg = "not dataset"});
            }
            var datasets = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(dataSetId, datasets) as JObject;
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
                await blob.UploadGenericObjectAsync(new JObject{{ "dataSets",new JObject{ { projectId, new JArray { dataSetId } } }}});
            }
            else
            {
                var dataSets = JsonUtils.GetJToken("dataSets", userJson) as JObject;
                if (Object.ReferenceEquals(dataSets, null))
                {
                    userJson.Add("dataSets", new JObject {{ projectId, new JArray { dataSetId } } });
                    await blob.UploadGenericObjectAsync(userJson);
                }
                else
                {
                    var dataSetArray = JsonUtils.GetJToken(projectId, dataSets) as JArray;
                    if (Object.ReferenceEquals(dataSetArray, null))
                    {
                        dataSets.Add(projectId, new JArray() { dataSetId });
                        await blob.UploadGenericObjectAsync(userJson);
                    }
                    else
                    {
                        if (!Json.ContainsKey(dataSetId,dataSetArray))
                        {
                            dataSetArray.Add(dataSetId);
                            await blob.UploadGenericObjectAsync(userJson);
                        }
                    }
                }
                HttpContext.Session.Remove($"user_{userId}_tasks_list");
                HttpContext.Session.Remove($"user_{userId}_task_{dataSetId}_list");
                HttpContext.Session.Remove($"user_{userId}_task_{dataSetId}_permission");
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
        public async Task<IActionResult> CheckUserExists(string projectId, string dataSetId,int userNumber)
        {
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, projectId))
            {
                return Ok(new Response { Msg = "You don't have access!" });
            }
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            if (userId == null)
            {
                return Ok(new Response { Msg = "Cannot find userId!" });
            }
            var datasets = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(dataSetId, datasets) as JObject;
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
    }
}
