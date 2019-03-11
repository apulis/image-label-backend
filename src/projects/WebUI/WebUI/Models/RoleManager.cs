using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Utils.Json;
using WebUI.Azure;
using WebUI.Utils;

namespace WebUI.Models
{
    public class RoleManager
    {
        public static RoleManager Current = new RoleManager(); 
        public async Task<string> FindRole( IdentityUser user)
        {
            var configAuthorization = Config.App.GetJToken(Constants.JsontagAuthorization) as JObject;
            // Additional authentication from cloud
            var container = CloudStorage.GetContainer(null);
            var dirPath = container.GetDirectoryReference("index");
            var authBlob = dirPath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
            var json = await authBlob.DownloadGenericObjectAsync();
            var addAuth = JsonUtils.GetJToken(Constants.JsontagAuthorization, json);
            var addAuthObj = addAuth == null ? null : addAuth as JObject; 
            if ( !Object.ReferenceEquals(addAuthObj, null ) )
            {
                configAuthorization.Merge(addAuthObj, new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Union
                });
            }
            if ( !Object.ReferenceEquals(configAuthorization, null) )
            {
                // Console.WriteLine($"Check Authorization of {user.Email} against {configAuthorization}");
                foreach( var pair in configAuthorization)
                {
                    var peopleArray = pair.Value as JArray;
                    foreach( var onepeople in peopleArray)
                    {
                        if ( String.Compare(onepeople.ToString(), user.Email, true)==0)
                        {
                            // Console.WriteLine($"{user.Email} is authorized as {pair.Key}");
                            return pair.Key; 
                        }
                    }
                }
            }
            // Console.WriteLine($"{user.Email} is unauthorized.");

            return null; 
        }
    }
}
