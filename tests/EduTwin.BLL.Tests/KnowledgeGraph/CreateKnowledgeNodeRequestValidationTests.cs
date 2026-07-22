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

public class CreateKnowledgeNodeRequestValidationTests
{
    private readonly CreateKnowledgeNodeUseCase _sut;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly EduTwinDbContext _dbContext;

    public CreateKnowledgeNodeRequestValidationTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        _dbContext = new EduTwinDbContext(options, tenantAccessorMock.Object);
        _sut = new CreateKnowledgeNodeUseCase(_dbContext, _tenantMock.Object, TimeProvider.System);

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));
    }

    private CreateKnowledgeNodeRequest GetValidRequest() => new()
    {
        SubjectId = Guid.NewGuid(),
        ParentNodeId = null,
        NodeType = "Topic",
        NodeCode = "T01",
        NodeName = "Valid Topic",
        Description = "Valid Description",
        OrderIndex = 1,
        ExamImportance = 10,
        EstimatedLearningMinutes = 30,
        IsActive = true
    };

    [Fact]
    public async Task SubjectId_Empty_ReturnsValidationFailed()
    {
        var request = GetValidRequest();
        request.SubjectId = Guid.Empty;

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this-is-a-very-long-node-code-that-exceeds-sixty-four-characters-limit-which-is-invalid")]
    public async Task NodeCode_Invalid_ReturnsValidationFailed(string? code)
    {
        var request = GetValidRequest();
        request.NodeCode = code;

        var result = await _sut.ExecuteAsync(request);

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

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task NodeName_Oversized_ReturnsValidationFailed()
    {
        var request = GetValidRequest();
        request.NodeName = new string('a', 201);

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("Subject")]
    [InlineData("Chapter")]
    [InlineData("Topic")]
    [InlineData("Skill")]
    [InlineData("Concept")]
    public async Task NodeType_Canonical_Accepted(string type)
    {
        // Setup proper tenant and subject to pass validation and hit DB layer / success
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        
        _dbContext.Centers.Add(new EduTwin.DAL.Organization.Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest();
        request.SubjectId = subjectId;
        request.NodeType = type;

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("topic")]
    [InlineData(" Topic ")]
    [InlineData("0")]
    [InlineData("Invalid")]
    public async Task NodeType_Invalid_ReturnsValidationFailed(string? type)
    {
        var request = GetValidRequest();
        request.NodeType = type;

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ParentNodeId_Null_Accepted()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        
        _dbContext.Centers.Add(new EduTwin.DAL.Organization.Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest();
        request.SubjectId = subjectId;
        request.ParentNodeId = null;

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
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
    [InlineData("\u0660")]
    public async Task ParentNodeId_Invalid_ReturnsValidationFailed(string parent)
    {
        var request = GetValidRequest();
        request.ParentNodeId = parent;

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public async Task ExamImportance_OutOfRange_ReturnsValidationFailed(double importance)
    {
        var request = GetValidRequest();
        request.ExamImportance = (decimal)importance;

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task EstimatedLearningMinutes_Zero_ReturnsValidationFailed()
    {
        var request = GetValidRequest();
        request.EstimatedLearningMinutes = 0;

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task IsActive_Missing_ReturnsValidationFailed()
    {
        var request = GetValidRequest();
        request.IsActive = null;

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }
}
