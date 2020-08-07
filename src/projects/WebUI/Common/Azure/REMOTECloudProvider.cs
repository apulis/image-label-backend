using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;
using Newtonsoft.Json.Linq;
using UrlCombineLib;
using Utils.Json;

namespace WebUI.Azure
{
    public class REMOTECloudProvider : CloudProvider
    {
        private readonly ILogger _logger;
        public JObject config = null;

        public REMOTECloudProvider(String configFile, ILogger logger)
        {
            var useConfigFile = configFile;
            config = Json.Read(useConfigFile);
            _logger = logger;
        }

        public override BlobContainer GetContainer(string storage, string path, string location)
        {
            string url = JsonUtils.GetJToken("url", config).ToString();
            string token = JsonUtils.GetJToken("token", config).ToString();
            return new REMOTEBlobContainer(this,new Uri(url.CombineUrl(path)), token); 
        }
        public override bool Ready()
        {
            return true;
        }
    }

    public class REMOTEBlobContainer : BlobContainer
    {
        private readonly CloudProvider _provider;
        private readonly Uri _uri;
        private readonly string _token;

        internal REMOTEBlobContainer(CloudProvider provider, Uri uri, string token) : base(provider)
        {
            _provider = provider;
            _uri = uri;
            _token = token;
        }
        public override BlobDirectory GetDirectoryReference(string path)
        {
            return new REMOTEBlobDirectory(Provider, path, _uri, _token);
        }
        public override BlockBlob GetBlockBlobReference(string path)
        {
            return new REMOTEBlockBlob(_uri.Combine(path), _token);
        }

    }

    public class REMOTEBlobDirectory : BlobDirectory
    {
        private readonly CloudProvider _provider;
        private readonly string _directoryPath;
        private readonly Uri _baseUri;
        private readonly string _token;

        internal REMOTEBlobDirectory(CloudProvider provider, string directoryPath, Uri baseUri,string token) : base(provider)
        {
            _provider = provider;
            _directoryPath = directoryPath;
            _baseUri = baseUri;
            _token = token;
        }

        public override Uri[] StorageUri()
        {
            return new Uri[] { new Uri(_directoryPath) };
        }
        public override BlockBlob GetBlockBlobReference(string path)
        {
            return new REMOTEBlockBlob(_baseUri.Combine(_directoryPath,path), _token);
        }
        public override void LinkPath(string dataPath)
        {
            var path = _baseUri.Combine(_directoryPath);
            Dictionary<string, string> headerDictionary = new Dictionary<string, string>() { ["Authorization"] = $"Bearer { _token}" };
            Requests.Patch(path.ToString()+ $"?linkPath={dataPath}", headerDictionary);
        }
        public override async Task<IEnumerable<string>> ListBlobsSegmentedAsync()
        {
            //DirectoryInfo dir = new DirectoryInfo(Path.Combine(_basePath, _directoryPath));
            var path = _baseUri.Combine(_directoryPath);
            Dictionary<string, string> headerDictionary = new Dictionary<string, string>() { ["Authorization"] = $"Bearer { _token}" };
            var s = await Requests.Put(path.ToString(), headerDictionary);
            return JArray.Parse(s).ToObject<List<string>>();
        }
    }

    public class REMOTEBlockBlob : BlockBlob
    {
        private Uri _path;
        private readonly string _token;

        internal REMOTEBlockBlob(Uri path,string token)
        {
            _path = path;
            _token = token;
        }
        public override async Task DeleteAsync(bool bCatchException = true)
        {
            Dictionary<string, string> headerDictionary = new Dictionary<string, string>() { ["Authorization"] =$"Bearer { _token}" };
            await Requests.Delete(_path.ToString(), new MemoryStream(), headerDictionary);
        }
        public override async Task DownloadToStreamAsync(Stream target)
        {
            try
            {
                Dictionary<string, string> headerDictionary = new Dictionary<string, string>() { ["Authorization"] = $"Bearer { _token}" };
                Stream fileStream = await Requests.GetStream(_path.ToString(), headerDictionary);
                await fileStream.CopyToAsync(target);
                target.Position = 0;
            }
            catch (WebException e)
            {
                if (e.Message.Contains("404"))
                {
                    return;
                }
                throw;
            }
            
        }
        public override async Task UploadFromStreamAsync(Stream source)
        {
            Dictionary<string, string> headerDictionary = new Dictionary<string, string>() { ["Authorization"] = $"Bearer { _token}" };
            await Requests.Post(_path.ToString(), source, headerDictionary);
        }
        public override async Task UploadFromStreamAsyncFailIfExist(Stream stream)
        {

        }
        public override String Name
        {
            get { return _path.ToString(); }
        }
    }
}
