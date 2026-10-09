using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Fel.Core.Interfaces;
using Fel.Core.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Fel.Infrastructure.Security
{
    // Misma llave y algoritmo (HMAC-SHA256, config "MasterKey" plano — no "Security:MasterKey",
    // que es la llave de cifrado de CryptoVault, un secreto distinto) en las tres APIs de portales.
    public class SessionTokenService : ISessionTokenService
    {
        private readonly byte[] _key;

        public SessionTokenService(IConfiguration configuration)
        {
            var keyStr = configuration["MasterKey"] ?? "SUPER_SECRET_FALLBACK_KEY_MUST_BE_32_CHARS_LONG_OR_MORE_123456";
            _key = Encoding.UTF8.GetBytes(keyStr.PadRight(32, '0'));
        }

        public SessionTokenResult IssueSession(IEnumerable<(string Type, string Value)> claims, Guid stamp, int sessionMinutes, DateTime sessionStartUtc)
        {
            var now = DateTime.UtcNow;
            var cap = sessionStartUtc + SessionPolicy.AbsoluteCap;
            var expires = now.AddMinutes(sessionMinutes);
            if (expires > cap) expires = cap;

            static string Unix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

            var own = new[] { SessionPolicy.StampClaim, SessionPolicy.SessionStartClaim, SessionPolicy.CapClaim };
            var list = claims.Where(c => !own.Contains(c.Type)).Select(c => new Claim(c.Type, c.Value)).ToList();
            list.Add(new Claim(SessionPolicy.StampClaim, stamp.ToString()));
            list.Add(new Claim(SessionPolicy.SessionStartClaim, Unix(sessionStartUtc), ClaimValueTypes.Integer64));
            list.Add(new Claim(SessionPolicy.CapClaim, Unix(cap), ClaimValueTypes.Integer64));

            var tokenHandler = new JwtSecurityTokenHandler();
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(list),
                IssuedAt = now,
                Expires = expires,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_key), SecurityAlgorithms.HmacSha256Signature)
            };

            return new SessionTokenResult(tokenHandler.WriteToken(tokenHandler.CreateToken(descriptor)), expires);
        }
    }
}
