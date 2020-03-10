using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Azure;
using WebUI.Models;
using WebUI.Services;

namespace WebUI.Controllers
{
    [EnableCors("dev-use")]
    [Route("api/nfs")]
    [ApiController]
    public class NfsController : ControllerBase
    {
        /// <remarks>
        /// 返回对应路径的文件
        /// </remarks>
        [HttpGet("{*path}")]
        public async Task<IActionResult> GetFile(string path)
        {
            //var localConfigFile = WebUIConfig.GetConfigFile("storage/configLocal.json");
            //var config = Json.Read(localConfigFile);
            //string basePath = JsonUtils.GetString("nfs_mount_local_path", config);
            //string finalPath = Path.Combine(basePath, path);
            //if (Directory.Exists(finalPath) || !System.IO.File.Exists(finalPath))
            //{
            //    return Ok("BlobNotFound");
            //}
            try
            {
                var container = CloudStorage.GetContainer("cdn", "private", null, "LOCAL");
                var blob = container.GetBlockBlobReference(path);
                var stream = new MemoryStream();
                await blob.DownloadToStreamAsync(stream);
                var type = Path.GetExtension(path);
                string contentType;
                if (type == ".json")
                {
                    contentType = "text/plain";
                }
                else if (type == ".jpg")
                {
                    contentType = "image/jpeg";
                }
                else
                {
                    contentType = "application/octet-stream";
                }
                return File(stream, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"get path {path} exception: {ex}");
                return StatusCode(404);
            }
        }
    }
}
