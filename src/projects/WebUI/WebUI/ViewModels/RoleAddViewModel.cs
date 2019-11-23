using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using WebUI.Controllers;

namespace WebUI.ViewModels
{
    public class RoleAddViewModel
    {
        [Required]
        [Display(Name = "角色名称")]
        [Remote(nameof(RoleController.CheckRoleExist), "Role", ErrorMessage = "角色已存在")]
        public string RoleName { get; set; }
    }
}
