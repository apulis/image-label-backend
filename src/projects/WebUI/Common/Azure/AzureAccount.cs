using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using WebUI.Utils;
using WebUI.Models;

namespace WebUI.Azure
{
    class CloudStorage
    {

        /// <summary>
        /// Get a azure blob container. This function should only be used by publish, as it access 
        /// containers of all location. To access the container of one default location, use
        /// AzureAccount.Container. 
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public static BlobContainer GetContainer( string location, String provider=CloudProvider.Any )
        {
            return CloudStorage.GetContainer("cdn", "public", location, provider);
        }

        public static BlobContainer GetPrivateContainer(string location, String provider = CloudProvider.Any)
        {
            return CloudStorage.GetContainer("journal", "private", location, provider);
        }

        /// <summary>
        /// storage: cdn, journal, local
        /// path: public, private, 
        /// location: westus, eastus, 
        /// Get a azure blob container. This function should only be used by publish, as it access 
        /// containers of all location. To access the container of one default location, use
        /// AzureAccount.Container. 
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        public static BlobContainer GetContainer(string storage, string path, string location, String provider = CloudProvider.Any)
        {
            var curp = CloudProviderSetting.Current.GetProvider( provider );
            return curp.GetContainer( storage, path, location );
            
        }

        /// <summary>
        /// Return a table client. 
        /// </summary>
        /// <returns>The table.</returns>
        /// <param name="storage">Storage.</param>
        /// <param name="tableName">Table name.</param>
        /// <param name="location">Location.</param>
        /// <param name="provider">Provider.</param>
        public static CloudTable GetTable(string tableName, string location, string storage = "local", String provider = CloudProvider.All)
        {
            var curp = CloudProviderSetting.Current.GetProvider(provider);
            return curp.GetTable(storage, tableName, location);

        }
    }
}
