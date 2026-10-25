using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.Organization;

public class TeacherStudentNotesUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<IStudentOwnershipGuard> _mockOwnershipGuard;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();

    public TeacherStudentNotesUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockTenantContext = new Mock<ITenantContext>();
        _mockOwnershipGuard = new Mock<IStudentOwnershipGuard>();

        var mockAccessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(() => _mockTenantContext.Object.CenterId ?? Guid.Empty);

        _dbContext = new EduTwinDbContext(options, mockAccessor.Object);

        _mockTenantContext.Setup(c => c.IsResolved).Returns(true);
        _mockTenantContext.Setup(c => c.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(c => c.UserId).Returns(_teacherId);
        _mockTenantContext.Setup(c => c.Role).Returns(nameof(UserRole.Teacher));

        _mockOwnershipGuard
            .Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private async Task SeedEntitiesAsync()
    {
        var now = DateTime.UtcNow;
        var teacherUser = new User
        {
            UserId = _teacherId,
            CenterId = _centerId,
            Username = "teacher01",
            DisplayName = "Thầy Giáo A",
            RoleName = UserRole.Teacher,
            Status = UserStatus.Active,
            PasswordHash = "hash",
            CreatedAt = now,
            UpdatedAt = now
        };
        var teacher = new Teacher
        {
            TeacherId = _teacherId,
            CenterId = _centerId,
            Department = "Toán",
            User = teacherUser,
            CreatedAt = now,
            UpdatedAt = now
        };

        var studentUser = new User
        {
            UserId = _studentId,
            CenterId = _centerId,
            Username = "student01",
            DisplayName = "Học Sinh B",
            RoleName = UserRole.Student,
            Status = UserStatus.Active,
            PasswordHash = "hash",
            CreatedAt = now,
            UpdatedAt = now
        };
        var student = new Student
        {
            StudentId = _studentId,
            CenterId = _centerId,
            FullName = "Nguyễn Văn B",
            GradeLevel = 10,
            User = studentUser,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Users.AddRange(teacherUser, studentUser);
        _dbContext.Teachers.Add(teacher);
        _dbContext.Students.Add(student);
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateNote_WithValidContent_PersistsToDatabase()
    {
        await SeedEntitiesAsync();
        var sut = new CreateTeacherStudentNoteUseCase(_dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object, TimeProvider.System);

        var request = new CreateTeacherStudentNoteRequest
        {
            StudentId = _studentId,
            Content = "Học sinh có tiến bộ rõ rệt trong chương Lượng giác."
        };

        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(request.Content, result.Data.Content);
        Assert.Equal(_studentId.ToString(), result.Data.StudentId);
        Assert.Equal(_teacherId.ToString(), result.Data.TeacherId);

        // Verify direct from DB
        var savedInDb = await _dbContext.TeacherStudentNotes.FirstOrDefaultAsync(n => n.StudentId == _studentId);
        Assert.NotNull(savedInDb);
        Assert.Equal(request.Content, savedInDb.Content);
        Assert.False(savedInDb.IsDeleted);
    }

    [Fact]
    public async Task CreateNote_WhenContentExceedsMax_ReturnsValidationFailed()
    {
        await SeedEntitiesAsync();
        var sut = new CreateTeacherStudentNoteUseCase(_dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object, TimeProvider.System);

        var request = new CreateTeacherStudentNoteRequest
        {
            StudentId = _studentId,
            Content = new string('A', 4001)
        };

        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpsertNote_InsertsNew_ThenUpdatesExisting()
    {
        await SeedEntitiesAsync();
        var sut = new UpsertTeacherStudentNoteUseCase(_dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object, TimeProvider.System);

        // 1. Initial Insert
        var initial = await sut.ExecuteAsync(_studentId, new UpsertTeacherStudentNoteRequest
        {
            Content = "Ghi chú ban đầu"
        }, CancellationToken.None);

        Assert.True(initial.IsSuccess);
        Assert.Equal("Ghi chú ban đầu", initial.Data!.Content);
        Assert.Equal("1", initial.Data.RowVersion);

        // 2. Upsert Update
        var updated = await sut.ExecuteAsync(_studentId, new UpsertTeacherStudentNoteRequest
        {
            Content = "Ghi chú đã được cập nhật sau buổi kèm",
            RowVersion = "1"
        }, CancellationToken.None);

        Assert.True(updated.IsSuccess);
        Assert.Equal("Ghi chú đã được cập nhật sau buổi kèm", updated.Data!.Content);
        Assert.Equal("2", updated.Data.RowVersion);

        // Verify only 1 active note exists for this teacher-student pair in DB
        var count = await _dbContext.TeacherStudentNotes.CountAsync(n => n.StudentId == _studentId && n.TeacherId == _teacherId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task UpdateNote_WithStaleRowVersion_ReturnsConcurrencyConflict()
    {
        await SeedEntitiesAsync();
        var note = new TeacherStudentNote
        {
            NoteId = Guid.NewGuid(),
            CenterId = _centerId,
            TeacherId = _teacherId,
            StudentId = _studentId,
            Content = "Gốc",
            RowVersion = 2ul,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.TeacherStudentNotes.Add(note);
        await _dbContext.SaveChangesAsync();

        var staleRowVersion = note.RowVersion.ToString();
        note.Content = "Đã được cập nhật bởi yêu cầu khác";
        await _dbContext.SaveChangesAsync();

        var sut = new UpdateTeacherStudentNoteUseCase(_dbContext, _mockTenantContext.Object, TimeProvider.System);

        var result = await sut.ExecuteAsync(note.NoteId, new UpdateTeacherStudentNoteRequest
        {
            Content = "Thay đổi",
            RowVersion = staleRowVersion
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task DeleteNote_SoftDeletes_AndExcludesFromSubsequentQueries()
    {
        await SeedEntitiesAsync();
        var note = new TeacherStudentNote
        {
            NoteId = Guid.NewGuid(),
            CenterId = _centerId,
            TeacherId = _teacherId,
            StudentId = _studentId,
            Content = "Cần xóa",
            RowVersion = 1ul,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.TeacherStudentNotes.Add(note);
        await _dbContext.SaveChangesAsync();

        var deleteSut = new DeleteTeacherStudentNoteUseCase(_dbContext, _mockTenantContext.Object, TimeProvider.System);
        var deleteResult = await deleteSut.ExecuteAsync(note.NoteId, "1", CancellationToken.None);

        Assert.True(deleteResult.IsSuccess);

        // Verify note is marked deleted in DB
        var rawNote = await _dbContext.TeacherStudentNotes.IgnoreQueryFilters().FirstOrDefaultAsync(n => n.NoteId == note.NoteId);
        Assert.NotNull(rawNote);
        Assert.True(rawNote.IsDeleted);

        // Querying via GetUseCase should return NotFound
        var getSut = new GetTeacherStudentNoteUseCase(_dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var getResult = await getSut.ExecuteAsync(note.NoteId, CancellationToken.None);
        Assert.False(getResult.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, getResult.ErrorCode);
    }

    [Fact]
    public async Task ListNotes_ForTeacher_ReturnsAllTeacherNotes()
    {
        await SeedEntitiesAsync();
        var student2Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        _dbContext.Users.Add(new User
        {
            UserId = student2Id, CenterId = _centerId, Username = "student02",
            DisplayName = "Học Sinh C", RoleName = UserRole.Student,
            Status = UserStatus.Active, PasswordHash = "hash", CreatedAt = now, UpdatedAt = now
        });
        _dbContext.Students.Add(new Student
        {
            StudentId = student2Id, CenterId = _centerId, FullName = "Nguyễn Văn C",
            GradeLevel = 10, CreatedAt = now, UpdatedAt = now
        });
        var note1 = new TeacherStudentNote
        {
            NoteId = Guid.NewGuid(),
            CenterId = _centerId,
            TeacherId = _teacherId,
            StudentId = _studentId,
            Content = "Note 1",
            RowVersion = 1ul,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var note2 = new TeacherStudentNote
        {
            NoteId = Guid.NewGuid(),
            CenterId = _centerId,
            TeacherId = _teacherId,
            StudentId = student2Id,
            Content = "Note 2",
            RowVersion = 1ul,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.TeacherStudentNotes.AddRange(note1, note2);
        await _dbContext.SaveChangesAsync();

        var sut = new ListTeacherStudentNotesUseCase(_dbContext, _mockTenantContext.Object, _mockOwnershipGuard.Object);
        var result = await sut.GetMyNotesAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.TotalItems);
        Assert.Contains(result.Data!, n => n.Content == "Note 1");
        Assert.Contains(result.Data!, n => n.Content == "Note 2");
    }
}
