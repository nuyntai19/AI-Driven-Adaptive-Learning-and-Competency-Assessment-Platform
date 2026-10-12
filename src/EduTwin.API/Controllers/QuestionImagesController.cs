using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Route("api/v1/questions/{questionId}/image")]
[Authorize]
public sealed class QuestionImagesController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = CompositePermissionPolicies.QuestionImagesRead)]
    public async Task<IActionResult> Get(ulong questionId, [FromServices] GetQuestionImageUseCase useCase, CancellationToken token)
    {
        var data = await useCase.ExecuteAsync(questionId, token);
        if (data is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(data, "image/png", enableRangeProcessing: false);
    }
}
