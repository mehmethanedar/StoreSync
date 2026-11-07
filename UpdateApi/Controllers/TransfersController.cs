using Microsoft.AspNetCore.Mvc;
using StoreSync.UpdateApi.Models;
using StoreSync.UpdateApi.Services;

namespace StoreSync.UpdateApi.Controllers
{
    [ApiController]
    [Route("api/transfers")]
    public class TransfersController : ControllerBase
    {
        private readonly TransferService _transferService;

        public TransfersController(TransferService transferService)
        {
            _transferService = transferService;
        }

        [HttpGet("status")]
        public ActionResult<TransferStatus> GetStatus()
        {
            TransferStatus status = _transferService.GetStatus();
            return Ok(status);
        }

        [HttpPost("analyze")]
        public IActionResult Analyze()
        {
            _transferService.Analyze();
            return Ok();
        }

        [HttpPost("start")]
        public IActionResult Start(TransferRequest request)
        {
            _transferService.Start(request.IsSlow);
            return Ok();
        }

        [HttpPost("pause")]
        public IActionResult Pause()
        {
            _transferService.Pause();
            return Ok();
        }

        [HttpPost("speed")]
        public IActionResult SetSpeed(TransferRequest request)
        {
            _transferService.SetSpeed(request.IsSlow);
            return Ok();
        }

        [HttpPost("reset")]
        public IActionResult ResetDemo()
        {
            _transferService.ResetDemo();
            return Ok();
        }
    }
}
