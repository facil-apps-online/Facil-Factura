using System;
using Microsoft.AspNetCore.Http;

namespace Fel.Api.Integration.Security
{
    public static class HttpContextBranchExtensions
    {
        // Sucursal dueña de la llave de API con la que se autenticó la petición (la deja el middleware HMAC).
        public static Guid? GetBranchId(this HttpContext httpContext) =>
            httpContext.Items["BranchId"] is string value && Guid.TryParse(value, out var branchId) ? branchId : null;
    }
}
