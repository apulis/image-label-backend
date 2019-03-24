using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WebUI.Models;
using Utils.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.WindowsAzure.Storage.Table;
using Common.Utils;

namespace WebUI.Azure
{
    public class AzureCloudProvider : CloudProvider {
        public JObject config = null;
        public Dictionary<String, bool> locations = new Dictionary<String, bool>(StringComparer.OrdinalIgnoreCase);
        public List<String> locationLst = new List<String>();
        public ILogger _logger;
        public List<String> urlEndPoints = new List<String>();

        public AzureCloudProvider( String configFile, ILogger logger )
        {
            _logger = logger; 
            var useConfigFile = configFile;
            // logger.LogInformation($"Azure config file === {useConfigFile}");
            config = Json.Read(useConfigFile);
            if (Object.ReferenceEquals(config, null))
            {
                throw new Exception($"Could configuration file {configFile} is not a json file.");
            }
            var tokens = JsonUtils.GetJToken("azure_cluster.azure_location", config);
            foreach (var pair in JsonUtils.Iterate(tokens))
            {
                locations[pair.Key] = true;
                locationLst.Add(pair.Key);
            }
        }

        public String[] GetLocations()
        {
            return locations.Keys.ToArray();
        }

        public override bool Ready()
        {
            return locations.Count > 0; 
        }

        public String GetBlobEndpoint(string location = null )
        {
            if ( String.IsNullOrEmpty(location))
                location = GetLocations()[0];

            var tokenCluster = JsonUtils.GetJToken("azure_cluster", config);
            var tokenCDN = JsonUtils.GetJToken("cdn", tokenCluster);
            var tokenLocation = JsonUtils.GetJToken(location, tokenCDN);
            var tokenPrimary = JsonUtils.GetJToken("primaryEndpoints", tokenLocation);
            var endpoint = JsonUtils.GetString("blob", tokenPrimary);
            return endpoint; 
        }

        public String GetLocation(string location)
        {
            if (String.IsNullOrEmpty(location))
                return LocalSetting.Current.Location;
            else if (locations.ContainsKey(location))
                return location;
            else
            {
                throw new Exception(String.Format("Location {0} has not been configured.", location));
            }
        }

        public String[] GetStorageUrlEndpoint(string type, string location = null)
        {
            var useLocation = GetLocation(location);
            var urls = new List<String>();
            var primaryEntity = "azure_cluster." + type + "." + useLocation + ".primaryEndpoints.blob";
            var primaryUrl = JsonUtils.GetString(primaryEntity, config);
            if ( !String.IsNullOrEmpty(primaryUrl))
            {
                urls.Add(primaryUrl);
            }
            var secondaryEntity = "azure_cluster." + type + "." + useLocation + ".secondaryEndpoints.blob";
            var secondaryUrl = JsonUtils.GetString(secondaryEntity, config);
            if (!String.IsNullOrEmpty(secondaryUrl))
            {
                urls.Add(secondaryUrl);
            }
            return urls.ToArray();
        }

        public String GetStorageAccountName(string type, string location = null)
        {
            var useLocation = GetLocation(location);
            var entity = "azure_cluster." + type + "." + useLocation + ".fullname";
            return JsonUtils.GetString(entity, config);
        }

        public String GetAccessKey(string type, string location = null)
        {
            var useLocation = GetLocation(location);
            var entity = "azure_cluster." + type + "." + useLocation + ".keys.value";
            return JsonUtils.GetString(entity, config);

        }

        public String GetKey( string property, string tag, string location = null )
        {
            var useLocation = GetLocation(location);
            var entity = property + "." + useLocation + "." + tag; 
            var key = JsonUtils.GetString(entity, config);
            if ( !String.IsNullOrEmpty(key))
            {
                return key; 
            }
            entity = property + ".default." + tag;
            return JsonUtils.GetString(entity, config);
        }

        public override String[] GetURLs(string storage, string path, string location)
        {
            var urls = GetStorageUrlEndpoint(storage, location);
            return Array.ConvertAll( urls, x => CloudProvider.urlCombine(x, path));
        }

        private string getEndpointSuffix( string endpoint )
        {
            if ( endpoint.EndsWith('/') )
            {
                endpoint = endpoint.Substring(0, endpoint.Length - 1); // Strip ending '/'
            }
            var chunks = endpoint.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var nchunks = chunks.Length;
            return chunks[nchunks - 3] + "." + chunks[nchunks - 2] + "." + chunks[nchunks - 1];
        }

        private CloudStorageAccount GetStorageAccount( string storage, string location )
        {
            var storageAccountName = GetStorageAccountName(storage, location);
            var accessKey = GetAccessKey(storage, location);
            if (String.IsNullOrEmpty(storageAccountName) ||
                String.IsNullOrEmpty(accessKey)
                )
            {
                return null;
            }
            // Console.WriteLine($"Storage account = {storageAccountName}, key = {accessKey}");
            var authCredentials = new Microsoft.WindowsAzure.Storage.Auth.StorageCredentials(
                storageAccountName, accessKey);
            var blobEndpoint = GetBlobEndpoint(location);
            var endpointSuffix = getEndpointSuffix(blobEndpoint);

            CloudStorageAccount storageAccount = new CloudStorageAccount(authCredentials, endpointSuffix, true);
            return storageAccount;
        }


        public override BlobContainer GetContainer( string storage, string path, string location ) {
            var storageAccount = GetStorageAccount(storage, location);
            if (Object.ReferenceEquals(storageAccount, null))
            {
                return null;
            }
            var blobClient = storageAccount.CreateCloudBlobClient();
            
            return new AzureBlobContainer( this, blobClient.GetContainerReference(path) );
        }

        public override CloudTable GetTable(string storage, string path, string location)
		{
            var storageAccount = GetStorageAccount(storage, location);
            if (Object.ReferenceEquals(storageAccount, null))
            {
                return null;
            }
            var tableClient = storageAccount.CreateCloudTableClient();
            var table = tableClient.GetTableReference(path);
            return table;
		}
	}

    public class AzureBlobContainer : BlobContainer {
        private CloudBlobContainer _container = null; 
        internal AzureBlobContainer( CloudProvider provider, CloudBlobContainer container ) :
            base(provider)
        {

            _container = container; 
        }
            
        public override BlobDirectory GetDirectoryReference(string path)
        {
            return new AzureBlobDirectory( Provider, _container.GetDirectoryReference(path));
        }
        public override BlockBlob GetBlockBlobReference(string path)
        {
            return new AzureBlockBlob(_container.GetBlockBlobReference(path));
        }

    }
    public class AzureBlobDirectory : BlobDirectory
    {
        internal CloudBlobDirectory _directory = null; 
        internal AzureBlobDirectory( CloudProvider provider, CloudBlobDirectory directory ): 
            base(provider)
        {
            _directory = directory; 
        }
        public override Uri[] StorageUri()
        {
            // see https://github.com/Azure-Samples/storage-blob-dotnet-getting-started/issues/8
            // for non RA-GRS, SecondaryUri is not working 
            return new Uri[] { _directory.StorageUri.PrimaryUri };
        }
        public override BlobDirectory GetDirectoryReference(string path)
        {
            return new AzureBlobDirectory(Provider, _directory.GetDirectoryReference(path));
        }

        public override BlockBlob GetBlockBlobReference(string path)
        {
            return new AzureBlockBlob(_directory.GetBlockBlobReference(path));
        }

        public override CustomizedBlobContinuationToken NewBlobContinuationToken()
        {
            return null; // new AzureBlobContinuationToken(); 
        }

        public override async Task<CustomizedResultSegment>
            ListBlobsSegmentedAsync(bool useFlatBlobListing, BlobListingDetails blobListingDetails,
            Nullable<int> maxResults, CustomizedBlobContinuationToken currentToken, BlobRequestOptions options,
            OperationContext operationContext)
        {
            var azureToken = currentToken as AzureBlobContinuationToken;
            BlobContinuationToken useToken = null; 
            if ( !Object.ReferenceEquals(azureToken, null) )
            {
                useToken = azureToken._token;
            }
            var results = await _directory.ListBlobsSegmentedAsync(useFlatBlobListing, blobListingDetails,
                maxResults, useToken, options, operationContext);
            //Console.WriteLine($"List yield {results.Results.Count()} items, token is {Object.ReferenceEquals(useToken, null)}, azureToken is {Object.ReferenceEquals(azureToken, null)}");
            return new AzureResultSegment(Provider, results);
        }

        public override async Task<CustomizedResultSegment>
            ListBlobsSegmentedAsync(CustomizedBlobContinuationToken currentToken)
        {
            var azureToken = currentToken as AzureBlobContinuationToken;
            BlobContinuationToken useToken = null;
            if (!Object.ReferenceEquals(azureToken, null))
            {
                useToken = azureToken._token;
            }
            // Console.WriteLine($"ListBlobs on {_directory.Prefix}");
            var results = await _directory.ListBlobsSegmentedAsync(useToken);
            //Console.WriteLine($"List yield {results.Results.Count()} items, token is {Object.ReferenceEquals(useToken, null)}, azureToken is {Object.ReferenceEquals(azureToken, null)}");
            return new AzureResultSegment(Provider, results);
        }

        public override String Name
        {
            get
            {
                return _directory.Prefix;
            }
        }
    }

    public class AzureBlockBlob: BlockBlob
    {
        CloudBlockBlob _blob = null; 
        internal AzureBlockBlob(CloudBlockBlob blob)
        {
            _blob = blob; 
        }
        private async Task UploadFromFileAsyncV0(string path)
        {
            await _blob.UploadFromFileAsync(path);
        }

        private async Task UploadTextAsyncV0(string info)
        {
            await _blob.UploadTextAsync(info);
        }

        private async Task<string> DownloadTextAsyncV0()
        {
            return await _blob.DownloadTextAsync(); 
        }

        private async Task UploadFromByteArrayAsyncV0(byte[] buffer, int index, int count)
        {
            await _blob.UploadFromByteArrayAsync(buffer, index, count);
        }

        public override async Task DownloadToStreamAsync(Stream target)
        {
            await _blob.DownloadToStreamAsync(target);
        }

        public override async Task UploadFromStreamAsync(Stream source )
        {
            await _blob.UploadFromStreamAsync(source);
        }

        // see https://stackoverflow.com/questions/27756853/forcing-etag-check-on-blob-creation
        public override async Task UploadFromStreamAsync(Stream source, string etag)
        {
            if (String.IsNullOrEmpty(etag))
            {
                // For those Cloud Provider that doesn't support etag, etag should always be null.
                // https://docs.microsoft.com/en-us/dotnet/api/microsoft.windowsazure.storage.accesscondition.generateifnonematchcondition?view=azure-dotnet
                // Check if the azure blob doesn't exist
                await _blob.UploadFromStreamAsync(source, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null); ;
            }
            else
            {
                await _blob.UploadFromStreamAsync(source, AccessCondition.GenerateIfMatchCondition(etag), null, null);
            }
            return;
        }

        public override bool hasETag
        {
            get
            {
                return true;
            }
        }

        public override string ETag
        {
            get
            {
                // For those Cloud Provider that doesn't support etag, etag should always be null.
                return _blob.Properties.ETag;
            }
        }

        public override async Task UploadFromStreamAsyncFailIfExist(Stream stream)
        {
            await _blob.UploadFromStreamAsync(stream, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null);
        }

        public override async Task DeleteAsync(bool bCatchException = true)
        {
            if (bCatchException)
            {
                try
                {
                    await _blob.DeleteAsync();
                } catch (Exception ex){
                    Console.WriteLine($"Delete blob {_blob.Uri} exception: {ex}");
                };
            }
            else { 
                await _blob.DeleteAsync();
            }
        }

        public override String Name
        {
            get
            {
                return _blob.Name; 
            }
        }

        public override async Task FetchAttributesAsync()
        {
            await _blob.FetchAttributesAsync(); 
        }

        public override CustomizedBlobProperties Properties
        {
            get
            {
                return new AzureBlobProperties( _blob.Properties );
            }
        }

    }

    public class AzureResultSegment: CustomizedResultSegment
    {
        internal BlobResultSegment _result = null; 
        public AzureResultSegment(CloudProvider Provider, BlobResultSegment result) :
            base(Provider)
        {
            _result = result; 
        }

        public override IEnumerable<ICustomizedBlobItem> Results
        {
            get
            {
                return _result.Results.Select(x => new IAzureBlobItem(Provider, x));
            }
        }
        public override CustomizedBlobContinuationToken ContinuationToken
        {
            get
            {
                var token = _result.ContinuationToken;
                if (token == null)
                    return null; // Necessary, as the external loop as comparison to null to indicate termination. 
                else 
                // Console.WriteLine($"Continuation Token for ResultSegment is {Object.ReferenceEquals(token, null)}");
                    return new AzureBlobContinuationToken(token);
            }
        }

    }

    public class AzureBlobContinuationToken: CustomizedBlobContinuationToken
    {
        internal BlobContinuationToken _token = null; 
        public AzureBlobContinuationToken(BlobContinuationToken token)
        {
            _token = token; 
        }

    }

    public class IAzureBlobItem : ICustomizedBlobItem 
    {
        internal IListBlobItem _item = null; 
        public IAzureBlobItem(CloudProvider provider, IListBlobItem item): 
            base(provider)
        {
            _item = item; 
        }
        public override BlockBlob ToBlockBlob()
        {
            var blockBlob = _item as CloudBlockBlob;
            if (Object.ReferenceEquals(blockBlob, null))
                return null; 
            else 
                return new AzureBlockBlob(blockBlob);

        }
        public override BlobDirectory ToBlobDirectory()
        {
            var dirBlob = _item as CloudBlobDirectory;
            if (Object.ReferenceEquals(dirBlob, null))
                return null;
            else
                return new AzureBlobDirectory(Provider, dirBlob);

        }
        public override Uri Uri
        {
            get
            {
                return _item.Uri;
            }
        }
    }

    public class AzureBlobProperties : CustomizedBlobProperties
    {
        BlobProperties _properties = null; 
        public AzureBlobProperties( BlobProperties properties)
        {
            _properties = properties;
        }
        public override Nullable<DateTimeOffset> LastModified
        {
            get
            {
                return _properties.LastModified; 
            }
        }
    }

}
