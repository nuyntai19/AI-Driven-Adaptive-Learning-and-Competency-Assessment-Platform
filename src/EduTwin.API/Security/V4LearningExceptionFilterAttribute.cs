using EduTwin.BLL.Organization;
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
        context.Result = new ObjectResult(new ProblemDetails { Status = status, Title = "Không thể thực hiện hoạt động học",
            Detail = status == 404 ? "Không tìm thấy dữ liệu trong phạm vi của bạn." : context.Exception.Message,
            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier } }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
