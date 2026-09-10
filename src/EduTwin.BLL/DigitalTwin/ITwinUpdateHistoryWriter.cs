using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.BLL.DigitalTwin;

public interface ITwinUpdateHistoryWriter
{
    Task<TwinUpdateHistory> WriteAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        ulong topicNodeId,
        ulong? attemptId,
        ulong? analysisId,
        TwinEventSource eventSource,
        MasteryCalculationResult calculation,
        DateTime utcNow,
        Guid? createdBy,
        CancellationToken cancellationToken);
}
