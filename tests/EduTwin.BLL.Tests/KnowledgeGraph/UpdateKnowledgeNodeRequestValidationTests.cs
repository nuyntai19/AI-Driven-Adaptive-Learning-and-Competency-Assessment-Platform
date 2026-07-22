using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class UpdateKnowledgeNodeRequestValidationTests
{
    private readonly UpdateKnowledgeNodeUseCase _sut;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<IKnowledgeNodeHierarchyCycleDetector> _cycleDetectorMock;

    public UpdateKnowledgeNodeRequestValidationTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        _dbContext = new EduTwinDbContext(options, tenantAccessorMock.Object);
        _cycleDetectorMock = new Mock<IKnowledgeNodeHierarchyCycleDetector>();

        _sut = new UpdateKnowledgeNodeUseCase(_dbContext, _tenantMock.Object, TimeProvider.System, _cycleDetectorMock.Object);

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));
    }

    private UpdateKnowledgeNodeRequest GetValidRequest() => new()
    {
        ParentNodeId = null,
        NodeName = "Updated Node Name",
        Description = "Updated Description",
        OrderIndex = 2,
        ExamImportance = 50,
        EstimatedLearningMinutes = 45,
        IsActive = true,
        RowVersion = "1"
    };

    [Fact]
    public async Task ValidRequest_Accepted()
    {
        var centerId = _tenantMock.Object.CenterId!.Value;
        var subjectId = Guid.NewGuid();

        _dbContext.Centers.Add(new EduTwin.DAL.Organization.Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new EduTwin.DAL.KnowledgeGraph.KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Topic, NodeCode = "T01", NodeName = "Old", IsDeleted = false, RowVersion = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest();
        var result = await _sut.ExecuteAsync("1", request);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1 ")]
    [InlineData(" 1")]
    [InlineData("1.0")]
    [InlineData("18446744073709551616")]
    [InlineData("abc")]
    [InlineData("\u0661")]
    public async Task InvalidNodeId_ReturnsValidationFailed(string? id)
    {
        var request = GetValidRequest();
        var result = await _sut.ExecuteAsync(id!, request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NodeName_Invalid_ReturnsValidationFailed(string? name)
    {
        var request = GetValidRequest();
        request.NodeName = name;

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task NodeName_Oversized_ReturnsValidationFailed()
    {
        var request = GetValidRequest();
        request.NodeName = new string('a', 201);

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task NodeName_OversizedWithTrailingSpaces_ReturnsValidationFailed()
    {
        var request = GetValidRequest();
        request.NodeName = new string('a', 190) + new string(' ', 11); // Total 201 chars before trim

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task MissingExamImportance_ReturnsValidationFailed()
    {
        var request = GetValidRequest();
        request.ExamImportance = null;

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-10)]
    [InlineData(100.1)]
    [InlineData(101)]
    public async Task ExamImportance_OutOfRange_ReturnsValidationFailed(double importance)
    {
        var request = GetValidRequest();
        request.ExamImportance = (decimal)importance;

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task EstimatedLearningMinutes_Zero_ReturnsValidationFailed()
    {
        var request = GetValidRequest();
        request.EstimatedLearningMinutes = 0;

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task IsActive_Missing_ReturnsValidationFailed()
    {
        var request = GetValidRequest();
        request.IsActive = null;

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1 ")]
    [InlineData(" 1")]
    [InlineData("1.0")]
    [InlineData("18446744073709551616")]
    [InlineData("abc")]
    [InlineData("\u0661")] // Unicode digit
    public async Task ParentNodeId_Invalid_ReturnsValidationFailed(string parent)
    {
        var request = GetValidRequest();
        request.ParentNodeId = parent;

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1 ")]
    [InlineData(" 1")]
    [InlineData("1.0")]
    [InlineData("18446744073709551616")]
    [InlineData("abc")]
    [InlineData("\u0661")] // Unicode digit
    public async Task RowVersion_Invalid_ReturnsValidationFailed(string? rv)
    {
        var request = GetValidRequest();
        request.RowVersion = rv;

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }
}
