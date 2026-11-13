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
            LearningMissionInProgressException => 409,
            LearningPathChangeBlockedException => 409,
            QuestionPoolDepletedException => 409,
            // Business-rule rejections (not enough questions, mission not
            // startable yet, budget exhausted...) are not state conflicts:
            // 422 lets the client tell "fix your request/state" apart from a
            // real 409 concurrency clash.
            InvalidOperationException => 422,
            _ => 0
        };
        if (status == 0) return;
        var problem = new ProblemDetails { Status = status, Title = "Không thể thực hiện hoạt động học",
            Detail = status == 404 ? "Không tìm thấy dữ liệu trong phạm vi của bạn."
                // EF's own text is English and technical; the student only needs to know that retrying is safe.
                : context.Exception is DbUpdateConcurrencyException ? "Bài làm vừa được cập nhật ở một thao tác khác (lưu nháp, gợi ý hoặc mở ở tab khác). Nội dung em nhập vẫn còn; hãy bấm nộp lại."
                : context.Exception.Message,
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
