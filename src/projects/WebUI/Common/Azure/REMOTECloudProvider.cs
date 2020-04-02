using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.Extensions.Logging;
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
            return new REMOTEBlobContainer(this,new Uri(url.CombineUrl(path))); 
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

        internal REMOTEBlobContainer(CloudProvider provider, Uri uri) : base(provider)
        {
            _provider = provider;
            _uri = uri;
        }
        public override BlobDirectory GetDirectoryReference(string path)
        {
            return new REMOTEBlobDirectory(Provider, path, _uri);
        }
        public override BlockBlob GetBlockBlobReference(string path)
        {
            return new REMOTEBlockBlob(_uri.Combine(path));
        }

    }

    public class REMOTEBlobDirectory : BlobDirectory
    {
        private readonly CloudProvider _provider;
        private readonly string _directoryPath;
        private readonly Uri _baseUri;

        internal REMOTEBlobDirectory(CloudProvider provider, string directoryPath, Uri baseUri) : base(provider)
        {
            _provider = provider;
            _directoryPath = directoryPath;
            _baseUri = baseUri;
        }

        public override Uri[] StorageUri()
        {
            // can't get httpcontext for url.link, only solution is config
            return new Uri[] { };
        }
        public override BlockBlob GetBlockBlobReference(string path)
        {
            return new REMOTEBlockBlob(_baseUri.Combine(_directoryPath,path));
        }
    }

    public class REMOTEBlockBlob : BlockBlob
    {
        private Uri _path;

        internal REMOTEBlockBlob(Uri path)
        {
            _path = path;
        }
        public override async Task DeleteAsync(bool bCatchException = true)
        {
            
        }
        public override async Task DownloadToStreamAsync(Stream target)
        {
            Stream fileStream =await Requests.GetStream(_path.ToString(),new Dictionary<string, string>());
            await fileStream.CopyToAsync(target);
            target.Position = 0;
        }
        public override async Task UploadFromStreamAsync(Stream source)
        {

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
