using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using WebUI.Services;

namespace WebUI.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableCors("dev-use")]
    [Route("api/[controller]")]
    [ApiController]
    public class UserInfoController:ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = HttpContext.User.Identity.Name;
            var obj = await AzureService.FindUserInfo(userId);
            return Content(new Response { Successful = "true", Msg = "", Data = obj }.JObjectToString());
        }
    }
}
