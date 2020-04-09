using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Utils.Json;

namespace WebUI.Azure
{
    public class NFSCloudProvider : CloudProvider
    {
        private readonly ILogger _logger;
        public JObject config = null;

        public NFSCloudProvider(String configFile, ILogger logger)
        {
            var useConfigFile = configFile;
            config = Json.Read(useConfigFile);
            _logger = logger;
        }

        public override BlobContainer GetContainer(string storage, string path, string location)
        {
            string basePath = JsonUtils.GetJToken("nfs_mount_local_path", config).ToString();
            return new NFSBlobContainer(this,Path.Combine(basePath, path));
        }
        public override bool Ready()
        {
            return true;
        }
    }

    public class NFSBlobContainer : BlobContainer
    {
        private readonly CloudProvider _provider;
        private readonly string _basePath;

        internal NFSBlobContainer(CloudProvider provider, string basePath) : base(provider)
        {
            _provider = provider;
            _basePath = basePath;
        }
        public override BlobDirectory GetDirectoryReference(string path)
        {
            return new NFSBlobDirectory(Provider, path, _basePath);
        }
        public override BlockBlob GetBlockBlobReference(string path)
        {
            return new NFSBlockBlob(Path.Combine(_basePath, path));
        }

    }

    public class NFSBlobDirectory : BlobDirectory
    {
        private readonly CloudProvider _provider;
        private readonly string _directoryPath;
        private readonly string _basePath;

        internal NFSBlobDirectory(CloudProvider provider, string directoryPath, string basePath) : base(provider)
        {
            _provider = provider;
            _directoryPath = directoryPath;
            _basePath = basePath;
        }

        public override Uri[] StorageUri()
        {
            // can't get httpcontext for url.link, only solution is config
            return new Uri[] { };
        }
        public override BlockBlob GetBlockBlobReference(string path)
        {

            return new NFSBlockBlob(Path.Combine(_basePath, _directoryPath, path));
        }

        public override async Task<IEnumerable<string>> ListBlobsSegmentedAsync()
        {
            //DirectoryInfo dir = new DirectoryInfo(Path.Combine(_basePath, _directoryPath));
            string path = Path.Combine(_basePath, _directoryPath);
            return await ListCurrentDepthFile(path, 1);
        }

        public static async Task<IEnumerable<string>> ListCurrentDepthFile(string path, int depth)
        {
            List<string> allFiles = new List<string>();
            if (depth > 0)
            {
                //FileInfo[] files = dir.GetFiles();
                allFiles.AddRange(Directory.EnumerateFiles(path));
                //DirectoryInfo[] directoryInfos = dir.GetDirectories();
                foreach (var oneDirectory in Directory.EnumerateDirectories(path))
                {
                    var curFiles = await ListCurrentDepthFile(oneDirectory, depth - 1);
                    allFiles.AddRange(curFiles);
                }
                return allFiles;
            }
            return allFiles;
        }
    }

    public class NFSBlockBlob : BlockBlob
    {
        private readonly string _path;

        internal NFSBlockBlob(string path)
        {
            _path = path;
        }
        public override async Task DeleteAsync(bool bCatchException = true)
        {
            if (bCatchException)
            {
                try
                {
                    new FileInfo(_path).Delete();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Delete blob {_path} exception: {ex}");
                };
            }
            else
            {
                new FileInfo(_path).Delete();
            }
        }
        public override async Task DownloadToStreamAsync(Stream target)
        {
            if (!File.Exists(_path))
            {
                return;
            }
            using (var filestream = new FileStream(_path,
                FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 16384, useAsync: true))
            {
                await filestream.CopyToAsync(target);
                target.Position = 0;
            }
        }
        public override async Task UploadFromStreamAsync(Stream source)
        {
            new DirectoryInfo(Path.GetDirectoryName(_path)).Create();
            using (var filestream = new FileStream(_path,
                FileMode.Create, FileAccess.Write, FileShare.Read,
                bufferSize: 16384, useAsync: true))
            {
                await source.CopyToAsync(filestream);
            }
        }
        public override async Task UploadFromStreamAsyncFailIfExist(Stream stream)
        {
            new DirectoryInfo(Path.GetDirectoryName(_path)).Create();
            using (var filestream = new FileStream(_path,
                FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                bufferSize: 16384, useAsync: true))
            {
                await stream.CopyToAsync(filestream);
            }
        }
        public override String Name
        {
            get
            {
                return new FileInfo(_path).Name;
            }
        }
        public FileAttributes Properties
        {
            get
            {
                return new FileInfo(_path).Attributes;
            }
        }
    }
}
