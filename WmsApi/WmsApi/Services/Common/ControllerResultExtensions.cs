using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WmsApi.Services.Common;

public static class ControllerResultExtensions
{
    public static IActionResult ToActionResult(this ControllerBase controller, ServiceResult result) =>
        result.StatusCode switch
        {
            StatusCodes.Status200OK => controller.Ok(result.Payload),
            StatusCodes.Status400BadRequest => controller.BadRequest(result.Payload),
            StatusCodes.Status404NotFound => controller.NotFound(result.Payload),
            _ => new ObjectResult(result.Payload) { StatusCode = result.StatusCode }
        };
}
