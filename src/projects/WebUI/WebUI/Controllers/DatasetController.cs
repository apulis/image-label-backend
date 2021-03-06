﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
    [Route("api/projects/{projectId}/datasets")]
    [ApiController]
    public class DatasetController:ControllerBase
    {
        private readonly ILogger _logger;
        public DatasetController(ILoggerFactory logger)
        {
            _logger = logger.CreateLogger("DatasetController");
        }
        /// <remarks>
        /// 获取当前用户的projectId对应的project下的所属数据集
        /// 返回List,数据集列表
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="page">当前第几页，从1开始递增</param>
        /// <param name="size">每页的数量</param>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DatasetViewModel>>> GetDatasets(Guid projectId, [FromQuery]QueryStringParameters parameters)
        {
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var role = await AzureService.FindUserRole(userId);
                List<DatasetViewModel> datasetList = await AzureService.getDatasets(userId, convertProjectId, role);
                if (!string.IsNullOrWhiteSpace(parameters.orderBy) && parameters.orderBy == "name")
                {
                    datasetList = datasetList.OrderBy(o => o.name).ToList();
                    if (!string.IsNullOrWhiteSpace(parameters.order) && parameters.order == "desc")
                    {
                        datasetList.Reverse();
                    }
                }
                else
                {
                    datasetList.Reverse();
                }

                if (!string.IsNullOrWhiteSpace(parameters.name))
                {
                    datasetList = datasetList.FindAll(p => p.name.Contains(parameters.name));
                }

                var list = PageOps.GetPageRange(datasetList, parameters.page, parameters.size, datasetList.Count);
                return Ok(new Response().GetJObject("datasets", list, "totalCount", datasetList.Count));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
            
        }
        /// <remarks>
        /// 为project添加数据集,需name、info和type字段，datasetId可选
        /// 关于datasetId字段，如果已存在azure blob上的GUID，则需填写，否则无需填写，将新生成
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="AddDatasetViewModel">字典，包含name\info\type\labels</param>
        [HttpPost]
        public async Task<ActionResult<Response>> AddDataset(Guid projectId,[FromBody]AddDatasetViewModel dataSetViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response { Successful = "true", Msg = ModelState.Values.ToString(), Data = null });
            }
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var currentUserId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var role = await AzureService.FindUserRole(currentUserId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
                {
                    return StatusCode(403);
                }
                var datasetId = await AzureService.AddDataset(convertProjectId, dataSetViewModel);
                return Ok(new Response().GetJObject("datasetId", datasetId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }

        }
        /// <remarks>
        /// 查询一个特定的dataset详细信息
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataSetId的GUid</param>
        [HttpGet("{dataSetId}")]
        public async Task<ActionResult<DatasetViewModel>> getDatasetInfo(Guid projectId, Guid dataSetId)
        {
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var role = await AzureService.FindUserRole(userId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
                {
                    var res = await AzureService.CheckUserHasThisDataset(userId, convertProjectId, convertDataSetId);
                    if (!res && role != "labeler")
                    {
                        return StatusCode(403);
                    }
                }
                var obj = await AzureService.getDatasetInfo(convertProjectId, convertDataSetId);
                return Ok(new Response().GetJObject("info", obj));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 修改一个特定的dataset
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataSetId的GUid</param>
        /// <param name="dataSetViewModel">新的name\info\type\labels字段,json格式</param>
        [HttpPatch("{dataSetId}")]
        public async Task<ActionResult<Response>> UpdateDataset(Guid projectId, Guid dataSetId, [FromBody]DatasetViewModel dataSetViewModel)
        {
            if (!ModelState.IsValid)
            {
                return Ok(new Response { Successful = "true", Msg = ModelState.Values.ToString(), Data = null });
            }
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var currentUserId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var role = await AzureService.FindUserRole(currentUserId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
                {
                    return StatusCode(403);
                }
                await AzureService.UpdateDataset(convertProjectId, convertDataSetId, dataSetViewModel);
                return Ok(new Response { Msg = "ok" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 为project删除数据集
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">将要删除dataset的GUid</param>
        [HttpDelete]
        public async Task<ActionResult<Response>> RemoveDataSet(Guid projectId,[FromBody]Guid dataSetId)
        {
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var reader = new StreamReader(Request.Body);
                var body = reader.ReadToEnd();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var currentUserId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var role = await AzureService.FindUserRole(currentUserId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
                {
                    return StatusCode(403);
                }
                var dataSetBindId = await AzureService.RemoveDataSet(convertProjectId, convertDataSetId);
                return Ok(new Response().GetJObject("dataSetBindId", int.Parse(dataSetBindId)));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 获取project下特定数据集的标注用户列表
        /// 返回用户信息列表
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="page">当前第几页，从1开始递增</param>
        /// <param name="size">每页的数量</param>
        [HttpGet("{datasetId}/users")]
        public async Task<ActionResult<IEnumerable<UserInfoViewModel>>> GetDataSetUsers(Guid projectId, Guid dataSetId, [FromQuery]QueryStringParameters parameters)
        {
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var currentUserId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var role = await AzureService.FindUserRole(currentUserId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
                {
                    return StatusCode(403);
                }
                var userList = await AzureService.GetDataSetUsers(convertProjectId, convertDataSetId);
                var list = PageOps.GetPageRange(userList, parameters.page, parameters.size, userList.Count);
                return Ok(new Response().GetJObject("users", list, "totalCount", userList.Count));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 为project下特定数据集删除指定的标注用户
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="userNumber">将要删除的用户唯一标识数字</param>
        [HttpDelete("{datasetId}/users")]
        public async Task<ActionResult<Response>> RemoveUser(Guid projectId, Guid dataSetId,[FromBody]int userNumber)
        {
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var currentUserId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var role = await AzureService.FindUserRole(currentUserId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
                {
                    return StatusCode(403);
                }
                await AzureService.RemoveUser(convertProjectId, convertDataSetId, HttpContext.Session, userNumber);
                return Ok(new Response { Msg = "ok" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 为project下特定数据集添加指定number的标注用户
        /// 如果成功，返回msg=ok,successful="true"
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="userNumber">用户唯一标识数字</param>
        [HttpPost("{datasetId}/users")]
        public async Task<ActionResult<Response>> AddUserToDataSet(Guid projectId, Guid dataSetId,[FromBody]int userNumber)
        {
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var currentUserId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var role = await AzureService.FindUserRole(currentUserId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
                {
                    return StatusCode(403);
                }
                await AzureService.AddUserToDataSet(convertProjectId, convertDataSetId, userNumber, HttpContext.Session);
                return Ok(new Response() { Msg = "ok" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 检测指定number的标注用户是否已经存在于project下特定数据集
        /// user already exists!表示用户已经存在于数据集，Cannot find userId!表示找不到该用户number，ok表示可以添加
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="userNumber">用户唯一标识数字</param>
        [HttpGet("{datasetId}/users/{userNumber}")]
        public async Task<ActionResult<Response>> CheckUserExists(Guid projectId, Guid dataSetId,int userNumber)
        {
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var currentUserId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var role = await AzureService.FindUserRole(currentUserId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(currentUserId, convertProjectId))
                {
                    return StatusCode(403);
                }
                var str = await AzureService.CheckUserExists(convertProjectId, convertDataSetId, userNumber);
                return Ok(new Response { Msg = str });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 获取数据集的可标注任务列表,包含已修改task+一个锁定的task
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="page">当前第几页，从1开始递增</param>
        /// <param name="size">每页的数量</param>
        [HttpGet("{datasetId}/tasks")]
        public async Task<ActionResult<IEnumerable<TaskViewModel>>> getTasks(Guid projectId, Guid dataSetId, [FromQuery]QueryStringParameters parameters)
        {
            try
            {
                var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var role = await AzureService.FindUserRole(userId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
                {
                    var res = await AzureService.CheckUserHasThisDataset(userId, convertProjectId, convertDataSetId);
                    if (!res && role != "labeler")
                    {
                        return StatusCode(403);
                    }
                    // for labels,get tasks from another func
                    //var taskList = await AzureService.getDatasetTaskList(userId, convertProjectId, convertDataSetId);
                    //return Ok(new Response().GetJObject("taskList", taskList));
                }
                var adminTaskList = await AzureService.getTasks(convertProjectId, convertDataSetId);
                var list = PageOps.GetPageRange(adminTaskList, parameters.page, parameters.size, adminTaskList.Count);
                return Ok(new Response().GetJObject("taskList", list, "totalCount", adminTaskList.Count));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 获取下一个可标注任务
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        [HttpGet("{datasetId}/tasks/next/{taskId}")]
        public async Task<ActionResult<TaskViewModel>> GetNextTask(Guid projectId, Guid dataSetId,string taskId)
        {
            try
            {
                var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var role = await AzureService.FindUserRole(userId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
                {
                    var has = await AzureService.CheckLabelerHasThisTask(userId, convertProjectId, convertDataSetId, taskId);
                    if (!has && role != "labeler")
                    {
                        return StatusCode(403);
                    }
                }
                JObject nextObj = await AzureService.getDatasetTaskNext(userId, convertProjectId, convertDataSetId, taskId);
                return Ok(new Response().GetJObject("next", nextObj));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 获取taskId对应的后缀
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        [HttpGet("{datasetId}/tasks/suffix/{taskId}")]
        public async Task<ActionResult<TaskViewModel>> GETTASKSUFFIX(Guid projectId, Guid dataSetId, string taskId)
        {
            try
            {
                var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var role = await AzureService.FindUserRole(userId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
                {
                    var has = await AzureService.CheckLabelerHasThisTask(userId, convertProjectId, convertDataSetId, taskId);
                    if (!has && role != "labeler")
                    {
                        return StatusCode(403);
                    }
                }
                string suffix = await AzureService.getTaskSuffix(convertProjectId, convertDataSetId, taskId);
                return Ok(new Response().GetJObject("suffix", suffix));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        [HttpGet("{datasetId}/tasks/previous/{taskId}")]
        public async Task<ActionResult<TaskViewModel>> GetPreviousTask(Guid projectId, Guid dataSetId, string taskId)
        {
            try
            {
                var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var role = await AzureService.FindUserRole(userId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
                {
                    var has = await AzureService.CheckLabelerHasThisTask(userId, convertProjectId, convertDataSetId, taskId);
                    if (!has && role != "labeler")
                    {
                        return StatusCode(403);
                    }
                }
                JObject nextObj = await AzureService.getDatasetTaskPrevious(userId, convertProjectId, convertDataSetId, taskId);
                return Ok(new Response().GetJObject("previous", nextObj));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 获取详细标注信息annotations
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="taskId">task的id</param>
        [HttpGet("{datasetId}/tasks/annotations/{taskId}")]
        public async Task<IActionResult> GetOneTask(Guid projectId, Guid dataSetId,string taskId)
        {
            try
            {
                var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var role = await AzureService.FindUserRole(userId);
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
                {
                    var has = await AzureService.CheckLabelerHasThisTask(userId, convertProjectId, convertDataSetId, taskId);
                    if (!has && role != "labeler")
                    {
                        return StatusCode(403);
                    }
                }
                var projectObj = await AzureService.GetOneTask(convertProjectId, convertDataSetId, taskId);
                return Ok(new Response().GetJObject("annotations", projectObj));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 提交标注信息annotations
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="taskId">task的id</param>
        /// <param name="value">标注信息，json格式</param>
        [HttpPost("{datasetId}/tasks/annotations/{taskId}")]
        public async Task<ActionResult<Response>> Post(Guid projectId, Guid dataSetId, string taskId, [FromBody] JObject value)
        {
            try
            {
                var userId = HttpContext.User.Claims.First(c => c.Type == "uid").Value.ToString();
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var role = await AzureService.FindUserRole(userId);
                _logger.LogInformation($"get role {role}");
                if (role != "admin" && !await AzureService.FindUserIsProjectManager(userId, convertProjectId))
                {
                    var has = await AzureService.CheckLabelerHasThisTask(userId, convertProjectId, convertDataSetId, taskId);
                    if (!has && role != "labeler")
                    {
                        return StatusCode(403);
                    }
                }
                await AzureService.PostOneTask(convertProjectId, convertDataSetId, taskId, userId, role, value);
                return Content(new Response { Msg = "ok" }.JObjectToString());
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <remarks>
        /// 该数据集的所有labels类别
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        [HttpGet("{datasetId}/tasks/labels")]
        [ProducesResponseType(typeof(List<LabelViewModel>), 200)]
        public async Task<ActionResult<Response>> GetDataSetLabels(Guid projectId, Guid dataSetId)
        {
            try
            {
                var convertProjectId = projectId.ToString().ToUpper();
                var convertDataSetId = dataSetId.ToString().ToUpper();
                var labels = await AzureService.GetDataSetLabels(convertProjectId, convertDataSetId);
                return Ok(new Response().GetJObject("annotations", labels));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
        /// <summary>
        /// explore接口，搜索数据集.
        /// </summary>
        /// <remarks>
        /// 该数据集下的搜索对应label列表类别的图片人工标注信息和模型预测结果
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        /// <param name="category_ids">要搜索的category id列表</param>
        /// <param name="page">当前第几页，从1开始递增</param>
        /// <param name="size">每页的数量</param>
        [HttpGet("{datasetId}/tasks/search")]
        [ProducesResponseType(typeof(List<AnnotationViewModel>), 200)]
        public async Task<ActionResult<Response>> GetDataSetByLabels(Guid projectId, Guid dataSetId, [FromQuery]QueryStringParameters parameters)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            List<JObject> annotationViewModels = new List<JObject>();
            List<JObject> predictAnnotationViewModels = new List<JObject>();
            List<string> taskIds = await AzureService.GetDataSetBySearch(convertProjectId, convertDataSetId, parameters);
            taskIds = await AzureService.FilterTasksByIOU(taskIds, parameters, convertProjectId, convertDataSetId);
            var list = PageOps.GetPageRange(taskIds, parameters.page, parameters.size, taskIds.Count);
            foreach (var taskId in list)
            {
                annotationViewModels.Add(await AzureService.GetOneTask(convertProjectId, convertDataSetId, taskId));
                predictAnnotationViewModels.Add(await AzureService.SelectAnnoByIouRange(convertProjectId, convertDataSetId, taskId, parameters));
            }
            return Ok(new Response().GetJObject("taskIds", annotationViewModels, "totalCount", taskIds.Count,"prediction", predictAnnotationViewModels));
        }
        /// <summary>
        /// mAP数据接口
        /// </summary>
        /// <remarks>
        /// 返回该数据集下的模型预测结果的mAP数据获取接口
        /// </remarks>
        /// <param name="projectId">project的GUid</param>
        /// <param name="dataSetId">dataset的GUid</param>
        [HttpGet("{datasetId}/tasks/map")]
        [ProducesResponseType(typeof(List<MapViewModel>), 200)]
        public async Task<ActionResult<Response>> GetDataSetMap(Guid projectId, Guid dataSetId, [FromQuery]QueryStringParameters parameters)
        {
            var convertProjectId = projectId.ToString().ToUpper();
            var convertDataSetId = dataSetId.ToString().ToUpper();
            var array = await AzureService.GetDatasetMap(convertProjectId, convertDataSetId);
            JArray newArray = new JArray();
            if (!object.ReferenceEquals(array, null))
            {
                foreach (var oneThr in array)
                {
                    var oneThrObj = oneThr as JObject;
                    var oneThrData = Json.GetJToken("data", oneThrObj) as JArray;
                    newArray.Add(new JObject() { { "iouThr", Json.GetJToken("iouThr", oneThrObj) }, { "data", JToken.FromObject(PageOps.GetPageRange(oneThrData.ToList(), parameters.page, parameters.size, oneThrData.Count)) },{"mean_ap", Json.GetJToken("mean_ap", oneThrObj) } });
                }
            }
            
            return Ok(new Response().GetJObject("data",newArray, "totalCount", array!=null?array[0]["data"].Count():0));
        }
    }
}
