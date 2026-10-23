using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduTwin.API.Controllers;

[ApiController, Authorize, Route("api/v1/knowledge/graph/versions")]
public class KnowledgeGraphVersionsController(KnowledgeGraphVersionService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = "knowledge.nodes.read")]
    public async Task<IActionResult> List([FromQuery] Guid subjectId, [FromQuery] byte? gradeLevel, CancellationToken ct)
    {
        var data = await service.ListAsync(subjectId, gradeLevel, ct);
        return data == null ? NotFound() : Ok(new { data });
    }

    [HttpPost, Authorize(Policy = "knowledge.nodes.create"), Authorize(Policy = "knowledge.edges.create")]
    public async Task<IActionResult> Create([FromBody] CreateKnowledgeGraphVersionRequest request, CancellationToken ct)
        => OperationResponse(await service.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), Authorize(Policy = "knowledge.nodes.update"), Authorize(Policy = "knowledge.edges.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKnowledgeGraphVersionRequest request, CancellationToken ct)
        => OperationResponse(await service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/publish"), Authorize(Policy = "knowledge.nodes.update"), Authorize(Policy = "knowledge.edges.update")]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishKnowledgeGraphVersionRequest request, CancellationToken ct)
        => OperationResponse(await service.PublishAsync(id, request, ct));

    private IActionResult OperationResponse(GraphVersionOperation result)
    {
        if (result.Data != null) return Ok(new { data = result.Data });
        var status = result.ErrorCode == ErrorCodes.ResourceNotFound ? 404 : result.ErrorCode == ErrorCodes.ValidationFailed ? 400 : 409;
        return StatusCode(status, new ProblemDetails { Status = status, Title = "Không thể thay đổi phiên bản khung tri thức", Detail = result.Message,
            Extensions = { ["errorCode"] = result.ErrorCode, ["traceId"] = HttpContext.TraceIdentifier } });
    }
}
