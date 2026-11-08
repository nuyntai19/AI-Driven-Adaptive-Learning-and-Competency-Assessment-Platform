using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.AssessmentAndReasoning.Bookmarks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController]
[Authorize(Policy = "learning.attempts.submit")]
[Route("api/v1/students/me/question-bookmarks")]
public sealed class StudentQuestionBookmarksController(StudentQuestionBookmarkService bookmarks, ITenantContext tenant) : ControllerBase
{
    private bool IsStudent => tenant.IsResolved && tenant.CenterId is not null && tenant.UserId is not null && tenant.Role == nameof(UserRole.Student);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        IsStudent ? Ok(await bookmarks.ListAsync(tenant.CenterId!.Value, tenant.UserId!.Value, ct)) : Forbid();

    [HttpPut("{questionId}")]
    public async Task<IActionResult> Add(ulong questionId, CancellationToken ct)
    {
        if (!IsStudent) return Forbid();
        return await bookmarks.AddAsync(tenant.CenterId!.Value, tenant.UserId!.Value, questionId, ct) ? NoContent() : NotFound();
    }

    [HttpDelete("{questionId}")]
    public async Task<IActionResult> Remove(ulong questionId, CancellationToken ct)
    {
        if (!IsStudent) return Forbid();
        await bookmarks.RemoveAsync(tenant.CenterId!.Value, tenant.UserId!.Value, questionId, ct);
        return NoContent();
    }
}
