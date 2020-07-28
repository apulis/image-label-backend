using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Models;
using WebUI.Parameters;
using WebUI.Services;
using WebUI.ViewModels;

namespace WebUI.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [EnableCors("dev-use")]
    [Route("api/projects")]
    [ApiController]
    public class ProjectController:ControllerBase
    {
        /// <remarks>
        /// 获取当前用户的所属project列表
        /// 返回List
        /// </remarks>
        /// <param name="page">当前第几页，从1开始递增</param>
        /// <param name="size">每页的数量</param>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProjectViewModel>>> GetProjects([FromQuery]QueryStringParameters parameters)
        {
            var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
            List<ProjectViewModel> accounts = await AzureService.FindUserRoleDetail(userId);
            if (!string.IsNullOrWhiteSpace(parameters.orderBy)&&parameters.orderBy=="name")
            {
                accounts = accounts.OrderBy(o => o.Name).ToList();
                if (!string.IsNullOrWhiteSpace(parameters.order)&&parameters.order=="desc")
                {
                    accounts.Reverse();
                }
            }
            else
            {
                accounts.Reverse();
            }
            if (!string.IsNullOrWhiteSpace(parameters.name))
            {
                accounts = accounts.FindAll(p => p.Name.Contains(parameters.name));
            }

            var list = PageOps.GetPageRange(accounts, parameters.page, parameters.size, accounts.Count);
            return Ok(new Response().GetJObject("projects", list, "totalCount", accounts.Count));
        }
        [HttpGet("/api/listDatasets")]
        public async Task<ActionResult<IEnumerable<DatasetViewModel>>> ListDatasets([FromQuery]QueryStringParameters parameters)
        {
            List<DatasetViewModel> datasetList = new List<DatasetViewModel>();
            var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
            List<ProjectViewModel> accounts = await AzureService.FindUserRoleDetail(userId);
            var role = await AzureService.FindUserRole(userId);
            foreach (var one in accounts)
            {
                datasetList.AddRange(await AzureService.getDatasets(userId, one.ProjectId, role));
            }
            datasetList.Reverse();
            var list = PageOps.GetPageRange(datasetList, parameters.page, parameters.size, datasetList.Count);
            return Ok(new Response().GetJObject("datasets", list, "totalCount", datasetList.Count));
        }
        /// <remarks>
        /// 删除一个project
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        [HttpDelete("{projectId}")]
        public async Task<ActionResult<Response>> DeleteProject(Guid projectId)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            var str = await AzureService.DeleteProject(convertProjectId, HttpContext.Session);
            return Ok(new Response { Msg = str??"" });
        }
        /// <remarks>
        /// 添加一个project
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="accountViewModel">需name和info字段</param>
        [HttpPost]
        public async Task<ActionResult<Response>> AddProject([FromBody]AddProjectViewModel accountViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response{Successful = "true",Msg=ModelState.Values.ToString(),Data= null });
            }
            var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
            var role = await AzureService.FindUserRole(userId);
            //if (role != "admin")
            //{
            //    return StatusCode(403);
            //}
            string projectId = await AzureService.AddProject(accountViewModel);
            await AzureService.AddProjectManagerByUserId(projectId, userId);
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 修改一个特定的project
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="accountViewModel">新的name和info字段</param>
        [HttpPatch("{projectId}")]
        public async Task<ActionResult<Response>> UpdateProject(Guid projectId, AddProjectViewModel accountViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response { Successful = "true", Msg = ModelState.Values.ToString(), Data = null });
            }
            var convertProjectId = projectId.ToString().ToUpper();
            var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            await AzureService.UpdateProject(convertProjectId, accountViewModel);
            return Ok(new Response { Msg = "ok" });
        }
        /// <remarks>
        /// 获取指定的project的项目管理员列表
        /// 返回List用户信息列表
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        [HttpGet("{projectId}/managers")]
        public async Task<ActionResult<IEnumerable<UserInfoViewModel>>> GetProjectManagers(Guid projectId)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
            var role = await AzureService.FindUserRole(userId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            var managerList = await AzureService.GetProjectManagers(convertProjectId);
            return Ok(new Response().GetJObject("managers", managerList));
        }
        /// <remarks>
        /// 检测用户是否已经是该project的项目管理员
        /// manager already exists!表示用户已经存在于数据集，Cannot find userId!表示找不到该用户number，ok表示可以添加
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        ///<param name = "userNumber" > 用户唯一标识数字 </param>
        [HttpGet("{projectId}/managers/{userNumber}")]
        public async Task<ActionResult<Response>> CheckProjectManagerExists(Guid projectId, int userNumber)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            var userId =await AzureService.FindUserIdByNumber(userNumber);
            if (userId == null)
            {
                return Ok(new Response {Msg = "Cannot find userId!"});
            }
            var str = await AzureService.CheckProjectManagerExists(convertProjectId, userId);
            return Ok(new Response {Msg = str??""});
        }
        /// <remarks>
        /// 为指定的project添加项目管理员
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="userNumbers">用户的唯一标识数字列表</param>
        [HttpPost("{projectId}/managers")]
        public async Task<ActionResult<Response>> AddProjectManager(Guid projectId,[FromBody]List<int> userNumbers)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            var str = await AzureService.AddProjectManager(convertProjectId, userNumbers);
            return Ok(new Response{Msg = str??"add succeed"});
        }
        /// <remarks>
        /// 为指定的project删除项目管理员
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="userNumber">用户唯一标识数字</param>
        [HttpDelete("{projectId}/managers")]
        public async Task<ActionResult<Response>> DeleteProjectManager(Guid projectId,[FromBody] int userNumber)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var currentUserId = HttpContext.User.Identity.Name;
            var role = await AzureService.FindUserRole(currentUserId);
            if (role != "admin")
            {
                return StatusCode(403);
            }
            var userId = await AzureService.FindUserIdByNumber(userNumber);
            if (userId == null)
            {
                return Ok(new Response { Msg = $"user number {userNumber} wrong!" });
            }
            await AzureService.DeleteProjectManager(convertProjectId, userId);
            return Ok(new Response{Msg = "delete success"});
        }
    }
}
