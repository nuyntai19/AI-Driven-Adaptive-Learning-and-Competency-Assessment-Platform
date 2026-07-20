using Xunit;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Tests.DigitalTwin;

public class UpsertStudentSubjectGoalRequestValidationTests
{
    [Fact]
    public void Request_Default_Values()
    {
        var request = new UpsertStudentSubjectGoalRequest();
        Assert.Equal(0m, request.TargetScore);
        Assert.Equal(0, request.RemainingDays);
        Assert.Null(request.RowVersion);
    }
}
