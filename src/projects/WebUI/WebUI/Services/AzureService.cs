using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
            var accounts = JsonUtils.GetJToken("accounts", json);
            if (accounts != null)
            {
                var AccountObj = accounts as JArray;
                List<string> list = new List<string>();
                foreach (var account in AccountObj)
                {
                    list.Add(account.ToString());
                }

                return list;
            }

            return null;
        }

        public static async Task CreateUserId(UserInfoViewModel userInfoViewModel,string microsoftId=null)
        {
            var newUserId =await FindUserIdByOpenId(userInfoViewModel.Id);
            if (String.IsNullOrEmpty(newUserId))
            {
                if (userInfoViewModel.LoginType != "microsoft" && microsoftId != null)
                {
                    newUserId = await FindUserIdByOpenId(microsoftId);
                    if (newUserId == null)
                    {
                        return;
                    }
                }
                else
                {
                    newUserId = Guid.NewGuid().ToString().ToUpper();
                }
            }
            var configBlob = GetBlob("cdn", "private", null, null, $"user-login/{userInfoViewModel.Id}", WebUIConfig.mapFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var userId = JsonUtils.GetJToken(Constants.JsontagUserId, json);
            if (userId == null)
            {
                userId = newUserId;
                var obj = new JObject
                {
                    { Constants.JsontagUserId, userId},
                };
                await configBlob.UploadGenericObjectAsync(obj);
            }

            var blob = GetBlob("cdn", "private", null, null, $"user", "list.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            if (userJson == null)
            {
                var obj = new JObject
                {
                    { userId.ToString(), new JObject
                    {
                        {$"{userInfoViewModel.LoginType}Id", userInfoViewModel.Id },
                        {"email", userInfoViewModel.Email},
                        {$"{userInfoViewModel.LoginType}Name", userInfoViewModel.Name}
                    } }
                };
                await blob.UploadGenericObjectAsync(obj);
            }
            else
            {
                var emailF = JsonUtils.GetJToken(userId.ToString(), userJson) as JObject;
                if (emailF == null)
                {
                    if (userInfoViewModel.LoginType != "microsoft")
                    {
                        return;
                    }
                    userJson.Add(userId.ToString(), new JObject
                    {
                        {"email", userInfoViewModel.Email}, {$"{userInfoViewModel.LoginType}Id", userInfoViewModel.Id },
                        {$"{userInfoViewModel.LoginType}Name", userInfoViewModel.Name}
                    });
                    await blob.UploadGenericObjectAsync(userJson);
                }
                else
                {
                    var id = JsonUtils.GetJToken($"{userInfoViewModel.LoginType}Id", emailF);
                    if (id == null)
                    {
                        emailF.Add($"{userInfoViewModel.LoginType}Id", userInfoViewModel.Id);
                        emailF.Add($"{userInfoViewModel.LoginType}Name", userInfoViewModel.Name);
                        await blob.UploadGenericObjectAsync(userJson);
                    }
                }
            }
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
        public static async Task<string> FindUserEmail(string userId)
        {
            var obj = await FindUserInfo(userId);
            if (!Object.ReferenceEquals(obj, null))
            {
                return obj["email"].ToString();
            }
            return null;
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
            if (!Object.ReferenceEquals(obj, null))
            {
                return obj;
            }
            return null;
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
    }
}
