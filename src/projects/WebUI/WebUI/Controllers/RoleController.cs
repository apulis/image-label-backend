using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Azure;
using WebUI.Models;
using WebUI.Utils;
using WebUI.ViewModels;

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
            var configAuthorization = Config.App.GetJToken(Constants.JsontagAuthorization) as JObject;
            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference("index");
            var authBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
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
                            roleEditViewModel.Users.Add(onepeople.ToString());
                        }
                    }
                }
            }
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
            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference("index");
            var authBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
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
                    peopleArray.Add(userRoleViewModel.Email);
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

            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference("index");
            var authBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
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
                            vm.Users.Add(onepeople.ToString());
                        }
                    }
                }
            }

            return View(vm);

        }

        [HttpPost]
        public async Task<IActionResult> DeleteUserFromRole(UserRoleViewModel userRoleViewModel)
        {
            var role = await _roleManager.FindByIdAsync(userRoleViewModel.RoleId);
            var configAuthorization = Config.App.GetJToken(Constants.JsontagAuthorization) as JObject;
            var container = CloudStorage.GetContainer(null);
            var dirpath = container.GetDirectoryReference("index");
            var authBlob = dirpath.GetBlockBlobReference(WebUIConfig.AppInfoConfigFile);
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
                            peopleArray.Remove(onepeople);
                            await authBlob.UploadGenericObjectAsync(json);
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
