using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks; 
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Models;
using Common.Utils;

namespace WebUI.Azure
{
    // We design the interface so that it has similiar interface to CloudBlobContainer
    // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.storage.blob.cloudblobcontainer?view=azure-dotnet
    // But this class is multicloud enabled. 
    public class BlobContainer {
        public CloudProvider Provider; 
        internal BlobContainer(CloudProvider inProvider)
        {
            Provider = inProvider; 
        }
        public virtual BlobDirectory GetDirectoryReference( string path )
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"GetDirectoryReference hasn't been implemented for BlobContainer {typeName}"); 
        }
        public virtual BlockBlob GetBlockBlobReference( string path )
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"GetBlockBlobReference hasn't been implemented for BlobContainer {typeName}"); 
        }
        
    }
    public class BlobDirectory {
        public CloudProvider Provider;
        internal BlobDirectory(CloudProvider inProvider)
        {
            Provider = inProvider;
        }
        public virtual BlobDirectory[] GetBlobDirectoriesForProviders() {
            return new BlobDirectory[] { this };
        }

        public virtual Uri[] StorageUri()
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"StorageUri hasn't been implemented for BlobDirectory {typeName}");
        }
        public virtual BlobDirectory GetDirectoryReference( string path )
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"GetDirectoryReference hasn't been implemented for BlobDirectory {typeName}"); 
        }

        public virtual BlockBlob GetBlockBlobReference( string path )
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"GetBlockBlobReference hasn't been implemented for BlobDirectory {typeName}"); 
        }

        public virtual CustomizedBlobContinuationToken NewBlobContinuationToken() {
            var typeName = this.GetType().FullName;
                throw new Exception($"NewBlobContinuationToken hasn't been implemented for BlobDirectory {typeName}"); 
        }

        public virtual List<BlobDirectory> GetAllProviders()
        {
            var lst = new List<BlobDirectory>();
            lst.Add(this);
            return lst;
        }

        public virtual String Name
        {
            get
            {
                var typeName = this.GetType().FullName;
                throw new Exception($"Name hasn't been implemented for BlockBlob {typeName}");
            }
        }

        public virtual Task<CustomizedResultSegment> 
            ListBlobsSegmentedAsync (bool useFlatBlobListing, BlobListingDetails blobListingDetails, 
            Nullable<int> maxResults, CustomizedBlobContinuationToken currentToken, BlobRequestOptions options, 
            OperationContext operationContext)
            {
                var typeName = this.GetType().FullName;
                throw new Exception($"ListBlobsSegmentedAsync hasn't been implemented for BlobDirectory {typeName}"); 
            }  

        public virtual Task<CustomizedResultSegment> 
            ListBlobsSegmentedAsync (CustomizedBlobContinuationToken currentToken)
            {
                var typeName = this.GetType().FullName;
                throw new Exception($"ListBlobsSegmentedAsync hasn't been implemented for BlobDirectory {typeName}"); 
            }
        public virtual Task<IEnumerable<FileInfo>> ListBlobsSegmentedAsync()
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"ListBlobsSegmentedAsync hasn't been implemented for BlobDirectory {typeName}");
        }
        public virtual async Task<Dictionary<string, bool>> GetAllFiles() {
            var allFiles = new Dictionary<string, bool>();
            var continueToken = NewBlobContinuationToken();
            var maxResults = 5000;
            var baseName = Name; 
            if ( !String.IsNullOrEmpty(baseName) && !baseName.EndsWith("/"))
                baseName += "/";
            // Find all images. 
            
            do
            {
                var blobResults = await ListBlobsSegmentedAsync(true, BlobListingDetails.None, maxResults, continueToken,
                    null, null);
                foreach (var entry in blobResults.Results)
                {
                    var blob = entry.ToBlockBlob();
                    if (!Object.ReferenceEquals(blob, null))
                    {
                        var blobName = blob.Name;
                        var idx = blobName.IndexOf(baseName);
                        if ( idx >= 0 )
                        {
                            var partialName = blobName.Substring(idx+baseName.Length);
                            if ( !allFiles.ContainsKey(partialName))
                            {
                                allFiles[partialName] = true; 
                                // Console.WriteLine($"Find {partialName}");
                            } else {
                                Console.WriteLine($"Duplicate {partialName} in {baseName}.");
                            }
                        } else {
                            Console.WriteLine($"Entry {blobName} that is not prefix with {baseName}.");
                        }
                    }
                }
                // logger.LogInformation($"Query return {segCount} images.");
                continueToken = blobResults.ContinuationToken; 
            } while ( continueToken!=null );
            return allFiles;                
        }

        public virtual String GetBaseName(BlobDirectory dir)
        {
            var curName = Name;
            var dirName = dir.Name;
            var idx = 0;

            for (int start_idx = 0; start_idx < dirName.Length; start_idx++)
            {
                var full_idx = curName.IndexOf(dirName.Substring(start_idx), StringComparison.OrdinalIgnoreCase);
                if (full_idx >= 0)
                {
                    idx = full_idx + dirName.Length - start_idx;
                    break;
                }
            }
            if (idx < curName.Length && curName[idx] == '/')
                idx++;
            return curName.Substring(idx);
        }



    }

    public class BlockBlob {
        public virtual List<BlockBlob> GetAllProviders()
        {
            var lst = new List<BlockBlob>();
            lst.Add(this);
            return lst;
        }

        public virtual async Task UploadFromFileAsync(string path)
        {
            using (var filestream = new FileStream(path,
                FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 16384, useAsync: true))
            {
                await UploadFromStreamAsync(filestream);
            }
            return;
        }

        public virtual async Task UploadTextAsync(string info)
        {
            var bytes = Encoding.UTF8.GetBytes(info);

            using (var mem = new MemoryStream(bytes))
            {
                await UploadFromStreamAsync(mem);
            }
            return;
        }

        public virtual async Task UploadTextAsync( string info, string etag )
        {
            var bytes = Encoding.UTF8.GetBytes(info);
            using (var mem = new MemoryStream(bytes))
            {
                await UploadFromStreamAsync(mem, etag);
            }
            return;
        }


        public virtual Task UploadFromStreamAsyncFailIfExist(Stream stream)
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"UploadFromStreamAsyncFailIfExist hasn't been implemented for BlockBlob {typeName}");
        }

        public virtual Task UploadFromStreamAsync(Stream source)
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"UploadFromStreamAsync hasn't been implemented for BlockBlob {typeName}");
        }

        public virtual async Task UploadFromStreamAsync(Stream source, string etag )
        {
            if ( !hasETag || String.IsNullOrEmpty(etag) )
            {
                // For those Cloud Provider that doesn't support etag, etag should always be null.
                await UploadFromStreamAsync(source);
            }
            else {
                var typeName = this.GetType().FullName;
                throw new Exception($"UploadFromStreamAsync hasn't been implemented for BlockBlob {typeName} with etag {etag}");
            }
            return;
        }

        public virtual bool hasETag {
            get {
                return false; 
            }
        }

        public virtual string ETag {
            get {
                // For those Cloud Provider that doesn't support etag, etag should always be null.
                return null; 
            }
        }


        public virtual async Task UploadFromByteArrayAsync(byte[] buffer, int index, int count)
        {
            using (var mem = new MemoryStream(buffer, index, count))
            {
                await UploadFromStreamAsync(mem);
            }
            return;
        }

        public virtual async Task UploadFromByteArrayAsync(byte[] buffer, int index, int count, string etag )
        {
            using (var mem = new MemoryStream(buffer, index, count))
            {
                await UploadFromStreamAsync(mem, etag);
            }

        }

        public virtual async Task<string> DownloadTextAsync()
        {
            // See https://cloud.google.com/appengine/docs/flexible/dotnet/using-cloud-storage
            var mem = new MemoryStream();
            await DownloadToStreamAsync(mem);
            var len = Convert.ToInt32(mem.Length);
            var bytes = mem.GetBuffer();
            var str = Encoding.UTF8.GetString(bytes, 0, len);
            return str;
        }

        public virtual async Task<string> DownloadTextAsyncExceptionNull()
        {
            // See https://cloud.google.com/appengine/docs/flexible/dotnet/using-cloud-storage
            try {
                return await DownloadTextAsync();
            } catch {
                return null;
            }
        }

        public virtual async Task<List<string>> DownloadAllTextAsync()
        {
            // See https://cloud.google.com/appengine/docs/flexible/dotnet/using-cloud-storage
            // Download potentially all text from multiple blobs.
            var lst = new List<string>();
            lst.Add(await DownloadTextAsync());
            return lst;
        }

        public async Task<JObject> DownloadGenericObjectAsync()
        {
            try
            {
                var str = await DownloadTextAsync();
                return JObject.Parse(str);
            }
            catch
            {
                return null;
            }
        }

        public async Task UploadGenericObjectAsync( JObject obj)
        {
            await UploadTextAsync(obj.ToString());
        }

        public virtual async Task<byte[]> DownloadByteArrayAsync()
        {
            var mem = new MemoryStream();
            await DownloadToStreamAsync(mem);
            return mem.ToArray();
        }

        public virtual Task DownloadToStreamAsync(Stream target)
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"DownloadTextAsync hasn't been implemented for BlockBlob {typeName}");
        }

        


        public virtual Task DeleteAsync( bool bCatchException = true ) {
            var typeName = this.GetType().FullName;
            throw new Exception($"DeleteAsync hasn't been implemented for BlockBlob {typeName}"); 
        }

        public virtual String Name {
            get {
                var typeName = this.GetType().FullName;
                throw new Exception($"Name hasn't been implemented for BlockBlob {typeName}");         
            }
        }

        public virtual String GetBaseName(BlobDirectory dir)
        {
            var curName = Name;
            var dirName = dir.Name;
            var idx = 0;

            for ( int start_idx = 0; start_idx < dirName.Length; start_idx++ )
            {
                var full_idx = curName.IndexOf(dirName.Substring(start_idx), StringComparison.OrdinalIgnoreCase);
                if ( full_idx >= 0 ) 
                {
                    idx = full_idx + dirName.Length - start_idx;
                    break;
                } 
            }
            if ( idx < curName.Length && curName[idx] == '/')
                idx++; 
            return curName.Substring( idx ); 
        }

        public virtual Task FetchAttributesAsync()
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"FetchAttributesAsync hasn't been implemented for BlockBlob {typeName}");
        }

        public virtual CustomizedBlobProperties Properties {
            get
            {
                var typeName = this.GetType().FullName;
                throw new Exception($"Properties hasn't been implemented for BlockBlob {typeName}");
            }
        }

    }

    public class CustomizedResultSegment {
        public CloudProvider Provider;
        internal CustomizedResultSegment(CloudProvider inProvider)
        {
            Provider = inProvider;
        }
        public virtual IEnumerable<ICustomizedBlobItem> Results { 
            get {
                var typeName = this.GetType().FullName;
                throw new Exception($"Results hasn't been implemented for CustomizedResultSegment {typeName}"); 
            }
        }
        public virtual CustomizedBlobContinuationToken ContinuationToken {
            get {
                var typeName = this.GetType().FullName;
                throw new Exception($"ContinuationToken hasn't been implemented for CustomizedResultSegment {typeName}");                 
            }
        }

    }

    public class CustomizedBlobContinuationToken {

    }

    public class ICustomizedBlobItem {
        public CloudProvider Provider;
        internal ICustomizedBlobItem(CloudProvider inProvider)
        {
            Provider = inProvider;
        }
        public virtual BlockBlob ToBlockBlob() {
            var typeName = this.GetType().FullName;
            throw new Exception($"ToBlockBlob hasn't been implemented for IBlobItem {typeName}");   
        }
        public virtual BlobDirectory ToBlobDirectory() {
            var typeName = this.GetType().FullName;
            throw new Exception($"ToBlobDirectory hasn't been implemented for IBlobItem {typeName}");   
        }
        public virtual Uri Uri {
            get { 
                var typeName = this.GetType().FullName;
                throw new Exception($"Uri hasn't been implemented for IBlobItem {typeName}");   
            }
        }
    }

    public class CustomizedBlobProperties
    {
        public virtual Nullable<DateTimeOffset> LastModified
        {
            get
            {
                var typeName = this.GetType().FullName;
                throw new Exception($"LastModified hasn't been implemented for IBlobItem {typeName}");
            }
        }
    }

    public class MultiCloud 
    {
        // Return: -1, all entries are consistent. 
        // Return: >=0, this entry is the latest entry, at least some entry not consistent. 
        public static int LatestModified(List<Int64> ticks)
        {
            if (ticks.Count <= 1)
            {
                return -1;
            }
            var maxVal = ticks[0];
            var maxIdx = 0;
            var bInconsistency = false;
            for (int i = 1; i < ticks.Count; i++)
            {
                if (ticks[i] > maxVal)
                {
                    maxVal = ticks[i];
                    maxIdx = i;
                    bInconsistency = true;
                }
                else if (ticks[i] < maxVal)
                {
                    bInconsistency = true;
                }
            }
            if (bInconsistency)
                return maxIdx;
            else
                return -1;
        }

        /// <summary>
        /// Gets the consistent JObject, this is used for JObject that are stored at multiple cloud provider without the hash (for consistency).
        /// A certain  key, usually time, is used to identity the freshness of the JObject, the object with the latest time is considered the latest. 
        /// </summary>
        /// <returns>The consistent JObject, a list of blob that needs to be updated, current blob, and eTag (if exists) </returns>
        /// <param name="dir">directory.</param>
        /// <param name="tag">path .</param>
        public static async Task<Tuple<JObject, List<int>, int, string>> GetConsistentJObject(BlobDirectory dir, String entry, String timeTag = Constant.JSonKeyTime)
        {
            var allDirs = dir.GetAllProviders();
            var allBlobs = allDirs.ConvertAll(x => x.GetBlockBlobReference(entry));
            var allTasks = allBlobs.ConvertAll(x => x.DownloadTextAsync());
            try
            {
                await Task.WhenAll(allTasks);
            } catch {};

            List<Tuple<String, int>> curStrs = new List<Tuple<string, int>>();
            for (int i = 0; i < allBlobs.Count; i++  )
            {
                var task = allTasks[i];
                var curBlob = allBlobs[i];
                String str = null;
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    str = task.Result;
                }
                curStrs.Add(new Tuple<String,int>(str, i));
            };
            // Nothing 
            if (curStrs.Count <= 0)
                return new Tuple<JObject, List<int>, int, string>(new JObject(), null, -1, null);
            var curObjs = curStrs.ConvertAll(x => JsonUtils.TryParse(x.Item1));
            var modTicks = curObjs.ConvertAll(x => JsonUtils.GetType<Int64>(timeTag, x, 0L));
            // Console.WriteLine($"Consistency object {entry} ticks: {String.Join( ',', modTicks.ConvertAll(x => x.ToString()) )}");
            var bestObj = LatestModified(modTicks);
            if (bestObj < 0)
                bestObj = 0; 
            var retObj = curObjs[bestObj];
            var retIdx = curStrs[bestObj].Item2;
            var eTag = allBlobs[retIdx].ETag;
            var lst = new List<int>();
            for (int i = 0; i < modTicks.Count; i++)
            {
                if (modTicks[i] < modTicks[bestObj])
                {
                    lst.Add(curStrs[i].Item2);
                }
            }

            if (lst.Count <= 0)
                lst = null; 

            return new Tuple<JObject, List<int>, int, string>(retObj, lst, retIdx, eTag);
        }

        /// <summary>
        /// Downloads and sync one entry (base on timestamp across multiple cloud provider).
        /// </summary>
        /// <returns>A Tuple, which contains: 1) one entry, 2) index of the current entry, 3) eTag </returns>
        /// <param name="dir">Dir.</param>
        /// <param name="entry">Entry.</param>
        /// <param name="statuses">Statuses.</param>
        /// <param name="timeTag">Time tag.</param>
        public static async Task<Tuple<JObject, int, string> > DownloadAndSyncOneEntry(BlobDirectory dir, String entry, List<int> statuses, String timeTag = Constant.JSonKeyTime)
        {
            var tuple = await GetConsistentJObject(dir, entry, timeTag);
            await SyncOneEntryObject(tuple, dir, entry, statuses);
            return new Tuple<JObject, int, string>(tuple.Item1, tuple.Item3, tuple.Item4);
        }


        private static async Task SyncOneEntryObject(
            Tuple<JObject, List<int>, int, string> tuple,
            BlobDirectory dir, String entry, List<int> statuses)
        {
            if (!Object.ReferenceEquals(tuple.Item2, null) && tuple.Item2.Count > 0)
            {
                var dirProviders = dir.GetAllProviders();
                if (Object.ReferenceEquals(statuses, null))
                {
                    statuses = dirProviders.ConvertAll(x => 0);
                }

                var content = tuple.Item1.ToString();
                foreach (var i in tuple.Item2)
                {
                    try
                    {
                        if (statuses[i] >= 0)
                        {
                            var item = dirProviders[i].GetBlockBlobReference(entry);
                            await item.UploadTextAsync(content);
                            Console.WriteLine($"Sync {dir.Name}/{entry} for {dirProviders[i].Provider.Name}");
                            statuses[i] = 1;
                        }
                    }
                    catch
                    {
                        var provider = dirProviders[i].Provider.Name;
                        Console.WriteLine($"Sync {entry} failed, the provider {provider} is not operational");
                        statuses[i] = -1;
                    }
                }
            }
        }
    }

}