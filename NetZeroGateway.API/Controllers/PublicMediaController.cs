using Microsoft.AspNetCore.Mvc;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.API.Controllers;

/// <summary>
/// Public, unauthenticated file serving — the URLs handed out by the Media Library.
/// Auth is disabled via authorization.json (PublicMedia), never with attributes.
/// </summary>
[ApiController]
[Route("media")]
public class PublicMediaController(IMediaLibraryService service) : ControllerBase
{
    // HEAD too: some providers probe a media URL with HEAD before fetching it, and MVC does
    // not answer HEAD for a GET-only action (405).
    [AcceptVerbs("GET", "HEAD")]
    [Route("{fileName}")]
    public async Task<IActionResult> Get(string fileName, [FromQuery] bool download = false)
    {
        var (content, contentType, downloadName) = await service.OpenPublicFileAsync(fileName);
        Response.Headers.Append("X-Content-Type-Options", "nosniff");

        return download
            ? File(content, contentType, downloadName, enableRangeProcessing: true)
            : File(content, contentType, enableRangeProcessing: true);
    }
}
