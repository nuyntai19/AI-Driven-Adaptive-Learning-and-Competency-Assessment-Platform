using EduTwin.BLL.Organization;
using EduTwin.BLL.Recommendations.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.API.Security;

public sealed class V4LearningExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var status = context.Exception switch
        {
            KeyNotFoundException => 404,
            LearningScopeDeniedException => 403,
            ArgumentException => 422,
            DbUpdateConcurrencyException => 409,
            InvalidOperationException => 409,
            _ => 0
        };
        if (status == 0) return;
        var problem = new ProblemDetails { Status = status, Title = "Không thể thực hiện hoạt động học",
            Detail = status == 404 ? "Không tìm thấy dữ liệu trong phạm vi của bạn." : context.Exception.Message,
            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier } };
        if (context.Exception is LearningMissionInProgressException ongoing)
        {
            problem.Extensions["errorCode"] = "LEARNING_MISSION_IN_PROGRESS";
            problem.Extensions["ongoingMission"] = new { missionId = ongoing.MissionId, runId = ongoing.RunId, title = ongoing.MissionTitle };
        }
        if (context.Exception is LearningPathChangeBlockedException blocked)
        {
            problem.Extensions["errorCode"] = "LEARNING_PATH_CHANGE_BLOCKED";
            problem.Extensions["blockingMissions"] = blocked.Missions;
        }
        context.Result = new ObjectResult(problem) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
