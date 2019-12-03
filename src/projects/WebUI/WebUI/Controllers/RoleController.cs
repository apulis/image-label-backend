using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Common.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Azure;
using WebUI.Models;
using WebUI.Utils;
using WebUI.ViewModels;
using AzureService = WebUI.Services.AzureService;

namespace WebUI.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoleController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public RoleController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            return View(roles);
        }

        public IActionResult AddRole()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddRole(RoleAddViewModel roleAddViewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(roleAddViewModel);
            }

            var role = new IdentityRole
            {
                Name = roleAddViewModel.RoleName
            };

            var result = await _roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(roleAddViewModel);
        }

        public async Task<IActionResult> EditRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);

            if (role == null)
            {
                return RedirectToAction("Index");
            }

            var roleEditViewModel = new RoleEditViewModel
            {
                Id = id,
                RoleName = role.Name,
                Users = new List<string>()
            };
            List<string> userList = new List<string>();

            var re = SessionOps.GetSessionList<string>(HttpContext.Session.Get($"role_{id}_user_list"));
            if (re != null)
            {
                userList = re;
            }
            else
            {
                var configAuthorization = Config.App.GetJToken(Constants.JsontagAuthorization) as JObject;
                var authBlob = AzureService.GetBlob(null, "index", WebUIConfig.AppInfoConfigFile);
                var json = await authBlob.DownloadGenericObjectAsync();
                var addAuth = JsonUtils.GetJToken(Constants.JsontagAuthorization, json);
                var addAuthObj = addAuth == null ? null : addAuth as JObject;
                if (!Object.ReferenceEquals(addAuthObj, null))
                {
                    addAuthObj.Merge(configAuthorization, new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Union
                    });
                }
                if (!Object.ReferenceEquals(addAuthObj, null))
                {
                    foreach (var pair in addAuthObj)
                    {
                        if (pair.Key == role.Name)
                        {
                            var peopleArray = pair.Value as JArray;
                            foreach (var onepeople in peopleArray)
                            {
                                userList.Add(onepeople.ToString());
                            }
                        }
                    }
                }
                SessionOps.SetSession($"role_{id}_user_list", userList,HttpContext.Session);
            }
            roleEditViewModel.Users = userList;
            return View(roleEditViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> EditRole(RoleEditViewModel roleEditViewModel)
        {
            var role = await _roleManager.FindByIdAsync(roleEditViewModel.Id);

            if (role != null)
            {
                role.Name = roleEditViewModel.RoleName;

                var result = await _roleManager.UpdateAsync(role);

                if (result.Succeeded)
                {
                    return RedirectToAction("Index");
                }

                ModelState.AddModelError(string.Empty, "更新角色时出错");

                return View(roleEditViewModel);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role != null)
            {
                var result = await _roleManager.DeleteAsync(role);
                if (result.Succeeded)
                {
                    return RedirectToAction("Index");
                }
                ModelState.AddModelError(string.Empty, "删除角色时出错");
            }
            ModelState.AddModelError(string.Empty, "没找到该角色");
            return View("Index", await _roleManager.Roles.ToListAsync());
        }

        public async Task<IActionResult> AddUserToRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);

            if (role == null)
            {
                return RedirectToAction("Index");
            }
            var vm = new UserRoleViewModel
            {
                RoleId = role.Id
            };
            return View(vm);

        }

        [HttpPost]
        public async Task<IActionResult> AddUserToRole(UserRoleViewModel userRoleViewModel)
        {
            var role = await _roleManager.FindByIdAsync(userRoleViewModel.RoleId);

            if (await AzureService.FindUserId(userRoleViewModel.Email)==null)
            {
                ModelState.AddModelError(string.Empty, "Email not exist!  Please confirm your email.");
                return View(userRoleViewModel);
            }
            var authBlob = AzureService.GetBlob(null, "index", WebUIConfig.AppInfoConfigFile);
            var json = await authBlob.DownloadGenericObjectAsync();
            var addAuth = JsonUtils.GetJToken(Constants.JsontagAuthorization, json);
            var addAuthObj = addAuth == null ? null : addAuth as JObject;
            foreach (var pair in addAuthObj)
            {
                if (pair.Key == role.Name)
                {
                    var peopleArray = pair.Value as JArray;
                    foreach (var onepeople in peopleArray)
                    {
                        if (String.Compare(onepeople.ToString(), userRoleViewModel.Email, true) == 0)
                        {
                            return RedirectToAction("EditRole", new { id = role.Id });
                        }
                    }
                    var user =await _userManager.FindByEmailAsync(userRoleViewModel.Email);
                    if (user!=null)
                    {
                        await _userManager.AddToRoleAsync(user, role.Name);
                    }
                    peopleArray.Add(userRoleViewModel.Email);
                    SessionOps.AddSession<string>($"role_{userRoleViewModel.RoleId}_user_list", userRoleViewModel.Email,
                        HttpContext.Session.Get($"role_{userRoleViewModel.RoleId}_user_list"),
                        HttpContext.Session);
                    await authBlob.UploadGenericObjectAsync(json);
                }
            }
            return RedirectToAction("EditRole", new { id = role.Id });

        }

        public async Task<IActionResult> DeleteUserFromRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                return RedirectToAction("Index");
            }

            var vm = new UserRoleViewModel
            {
                RoleId = role.Id
            };

            List<string> userList = new List<string>();
            var re = SessionOps.GetSessionList<string>(HttpContext.Session.Get($"role_{roleId}_user_list"));
            if (re != null)
            {
                userList = re;
            }
            else
            {
                var authBlob = AzureService.GetBlob(null, "index", WebUIConfig.AppInfoConfigFile);
                var json = await authBlob.DownloadGenericObjectAsync();
                var addAuth = JsonUtils.GetJToken(Constants.JsontagAuthorization, json);
                var addAuthObj = addAuth == null ? null : addAuth as JObject;

                if (!Object.ReferenceEquals(addAuthObj, null))
                {
                    foreach (var pair in addAuthObj)
                    {
                        if (pair.Key == role.Name)
                        {
                            var peopleArray = pair.Value as JArray;
                            foreach (var onepeople in peopleArray)
                            {
                                userList.Add(onepeople.ToString());
                            }
                        }
                    }
                }
                SessionOps.SetSession($"role_{roleId}_user_list", userList,HttpContext.Session);
            }
            vm.Users = userList;
            return View(vm);

        }

        [HttpPost]
        public async Task<IActionResult> DeleteUserFromRole(UserRoleViewModel userRoleViewModel)
        {
            var role = await _roleManager.FindByIdAsync(userRoleViewModel.RoleId);
            var authBlob = AzureService.GetBlob(null, "index", WebUIConfig.AppInfoConfigFile);
            var json = await authBlob.DownloadGenericObjectAsync();
            var addAuth = JsonUtils.GetJToken(Constants.JsontagAuthorization, json);
            var addAuthObj = addAuth == null ? null : addAuth as JObject;
            List<string> userList = new List<string>();
            foreach (var pair in addAuthObj)
            {
                if (pair.Key == role.Name)
                {
                    var peopleArray = pair.Value as JArray;
                    foreach (var onepeople in peopleArray)
                    {
                        if (String.Compare(onepeople.ToString(), userRoleViewModel.Email, true) == 0)
                        {
                            var user = await _userManager.FindByEmailAsync(userRoleViewModel.Email);
                            if (user!=null)
                            {
                                await _userManager.RemoveFromRoleAsync(user, role.Name);
                            }
                            peopleArray.Remove(onepeople);
                            await authBlob.UploadGenericObjectAsync(json);
                            SessionOps.RemoveSession<string>($"role_{userRoleViewModel.RoleId}_user_list", onepeople.ToString(),
                                HttpContext.Session.Get($"role_{userRoleViewModel.RoleId}_user_list"),
                                HttpContext.Session);
                            break;
                        }
                    }

                }
            }
            return RedirectToAction("EditRole", new { id = role.Id });
        }

        [AcceptVerbs("Get", "Post")]
        public async Task<IActionResult> CheckRoleExist([Bind("RoleName")] string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                // return Json(false);
                return Json("角色已经存在了");
            }

            return Json(true);
        }
    }
}
