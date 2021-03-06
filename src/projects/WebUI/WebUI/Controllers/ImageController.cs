﻿using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Extensions;
using Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Azure;
using WebUI.Models;
using WebUI.Services;


namespace WebUI.Controllers
{
    //[Authorize(Roles = "Admin,User")]
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("dev-use")]
    //[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ImageController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly UserManager<IdentityUser> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public ImageController(ILoggerFactory logger, UserManager<IdentityUser> userManager,RoleManager<IdentityRole> roleManager,SignInManager<IdentityUser> signInManager)
        {
            // _tokenCache = tokenCache;
            _logger = logger.CreateLogger("ImageController");
            this.userManager = userManager;
            this.roleManager = roleManager;
            _signInManager = signInManager;
        }

        // GET: api/Image
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = HttpContext.User.Identity.Name;
            var tasks = await AzureService.FindUserTasks(userId, HttpContext.Session);
            return Content(Base64Ops.Base64Encode(tasks.JObjectToString()));
        }

        // get: api/image/5
        [HttpGet("{datasetId}")]
        public async Task<IActionResult> Get(Guid dataSetId)
        {
            var userId = HttpContext.User.Identity.Name;
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var content =await AzureService.FindUserOneTaskInfo(userId, HttpContext.Session, convertDataSetId);
            return Ok(new Response());
        }

        // GET: api/Image/5
        [HttpGet("{task_id}/{id}")]
        public async Task<IActionResult> Get(string task_id, int id)
        {
            var userId = HttpContext.User.Identity.Name;
            bool re = await AzureService.FindUserHasThisTask(userId, HttpContext.Session, task_id);
            if (re)
            {
                var blob = AzureService.GetBlob(null, $"tasks/{task_id}/images", $"{id}.json");
                var json = await blob.DownloadGenericObjectAsync();
                if (json != null)
                {
                    return Content(new Response { Successful = "true", Msg = "ok", Data = json }.JObjectToString());
                }
            }
            return Content(new Response { Successful = "true", Msg = "user don't has this dataset", Data = null }.JObjectToString());
        }

        // POST: api/Image
        [HttpPost("{task_id}/{id}")]
        public async Task<IActionResult> Post(string task_id, int id,[FromBody] string value)
        {
            var userId = HttpContext.User.Identity.Name;
            bool re = await AzureService.FindUserHasThisTask(userId, HttpContext.Session, task_id);
            if (re)
            {
                var blob = AzureService.GetBlob(null, $"tasks/{task_id}/images", $"{id}.json");
                try
                {

                    var json = JsonConvert.DeserializeObject<JObject>(Base64Ops.Base64Decode(value));
                    await AzureService.UpdateTaskStatusToBlob(task_id, id, userId,"commit");
                    await blob.UploadGenericObjectAsync(json);
                }
                catch (Exception e)
                {
                    return Content(new Response { Successful = "false", Msg = e.Message, Data = null }.JObjectToString());
                }
            }
            return Content(new Response { Successful = "true", Msg = "ok", Data = null }.JObjectToString());
        }

        [HttpGet("{taskId}/next")]
        public async Task<IActionResult> GetNext(string taskId)
        {
            var userId = HttpContext.User.Identity.Name;
            bool re = await AzureService.FindUserHasThisTask(userId, HttpContext.Session, taskId);
            if (re)
            {
                int nextTaskId = await AzureService.GetNextTaskId(taskId, userId);
                if (nextTaskId != -1)
                {
                    return Content(new Response { Successful = "true", Msg = "ok", Data = new JObject { "next", nextTaskId }}.JObjectToString());
                }
            }
            return Content(new Response { Successful = "true", Msg = "no next id", Data = null }.JObjectToString());
        }


        // PUT: api/Image/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }

        // GET: api/Image/5
        [HttpGet("GetAllPaths/{prefix}", Name = "GetAllPaths")]
        public async Task<IActionResult> GetAllPaths(string prefix)
        {
            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference(prefix);
            var dic =await dirpath.GetAllFiles();
            var lst = new List<string>();
            foreach( var pair in dic)
            {
                lst.Add(pair.Key);
            }
            return Content(lst.ToString(), "application/json");
        }

        // GET: api/Image/GetCurrent
        [HttpGet("GetCurrent", Name = "GetCurrent")]
        public IActionResult GetCurrent()
        {
            var retJson = new JObject();
            foreach (var tag in Constants.AllCurrentTags)
            {
                var tagValue = HttpContext.Session.GetString(tag);
                if (Object.ReferenceEquals(tagValue, null))
                {
                    tagValue = ""; //  $"#{tag}#";
                }
                retJson[tag] = tagValue;
            }
            var dirPath = GetDirectory(null);
            var uris = dirPath.StorageUri();
            if ( uris.Length > 0 )
            {
                var cdnpath = uris[0].ToString();
                if (!String.IsNullOrEmpty(cdnpath))
                {
                    retJson[Constants.CDNEntry] = cdnpath;
                }
            }
            retJson[Constants.OperationEntry] = JObject.FromObject(Constants.AllOperations);
            _logger.LogDebug($"GetCurrent == {retJson}");
            return Content(retJson.ToString(), "application/json");
        }

        // Post: api/Image/SetCurrent
        [HttpPost("SetCurrent", Name = "SetCurrent")]
        [IgnoreAntiforgeryToken]
        public IActionResult SetCurrent([FromBody] JObject postdata)
        {
            foreach (var pair in postdata)
            {
                HttpContext.Session.SetString(pair.Key, pair.Value.ToString());
            }
            // postdata["prefix"] = "#Prefix#"; // Test Feedback. 
            return Content(postdata.ToString(), "application/json");
        }

        private BlobDirectory GetDirectory(String prefix)
        {
            var container = CloudStorage.GetContainer(null);
            var dirPath = container.GetDirectoryReference(String.IsNullOrEmpty(prefix)?"":prefix);
            return dirPath; 
        }

        private async Task<List<String>> SearchFor( BlobDirectory dir, String filename )
        {
            var dic = await dir.GetAllFiles();
            var lst = new List<string>();
            var flen = filename.Length + 1; 
            foreach( var pair in dic )
            {
                if ( pair.Key.EndsWith(filename ))
                {
                    var dirInfo = pair.Key.Substring(0, pair.Key.Length - flen);
                    lst.Add(dirInfo);
                }
            }
            return lst; 
        }


        // Post: api/Image/SetCurrent
        [HttpPost("SelectPrefix", Name = "SelectPrefix")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SelectPrefix([FromBody] JObject postdata)
        {
            var prefix = JsonUtils.GetString(Constants.PrefixEntry, postdata);
            var dirPath = GetDirectory(prefix);
            var lst = await SearchFor(dirPath, Constant.MetadataJson);
            var retJson = new JObject();
            retJson[Constants.PrefixEntry] = new JArray(lst);
            _logger.LogDebug($"SelectPrefix: {retJson}");
            return Content(retJson.ToString(), "application/json");
        }

        // Post: api/Image/UploadImage
        [IgnoreAntiforgeryToken]
        [HttpPost("UploadImage", Name = "UploadImage")]
        public async Task<IActionResult> UploadImage()
        {
            Int64 totalUpload = 0;
            var files = Request.Form.Files;
            JObject ret = new JObject();
            var location = LocalSetting.Current.Location;

            try
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        using (var stream = new MemoryStream())
                        {
                            await file.CopyToAsync(stream);
                            /*
                            totalUpload += stream.Length;
                            var retJson = await ImageUpload.UploadImageAndResize(CloudProvider.All,
                                location,
                                "restaurant/" + uuid + "/image",
                                stream,
                                Constant.ImageSizeMax,
                                Constant.ImageSizeMin,
                                null,
                                file.FileName,
                                _logger);
                            ret[file.FileName] = retJson;*/
                        }
                    }
                }
                _logger.LogInformation($"UploadImage: {files.Count}");
                return Ok();
            }
            catch (Exception ex)
            {
                var errorLog = new { exception = ex.ToString() };
                _logger.LogInformation("UploadImage: {0}", errorLog);
                return Ok(new { error = ex.ToString() });
            }
        }

        private async Task<JObject> GetMetadata(String prefix )
        {
            
            var container = CloudStorage.GetContainer(null);
            var dirPath = container.GetDirectoryReference(prefix);
            var metadataBlob = dirPath.GetBlockBlobReference(Constant.MetadataJson);
            return await metadataBlob.DownloadGenericObjectAsync(); 
        }

        private IActionResult ValidateName(JObject postdata, JObject metadata, string name )
        {
            var row = JsonUtils.GetType<int>("row", postdata, 0);
            var col = JsonUtils.GetType<int>("col", postdata, 0);
            var imgobj = JsonUtils.GetJToken("images", metadata); 
            if ( Object.ReferenceEquals(imgobj, null ))
            {
                return Ok(new { error = "Metadata has no images" }); 
            }
            var imgarr = imgobj as JArray; 
            if (Object.ReferenceEquals(imgarr, null))
            {
                return Ok(new { error = $"Metadata.images is not array" });
            }
            if (row < 0 || row >= imgarr.Count )
            {
                return Ok(new { error = $"Metadata.images has {imgarr.Count} rows, but request row = {row}" });
            }
            var onerow = imgarr[row] as JArray;
            if (Object.ReferenceEquals(onerow, null))
            {
                return Ok(new { error = $"Metadata.image[{row}] is empty array " });
            }
            if ( col < 0 || col >= onerow.Count )
            {
                return Ok(new { error = $"Metadata.image[{row}] has {onerow.Count} columns, but request col = {col} " });
            }
            var oneimage = onerow[col] as JObject;
            if (Object.ReferenceEquals(oneimage, null))
            {
                return Ok(new { error = $"Metadata.image[{row}][{col}] is empty JObject." });
            }
             
            var curimage = JsonUtils.GetString(Constants.OperationImage, oneimage);
            var basename = curimage.Split('.')[0];
            if ( name.IndexOf(basename, StringComparison.Ordinal)<0 )
            {
                return Ok(new { error = $"Metadata.image[{row}][{col}] has image {basename}, but {name} is requested." });
            }
            return null; 
        }




        // Post: api/Image/UploadSegmentation
        [HttpPost("UploadSegmentation", Name = "UploadSegmentation")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UploadSegmentation([FromBody] JObject postdata)
        {
            var prefix = JsonUtils.GetString(Constants.PrefixEntry, postdata);
            var metadata = await GetMetadata(prefix);
            var name = JsonUtils.GetString("name", postdata);
            var ret = ValidateName(postdata, metadata, name);
            if (!Object.ReferenceEquals(ret, null))
            {
                return ret;
            }
            var overlayBase64 = JsonUtils.GetString("overlay", postdata);
            var segBase64 = JsonUtils.GetString("seg", postdata);
            overlayBase64 = overlayBase64.FromJSBase64();
            segBase64 = segBase64.FromJSBase64();

            var container = CloudStorage.GetContainer(null);
            var dirPath = container.GetDirectoryReference(prefix);

            var segBytes = Convert.FromBase64String(segBase64);
            var basename = name.Split('.')[0];
            var segBlob = dirPath.GetBlockBlobReference("seg_" + basename + ".png");
            await segBlob.UploadFromByteArrayAsync(segBytes, 0, segBytes.Length);

            var overlayBlob = dirPath.GetBlockBlobReference("overlay_" + name);

            var overlayImage = ImageOps.FromBase64(overlayBase64, _logger);
            var overlayJpeg = overlayImage.ToJPEG();
            
            await overlayBlob.UploadFromByteArrayAsync(overlayJpeg, 0, overlayJpeg.Length);
            _logger.LogInformation($"UploadSegmentation update segment {segBytes.Length} && overlay image {overlayJpeg.Length}");





            return Ok();
        }

        // Post: api/Image/UploadSegmentation
        [HttpPost("UploadJson", Name = "UploadJson")]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> UploadJson([FromBody] JObject postdata)
        {
            // _logger.LogInformation($"UploadJson: {postdata} ");
            var prefix = JsonUtils.GetString(Constants.PrefixEntry, postdata);
            var metadata = await GetMetadata(prefix);
            var name = JsonUtils.GetString("name", postdata);
            var row = JsonUtils.GetType<int>("row", postdata, 0);
            var col = JsonUtils.GetType<int>("col", postdata, 0);
            var ret = ValidateName(postdata, metadata, name);
            if (!Object.ReferenceEquals(ret, null))
            {
                
                _logger.LogInformation($"UploadJson is not valid, prefix = {prefix}, name = {name}, row = {row}, col = {col}");
                return ret;
            }
            var data64 = JsonUtils.GetString("data", postdata);
            data64 = data64.FromJSBase64();

            var container = CloudStorage.GetContainer(null);
            var dirPath = container.GetDirectoryReference(prefix);

            var dataBytes = Convert.FromBase64String(data64);
            var dataBlob = dirPath.GetBlockBlobReference(name);
            await dataBlob.UploadFromByteArrayAsync(dataBytes, 0, dataBytes.Length);

            _logger.LogInformation($"UploadJson update Json {dataBytes.Length}, prefix = {prefix}, name = {name}, row = {row}, col = {col}");

            return Ok();
        }

        // Post: api/Image/UploadSegmentation
        [HttpPost("UploadJsons", Name = "UploadJsons")]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadJsons([FromBody] JObject postdata)
        {
            // _logger.LogInformation($"UploadJson: {postdata} ");
            var prefix = JsonUtils.GetString(Constants.PrefixEntry, postdata);
            var metadata = await GetMetadata(prefix);
            var contentTag = JsonUtils.GetJToken("content", postdata);
            var content = contentTag as JArray;
            if ( Object.ReferenceEquals(content,null) || content.Count==0 )
            {
                var msg = $"UploadJsons has an empty content JArray, content = {contentTag}";
                _logger.LogInformation(msg);
                return Ok(new { error = msg }); ;
            }
            var tasks = new List<Task>();
            var errMsg = "";
            var cnt = -1;
            var uploadLength = 0;
            var uploadFiles = 0; 
            foreach (var onecontent in content)
            {
                var onedata = onecontent as JObject;
                cnt += 1;
                if ( Object.ReferenceEquals(onedata, null))
                {
                    errMsg += $"Entry {cnt} is an empty JObject\n";
                    continue; 
                }
                var name = JsonUtils.GetString("name", onedata);
                var row = JsonUtils.GetType<int>("row", onedata, 0);
                var col = JsonUtils.GetType<int>("col", onedata, 0);
                var ret = ValidateName(onedata, metadata, name);
                if (!Object.ReferenceEquals(ret, null))
                {
                    errMsg += $"Failed to validate entry {cnt}, prefix = {prefix}, name = {name}, row = {row}, col = {col}\n";
                    _logger.LogInformation($"UploadJson is not valid, prefix = {prefix}, name = {name}, row = {row}, col = {col}");
                    continue;
                }
                var data64 = JsonUtils.GetString("data", onedata);
                data64 = data64.FromJSBase64();

                var container = CloudStorage.GetContainer(null);
                var dirPath = container.GetDirectoryReference(prefix);

                var dataBytes = Convert.FromBase64String(data64);
                var dataBlob = dirPath.GetBlockBlobReference(name);
                tasks.Add(dataBlob.UploadFromByteArrayAsync(dataBytes, 0, dataBytes.Length));
                uploadFiles++;
                uploadLength += dataBytes.Length;
            }
            await Task.WhenAll(tasks);
            _logger.LogInformation($"UploadJsons update {uploadFiles} files, total length = {uploadLength}B, error = {errMsg}");
            if ( errMsg.Length==0)
            { 
                return Ok();
            } else
            {
                return Ok(new { error = errMsg});
            }
        }
        [HttpPost("UploadGeoJson")]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadGeoJson([FromBody] List<JObject> postdata)
        {
            var errMsg = "";
            var tasks = new List<Task>();
            var cnt = -1;
            foreach (var oneData in postdata)
            {
                cnt += 1;
                var filename = JsonUtils.GetString("filename", oneData);
                var filePathArray = filename.Split("api/nfs/public/demo/", 2);
                string filePath;
                if (filePathArray.Length == 2)
                {
                    filePath = filePathArray[1];
                }
                else
                {
                    filePath = Path.Combine("bingmap/pinggu/", filePathArray[0]);
                }
                var data = JsonUtils.GetJToken("data", oneData);
                if (Object.ReferenceEquals(data, null))
                {
                    var msg = $"Entry {cnt} is an empty JObject\n";
                    _logger.LogInformation($"UploadJson is not valid, name = {filename}");
                    errMsg += msg;
                    continue;
                }
                var data64 = data.ToString().FromJSBase64();
                var dataBytes = Convert.FromBase64String(data64);
                var container = CloudStorage.GetContainer("cdn", "public", null, null);
                var dataBlob = container.GetBlockBlobReference(Path.Combine("demo", filePath));
                tasks.Add(dataBlob.UploadFromByteArrayAsync(dataBytes, 0, dataBytes.Length));
            }
            await Task.WhenAll(tasks);
            if (errMsg.Length == 0)
            {
                return Ok();
            }
            return Ok(new { error = errMsg });
        }
    }
}
