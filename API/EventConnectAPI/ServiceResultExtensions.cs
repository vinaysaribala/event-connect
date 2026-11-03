using EventConnectService;
using Microsoft.AspNetCore.Mvc;

namespace EventConnectAPI;

public static class ServiceResultExtensions
{
    /// <summary>Maps a failed service result to its HTTP response (400 validation problem, 409, 401).</summary>
    public static ActionResult Failure(this ControllerBase controller, ServiceResult result)
    {
        switch (result.Status)
        {
            case ServiceResultStatus.Invalid:
                // Through ModelState so the body matches the automatic [ApiController] validation responses.
                foreach (var (field, messages) in result.Errors)
                {
                    foreach (var message in messages)
                    {
                        controller.ModelState.AddModelError(field, message);
                    }
                }
                return controller.ValidationProblem();
            case ServiceResultStatus.Conflict:
                return controller.Problem(statusCode: StatusCodes.Status409Conflict, title: result.Message);
            case ServiceResultStatus.Unauthorized:
                return controller.Unauthorized();
            default:
                throw new InvalidOperationException($"Service result {result.Status} is not a failure.");
        }
    }
}
