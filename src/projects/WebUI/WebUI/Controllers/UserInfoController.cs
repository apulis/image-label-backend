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
        /// <remarks>
        /// 返回当前登录用户的基本信息
        /// </remarks>
        [HttpGet]
        public async Task<ActionResult<UserInfoViewModel>> Get()
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
        /// <summary>
        /// Creates a TodoItem.
        /// </summary>
        /// <remarks>
        /// 返回用户number对应的用户唯一标识id
        /// </remarks>
        /// <param name="userNumber">用户唯一标识数字</param>
        [HttpGet("userId/{userNumber}")]
        public async Task<ActionResult<Response>> GetUserId(int userNumber)
        {
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            return Ok(new Response().GetJObject("userId", JToken.FromObject(userId)));
        }
        /// <remarks>
        /// 添加用户到管理员，需管理员才可请求成功
        /// </remarks>
        /// <param name="userNumber">用户唯一标识数字</param>
        [HttpPost("AddUserToAdmin")]
        public async Task<ActionResult<Response>> AddUserToAdmin([FromBody]int userNumber)
        {
            var identityId = await AzureService.FindUserIdByNumber(userNumber);
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            bool ret = false;
            if (role == "admin")
            {
                ret = await AzureService.AddUserToAdmin(identityId);
            }
            return Ok(new Response().GetJObject("result", ret));
        }
        /// <remarks>
        /// 管理员获取用户列表
        /// </remarks>
        [HttpGet("GetUserList")]
        public async Task<ActionResult<UserListViewModel>> GetUserList()
        {
            var userId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            return Ok(new Response().GetJObject("users", JToken.FromObject(await AzureService.GetUserList())));
        }
    }
}
