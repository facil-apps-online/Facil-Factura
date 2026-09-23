using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Fel.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Fel.Infrastructure.Security
{
    // Misma llave y algoritmo (HMAC-SHA256, config "MasterKey" plano — no "Security:MasterKey",
    // que es la llave de cifrado de CryptoVault, un secreto distinto) que ya usa
    // SuperadminAuthController para firmar su JWT — se centraliza acá para no repetir esta lógica
    // en TenantAuthController/ClientAuthController/DeveloperAuthController.
    public class SessionTokenService : ISessionTokenService
    {
        private readonly byte[] _key;

        public SessionTokenService(IConfiguration configuration)
        {
            var keyStr = configuration["MasterKey"] ?? "SUPER_SECRET_FALLBACK_KEY_MUST_BE_32_CHARS_LONG_OR_MORE_123456";
            _key = Encoding.UTF8.GetBytes(keyStr.PadRight(32, '0'));
        }

        public string GenerateToken(IEnumerable<(string Type, string Value)> claims, TimeSpan validity)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = identity,
                Expires = DateTime.UtcNow.Add(validity),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
