using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Services;

namespace WebUI.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableCors("dev-use")]
    [Route("api/labels")]
    [ApiController]
    public class LabelController:ControllerBase
    {
        /// <remarks>
        /// 返回所有的label类别
        /// </remarks>
        [HttpGet]
        public async Task<IActionResult> GetLabels()
        {
            var blob = AzureService.GetBlob("cdn", "private", null, null, "categories", "meta.json");
            var json = await blob.DownloadGenericObjectAsync();
            var obj = JsonUtils.GetJToken("categories", json) as JArray;
            return Ok(new Response().GetJObject("categories", JToken.FromObject(obj)));
        }
    }
}
