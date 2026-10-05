using System;
using System.Linq;
using System.Threading.Tasks;
using Fel.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fel.Api.Security
{
    // Restringe un controlador o una acción del portal a ciertos roles. Sin este atributo cualquier rol puede usarla; el
    // atributo más específico (el de la acción) manda sobre el del controlador.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ClientRoleAttribute : Attribute
    {
        public string[] Roles { get; }

        public ClientRoleAttribute(params string[] roles)
        {
            Roles = roles;
        }
    }

    // Resuelve cliente, sucursal y rol antes de cada acción de ClientPortalControllerBase y hace cumplir los roles:
    // responde 401/403 sin llegar a la acción cuando la sesión, la sucursal o el rol no corresponden.
    public sealed class ClientPortalFilter : IAsyncActionFilter
    {
        private readonly FelDbContext _dbContext;

        public ClientPortalFilter(FelDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            BranchContext branch;
            try
            {
                branch = await BranchContext.ResolveAsync(context.HttpContext, _dbContext);
            }
            catch (BranchAccessException ex)
            {
                context.Result = new ObjectResult(new { message = ex.Message }) { StatusCode = ex.StatusCode };
                return;
            }

            var required = context.ActionDescriptor.EndpointMetadata.OfType<ClientRoleAttribute>().LastOrDefault();
            if (required != null && !required.Roles.Contains(branch.Role))
            {
                context.Result = new ObjectResult(new { message = "Tu rol no permite esta acción." }) { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }

            // "Todas las sucursales" es solo para consultar: crear, editar o eliminar exige una sucursal concreta.
            if (branch.BranchId == null && !HttpMethods.IsGet(context.HttpContext.Request.Method) && !HttpMethods.IsHead(context.HttpContext.Request.Method))
            {
                context.Result = new ObjectResult(new { message = "Selecciona una sucursal para esta operación." }) { StatusCode = StatusCodes.Status400BadRequest };
                return;
            }

            context.HttpContext.Items[BranchContext.ItemKey] = branch;
            await next();
        }
    }
}
