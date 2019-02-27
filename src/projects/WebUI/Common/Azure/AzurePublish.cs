using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebUI.Models;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text.RegularExpressions;

namespace WebUI.Azure
{
    public class CloudPublish
    {
        public static async Task<bool> UploadConfig( String filename, String loc, String provider, bool bExecute, ILogger log)
        {
            var configAppFile = WebUIConfig.GetConfigFile(filename);
            return await UploadFile(configAppFile, "config/" + filename, loc, provider, bExecute, log);
        }

        public static async Task<bool> UploadFolder(String srcFolder, String dstFolder, String location, String provider, bool bExecute, ILogger log)
        {
            return await UploadFolder(srcFolder, dstFolder, "cdn", "public", location, provider, bExecute, log);
        }

        public static async Task<bool> UploadFolder(String srcFolder, String dstFolder, String bucket, String container, String location, String provider, bool bExecute, ILogger log)
        {
            srcFolder = srcFolder.Trim();
            dstFolder = dstFolder.Trim();
            if (dstFolder.StartsWith("/"))
            {
                dstFolder = dstFolder.Substring(1, dstFolder.Length-1);
            }
            if (dstFolder.EndsWith("/"))
            {
                dstFolder = dstFolder.Substring(0, dstFolder.Length - 1);
            }
            // log.LogInformation($"DstFolder == {dstFolder}");
            var srcPath = Path.GetFullPath(srcFolder);
            
            foreach (var dir in Directory.GetDirectories(srcFolder))
            {
                var basename = dir.Substring(srcPath.Length);
                var newDstFolder = dstFolder + "/" + basename;
                if ( dstFolder.EndsWith("/") || basename.StartsWith("/"))
                {
                    newDstFolder = dstFolder + basename;
                }
                log.LogInformation($"Directory: {dir} --> {newDstFolder}");
                await UploadFolder(dir, newDstFolder, bucket, container, location, provider, bExecute, log);
                // Console.WriteLine($"Directory: {dir}");
            }
            foreach (var file in Directory.GetFiles(srcFolder))
            {
                var basename = file.Substring(srcPath.Length);
                var targetFile = dstFolder + basename;
                await CloudPublish.UploadFile(file, targetFile, bucket, container, location, provider, bExecute, log);
                
            }
            
            return true;
        }

        public static async Task<bool> UploadFile(String localFile, String targetFile, String loc, String provider, bool bExecute, ILogger log)
        {
            return await UploadFile(localFile, targetFile, "cdn", "public", loc, provider, bExecute, log);
        }

        public static async Task<bool> UploadFile(String localFile, String targetFile, String bucket, String path, String loc, String provider, bool bExecute, ILogger log)
        {
            var locations = ConfigCloud.Current.GetLocations();
            if ( !String.IsNullOrEmpty(loc))
            {
                locations = new string[] { loc };
            }
            foreach (var location in locations)
            {
                var container = CloudStorage.GetContainer(bucket, path, location, provider);
                if (Object.ReferenceEquals(container, null))
                {
                    continue;
                }
                var blobReference = container.GetBlockBlobReference(targetFile);
                if (bExecute)
                {
                    await blobReference.UploadFromFileAsync(localFile);
                    log.LogInformation($"File: {localFile} --> {targetFile} @ ({bucket}{location}/{path}), ({provider})");
                }
                else
                {
                    log.LogInformation($"Staging file: {localFile} --> {targetFile} @ ({bucket}{location}/{path}), ({provider})");
                }

            }
            return true;
        }

        public static async Task List( string bucket, string containerPrefix, 
            string path, string location, String provider, bool bUseFlat, ILogger logger)
        {
            logger.LogInformation($"Listing {provider}@{bucket}-{location}, {containerPrefix}/{path}, flat = {bUseFlat}");
            var container = CloudStorage.GetContainer(bucket, containerPrefix, location, provider);
            var dir = container.GetDirectoryReference(path);
            int cnt = 0; 
            int cntdir = 0; 
            if ( !Object.ReferenceEquals(container,null))
            {
                CustomizedBlobContinuationToken token = null;
                do
                {
                    var lst = await dir.ListBlobsSegmentedAsync(bUseFlat, BlobListingDetails.Metadata,
                        null, token, null, null);
                    foreach (var item in lst.Results)
                    {
                        logger.LogInformation($"{item.Uri}");
                        var blockItem = item.ToBlockBlob();
                        if ( !Object.ReferenceEquals(blockItem, null))
                        {
                            cnt ++; 
                        }
                        var dirItem = item.ToBlobDirectory();
                        if ( !Object.ReferenceEquals(dirItem, null))
                        {
                            cntdir++;
                        }
                    };
                    token = lst.ContinuationToken;
                } while (token != null);
            }
            logger.LogInformation($"Total files == {cnt}, dir == {cntdir}");
        }

        public static async Task<bool> Remove( string bucket, string containerPrefix, 
            string path, string location, String provider, String pattern,  bool bUseFlat, ILogger logger)
        {
            logger.LogInformation($"You are about to delete all content at {provider}@{bucket}-{location}, {containerPrefix}/{path}");
            logger.LogInformation($"Please type DELETE in all capital to confirm the operation ----> ");
            if ( String.IsNullOrEmpty(pattern))
                pattern = "";
            var re = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var confirmationString = Console.ReadLine();
            if (confirmationString == "DELETE")
            {
                var container = CloudStorage.GetContainer(bucket, containerPrefix, location, provider);
                var dirs = container.GetDirectoryReference(path);
                foreach( var dir in dirs.GetBlobDirectoriesForProviders() )
                {
                    try {
                        var cnt = 0; 
                        if ( !Object.ReferenceEquals(container,null))
                        {
                            CustomizedBlobContinuationToken token = null;
                            do
                            {
                                var lst = await dir.ListBlobsSegmentedAsync(bUseFlat, BlobListingDetails.Metadata,
                                    null, token, null, null);
                                foreach (var item in lst.Results)
                                {
                                    // logger.LogInformation($"{item.Uri}");
                                    var blockItem = item.ToBlockBlob(); 
                                    if ( !Object.ReferenceEquals(blockItem,null) && re.IsMatch(blockItem.Name))
                                    {
                                        logger.LogInformation($"Delete {blockItem.Name}");
                                        var basename = blockItem.GetBaseName(dir);
                                        var deleteBlob = dirs.GetBlockBlobReference(basename);
                                        cnt ++; 
                                        // This way, we will delete blobs across all providers. 
                                        await deleteBlob.DeleteAsync(false); 
                                    }
                                };
                                token = lst.ContinuationToken;
                            } while (token != null);
                            logger.LogInformation($"# of items to be deleted ==  {cnt}");
                        }
                        
                    } catch 
                    {
                        // Deletion failed
                        return false;
                    }
                }
                return true;
            } else
            {
                return false; 
            }
        }

        public static async Task Copy(string bucket, string containerPrefix, string path, string location, String provider,
            string bucket2, string containerPrefix2, string path2, string location2, String provider2,
            int maxDegreeOfParallelism, 
            ILogger logger)
        {
            logger.LogInformation($"source {provider}@{bucket}-{location}, {containerPrefix}/{path}");
            logger.LogInformation($"destination {provider2}@{bucket2}-{location2}, {containerPrefix2}/{path2}");
            var container = CloudStorage.GetContainer(bucket, containerPrefix, location, provider);
            var dir = container.GetDirectoryReference(path);
            var basename = dir.Name;
            var prefixLength = basename.Length; 
            var container2 = CloudStorage.GetContainer(bucket2, containerPrefix2, location2, provider2);
            var dir2 = container2.GetDirectoryReference(path2);
            if (!Object.ReferenceEquals(container, null))
            {
                CustomizedBlobContinuationToken token = null;
                do
                {
                    var lst = await dir.ListBlobsSegmentedAsync(true, BlobListingDetails.Metadata, null, token, null, null);
                    var opt = new ParallelOptions();
                    opt.MaxDegreeOfParallelism = maxDegreeOfParallelism;
                    foreach( var item in lst.Results)
                    {
                        var blockItem = item.ToBlockBlob();
                        if ( !Object.ReferenceEquals(blockItem,null))
                        { 
                            // No need to deal with directories.
                            var itemname = blockItem.Name.Substring( prefixLength );
                            var destItem = dir2.GetBlockBlobReference(itemname);
                            var bytes = await blockItem.DownloadByteArrayAsync();
                            await destItem.UploadFromByteArrayAsync(bytes, 0, bytes.Length);
                            logger.LogInformation($"Copy item {itemname}");
                        }
                    };
                    token = lst.ContinuationToken;
                } while (token != null);
            }
        }
    }
}
