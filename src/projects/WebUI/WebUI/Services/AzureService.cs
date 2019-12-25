using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Azure;
using WebUI.Models;
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
            if (json.ContainsKey(userId))
            {
                return true;
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

        public static async Task<bool> FindUserHasThisTask(string userId, ISession session, string taskId)
        {
            if (userId == null)
            {
                return false;
            }
            var result = SessionOps.GetSession<string>(session.Get($"user_{userId}_task_{taskId}_permission"));
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
                if (JsonUtils.GetJToken(taskId, tasks.Data) != null)
                {
                    SessionOps.SetSession($"user_{userId}_task_{taskId}_permission", "true", session);
                    return true;
                }
                SessionOps.SetSession($"user_{userId}_task_{taskId}_permission", "false", session);
            }
            return false;
        }

        public static async Task<int> GetNextTaskId(string taskId,string userId)
        {
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

        public static async Task<string> FindUserRole(string userId)
        {
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
            return null;
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
            var datasetObj = JsonUtils.GetJToken("datasets", accJson) as JObject;
            var infoObj = JsonUtils.GetJToken(datasetId, datasetObj) as JObject;
            return infoObj;
        }
    }
}
