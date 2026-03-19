using Microsoft.AspNetCore.Mvc;
using testapi1.Application;

namespace testapi1.Controllers
{
    [ApiController]
    [Route("api/v1/infra")]
    public sealed class InfraController : ControllerBase
    {
        private readonly IRemoteDependencyProbe _dependencyProbe;

        public InfraController(IRemoteDependencyProbe dependencyProbe)
        {
            _dependencyProbe = dependencyProbe;
        }

        [HttpGet("dependencies")]
        public async Task<ActionResult<RemoteDependencyProbeReport>> Dependencies(CancellationToken cancellationToken)
        {
            var report = await _dependencyProbe.ProbeAsync(cancellationToken);
            if (report.AllHealthy)
            {
                return Ok(report);
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, report);
        }
    }
}
