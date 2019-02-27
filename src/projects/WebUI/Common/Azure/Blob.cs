using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebUI.Utils;
using Utils.Json;

namespace WebUI.Azure
{
    public class Blob
    {
        public static async Task<List<ICustomizedBlobItem>> ListBlobsAsync(BlobDirectory dirInfo)
        {
            var continuationToken = dirInfo.NewBlobContinuationToken();
            List<ICustomizedBlobItem> results = new List<ICustomizedBlobItem>();
            do
            {
                var response = await dirInfo.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = response.ContinuationToken;
                results.AddRange(response.Results);
            }
            while (continuationToken != null);
            return results;
        }

        public static async Task<JObject> FetchBlobs(BlobDirectory dirInfo, JToken metadata, String current, int maxItem = int.MaxValue )
        {
            var entryList = await Blob.ListBlobsAsync(dirInfo);
            var blockList = new List<BlockBlob>();
            // How many items are BlockBlob?
            foreach (var item in entryList)
            {
                if (item.GetType() == typeof(CloudBlockBlob))
                {
                    var blob = item.ToBlockBlob();
                    await blob.FetchAttributesAsync();
                    blockList.Add(blob);
                }
            }
            if (blockList.Count > maxItem && maxItem > 0 )
            {
                // Need to filter. 
                var dicSorted = new SortedDictionary<Int64, BlockBlob>();
                foreach (var item in blockList)
                {
                    var ticks = item.Properties.LastModified.Value.Ticks;
                    if (String.Compare(item.Name, current, true) == 0)
                        ticks = DateTime.MaxValue.Ticks;
                    if (Json.ContainsKey(item.Name, metadata))
                        ticks += TimeSpan.TicksPerDay * 10000; 
                    dicSorted[ticks] = item; 
                }
                blockList.Clear(); 
                foreach( var pair in dicSorted.Reverse() )
                {
                    blockList.Add(pair.Value);
                    if (blockList.Count >= maxItem)
                        break; 
                }
            }
            var retDic = new JObject();
            foreach ( var item in blockList)
            {
                var strValue = await item.DownloadTextAsync();
                var fullpathname = item.Name; 
                var pathes = fullpathname.Split(new Char[] { '/' });
                var lastName = pathes[pathes.Length - 1];
                retDic[lastName] = strValue; 
            }
            return retDic; 
        }

        public static async Task<bool> DeleteBlobs(BlobDirectory dirInfo)
        {
            var continuationToken = dirInfo.NewBlobContinuationToken();
            do
            {
                var response = await dirInfo.ListBlobsSegmentedAsync(continuationToken);
                continuationToken = response.ContinuationToken;
                var results = response.Results;
                foreach (var item in results)
                {
                    var subdir = item.ToBlobDirectory();
                    if (!Object.ReferenceEquals(subdir, null))
                    {
                        await DeleteBlobs(subdir);
                    }
                    else
                    {
                        var file = item.ToBlockBlob();
                        await file.DeleteAsync();
                    }
                }
            }
            while (continuationToken != null);
                return true;
        }
    }
}
