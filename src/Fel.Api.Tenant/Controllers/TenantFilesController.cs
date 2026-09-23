using System.Threading.Tasks;
using Fel.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fel.Api.Tenant.Controllers
{
    [ApiController]
    [Route("api/tenant/files")]
    public class TenantFilesController : ControllerBase
    {
        private readonly IPublicFileStorageService _fileStorage;

        public TenantFilesController(IPublicFileStorageService fileStorage)
        {
            _fileStorage = fileStorage;
        }

        [HttpGet("{**path}")]
        public async Task<IActionResult> GetFile(string path)
        {
            var file = await _fileStorage.GetFileAsync(path);
            if (file == null) return NotFound();

            return File(file.Bytes, file.ContentType);
        }
    }
}
