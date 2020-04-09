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
    [EnableCors("dev-use")]
    [Route("api/labels")]
    [ApiController]
    public class LabelController:ControllerBase
    {
        /// <remarks>
        /// 返回所有的label类别
        /// </remarks>
        [HttpGet]
        public async Task<ActionResult<Response>> GetLabels()
        {
            var obj = await AzureService.GetLabels();
            return Ok(new Response().GetJObject("categories", obj));
        }
    }
}
