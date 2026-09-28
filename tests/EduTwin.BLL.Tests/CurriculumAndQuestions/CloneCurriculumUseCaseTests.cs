using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class CloneCurriculumUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _dbOptions;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<ITenantIdAccessor> _tenantAccessorMock;
    private readonly Mock<TimeProvider> _timeProviderMock;

    public CloneCurriculumUseCaseTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _tenantMock = new Mock<ITenantContext>();
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));
        _tenantAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ExecuteAsync_TenantNotResolved_ReturnsNotFound()
    {
        using var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object);
        _tenantMock.Setup(t => t.IsResolved).Returns(false);

        var sut = new CloneCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), new CloneCurriculumRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_SourceNotFound_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(userId);

        using var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object);
        var sut = new CloneCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), new CloneCurriculumRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherDoesNotOwnSource_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherAId = Guid.NewGuid();
        var teacherBId = Guid.NewGuid();

        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(teacherBId);
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));

        var curriculumId = Guid.NewGuid();
        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Curriculums.Add(new Curriculum
            {
                CurriculumId = curriculumId,
                CenterId = centerId,
                TeacherId = teacherAId,
                SubjectId = Guid.NewGuid(),
                Title = "Teacher A Curriculum",
                ReviewStatus = ReviewStatus.Published,
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new CloneCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(curriculumId, new CloneCurriculumRequest());

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_TitleTooLong_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(userId);

        var curriculumId = Guid.NewGuid();
        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Curriculums.Add(new Curriculum
            {
                CurriculumId = curriculumId,
                CenterId = centerId,
                TeacherId = userId,
                SubjectId = Guid.NewGuid(),
                Title = "Math 10",
                ReviewStatus = ReviewStatus.Published,
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new CloneCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(curriculumId, new CloneCurriculumRequest
            {
                Title = new string('A', 251) // Exceeds 250
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Success_ClonesOnlyActiveNodesAndResetsDraftState()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(teacherId);
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));

        var sourceCurriculumId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        ulong node1 = 101; // Active
        ulong node2 = 102; // Inactive
        ulong node3 = 103; // Soft-deleted
        ulong node4 = 104; // Active

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            // Seed KnowledgeNodes
            dbContext.KnowledgeNodes.AddRange(
                new KnowledgeNode { NodeId = node1, CenterId = centerId, SubjectId = subjectId, NodeCode = "N1", NodeName = "Node 1", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new KnowledgeNode { NodeId = node2, CenterId = centerId, SubjectId = subjectId, NodeCode = "N2", NodeName = "Node 2", IsActive = false, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new KnowledgeNode { NodeId = node3, CenterId = centerId, SubjectId = subjectId, NodeCode = "N3", NodeName = "Node 3", IsActive = true, IsDeleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new KnowledgeNode { NodeId = node4, CenterId = centerId, SubjectId = subjectId, NodeCode = "N4", NodeName = "Node 4", IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            );

            // Seed Source Curriculum
            dbContext.Curriculums.Add(new Curriculum
            {
                CurriculumId = sourceCurriculumId,
                CenterId = centerId,
                TeacherId = teacherId,
                SubjectId = subjectId,
                Title = "Math Grade 10 - 2025",
                Description = "2025 Curriculum Plan",
                ReviewStatus = ReviewStatus.Published,
                RowVersion = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // Seed 4 nodes in source curriculum
            dbContext.CurriculumNodes.AddRange(
                new CurriculumNode { CenterId = centerId, CurriculumId = sourceCurriculumId, NodeId = node1, OrderIndex = 1, CreatedAt = DateTime.UtcNow },
                new CurriculumNode { CenterId = centerId, CurriculumId = sourceCurriculumId, NodeId = node2, OrderIndex = 2, CreatedAt = DateTime.UtcNow },
                new CurriculumNode { CenterId = centerId, CurriculumId = sourceCurriculumId, NodeId = node3, OrderIndex = 3, CreatedAt = DateTime.UtcNow },
                new CurriculumNode { CenterId = centerId, CurriculumId = sourceCurriculumId, NodeId = node4, OrderIndex = 4, CreatedAt = DateTime.UtcNow }
            );

            // Seed 1 class in source curriculum
            dbContext.CurriculumClasses.Add(
                new CurriculumClass { CenterId = centerId, CurriculumId = sourceCurriculumId, ClassId = classId, AssignedAt = DateTime.UtcNow, AssignedBy = teacherId }
            );

            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new CloneCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(sourceCurriculumId, new CloneCurriculumRequest
            {
                Title = "Math Grade 10 - 2026 (New Cohort)"
            });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);

            var clonedId = Guid.Parse(result.Data.CurriculumId);
            Assert.NotEqual(sourceCurriculumId, clonedId);
            Assert.Equal("Math Grade 10 - 2026 (New Cohort)", result.Data.Title);
            Assert.Equal("2025 Curriculum Plan", result.Data.Description);
            Assert.Equal(ReviewStatus.Draft.ToString(), result.Data.ReviewStatus);
            Assert.Equal("1", result.Data.RowVersion);

            // Classes must be empty
            Assert.Empty(result.Data.ClassIds);

            // Only node1 and node4 should be cloned (node2 inactive, node3 deleted)
            Assert.Equal(2, result.Data.NodeIds.Count);
            Assert.Equal("101", result.Data.NodeIds[0]);
            Assert.Equal("104", result.Data.NodeIds[1]);

            // Verify in DB
            var clonedInDb = await dbContext.Curriculums.SingleAsync(c => c.CurriculumId == clonedId);
            Assert.Equal(ReviewStatus.Draft, clonedInDb.ReviewStatus);
            Assert.Equal(teacherId, clonedInDb.TeacherId);
            Assert.Equal(subjectId, clonedInDb.SubjectId);

            var clonedNodesInDb = await dbContext.CurriculumNodes
                .Where(cn => cn.CurriculumId == clonedId)
                .OrderBy(cn => cn.OrderIndex)
                .ToListAsync();

            Assert.Equal(2, clonedNodesInDb.Count);
            Assert.Equal(node1, clonedNodesInDb[0].NodeId);
            Assert.Equal((uint)1, clonedNodesInDb[0].OrderIndex);
            Assert.Equal(node4, clonedNodesInDb[1].NodeId);
            Assert.Equal((uint)2, clonedNodesInDb[1].OrderIndex);

            var clonedClassesInDb = await dbContext.CurriculumClasses
                .Where(cc => cc.CurriculumId == clonedId)
                .ToListAsync();
            Assert.Empty(clonedClassesInDb);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DefaultTitle_GeneratesSuffix()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(teacherId);

        var sourceCurriculumId = Guid.NewGuid();
        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Curriculums.Add(new Curriculum
            {
                CurriculumId = sourceCurriculumId,
                CenterId = centerId,
                TeacherId = teacherId,
                SubjectId = subjectId,
                Title = "Physics 11",
                ReviewStatus = ReviewStatus.Published,
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new CloneCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(sourceCurriculumId, new CloneCurriculumRequest { Title = null });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Physics 11 (Bản sao)", result.Data.Title);
        }
    }
}
