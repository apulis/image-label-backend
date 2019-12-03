using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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

        public static async Task CreateUserId(string openId,string email)
        {
            var newUserId =await FindUserId(email);
            if (String.IsNullOrEmpty(newUserId))
            {
                newUserId = Guid.NewGuid().ToString().ToUpper();
            }
            var configBlob = AzureService.GetBlob("cdn", "private", null, null, $"user-login/{openId}", WebUIConfig.mapFile);
            var json = await configBlob.DownloadGenericObjectAsync();
            var userId = JsonUtils.GetJToken(Constants.JsontagUserId, json);
            if (userId == null)
            {
                userId = newUserId;
                var obj = new JObject
                {
                    { Constants.JsontagUserId, userId},
                    {"email", email}
                };
                await configBlob.UploadGenericObjectAsync(obj);
            }

            var blob = AzureService.GetBlob("cdn", "private", null, null, $"user", "list.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            if (userJson == null)
            {
                var obj = new JObject
                {
                    { userId.ToString(), new JObject(){{"email",email},{"id",openId}} }
                };
                await blob.UploadGenericObjectAsync(obj);
            }
            else
            {
                var emailF = JsonUtils.GetJToken(userId.ToString(), userJson) as JObject;
                if (emailF == null)
                {
                    userJson.Add(userId.ToString(), new JObject() {{"email", email}, {"id", openId}});
                    await blob.UploadGenericObjectAsync(userJson);
                }
            }

            var emailBlob = AzureService.GetBlob("cdn", "private", null, null, $"user", "email.json");
            var emailJson = await emailBlob.DownloadGenericObjectAsync();
            if (emailJson == null)
            {
                var obj = new JObject
                {
                    { email, new JObject(){{"userId", userId.ToString() } } }
                };
                await emailBlob.UploadGenericObjectAsync(obj);
            }
            else
            {
                var emailF = JsonUtils.GetJToken(email, emailJson) as JObject;
                if (emailF == null)
                {
                    emailJson.Add(email, new JObject() { { "userId", userId.ToString() } });
                    await emailBlob.UploadGenericObjectAsync(emailJson);
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
        public static async Task<string> FindUserEmail(string userId)
        {
            var blob = AzureService.GetBlob("cdn", "private", null, null, $"user", "list.json");
            var userJson = await blob.DownloadGenericObjectAsync();
            var obj = JsonUtils.GetJToken(userId, userJson) as JObject;
            string email = null;
            if (!Object.ReferenceEquals(obj, null))
            {
                email = obj["email"].ToString();
            }

            return email;
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
                return new Response{Code = 401,Msg = "Not UserId",Data = tasks};
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
            return new Response { Code = 200, Msg = "ok", Data = tasks };
        }
        public static async Task<Response> FindUserOneTaskInfo(string userId, ISession session,string taskId)
        {
            var content = new JObject();
            if (userId == null)
            {
                return new Response { Code = 401, Msg = "Not UserId", Data = content };
            }
            var result = SessionOps.GetSession<JObject>(session.Get($"user_{userId}_task_{taskId}_list"));
            if (result != null)
            {
                content = result;
            }
            else
            {
                Response re = await FindUserHasThisTask(userId, session, taskId);
                if (re.Code==200)
                {
                    var blob = GetBlob(null, $"tasks/{taskId}", "list.json");
                    var json = await blob.DownloadGenericObjectAsync();
                    if (json != null)
                    {
                        content = json;
                    }
                }
                SessionOps.SetSession($"user_{userId}_task_{taskId}_list", content, session);
            }
            return new Response { Code = 200, Msg = "ok", Data = content };
        }

        public static async Task<Response> FindUserHasThisTask(string userId, ISession session, string taskId)
        {
            int code = 401;
            if (userId == null)
            {
                return new Response { Code = code, Msg = "Not UserId"};
            }
            var result = SessionOps.GetSession<string>(session.Get($"user_{userId}_task_{taskId}_permission"));
            if (result != null)
            {
                if (result == "true")
                {
                    code = 200;
                }
            }
            else
            {
                Response tasks = await FindUserTasks(userId, session);
                if (JsonUtils.GetJToken(taskId, tasks.Data) != null)
                {
                    code = 200;
                    SessionOps.SetSession($"user_{userId}_task_{taskId}_permission", "true", session);
                }
                else
                {
                    SessionOps.SetSession($"user_{userId}_task_{taskId}_permission", "false", session);
                }
            }
            return new Response { Code = code, Msg = "ok" };

        }
    }
}
