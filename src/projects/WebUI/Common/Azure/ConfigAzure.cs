using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Utils.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using WebUI.Models;

namespace WebUI.Azure
{
    public class ConfigCloud : AzureCloudProvider
    {
        public static ConfigCloud Current = new ConfigCloud();

        public ConfigCloud() : 
            base( WebUIConfig.GetConfigFile(WebUIConfig.AzureConfigFile), null )
            {

            }
    }
}
