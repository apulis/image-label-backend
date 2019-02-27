using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Utils.Json;
using WebUI.Models;

namespace WebUI.Utils
{
    public class AuthenticationSetting
    {
        public static bool AllowLocal = false;
    }

    public class Config
    {
        public static Config General = null; // for config.json 
        public static Config App = null; // for configApp.json

        public JObject Obj = null;

        public Config( String filename )
        {
            var configFile = WebUIConfig.GetConfigFile(filename);
            using (var stream = new FileStream(configFile, FileMode.Open))
            {
                Obj = JsonUtils.Read(stream);
            }
        }

        public JToken GetJToken(string entryname)
        {
            return JsonUtils.GetJToken(entryname, Obj);
        }

    }
}
