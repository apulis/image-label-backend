using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Azure;
using WebUI.Models;
using WebUI.Parameters;
using WebUI.ViewModels;

namespace WebUI.Services
{
    public class AzureService
    {

        public static BlockBlob GetBlob(string location,string dirpath,string blobPath)
        {
            var container = CloudStorage.GetContainer(location);
            var dirPath = container.GetDirectoryReference(dirpath);
            var Blob = dirPath.GetBlockBlobReference(blobPath);
            return Blob;
        }
        public static BlockBlob GetBlob(string storage, string path, string location, String provider, string dirpath, string blobPath)
        {
            var container = CloudStorage.GetContainer(storage,path,location,provider);
            var dirPath = container.GetDirectoryReference(dirpath);
            var Blob = dirPath.GetBlockBlobReference(blobPath);
            return Blob;
        }
        public static BlobDirectory GetDirBlob(string storage, string path, string location, String provider, string dirpath)
        {
            var container = CloudStorage.GetContainer(storage, path, location, provider);
            var dirPath = container.GetDirectoryReference(dirpath);
            return dirPath;
        }
        public static async Task<List<string>> GetUserAccountIdList(string userId)
        {
            var Blob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", WebUIConfig.membershipFile);
            var json = await Blob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken("accounts", json) as JArray;
            List<string> list = new List<string>();
            if (accounts != null)
            {
                foreach (var pair in accounts)
                {
                    list.Add(pair.ToString());
                }
            }
            return list;
        }
        public static async Task<List<string>> GetUserLabelAccountIdList(string userId)
        {
            var Blob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", WebUIConfig.membershipFile);
            var json = await Blob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken("dataSets", json) as JObject;
            List<string> list = new List<string>();
            if (accounts != null)
            {
                foreach (var account in accounts)
                {
                    list.Add(account.Key);
                }
            }
            return list;
        }
        public static async Task<int> GenUserId()
        {
            var blob = GetBlob("cdn", "private", null, null, $"user", "id.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            if (userJson == null)
            {
                await blob.UploadGenericObjectAsync(new JObject{{ "maxId", 1000 } });
                return 1000;
            }
            var maxId = (int)JsonUtils.GetJToken("maxId", userJson);
            await blob.UploadGenericObjectAsync(new JObject { { "maxId", maxId + 1 } });
            return maxId + 1;
        }
        public static async Task<string> CreateUserId(UserInfoViewModel userInfoViewModel)
        {
            var userId =await FindUserIdByOpenId(userInfoViewModel.Id);
            if (string.IsNullOrEmpty(userId))
            {
                userId = Guid.NewGuid().ToString().ToUpper();
                var configBlob = GetBlob("cdn", "private", null, null, $"user-login/{userInfoViewModel.Id}", WebUIConfig.mapFile);
                var obj = new JObject
                {
                    { Constants.JsontagUserId, userId}
                };
                await configBlob.UploadGenericObjectAsync(obj);

                var blob = GetBlob("cdn", "private", null, null, $"user", "list.json");
                var userJson = await blob.DownloadGenericObjectAsync();
                if (userJson == null)
                {
                    var newUserNumber = await GenUserId();
                    var newObj = new JObject
                    {
                        { userId, new JObject
                        {
                            {"id",newUserNumber},
                            {"loginId", userInfoViewModel.Id },
                            {"email", userInfoViewModel.Email},
                            {"name", userInfoViewModel.Name},
                            {"loginType",userInfoViewModel.LoginType },
                            {"externalLoginMessage",new JObject() }
                        } }
                    };
                    await blob.UploadGenericObjectAsync(newObj);
                    var numberBlob = GetBlob("cdn", "private", null, null, $"userNumber/{newUserNumber}", "map.json");
                    await numberBlob.UploadGenericObjectAsync(new JObject{{ "userId", userId } });
                }
                else
                {
                    var emailF = JsonUtils.GetJToken(userId, userJson) as JObject;
                    if (emailF == null)
                    {
                        var newUserNumber = await GenUserId();
                        userJson.Add(userId, new JObject
                        {
                            {"id",newUserNumber},
                            {"loginId", userInfoViewModel.Id },
                            {"email", userInfoViewModel.Email},
                            {"name", userInfoViewModel.Name},
                            {"loginType",userInfoViewModel.LoginType },
                            {"externalLoginMessage",new JObject() }
                        });
                        await blob.UploadGenericObjectAsync(userJson);
                        var numberBlob = GetBlob("cdn", "private", null, null, $"userNumber/{newUserNumber}", "map.json");
                        await numberBlob.UploadGenericObjectAsync(new JObject { { "userId", userId } });
                    }
                }
            }
            return userId;
        }
        public static async Task<string> BindLogin(UserInfoViewModel userInfoViewModel)
        {
            var userId = await FindUserIdByOpenId(userInfoViewModel.BindId);
            if (string.IsNullOrEmpty(userId))
            {
                return "用户要绑定的账号不存在";
            }
            //当前登录方式的对应信息
            var configBlob = GetBlob("cdn", "private", null, null, $"user-login/{userInfoViewModel.Id}", WebUIConfig.mapFile);
            var prevObj =await configBlob.DownloadGenericObjectAsync();
            var prevUserId = JsonUtils.GetJToken(Constants.JsontagUserId, prevObj)?.ToString();
            if (prevUserId == userId)
            {
                return "已经绑定过了";
            }
            var obj = new JObject
            {
                { Constants.JsontagUserId, userId}
            };
            await configBlob.UploadGenericObjectAsync(obj);

            var blob = GetBlob("cdn", "private", null, null, $"user", "list.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            var emailF = JsonUtils.GetJToken(userId, userJson) as JObject;
            if (emailF == null)
            {
                return "用户要绑定的账号不存在";
            }
            var currObj = JsonUtils.GetJToken("externalLoginMessage", emailF) as JObject;
            if (prevUserId == null)
            {
                currObj.Add(
                    userInfoViewModel.LoginType, new JObject()
                    {
                        {"id", await GenUserId()},
                        {"loginId", userInfoViewModel.Id},
                        {"email", userInfoViewModel.Email},
                        {"name", userInfoViewModel.Name},
                        {"prevUserId",prevUserId }
                    });
            }
            else
            {
                var prevUserObj = JsonUtils.GetJToken(prevUserId, userJson) as JObject;
                currObj.Add(userInfoViewModel.LoginType,new JObject()
                {
                    {"id",prevUserObj["id"]},
                    {"loginId", prevUserObj["loginId"]},
                    {"email", prevUserObj["email"]},
                    {"name",prevUserObj["name"]},
                    {"prevUserId",prevUserId }
                });
                var prevLoginMsg = JsonUtils.GetJToken("externalLoginMessage", prevUserObj) as JObject;
                foreach (var one in prevLoginMsg)
                {
                    currObj.Add(one.Key, one.Value as JObject);
                }
            }
            await blob.UploadGenericObjectAsync(userJson);
            return "绑定成功";
        }
        public static async Task<string> FindUserId(IdentityUser user)
        {
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user-login/{user.Id}", WebUIConfig.mapFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var userId = JsonUtils.GetJToken(Constants.JsontagUserId, json);
            return (string)userId;
        }
        public static async Task<string> FindUserIdByOpenId(string openId)
        {
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user-login/{openId}", WebUIConfig.mapFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var userId = JsonUtils.GetJToken(Constants.JsontagUserId, json);
            return (string)userId;
        }

        public static async Task<string> FindUserIdByNumber(int userNumber)
        {
            var numberBlob = GetBlob("cdn", "private", null, null, $"userNumber/{userNumber}", "map.json");
            var json = await numberBlob.DownloadGenericObjectAsync();
            var userId = JsonUtils.GetJToken(Constants.JsontagUserId, json);
            return (string)userId;
        }
        public static async Task<string> FindUserEmail(string userId)
        {
            var obj = await FindUserInfo(userId);
            if (!Object.ReferenceEquals(obj, null))
            {
                return obj["email"].ToString();
            }
            return null;
        }

        public static async Task<bool> CheckUserIdExists(string userId)
        {
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"user", "list.json");
            var json = await blob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(json, null))
            {
                if (json.ContainsKey(userId))
                {
                    return true;
                }
            }
            return false;
        }
        public static async Task<string> FindUserId(string email)
        {
            var emailBlob = AzureService.GetBlob("cdn", "private", null, null, $"user", "email.json");
            var emailJson = await emailBlob.DownloadGenericObjectAsync();
            var emailF = JsonUtils.GetJToken(email, emailJson) as JObject;
            if (emailF != null)
            {
                return emailF["userId"].ToString();
            }
            return null;
        }

        public static async Task<string> FindAccountName(string accountId)
        {
            string Name = null;
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var accounts = await configBlob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(accounts, null))
            {
                foreach (var one in accounts)
                {
                    if (one.Key == accountId)
                    {
                        Name = one.Value["name"].ToString();
                        break;
                    }
                }
            }
            return Name;
        }

        public static async Task<JObject> FindUserInfo(string userId)
        {
            var blob = GetBlob("cdn", "private", null, null, $"user", "list.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            var obj = JsonUtils.GetJToken(userId, userJson) as JObject;
            return obj;
        }
        public static async Task<JToken> FindDataSetInfo(string accountId,string dataSetId)
        {
            var blob = GetBlob("cdn", "private", null, null, $"account/{accountId}", "membership.json");
            var json = await blob.DownloadGenericObjectAsync();
            var dataSets = JsonUtils.GetJToken("dataSets", json);
            if (!Object.ReferenceEquals(dataSets, null))
            {
                var dataSetObj = dataSets as JObject;
                foreach (var one in dataSetObj)
                {
                    if (one.Key == dataSetId)
                    {
                        return one.Value;
                    }
                }
            }
            return null;
        }

        public static async Task<Response> FindUserTasks(string userId, ISession session)
        {
            JObject tasks = new JObject();
            if (userId == null)
            {
                return new Response{Successful = "false",Msg = "Not UserId",Data = tasks};
            }
            var result = SessionOps.GetSession<JObject>(session.Get($"user_{userId}_tasks_list"));
            if (result != null)
            {
                tasks = result;
            }
            else
            {
                var blob = GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
                var json = await blob.DownloadGenericObjectAsync();
                var dataSets = JsonUtils.GetJToken("dataSets", json);

                if (!Object.ReferenceEquals(dataSets, null))
                {
                    var dataSetObj = dataSets as JObject;
                    foreach (var account in dataSetObj)
                    {
                        var dataSetArray = account.Value as JArray;
                        foreach (var one in dataSetArray)
                        {
                            tasks.Add(one.ToString(),new JObject()
                            {
                                {"info",await FindDataSetInfo(account.Key,one.ToString())}
                            });
                        }
                    }
                }
                SessionOps.SetSession($"user_{userId}_tasks_list", tasks, session);
            }
            return new Response { Successful = "true", Msg = "ok", Data = tasks };
        }
        public static async Task<Response> FindUserOneTaskInfo(string userId, ISession session,string taskId)
        {
            var content = new JObject();
            if (userId == null)
            {
                return new Response { Successful = "false", Msg = "Not UserId", Data = content };
            }
            var result = SessionOps.GetSession<JObject>(session.Get($"user_{userId}_task_{taskId}_list"));
            if (result != null)
            {
                content = result;
            }
            else
            {
                bool re = await FindUserHasThisTask(userId, session, taskId);
                if (re)
                {
                    var blob = GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
                    var json = await blob.DownloadGenericObjectAsync();
                    var logs = Json.GetJToken("commitLog", json);
                    var taskLog = Json.GetJToken(taskId, logs) as JArray;
                    List<int> idList = new List<int>();
                    if (taskLog != null)
                    {
                        foreach (var one in taskLog)
                        {
                            var oneObj = one as JObject;
                            idList.Add(int.Parse(oneObj["id"].ToString()));
                        }
                    }

                    int nextTaskId =await GetNextTaskId(taskId, userId);
                    if (nextTaskId != 0)
                    {
                        idList.Add(nextTaskId);
                    }
                    content = new JObject { { "ImgIDs", JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(idList)) } };
                }
                SessionOps.SetSession($"user_{userId}_task_{taskId}_list", content, session);
            }
            return new Response { Successful = "true", Msg = "ok", Data = content };
        }

        public static async Task<bool> FindUserHasThisTask(string userId, ISession session,string datasetId)
        {
            if (userId == null)
            {
                return false;
            }
            var result = SessionOps.GetSession<string>(session.Get($"user_{userId}_task_{datasetId}_permission"));
            if (result != null)
            {
                if (result == "true")
                {
                    return true;
                }
            }
            else
            {
                Response tasks = await FindUserTasks(userId, session);
                if (JsonUtils.GetJToken(datasetId, tasks.Data) != null)
                {
                    SessionOps.SetSession($"user_{userId}_task_{datasetId}_permission", "true", session);
                    return true;
                }
                SessionOps.SetSession($"user_{userId}_task_{datasetId}_permission", "false", session);
            }
            return false;
        }

        public static async Task<int> GetNextTaskId(string taskId,string userId)
        {
            //wait repair,wrong path
            var blob = GetBlob("cdn", "private", null, null, $"tasks/{taskId}", "commit.json");
            var json = await blob.DownloadGenericObjectAsync() as JObject;
            if (!Object.ReferenceEquals(json, null))
            {
                foreach (var pair in json)
                {
                    var one = pair.Value as JObject;
                    var status = Json.GetJToken("status", one).ToString();
                    var getUserId = Json.GetJToken("userId", one).ToString();
                    if (status == "normal")
                    {
                        await UpdateTaskStatus(one,"lock", userId);
                        await blob.UploadGenericObjectAsync(json);
                        return int.Parse(pair.Key);
                    }
                    else if(status == "lock" && getUserId == userId)
                    {
                        return int.Parse(pair.Key);
                    }
                }
            }

            return 0;
        }

        public static async Task UpdateTaskStatus(JObject obj,string targetStatus,string userId=null)
        {
            obj["status"] = targetStatus;
            if (userId != null)
            {
                obj["userId"] = userId;
            }
        }

        public static async Task UpdateTaskStatusToBlob(string task_id,int id,string userId, string targetStatus)
        {
            var taskBlob = AzureService.GetBlob("cdn", "private", null, null, $"tasks/{task_id}", "commit.json");
            var taskJson = await taskBlob.DownloadGenericObjectAsync() as JObject;
            var taskObj = Json.GetJToken(id.ToString(), taskJson) as JObject;
            await UpdateTaskStatus(taskObj, targetStatus, userId);
            await taskBlob.UploadGenericObjectAsync(taskJson);
        }

        public static bool IsAdmin(JObject jObject)
        {
            if (!Object.ReferenceEquals(Json.GetJToken("currentRole", jObject), null))
            {
                JArray roleList = Json.GetJToken("currentRole", jObject) as JArray;
                foreach (var one in roleList)
                {
                    if (one.ToString() == "System Admin")
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public static async Task<string> FindUserRole(string userId)
        {
            var configuration = Startup.Configuration;
            string baseUrl = configuration["userDashboardUrl"];
            //string token = configuration["userDashboardToken"];
            string token = JwtService.GenerateToken(userId);
            JObject resJObject = JObject.Parse(await Requests.Get(baseUrl + "/auth/currentUser",
                new Dictionary<string, string>() { ["Authorization"] = $"Bearer {token}" }));
            return AzureService.IsAdmin(resJObject) ? "admin" : "user";

            var taskBlob = AzureService.GetBlob("cdn", "private", null, null, "user", "role.json");
            var taskJson = await taskBlob.DownloadGenericObjectAsync() as JObject;
            if (!Object.ReferenceEquals(taskJson, null))
            {
                foreach (var pair in taskJson)
                {
                    var peopleArray = pair.Value as JArray;
                    foreach (var onepeople in peopleArray)
                    {
                        if (String.Compare(onepeople.ToString(), userId, true) == 0)
                        {
                            return pair.Key;
                        }
                    }
                }
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, "user", "list.json");
            var json = await blob.DownloadGenericObjectAsync() as JObject;
            var info = JsonUtils.GetJToken(userId, json) as JObject;
            var email = JsonUtils.GetJToken("email", info).ToString();
            if (email != null)
            {
                if (email.EndsWith("@apulis.com"))
                {
                    return "admin";
                }
            }
            return null;
        }
        public static async Task<bool> AddUserToAdmin(string userId)
        {
            var taskBlob = AzureService.GetBlob("cdn", "private", null, null, "user", "role.json");
            var taskJson = await taskBlob.DownloadGenericObjectAsync() as JObject;
            var admins = JsonUtils.GetJToken("admin", taskJson) as JArray;
            if (!Object.ReferenceEquals(admins, null))
            {
                if (Json.ContainsKey(userId, admins))
                {
                    return false;
                }
                admins.Add(userId);
                await taskBlob.UploadGenericObjectAsync(taskJson);
            }
            else
            {
                await taskBlob.UploadGenericObjectAsync(new JObject() { { "admin", new JArray() { userId} } });
            }
            return true;
        }
        public static async Task<bool> FindUserIsProjectManager(string userId, string projectId)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
            var accJson = await accountBlob.DownloadGenericObjectAsync();
            var admins = JsonUtils.GetJToken("admin", accJson) as JArray;
            if (admins != null)
            {
                if (Json.ContainsKey(userId,admins))
                {
                    return true;
                }
            }
            return false;
        }
        public static async Task<JObject> FindDatasetInfo(string projectId, string datasetId)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{projectId}", "membership.json");
            var accJson = await accountBlob.DownloadGenericObjectAsync();
            var datasetObj = JsonUtils.GetJToken("dataSets", accJson) as JObject;
            var infoObj = JsonUtils.GetJToken(datasetId, datasetObj) as JObject;
            return infoObj;
        }

        public static async Task<List<ProjectViewModel>> FindUserRoleDetail(string userId)
        {
            var role = await AzureService.FindUserRole(userId);
            List<ProjectViewModel> accounts = new List<ProjectViewModel>();
            List<string> accountList = new List<string>();
            List<string> labelAccountList = new List<string>();
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var allAccounts = await accountBlob.DownloadGenericObjectAsync();
            if (allAccounts != null)
            {
                accountList = await AzureService.GetUserAccountIdList(userId);
                labelAccountList = await AzureService.GetUserLabelAccountIdList(userId);
                foreach (var oneAccount in allAccounts)
                {
                    var oneObj = oneAccount.Value as JObject;
                    if (role == "admin")
                    {
                        accounts.Add(new ProjectViewModel
                            { ProjectId = oneAccount.Key, Name = oneObj["name"].ToString(), Info = oneObj["info"].ToString(), Role = "admin" });
                    }
                    else
                    {
                        if (accountList != null)
                        {
                            if (accountList.Contains(oneAccount.Key))
                            {
                                accounts.Add(new ProjectViewModel()
                                    { ProjectId = oneAccount.Key, Name = oneObj["name"].ToString(), Info = oneObj["info"].ToString(), Role = "manager" });
                            }
                        }
                        if (labelAccountList.Contains(oneAccount.Key) && accountList != null && !accountList.Contains(oneAccount.Key))
                        {
                            accounts.Add(new ProjectViewModel
                                { ProjectId = oneAccount.Key, Name = oneObj["name"].ToString(), Info = oneObj["info"].ToString(), Role = "labeler" });
                        }
                    }
                }
            }
            return accounts;
        }

        public static async Task<List<AddLabelViewModel>> UpdateLabelInfoToAzure(List<AddLabelViewModel> lables)
        {
            var blob = GetBlob("cdn", "private", null, null, "categories", "meta.json");
            var json = await blob.DownloadGenericObjectAsync();
            var obj = JsonUtils.GetJToken("categories", json) as JArray;
            if (obj == null)
            {
                obj = new JArray();
            }
            bool isExists = false;
            int max_id = 0;
            foreach (var one in obj)
            {
                if (max_id < int.Parse(one["id"].ToString()))
                {
                    max_id = int.Parse(one["id"].ToString());
                }
            }
            foreach (var label in lables)
            {
                isExists = false;
                foreach (var one in obj)
                {
                    if (one["name"].ToString()==label.name)
                    {
                        isExists = true;
                        break;
                    }
                }
                if (!isExists)
                {
                    max_id += 1;
                    obj.Add(new JObject(){{"id",max_id},{"name",label.name},{"supercategory", label.supercategory} });
                    if (json == null)
                    {
                        await blob.UploadGenericObjectAsync(new JObject(){{ "categories",obj } });
                    }
                    else
                    {
                        await blob.UploadGenericObjectAsync(json);
                    }
                    
                    label.id = max_id;
                }
            }
            return lables;
        }

        public static async Task<JObject> getDatasetTaskNext(string userId, string projectId, string dataSetId,string taskId=null)
        {
            var blob = GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
            var json = await blob.DownloadGenericObjectAsync();
            var lockObj = JsonUtils.GetJToken("lockLog", json) as JObject;
            var projectLockObj = JsonUtils.GetJToken(projectId, lockObj) as JObject;
            var datasetObj = JsonUtils.GetJToken(dataSetId, projectLockObj) as JObject;
            var role = await AzureService.FindUserRole(userId);
            if (taskId != null)
            {
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, projectId))
                {
                    var commitObj = JsonUtils.GetJToken("commitLog", json) as JObject;
                    var commitLockObj = JsonUtils.GetJToken(projectId, commitObj) as JObject;
                    var commitDatasetObj = JsonUtils.GetJToken(dataSetId, commitLockObj) as JObject;
                    var taskObj = JsonUtils.GetJToken(taskId, commitDatasetObj) as JObject;
                    if (taskObj == null)
                    {
                        return null;
                    }
                    var keysList = commitDatasetObj.Properties().Select(p => p.Name).ToList();
                    var index = keysList.IndexOf(taskId);
                    if (index != keysList.Count - 1)
                    {
                        var obj = Json.GetJToken(keysList[index + 1], commitDatasetObj) as JObject;
                        return new JObject(){{"id", keysList[index + 1] },{ "createTime", obj["createTime"] },{ "updateTime",obj["updateTime"] } };
                    }
                }
            }
            if (datasetObj == null)
            {
                await AzureService.GenerateCommitJsonFile(projectId, dataSetId);
                var taskBlob = GetBlob("cdn", "private", null, null, $"tasks/{dataSetId}/{projectId}", "commit.json");
                var projectObj = await taskBlob.DownloadGenericObjectAsync() as JObject;
                if (projectObj != null)
                {
                    if (role == "admin" || await AzureService.FindUserIsProjectManager(userId, projectId))
                    {
                        var keysList = projectObj.Properties().Select(p => p.Name).ToList();
                        if (taskId == null)
                        {
                            return new JObject(){{"id", keysList[0] },{ "createTime",null },{ "updateTime",null } };
                        }
                        var index = keysList.IndexOf(taskId);
                        return new JObject() { { "id", keysList[index+1] }, { "createTime", null }, { "updateTime", null } };
                    }
                    foreach (var pair in projectObj)
                    {
                        var info = pair.Value as JObject;
                        if (info["status"].ToString() == "normal")
                        {
                            info["status"] = "lock";
                            info["userId"] = userId;
                            JObject obj = new JObject()
                            {
                                {"id",pair.Key },
                                {"createTime",TimeOps.GetCurrentTimeStamp()},
                                {"updateTime",null }
                            };
                            var res = Json.AddValueToJObject(new string[] {"lockLog", projectId, dataSetId}, json, obj);
                            if (res)
                            {
                                await blob.UploadGenericObjectAsync(json);
                            }
                            await taskBlob.UploadGenericObjectAsync(projectObj);
                            return obj;
                        }
                    }
                }
                return null;
            }
            return datasetObj;
        }
        public static async Task<List<JObject>> getDatasetTaskList(string userId, string projectId, string dataSetId)
        {
            List<JObject> taskList = new List<JObject>();
            var blob = GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
            var json = await blob.DownloadGenericObjectAsync();
            var commitObj = JsonUtils.GetJToken("commitLog", json) as JObject;
            var projectObj = JsonUtils.GetJToken(projectId, commitObj) as JObject;
            var datasetListObj = JsonUtils.GetJToken(dataSetId, projectObj) as JObject;
            if (datasetListObj != null)
            {
                foreach (var one in datasetListObj)
                {
                    var value = one.Value as JObject;
                    taskList.Add(new JObject(){{"id", one.Key},{"createTime", value["createTime"]},{"updateTime", value["updateTime"] }});
                }
            }
            var datasetObj =await getDatasetTaskNext(userId,projectId,dataSetId);
            if (datasetObj != null)
            {
                taskList.Add(datasetObj);
            }
            return taskList;
        }

        public static async Task<bool> setTaskStatusToCommited(string userId, string projectId, string dataSetId,string taskId,List<int> categoryIds,string role)
        {
            var taskBlob = GetBlob("cdn", "private", null, null, $"tasks/{dataSetId}/{projectId}", "commit.json");
            var projectObj = await taskBlob.DownloadGenericObjectAsync() as JObject;
            var taskObj = JsonUtils.GetJToken(taskId, projectObj) as JObject;
            if (taskObj == null)
            {
                return false;
            }
            var status = JsonUtils.GetJToken("status", taskObj).ToString();
            if (status != "normal" || role=="admin")
            {
                taskObj["status"] = "commited";
                taskObj["userId"] = userId;
                taskObj["categoryIds"] = categoryIds==null?null:JToken.FromObject(categoryIds);
                await taskBlob.UploadGenericObjectAsync(projectObj);
            }
            var blob = GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
            var json = await blob.DownloadGenericObjectAsync();
            var lockObj = JsonUtils.GetJToken("lockLog", json) as JObject;
            var projectLockObj = JsonUtils.GetJToken(projectId, lockObj) as JObject;
            var datasetObj = JsonUtils.GetJToken(dataSetId, projectLockObj) as JObject;

            var commitObj = JsonUtils.GetJToken("commitLog", json) as JObject;
            var projectCommitObj = JsonUtils.GetJToken(projectId, commitObj) as JObject;
            var datasetCommitObj = JsonUtils.GetJToken(dataSetId, projectCommitObj) as JObject;
            if (datasetObj != null)
            {
                if (datasetObj["id"].ToString() == taskId)
                {
                    json.Remove("lockLog");
                    projectLockObj[taskId] = null;
                    var obj = new JObject()
                    {
                        {"createTime", TimeOps.GetCurrentTimeStamp()},
                        {"updateTime", TimeOps.GetCurrentTimeStamp()}
                    };
                    Json.AddValueToJObject(new string[]{"commitLog",projectId,dataSetId,taskId},json,obj);
                    await blob.UploadGenericObjectAsync(json);
                    return true;
                }
            }
            if (datasetCommitObj == null)
            {
                return false;
            }
            var comObj = JsonUtils.GetJToken(taskId, datasetCommitObj) as JObject;
            comObj["updateTime"] = TimeOps.GetCurrentTimeStamp();
            await blob.UploadGenericObjectAsync(json);
            return true;
        }

        public static async Task GenerateListJsonFile(string projectId, string dataSetId)
        {
            var blob = GetBlob("cdn", "public", null, null, $"tasks/{dataSetId}", "list.json");
            var json = await blob.DownloadGenericObjectAsync();
            var obj = JsonUtils.GetJToken("ImgIDs", json) as JArray;
            if (obj != null)
            {
                return;
            }
            JObject listJObject = new JObject();
            JArray idListArray = new JArray();
            var dirBlob = GetDirBlob("cdn", "public", null, null, $"tasks/{dataSetId}/images");
            IEnumerable<string> allFiles = await dirBlob.ListBlobsSegmentedAsync();
            foreach (var oneFile in allFiles)
            {
                idListArray.Add(Path.GetFileNameWithoutExtension(oneFile));
            }
            listJObject.Add("ImgIDs",idListArray);
            await blob.UploadGenericObjectAsync(listJObject);
        }
        public static async Task<JObject> GenerateCommitJsonFile(string projectId, string dataSetId)
        {
            var taskBlob = GetBlob("cdn", "private", null, null, $"tasks/{dataSetId}/{projectId}", "commit.json");
            var taskJson = await taskBlob.DownloadGenericObjectAsync() as JObject;
            if (taskJson != null)
            {
                return null;
            }
            var blob = GetBlob("cdn", "public", null, null, $"tasks/{dataSetId}", "list.json");
            var json = await blob.DownloadGenericObjectAsync();
            var obj = JsonUtils.GetJToken("ImgIDs", json) as JArray;
            if (obj == null)
            {
                return null;
            }
            JObject idObj = new JObject();
            foreach (var one in obj)
            {
                idObj.Add(one.ToString(),new JObject(){{"status","normal"},{"userId",null}});
            }
            if (taskJson == null)
            {
                await taskBlob.UploadGenericObjectAsync(idObj);
            }
            else
            {
                taskJson.Add(projectId, idObj);
                await taskBlob.UploadGenericObjectAsync(taskJson);
            }
            return idObj;
        }
        public static async Task<bool> CheckUserHasThisDataset(string userId, string projectId, string datasetId)
        {
            var blob = GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
            var json = await blob.DownloadGenericObjectAsync();
            var projects = JsonUtils.GetJToken("dataSets", json);
            var dataSets = JsonUtils.GetJToken(projectId, projects) as JArray;
            if (dataSets != null)
            {
                if (Json.ContainsKey(datasetId, dataSets))
                {
                    return true;
                }
            }
            return false;
        }

        public static async Task<bool> CheckLabelerHasThisTask(string userId, string projectId, string datasetId,string taskId)
        {
            if (!await CheckUserHasThisDataset(userId, projectId, datasetId))
            {
                return false;
            }
            var blob = GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
            var json = await blob.DownloadGenericObjectAsync();
            var lockId = JsonUtils.GetJToken(new string[]{"lockLog",projectId,datasetId,"id"}, json);
            if (lockId!=null&&lockId.ToString() == taskId)
            {
                return true;
            }
            var commitObj = JsonUtils.GetJToken(new string[] { "commitLog", projectId, datasetId,taskId}, json);
            if (commitObj != null)
            {
                return true;
            }
            return false;
        }

        public static async Task<List<AddLabelViewModel>> FindDatasetCategoryIds(string convertProjectId,string convertDatasetId)
        {
            List<AddLabelViewModel> labels = new List<AddLabelViewModel>();
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"tasks/{convertDatasetId}/{convertProjectId}", "category.json");
            var obj = await blob.DownloadGenericObjectAsync();
            var array = Json.GetJToken("categories", obj) as JArray;
            if (array != null)
            {
                foreach (var one in array)
                {
                    labels.Add(new AddLabelViewModel()
                    {
                        id = int.Parse(one["id"].ToString()),
                        name = Json.GetJToken("name", one) == null ? null : Json.GetJToken("name", one).ToString(),
                        type = Json.GetJToken("type", one) == null ? null : Json.GetJToken("type", one).ToString(),
                        supercategory = Json.GetJToken("supercategory", one) == null ? null : Json.GetJToken("supercategory", one).ToString(),
                    });
                }
            }
            
            return labels;
        }
        public static async Task<List<DatasetViewModel>> getDatasets(string userId, string convertProjectId,string role)
        {
            List<DatasetViewModel> datasetList = new List<DatasetViewModel>();
            if (role == "admin" || await AzureService.FindUserIsProjectManager(userId, convertProjectId))
            {
                var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
                var accJson = await accountBlob.DownloadGenericObjectAsync();
                var allAccounts = Json.GetJToken("dataSets", accJson) as JObject;
                if (allAccounts != null)
                {
                    foreach (var oneAccount in allAccounts)
                    {
                        List<AddLabelViewModel> labels = new List<AddLabelViewModel>();
                        labels = await FindDatasetCategoryIds(convertProjectId, oneAccount.Key);
                        datasetList.Add(new DatasetViewModel
                        {
                            dataSetId = oneAccount.Key,
                            Name = oneAccount.Value["name"].ToString(),
                            Info = oneAccount.Value["info"].ToString(),
                            Type = oneAccount.Value["type"].ToString(),
                            Role = "admin",
                            Labels = labels
                        });
                    }
                }
            }
            else
            {
                var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", WebUIConfig.membershipFile);
                var json = await configBlob.DownloadGenericObjectAsync();
                var accounts = JsonUtils.GetJToken("dataSets", json) as JObject;
                var datasets = JsonUtils.GetJToken(convertProjectId, accounts) as JArray;
                if (datasets != null)
                {
                    foreach (var datasetId in datasets)
                    {
                        var infoObj = await AzureService.FindDatasetInfo(convertProjectId, datasetId.ToString());
                        if (infoObj != null)
                        {
                            List<AddLabelViewModel> labels = new List<AddLabelViewModel>();
                            labels = await FindDatasetCategoryIds(convertProjectId, datasetId.ToString());
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
            return datasetList;
        }

        public static async Task AddDatasetLabels(string convertProjectId,string convertDatasetId, List<AddLabelViewModel> labels)
        {
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"tasks/{convertDatasetId}/{convertProjectId}", "category.json");
            await blob.UploadGenericObjectAsync(new JObject(){{ "categories", labels!=null?JArray.FromObject(labels):new JArray()}});
        }
        public static async Task AddDataset(string convertProjectId, AddDatasetViewModel dataSetViewModel)
        {
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
            newObj.Add("dataSetBindId", dataSetViewModel.dataSetBindId);
            newObj.Add("dataSetPath", dataSetViewModel.dataSetPath);
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
                var res = Json.AddValueToJObject(new[] { "dataSets", dataSetId }, json, newObj);
                if (res)
                {
                    await accountBlob.UploadGenericObjectAsync(json);
                }
            }

            if (dataSetViewModel.Labels != null)
            {
                await AddDatasetLabels(convertProjectId, dataSetId, dataSetViewModel.Labels);
            }

            await LinkDataset(dataSetViewModel.dataSetPath, dataSetId);

        }
        public static async Task LinkDataset(string dataPath, string convertDataSetId)
        {
            //$"tasks/{convertDataSetId}/images";
            var dirBlob = GetDirBlob("cdn", "public", null, null, $"tasks/{convertDataSetId}/images");
            dirBlob.LinkPath(dataPath);

        }
        public static async Task<JObject> getDatasetInfo(string convertProjectId,string convertDataSetId)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);
            var obj = JsonUtils.GetJToken(convertDataSetId, allAccounts) as JObject;
            obj["labels"] = JToken.FromObject(await FindDatasetCategoryIds(convertProjectId, convertDataSetId));
            return obj;
        }

        public static async Task UpdateDataset(string convertProjectId,string convertDataSetId, AddDatasetViewModel dataSetViewModel)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var allAccounts = JsonUtils.GetJToken("dataSets", json);
            var obj = JsonUtils.GetJToken(convertDataSetId, allAccounts) as JObject;
            if (obj != null)
            {
                obj["name"] = dataSetViewModel.Name;
                obj["info"] = dataSetViewModel.Info;
                obj["type"] = dataSetViewModel.Type;
                obj["dataSetBindId"] = dataSetViewModel.dataSetBindId;
                obj["dataSetPath"] = dataSetViewModel.dataSetPath;
                await accountBlob.UploadGenericObjectAsync(json);
                await AddDatasetLabels(convertProjectId, convertDataSetId, dataSetViewModel.Labels);
            }
        }

        public static async Task RemoveDataSet(string convertProjectId,string convertDataSetId)
        {
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
        }

        public static async Task<List<JObject>> GetDataSetUsers(string convertProjectId,string convertDataSetId)
        {
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
                    JObject userInfo = await AzureService.FindUserInfo(userId.ToString());
                    if (userInfo == null)
                    {
                        userIdList.Remove(userId);
                        await accountBlob.UploadGenericObjectAsync(json);
                        break;
                    }
                    userList.Add(userInfo);
                }
            }

            return userList;
        }

        public static async Task RemoveUser(string convertProjectId, string convertDataSetId, ISession session,int userNumber)
        {
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
                        session.Remove($"user_{userId}_tasks_list");
                        session.Remove($"user_{userId}_task_{convertDataSetId}_list");
                        session.Remove($"user_{userId}_task_{convertDataSetId}_permission");
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
        }

        public static async Task AddUserToDataSet(string convertProjectId, string convertDataSetId,int userNumber,ISession session)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            if (json == null)
            {
                return;
            }
            var datasets = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(convertDataSetId, datasets) as JObject;
            if (datasetInfo == null)
            {
                return;
            }
            var userIdList = JsonUtils.GetJToken("users", datasetInfo) as JArray;
            if (userIdList == null)
            {
                datasetInfo.Add("users", new JArray() { userId });
                await accountBlob.UploadGenericObjectAsync(json);
            }
            else
            {
                if (!Json.ContainsKey(userId, userIdList))
                {
                    userIdList.Add(userId);
                    await accountBlob.UploadGenericObjectAsync(json);
                }
            }
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", "membership.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            if (Object.ReferenceEquals(userJson, null))
            {
                await blob.UploadGenericObjectAsync(new JObject { { "dataSets", new JObject { { convertProjectId, new JArray { convertDataSetId } } } } });
            }
            else
            {
                var res = Json.AddValueToJArray(new string[] { "dataSets", convertProjectId }, userJson, convertDataSetId);
                if (res)
                {
                    await blob.UploadGenericObjectAsync(userJson);
                }
                session.Remove($"user_{userId}_tasks_list");
                session.Remove($"user_{userId}_task_{convertDataSetId}_list");
                session.Remove($"user_{userId}_task_{convertDataSetId}_permission");
            }
        }

        public static async Task<string> CheckUserExists(string convertProjectId, string convertDataSetId, int userNumber)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", "membership.json");
            var json = await accountBlob.DownloadGenericObjectAsync();
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            if (userId == null)
            {
                return "Cannot find userId!";
            }
            var datasets = JsonUtils.GetJToken("dataSets", json) as JObject;
            var datasetInfo = JsonUtils.GetJToken(convertDataSetId, datasets) as JObject;
            var userIdList = JsonUtils.GetJToken("users", datasetInfo) as JArray;
            if (userIdList != null)
            {
                if (Json.ContainsKey(userId, userIdList))
                {
                    return "user already exists!";
                }
            }
            return "";
        }
        public static async Task<List<JObject>> getTasks(string convertProjectId, string convertDataSetId)
        {
            await GenerateListJsonFile(convertProjectId, convertDataSetId);
            await AzureService.GenerateCommitJsonFile(convertProjectId, convertDataSetId);
            var taskBlob = AzureService.GetBlob("cdn", "private", null, null, $"tasks/{convertDataSetId}/{convertProjectId}", "commit.json");
            var lockObj = await taskBlob.DownloadGenericObjectAsync() as JObject;
            List<JObject> adminTaskList = new List<JObject>();
            if (lockObj != null)
            {
                foreach (var one in lockObj)
                {
                    adminTaskList.Add(new JObject() { { "id", one.Key }, { "status", one.Value["status"] }, { "userId", one.Value["userId"] } });
                }
            }
            return adminTaskList;
        }

        public static async Task<JObject> GetOneTask(string convertProjectId,string convertDataSetId, string taskId)
        {
            var blob = AzureService.GetBlob("cdn", "private",null,null, $"tasks/{convertDataSetId}/{convertProjectId}/images", $"{taskId}.json");
            var json = await blob.DownloadGenericObjectAsync();
            return json;
        }

        public static List<int> GetCategoryIdsFromPostData(JObject value)
        {
            List<int> labels=new List<int>();
            if (!Object.ReferenceEquals(value, null))
            {
                var array = JsonUtils.GetJToken("annotations", value) as JArray;
                foreach (var one in array)
                {
                    var obj = one as JObject;
                    var category_id = JsonUtils.GetJToken("category_id", obj).ToString();
                    if (!Object.ReferenceEquals(category_id, null))
                    {
                        labels.Add(int.Parse(category_id));
                    }
                }
            }
            return labels;
        }
        public static async Task PostOneTask(string convertProjectId, string convertDataSetId, string taskId,string userId,string role,JObject value)
        {
            var blob = AzureService.GetBlob("cdn", "private",null,null, $"tasks/{convertDataSetId}/{convertProjectId}/images", $"{taskId}.json");
            List<int> category_ids = GetCategoryIdsFromPostData(value);
            var res = await AzureService.setTaskStatusToCommited(userId, convertProjectId, convertDataSetId, taskId,category_ids, role);
            if (res || role == "admin")
            {
                await blob.UploadGenericObjectAsync(value);
            }
        }

        public static async Task<JArray> GetLabels()
        {
            var blob = AzureService.GetBlob("cdn", "private", null, null, "categories", "meta.json");
            var json = await blob.DownloadGenericObjectAsync();
            var obj = JsonUtils.GetJToken("categories", json) as JArray;
            return obj;
        }

        public static async Task<string> DeleteProject(string convertProjectId,ISession session)
        {
            var accblob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var accjson = await accblob.DownloadGenericObjectAsync();
            var accObj = JsonUtils.GetJToken(convertProjectId, accjson) as JObject;
            if (Object.ReferenceEquals(accObj, null))
            {
                return "The project doesn't exists!";
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
            session.Clear();
            return null;
        }

        public static async Task<string> AddProject(AddProjectViewModel accountViewModel)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var allAccounts = await accountBlob.DownloadGenericObjectAsync();
            string projectId = Guid.NewGuid().ToString().ToUpper();
            if (allAccounts == null)
            {
                var obj = new JObject();
                var accountObj = new JObject
                {
                    {"name", accountViewModel.Name },
                    {"info",accountViewModel.Info }
                };
                obj.Add(projectId, accountObj);
                await accountBlob.UploadGenericObjectAsync(obj);
            }
            else
            {
                var Obj = new JObject
                {
                    {"name", accountViewModel.Name },
                    {"info",accountViewModel.Info }
                };
                allAccounts.Add(projectId, Obj);
                await accountBlob.UploadGenericObjectAsync(allAccounts);
            }
            return projectId;
        }

        public static async Task UpdateProject(string convertProjectId,AddProjectViewModel accountViewModel)
        {
            var accountBlob = AzureService.GetBlob("cdn", "private", null, null, "account", "index.json");
            var allAccounts = await accountBlob.DownloadGenericObjectAsync();
            var accountObj = JsonUtils.GetJToken(convertProjectId, allAccounts) as JObject;
            if (accountObj != null)
            {
                accountObj["name"] = accountViewModel.Name;
                accountObj["info"] = accountViewModel.Info;
                await accountBlob.UploadGenericObjectAsync(allAccounts);
            }
        }

        public static async Task<List<JObject>> GetProjectManagers(string convertProjectId)
        {
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

            return managerList;
        }

        public static async Task<string> CheckProjectManagerExists(string convertProjectId,string userId)
        {
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", WebUIConfig.membershipFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken("admin", json) as JArray;
            if (accounts != null)
            {
                foreach (var one in accounts)
                {
                    if (one.ToString() == userId)
                    {
                        return "manager already exists!";
                    }
                }
            }
            return null;
        }

        public static async Task<string> AddProjectManager(string convertProjectId, List<int> userNumbers)
        {
            List<string> userIdList = new List<string>();
            foreach (var userNumber in userNumbers)
            {
                var userId = await AzureService.FindUserIdByNumber(userNumber);
                if (userId == null)
                {
                    return $"user number {userNumber} wrong!";
                }
                userIdList.Add(userId);
                var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", WebUIConfig.membershipFile);
                var json = await configBlob.DownloadGenericObjectAsync();
                var accounts = JsonUtils.GetJToken("accounts", json) as JArray;
                if (json == null)
                {
                    await configBlob.UploadGenericObjectAsync(new JObject { { "accounts", new JArray { convertProjectId } } });
                }
                else
                {
                    if (accounts == null)
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
                await blob.UploadGenericObjectAsync(new JObject() { { "admin", new JArray { userIdList } } });
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

            return null;
        }
        public static async Task<string> AddProjectManagerByUserId(string convertProjectId, string userId)
        {
            List<string> userIdList = new List<string>();
            userIdList.Add(userId);
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user/{userId}", WebUIConfig.membershipFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var accounts = JsonUtils.GetJToken("accounts", json) as JArray;
            if (json == null)
            {
                await configBlob.UploadGenericObjectAsync(new JObject { { "accounts", new JArray { convertProjectId } } });
            }
            else
            {
                if (accounts == null)
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

            var blob = AzureService.GetBlob("cdn", "private", null, null, $"account/{convertProjectId}", WebUIConfig.membershipFile);
            var accJson = await blob.DownloadGenericObjectAsync();
            if (accJson == null)
            {
                await blob.UploadGenericObjectAsync(new JObject() { { "admin", new JArray { userIdList } } });
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

            return null;
        }

        public static async Task DeleteProjectManager(string convertProjectId, string userId)
        {
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
        }

        public static async Task<JArray> GetDataSetLabels(string convertProjectId, string convertDataSetId)
        {
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"tasks/{convertDataSetId}/{convertProjectId}", $"category.json");
            var obj = await blob.DownloadGenericObjectAsync();
            var array = JsonUtils.GetJToken("categories", obj) as JArray;
            return array;
        }

        public static async Task<JObject> SelectAnnoByIouRange(string convertProjectId, string convertDataSetId,string taskId,QueryStringParameters parameters)
        {
            JObject obj = new JObject();
            var json = await GetSecondDataSetAnnotation(convertProjectId, convertDataSetId, taskId);
            if (parameters.iou_start == 0 && parameters.iou_end == 0)
            {
                return json;
            }
            var array = JsonUtils.GetJToken("annotations", json) as JArray;
            var imagesArray = JsonUtils.GetJToken("images", json) as JArray;
            JArray annoArray = new JArray();
            if (array != null)
            {
                foreach (var one in array)
                {
                    var oneObj = one as JObject;
                    var iou = JsonUtils.GetJToken("iou", oneObj);
                    if (iou != null)
                    {
                        float iou_value = float.Parse(iou.ToString());
                        if (parameters.iou_start != 0.0f && iou_value< parameters.iou_start)
                        {
                            continue;
                        }
                        if (parameters.iou_end != 0.0f && iou_value> parameters.iou_end)
                        {
                            continue;
                        }
                        annoArray.Add(oneObj);
                    }
                }
            }

            if (annoArray != null&& annoArray.Count != 0)
            {
                obj.Add("annotations",annoArray);
                obj.Add("images", imagesArray);
                return obj;
            }
            return null;
        }
        public static async Task<List<string>> GetDataSetBySearch(string convertProjectId,string convertDataSetId,QueryStringParameters parameters)
        {
            List<string> taskIds = new List<string>();
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"tasks/{convertDataSetId}/{convertProjectId}",$"commit.json");
            var tasksList = await blob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(tasksList, null))
            {
                foreach (var pair in tasksList)
                {
                    if (parameters.image_id != null)
                    {
                        if (!pair.Key.Contains(parameters.image_id))
                        {
                            continue;
                        }
                    }
                    var obj = pair.Value as JObject;
                    if (!string.IsNullOrWhiteSpace(parameters.level))
                    {
                        var level = JsonUtils.GetJToken("level", obj);
                        if (!object.ReferenceEquals(level, null))
                        {
                            if (level.ToString().Trim() != parameters.level.Trim())
                            {
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                    var categoryIds = JsonUtils.GetJToken("categoryIds", obj) as JArray;
                    if (categoryIds != null)
                    {
                        List<int> ids = categoryIds.ToObject<List<int>>();
                        if (parameters.category_ids.All(b => ids.Any(a => a == b)))
                        {
                            taskIds.Add(pair.Key);
                        }
                    }
                }
            }
            return taskIds;
        }
        public static async Task<JObject> GetUserList()
        {
            var blob = GetBlob("cdn", "private", null, null, $"user", "list.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            return userJson;
        }
        public static async Task<JObject> GetSecondDataSetAnnotation(string convertProjectId, string convertDataSetId, string taskId)
        {
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"predict/{convertDataSetId}/{convertProjectId}/images", $"{taskId}.json");
            var json = await blob.DownloadGenericObjectAsync();
            return json;
        }

        public static async Task<List<string>> FilterTasksByIOU(List<string> taskIds, QueryStringParameters parameters,string projectId, string dataSetId)
        {
            List<string> newTaskIds = new List<String>();
            if (parameters.iou_start == 0 && parameters.iou_end == 0)
            {
                return taskIds;
            }
            var blob = GetBlob("cdn", "private", null, null, $"tasks/{dataSetId}/{projectId}", "iou.json");
            //var start = parameters.page > 0?(parameters.page - 1) * parameters.size:0;
            //var target = start + parameters.size;
            var obj = await blob.DownloadGenericObjectAsync();
            if (!Object.ReferenceEquals(obj, null))
            {
                foreach (var oneId in taskIds)
                {
                    var oneObj = JsonUtils.GetJToken(oneId, obj) as JObject;
                    if (oneObj != null)
                    {
                        if (parameters.category_ids.Count != 0)
                        {
                            int index = 0;
                            foreach (var category_id in parameters.category_ids)
                            {
                                var iouArray = JsonUtils.GetJToken(category_id.ToString(), oneObj) as JArray;
                                bool flag = false;
                                if (Object.ReferenceEquals(iouArray, null))
                                {
                                    continue;
                                }
                                foreach (var one_iou in iouArray)
                                {
                                    float iou = float.Parse(one_iou.ToString());
                                    if (parameters.iou_start != 0 && parameters.iou_start > iou)
                                    {
                                        continue;
                                    }
                                    if (parameters.iou_end != 0 && parameters.iou_end < iou)
                                    {
                                        continue;
                                    }
                                    index += 1;
                                    break;
                                }
                            }
                            if (index== parameters.category_ids.Count)
                            {
                                newTaskIds.Add(oneId);
                            }
                        }
                        else
                        {
                            bool flag = false;
                            foreach (var pair in oneObj)
                            {
                                var oneArray = pair.Value as JArray;
                                foreach (var one_iou in oneArray)
                                {
                                    float iou = float.Parse(one_iou.ToString());
                                    if (parameters.iou_start != 0 && parameters.iou_start > iou)
                                    {
                                        continue;
                                    }
                                    if (parameters.iou_end != 0 && parameters.iou_end < iou)
                                    {
                                        continue;
                                    }
                                    flag  = true;
                                    break;
                                }

                                if (flag)
                                {
                                    break;
                                }
                            }
                            if (flag)
                            {
                                newTaskIds.Add(oneId);
                            }
                        }
                    }
                }
            }
            return newTaskIds;
        }

        public static async Task<JArray> GetDatasetMap(string convertProjectId, string convertDataSetId)
        {
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"tasks/{convertDataSetId}/{convertProjectId}", $"map.json");
            var json = await blob.DownloadGenericObjectAsync();
            var array = JsonUtils.GetJToken("map", json) as JArray;
            return array;
        }
    }
}
