using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class SuggestKnowledgeNodeCodeUseCaseTests
{
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly EduTwinDbContext _db;
    private readonly SuggestKnowledgeNodeCodeUseCase _sut;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private ulong _nextNodeId = 1;

    public SuggestKnowledgeNodeCodeUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var accessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        accessor.Setup(x => x.CenterId).Returns(() => _tenant.Object.CenterId);
        _db = new EduTwinDbContext(options, accessor.Object);
        _sut = new SuggestKnowledgeNodeCodeUseCase(_db, _tenant.Object);
        _tenant.SetupGet(x => x.IsResolved).Returns(true);
        _tenant.SetupGet(x => x.CenterId).Returns(_centerId);
        _tenant.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenant.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));
        _db.Subjects.Add(new Subject { SubjectId = _subjectId, CenterId = _centerId, SubjectCode = "MATH", SubjectName = "Toán", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.SaveChanges();
    }

    private void AddNode(string code, bool deleted = false, Guid? centerId = null, Guid? subjectId = null)
    {
        _db.KnowledgeNodes.Add(new KnowledgeNode
        {
            NodeId = _nextNodeId++, CenterId = centerId ?? _centerId, SubjectId = subjectId ?? _subjectId, NodeType = NodeType.Topic,
            NodeCode = code, NodeName = code, OrderIndex = 1, IsActive = true, IsDeleted = deleted,
            DeletedAt = deleted ? DateTime.UtcNow : null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task FirstCodeForPrefix_Is01()
    {
        var result = await _sut.ExecuteAsync(_subjectId, "MATH10_T_PTBH", CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("MATH10_T_PTBH_01", result.Data!.Code);
        Assert.Equal(1, result.Data.Sequence);
        Assert.Equal("MATH10_T_PTBH", result.Data.Prefix);
    }

    [Fact]
    public async Task NextCode_IsMaxPlusOne_IncludingSoftDeletedNodes()
    {
        AddNode("MATH10_T_PTBH_01");
        AddNode("MATH10_T_PTBH_04", deleted: true); // reusing it would restore the deleted node
        AddNode("MATH10_T_PTBH_02");

        var result = await _sut.ExecuteAsync(_subjectId, "MATH10_T_PTBH", CancellationToken.None);

        Assert.Equal("MATH10_T_PTBH_05", result.Data!.Code);
    }

    [Fact]
    public async Task OnlyExactPrefixWithNumericSuffixCounts()
    {
        AddNode("MATH10_T_PTBHX_07");      // different abbreviation
        AddNode("MATH10_T_PTBH_A1");       // non-numeric suffix
        AddNode("MATH10_TXPTBH_09");       // "_" must not act as a wildcard
        AddNode("MATH10_T_PTBH_03_EXTRA"); // not PREFIX_<digits>
        AddNode("MATH10.T.PTBH.08");

        var result = await _sut.ExecuteAsync(_subjectId, "MATH10_T_PTBH", CancellationToken.None);

        Assert.Equal("MATH10_T_PTBH_01", result.Data!.Code);
    }

    [Fact]
    public async Task OtherSubjectsAndCentersAreIgnored()
    {
        AddNode("MATH10_T_PTBH_05", subjectId: Guid.NewGuid());
        AddNode("MATH10_T_PTBH_06", centerId: Guid.NewGuid());

        var result = await _sut.ExecuteAsync(_subjectId, "MATH10_T_PTBH", CancellationToken.None);

        Assert.Equal("MATH10_T_PTBH_01", result.Data!.Code);
    }

    [Fact]
    public async Task SequenceGrowsBeyondTwoDigits()
    {
        AddNode("MATH10_T_PTBH_99");
        var result = await _sut.ExecuteAsync(_subjectId, "MATH10_T_PTBH", CancellationToken.None);
        Assert.Equal("MATH10_T_PTBH_100", result.Data!.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("math10_t_ptbh")]
    [InlineData("MATH10__PTBH")]
    [InlineData("MATH10_T_PTBH_")]
    [InlineData("MATH-10")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // 48 chars: no room for "_NN"
    public async Task InvalidPrefix_ReturnsValidationFailed(string? prefix)
    {
        var result = await _sut.ExecuteAsync(_subjectId, prefix, CancellationToken.None);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UnknownOrForeignSubject_ReturnsNotFound()
    {
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), "MATH10_T_PTBH", CancellationToken.None);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task StudentRole_ReturnsNotFound()
    {
        _tenant.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));
        var result = await _sut.ExecuteAsync(_subjectId, "MATH10_T_PTBH", CancellationToken.None);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }
}
