﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using WebUI.Services;
using WebUI.ViewModels;

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
            var role = await AzureService.FindUserRole(userId);
            JObject obj = await AzureService.FindUserInfo(userId);
            if (role != "admin")
            {
                List<ProjectViewModel> accounts = await AzureService.FindUserRoleDetail(userId);
                obj.Add("role", JToken.FromObject(accounts));
            }
            obj.Add("isAdmin", role == "admin");
            return Ok(new Response().GetJObject("userInfo",JToken.FromObject(obj)));
        }
        [HttpGet("userId/{userNumber}")]
        public async Task<IActionResult> GetUserId(int userNumber)
        {
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            return Ok(new Response().GetJObject("userId", JToken.FromObject(userId)));
        }
    }
}
