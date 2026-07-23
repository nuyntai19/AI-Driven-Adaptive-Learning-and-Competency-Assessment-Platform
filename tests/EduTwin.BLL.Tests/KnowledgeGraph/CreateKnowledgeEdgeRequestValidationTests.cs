using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class CreateKnowledgeEdgeRequestValidationTests
{
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly KnowledgeGraphValidator _validator;
    private readonly EduTwinDbContext _dbContext;
    private readonly CreateKnowledgeEdgeUseCase _sut;

    public CreateKnowledgeEdgeRequestValidationTests()
    {
        _tenantContextMock = new Mock<ITenantContext>();
        _timeProviderMock = new Mock<TimeProvider>();
        _validator = new KnowledgeGraphValidator();

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new EduTwinDbContext(options);

        _tenantContextMock.Setup(x => x.IsResolved).Returns(true);
        _tenantContextMock.Setup(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantContextMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _tenantContextMock.Setup(x => x.Role).Returns(nameof(UserRole.Teacher));

        _sut = new CreateKnowledgeEdgeUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object, _validator);
    }

    private CreateKnowledgeEdgeRequest CreateValidRequest()
    {
        return new CreateKnowledgeEdgeRequest
        {
            SubjectId = Guid.NewGuid(),
            SourceNodeId = "100",
            TargetNodeId = "101",
            RelationType = "PrerequisiteOf",
            Weight = 1.0m
        };
    }

    [Fact]
    public async Task Valid_Request_Fails_At_Resource_Validation_Not_Request_Validation()
    {
        var request = CreateValidRequest();
        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        // It should pass request validation and fail at resource validation (RESOURCE_NOT_FOUND)
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Empty_SubjectId_Fails()
    {
        var request = CreateValidRequest();
        request.SubjectId = Guid.Empty;

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task Null_Empty_Whitespace_SourceNodeId_Fails(string? sourceNodeId)
    {
        var request = CreateValidRequest();
        request.SourceNodeId = sourceNodeId!;

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-100")]
    [InlineData("100.5")]
    [InlineData("0")]
    [InlineData(" 100 ")]
    [InlineData("18446744073709551616")] // Overflow ulong
    public async Task Invalid_Source_Node_Raw_Matrix(string sourceNodeId)
    {
        var request = CreateValidRequest();
        request.SourceNodeId = sourceNodeId;

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-101")]
    [InlineData("101.5")]
    [InlineData("0")]
    [InlineData(" 101 ")]
    [InlineData("18446744073709551616")] // Overflow ulong
    public async Task Invalid_Target_Node_Raw_Matrix(string targetNodeId)
    {
        var request = CreateValidRequest();
        request.TargetNodeId = targetNodeId;

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Same_Source_And_Target_Fails()
    {
        var request = CreateValidRequest();
        request.SourceNodeId = "100";
        request.TargetNodeId = "100";

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData("PrerequisiteOf")]
    [InlineData("RelatedTo")]
    [InlineData("PartOf")]
    [InlineData("CausesErrorIn")]
    public async Task Valid_RelationType_Values_Pass(string relationType)
    {
        var request = CreateValidRequest();
        request.RelationType = relationType;

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode); // Passed validation
    }

    [Theory]
    [InlineData("prerequisiteOf")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData(" PrerequisiteOf ")]
    [InlineData("UnknownType")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Wrong_Casing_Numeric_Unknown_Relation_Type_Fails(string? relationType)
    {
        var request = CreateValidRequest();
        request.RelationType = relationType!;

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Missing_Null_Weight_Fails()
    {
        var request = CreateValidRequest();
        request.Weight = null;

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-100)]
    [InlineData(100)]
    public async Task Weight_Below_0_Or_Above_1_Fails(decimal weight)
    {
        var request = CreateValidRequest();
        request.Weight = weight;

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Boundary_Weight_0_And_1_Pass(decimal weight)
    {
        var request = CreateValidRequest();
        request.Weight = weight;

        var result = await _sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode); // Passed validation
    }
}
