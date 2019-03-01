using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Azure;
using WebUI.Models;

namespace WebUI.Controllers
{
    [Authorize(Roles = "Admin,User")]
    [Route("api/[controller]")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly ILogger _logger;

        public ImageController(ILoggerFactory logger)
        {
            // _tokenCache = tokenCache;
            _logger = logger.CreateLogger("ImageController");
        }

        // GET: api/Image
        [HttpGet]
        public IEnumerable<string> Get()
        {

            return new string[] { "value1", "value2" };

        }

        // GET: api/Image/5
        [HttpGet("{id}", Name = "Get")]
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/Image
        [HttpPost]
        public void Post([FromBody] string value)
        {
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
        public string GetAllPaths(string prefix)
        {
            return prefix;
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

            var cdnpath = dirPath.StorageUri()[0];
            _logger.LogInformation($"CDN path === {cdnpath}, lst = {lst}");
            // postdata["prefix"] = "#Prefix#"; // Test Feedback. 
            return Content(postdata.ToString(), "application/json");
        }
    }
}
