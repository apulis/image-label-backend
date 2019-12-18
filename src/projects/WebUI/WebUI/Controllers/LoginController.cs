﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Utils.Json;
using WebUI.Models;
using WebUI.Services;
using WebUI.ViewModels;

namespace WebUI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        public IConfiguration _configuration { get; }
        private readonly ILogger _logger;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public string LoginProvider { get; set; }


        public LoginController(ILoggerFactory logger, IConfiguration configuration, SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
        {
            _configuration = configuration;
            _signInManager = signInManager;
            _logger = logger.CreateLogger("ImageController");
            _userManager = userManager;
        }
        
        public UserInfoViewModel Input { get; set; }

        public static string Get(string _url)
        {
            WebClient wc = new WebClient();
            string strReturn = wc.DownloadString(_url);
            return strReturn;
        }

        public static string Get(string _url, Dictionary<string, string> dic)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url);    //创建一个请求示例 
            foreach (var one in dic)
            {
                request.Headers[one.Key] = one.Value;
            }
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();  //获取响应，即发送请求
            Stream responseStream = response.GetResponseStream();
            StreamReader streamReader = new StreamReader(responseStream, Encoding.UTF8);
            string str = streamReader.ReadToEnd();
            return str;
        }
        public static string Post(string url, Dictionary<string, string> dic)
        {
            string result = "";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            #region 添加Post 参数
            StringBuilder builder = new StringBuilder();
            int i = 0;
            foreach (var item in dic)
            {
                if (i > 0)
                    builder.Append("&");
                builder.AppendFormat("{0}={1}", item.Key, item.Value);
                i++;
            }
            byte[] data = Encoding.UTF8.GetBytes(builder.ToString());
            req.ContentLength = data.Length;
            using (Stream reqStream = req.GetRequestStream())
            {
                reqStream.Write(data, 0, data.Length);
                reqStream.Close();
            }
            #endregion
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            //获取响应内容
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }
        private string getToken(string signinType,string code)
        {
            if (signinType == "wechat")
            {
                string _url = "https://api.weixin.qq.com/sns/oauth2/access_token?appid=" +
                              _configuration["Authentication:WeChat:AppId"] + "&secret=" +
                              _configuration["Authentication:WeChat:AppSecret"] + "&code=" + code + "&grant_type=authorization_code";
                return Get(_url);
            }
            if(signinType == "microsoft")
            {
                Dictionary<string, string> myDictionary = new Dictionary<string, string>();
                myDictionary.Add("client_id", _configuration["Authentication:Microsoft:ClientId"]);
                myDictionary.Add("client_secret", _configuration["Authentication:Microsoft:ClientSecret"]);
                myDictionary.Add("code", code);
                myDictionary.Add("grant_type", "authorization_code");
                myDictionary.Add("redirect_uri", $"{_configuration["BackEndUrl"]}/api/login/microsoft");
                return Post("https://login.microsoftonline.com/common/oauth2/token", myDictionary);
            }

            return null;
        }
        private string getUserInfo(string signinType,string access_token,string openid)
        {
            if (signinType == "wechat")
            {
                string _url = "https://api.weixin.qq.com/sns/userinfo?access_token=" +
                              access_token + "&openid=" + openid + "&lang=zh_CN";
                return Get(_url);
            }

            if (signinType == "microsoft")
            {
                string url = "https://graph.microsoft.com/v1.0/me";
                Dictionary<string,string> header = new Dictionary<string, string>();
                header.Add("Authorization", "Bearer "+ access_token);
                return Get(url, header);
            }

            return null;

        }

        [HttpGet("{signinType}")]
        public async Task<IActionResult> Get(string signinType,string returnUrl = null, string remoteError = null,string code = null)
        {
            if (remoteError != null)
            {
                return Redirect($"{_configuration["FontEndUrl"]}");
            }
            string tokenStr = getToken(signinType,code);
            if (tokenStr == null)
            {
                return Redirect($"{_configuration["FontEndUrl"]}");
            }
            var json = JsonConvert.DeserializeObject<JObject> (tokenStr);
            string access_token = JsonUtils.GetJToken("access_token", json).ToString();
            string refresh_token = JsonUtils.GetJToken("refresh_token", json).ToString();
            string openid = JsonUtils.GetJToken("openid", json)==null?null: JsonUtils.GetJToken("openid", json).ToString();

            string infoStr = getUserInfo(signinType,access_token, openid);
            var infoJson = JsonConvert.DeserializeObject<JObject>(infoStr);
            if (infoJson.GetValue("mail") != null)
            {
                var name = infoJson.GetValue("displayName").ToString();
                var email = infoJson.GetValue("mail").ToString();
                if (name == null)
                {
                    name = email;
                }
                Input = new UserInfoViewModel
                {
                    Email = email,
                    Name = name,
                    Id = infoJson.GetValue("id").ToString()
                };
            }
            else if(infoJson.GetValue("openid") != null)
            {
                Input = new UserInfoViewModel
                {
                    Name = infoJson.GetValue("nickname").ToString(),
                    Id = infoJson.GetValue("openid").ToString(),
                    Email = JsonUtils.GetJToken("email", json) == null ? null : JsonUtils.GetJToken("email", json).ToString()
            };
            }

            if (Input != null)
            {
                await AzureService.CreateUserId(Input);
                var userId = await AzureService.FindUserIdByOpenId(Input.Id);
                if (userId == null)
                {
                    return Redirect($"{_configuration["FontEndUrl"]}");
                }
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["SecurityKey"]));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, userId)
                };
                var token = new JwtSecurityToken(
                    issuer: "apulis-china-infra01.sigsus.cn",
                    audience: "apulis-china-infra01.sigsus.cn",
                    claims: claims,
                    expires: DateTime.Now.AddDays(1),
                    signingCredentials: creds);

                var tokenGenerate = new JwtSecurityTokenHandler().WriteToken(token);
                return Redirect($"{_configuration["FontEndUrl"]}/?token={tokenGenerate}");
            }
            return Redirect($"{_configuration["FontEndUrl"]}");

        }
    }
}