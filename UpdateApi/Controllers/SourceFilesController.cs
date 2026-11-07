using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using StoreSync.UpdateApi.Services;

namespace StoreSync.UpdateApi.Controllers
{
    [ApiController]
    [Route("api/source")]
    public class SourceFilesController : ControllerBase
    {
        private readonly DemoFolderService _folderService;
        private readonly ILogger<SourceFilesController> _logger;

        public SourceFilesController(DemoFolderService folderService, ILogger<SourceFilesController> logger)
        {
            _folderService = folderService;
            _logger = logger;
        }

        [HttpGet("files/{fileName}")]
        public IActionResult Download(string fileName)
        {
            string sourcePath = _folderService.GetSourcePath(fileName);
            if (!System.IO.File.Exists(sourcePath))
            {
                return NotFound();
            }

            _logger.LogInformation("Kaynak dosya {FileName}; Range {Range}",
                fileName, Request.Headers.Range.ToString());

            string hash = DemoFolderService.CalculateHash(sourcePath);
            PhysicalFileResult response = new PhysicalFileResult(sourcePath, "application/octet-stream");

            // Range: dosyanın istenen byte konumundan itibaren gönderilmesini sağlar.
            response.EnableRangeProcessing = true;
            response.EntityTag = new EntityTagHeaderValue("\"" + hash + "\"");
            return response;
        }
    }
}
