using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace WebUI.Services
{
    public class JwtService
    {
        public static string GenerateToken(string userId)
        {
            var configuration = Startup.Configuration;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["SecurityKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds);
            token.Payload["uid"] = userId;
            var tokenGenerate = new JwtSecurityTokenHandler().WriteToken(token);
            return tokenGenerate;
        }
    }
}
