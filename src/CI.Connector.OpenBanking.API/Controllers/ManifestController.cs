using CI.Kernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CI.Connector.OpenBanking.API.Controllers;

[ApiController]
[Route("meta")]
[AllowAnonymous]
[AllowWithoutModule]
public sealed class ManifestController(IModuleManifest manifest) : ControllerBase
{
    [HttpGet("manifest")]
    public IActionResult Get() => Ok(manifest.Describe());
}
