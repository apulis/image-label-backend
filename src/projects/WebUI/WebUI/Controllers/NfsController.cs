﻿using System;
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
using WebUI.ViewModels;

namespace WebUI.Controllers
{
    [EnableCors("dev-use")]
    [Route("api/nfs")]
    [ApiController]
    public class NfsController : ControllerBase
    {
        /// <summary>
        /// 返回对应路径的文件，公开访问接口，无需认证
        /// </summary>
        [HttpGet("{*path}")]
        public async Task<IActionResult> GetFile(string path)
        {
            //var localConfigFile = WebUIConfig.GetConfigFile("storage/configLocal.json");
            //var config = Json.Read(localConfigFile);
            //string basePath = JsonUtils.GetString("nfs_mount_local_path", config);
            //string finalPath = Path.Combine(basePath,"public", path.Split("/", 2)[1]);
            //if (!System.IO.File.Exists(finalPath))
            //{
            //    return StatusCode(404,"file not found");
            //}
            try
            {
                if (!path.StartsWith("public"))
                {
                    return StatusCode(404, "file not found");
                }
                var container = CloudStorage.GetContainer("cdn", "public", null, null);
                var blob = container.GetBlockBlobReference(path.Split("/",2)[1]);
                var stream = new MemoryStream();
                await blob.DownloadToStreamAsync(stream);
                if (stream.Length == 0)
                {
                    return StatusCode(404, "file not found");
                }
                var contentType = FileOps.GetFileContentType(path);
                return File(stream, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"get path {path} exception: {ex}");
                return StatusCode(404,ex);
            }
        }
        /// <summary>
        /// 将数据写入对应路径的文件里，公开接口
        /// </summary>
        [HttpPost("/api/nfs/{*path}")]
        [ProducesResponseType(typeof(JObject), 200)]
        public async Task<IActionResult> WriteFilePublic(string path,[FromBody] JObject value)
        {
            try
            {
                if (!path.StartsWith("public"))
                {
                    return StatusCode(404, "file not found");
                }
                var container = CloudStorage.GetContainer("cdn", "public", null, null);
                var blob = container.GetBlockBlobReference(path.Split("/", 2)[1]);
                await blob.UploadGenericObjectAsync(value);
                return StatusCode(204);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"write path {path} exception: {ex}");
                return StatusCode(500,ex);
            }
        }
        /// <summary>
        /// 返回对应路径的文件v2，需token认证
        /// </summary>
        [HttpGet("/api/nfs2/{*path}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetFile2(string path)
        {
            //var localConfigFile = WebUIConfig.GetConfigFile("storage/configLocal.json");
            //var config = Json.Read(localConfigFile);
            //string basePath = JsonUtils.GetString("nfs_mount_local_path", config);
            //string finalPath = Path.Combine(basePath, path);
            //if (!System.IO.File.Exists(finalPath))
            //{
            //    return StatusCode(404, "file not found");
            //}
            try
            {
                var container = CloudStorage.GetContainer("cdn", path.Split("/", 2)[0], null, null);
                var blob = container.GetBlockBlobReference(path.Split("/", 2)[1]);
                var stream = new MemoryStream();
                await blob.DownloadToStreamAsync(stream);
                if (stream.Length==0)
                {
                    return StatusCode(404, "file not found");
                }
                var contentType = FileOps.GetFileContentType(path);
                return File(stream, contentType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"get path {path} exception: {ex}");
                return StatusCode(404,ex);
            }
        }
        /// <summary>
        /// 将数据写入对应路径的文件里
        /// </summary>
        [HttpPost("/api/nfs2/{*path}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> WriteFile(string path,[FromBody] JObject value)
        {
            try
            {
                var container = CloudStorage.GetContainer("cdn", path.Split("/", 2)[0], null, null);
                var blob = container.GetBlockBlobReference(path.Split("/", 2)[1]);
                await blob.UploadGenericObjectAsync(value);
                return StatusCode(204);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"write path {path} exception: {ex}");
                return StatusCode(500,ex);
            }
        }
        /// <summary>
        /// 删除文件接口，需认证
        /// </summary>
        [HttpDelete("/api/nfs2/{*path}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteFile(string path)
        {
            try
            {
                var container = CloudStorage.GetContainer("cdn", path.Split("/", 2)[0], null, null);
                var blob = container.GetBlockBlobReference(path.Split("/", 2)[1]);
                await blob.DeleteAsync();
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"delete path {path} exception: {ex}");
                return StatusCode(500,ex);
            }
        }
        [HttpPut("/api/nfs2/{*path}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ListAllFile(string path)
        {
            try
            {
                var container = CloudStorage.GetContainer("cdn", path.Split("/", 2)[0], null, null);
                var directory = container.GetDirectoryReference(path.Split("/", 2)[1]);
                var files =await directory.ListBlobsSegmentedAsync();
                return Ok(files);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"get path {path} exception: {ex}");
                return StatusCode(404, ex);
            }
        }
        [HttpPatch("/api/nfs2/{*path}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> exec([FromRoute]string path,[FromQuery]string linkPath)
        {
            try
            {
                var container = CloudStorage.GetContainer("cdn", path.Split("/", 2)[0], null, null);
                var directory = container.GetDirectoryReference(path.Split("/", 2)[1]);
                directory.LinkPath(linkPath);
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"get path {path} exception: {ex}");
                return StatusCode(404, ex);
            }
        }
    }
}
