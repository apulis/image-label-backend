using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace WebUI.Services
{
    public class CustomJwtTokenValicator : ISecurityTokenValidator
    {
        private readonly SecurityTokenHandler _jwtSecurityTokenHandler=new JwtSecurityTokenHandler();

        //判断当前token是否有值
        public bool CanValidateToken => true;

        public int MaximumTokenSizeInBytes { get; set; }//顾名思义是验证token的最大bytes

        public bool CanReadToken(string securityToken)
        {
            return true;
        }
        ///验证securityToken
        public ClaimsPrincipal ValidateToken(string securityToken, TokenValidationParameters validationParameters, out SecurityToken validatedToken)
        {
            ClaimsPrincipal principal = _jwtSecurityTokenHandler.ValidateToken(securityToken, validationParameters, out validatedToken);
            string userId = principal.FindFirstValue("uid");
            if (userId == null)
            {
                throw new SecurityTokenInvalidSignatureException("userId is null");
            }
            //var task = AzureService.CheckUserIdExists(userId);
            //var result = task.Result;
            //if (userId == null|| !result)
            //{
            //    throw new SecurityTokenInvalidSignatureException("userId is invalid");
            //}
            return principal;
        }
    }
}