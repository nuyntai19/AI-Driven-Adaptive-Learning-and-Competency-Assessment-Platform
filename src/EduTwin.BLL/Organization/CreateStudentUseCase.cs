using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EduTwin.BLL.Organization;

public class CreateStudentUseCase : ICreateStudentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILogger<CreateStudentUseCase> _logger;

    public CreateStudentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IPasswordHasher<User> passwordHasher,
        ILogger<CreateStudentUseCase> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<CreateStudentResult> ExecuteAsync(CreateStudentRequest request, CancellationToken cancellationToken = default)
    {
        // 1. Validate request manually
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100)
            return CreateStudentResult.Failure(ErrorCodes.ValidationFailed);
        
        if (string.IsNullOrEmpty(request.TemporaryPassword) || request.TemporaryPassword.Length < 12 || request.TemporaryPassword.Length > 200)
            return CreateStudentResult.Failure(ErrorCodes.ValidationFailed);
            
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 200)
            return CreateStudentResult.Failure(ErrorCodes.ValidationFailed);
            
        if (request.GradeLevel is < 10 or > 12)
            return CreateStudentResult.Failure(ErrorCodes.ValidationFailed);
            
        if (request.ClassIds == null || request.ClassIds.Any(id => id == Guid.Empty) || request.ClassIds.Distinct().Count() != request.ClassIds.Count)
            return CreateStudentResult.Failure(ErrorCodes.ValidationFailed);

        // 2. Validate tenant and role
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return CreateStudentResult.Failure(ErrorCodes.ForbiddenResource); // Typically fail closed
        }

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        var center = await _dbContext.Centers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
            return CreateStudentResult.Failure(ErrorCodes.ForbiddenResource);

        // 3. Validate Classes
        List<Class> classes = new();
        if (request.ClassIds.Any())
        {
            classes = await _dbContext.Classes
                .Where(c => request.ClassIds.Contains(c.ClassId) && c.CenterId == centerId && !c.IsDeleted && c.Status == ClassStatus.Active)
                .ToListAsync(cancellationToken);

            if (classes.Count != request.ClassIds.Count)
                return CreateStudentResult.Failure(ErrorCodes.ResourceNotFound);

            if (isTeacher && classes.Any(c => c.TeacherId != actorId))
                return CreateStudentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 4. Check duplicate username in Center
        var isDuplicate = await _dbContext.Users.AnyAsync(u => u.CenterId == centerId && u.Username == request.Username, cancellationToken);
        if (isDuplicate)
            return CreateStudentResult.Failure(ErrorCodes.DuplicateResource);

        // 5. Create entities in transaction
        var now = DateTime.UtcNow;
        var studentId = Guid.NewGuid();

        var user = new User
        {
            UserId = studentId,
            CenterId = centerId,
            Username = request.Username,
            RoleName = UserRole.Student,
            Status = UserStatus.Active,
            AuthVersion = 1,
            DisplayName = request.FullName, // Display name can match FullName for now, or just leave it
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };
        
        // Hash password (receives raw, without trimming)
        user.PasswordHash = _passwordHasher.HashPassword(user, request.TemporaryPassword);

        var student = new Student
        {
            StudentId = studentId,
            CenterId = centerId,
            FullName = request.FullName,
            GradeLevel = (byte)request.GradeLevel,
            DateOfBirth = null,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        var classStudents = classes.Select(c => new ClassStudent
        {
            ClassId = c.ClassId,
            StudentId = studentId,
            CenterId = centerId,
            Status = ClassStudentStatus.Active,
            JoinedAt = now,
            CreatedBy = actorId
        }).ToList();

        var studentTwin = new StudentTwin
        {
            TwinId = Guid.NewGuid(),
            CenterId = centerId,
            StudentId = studentId,
            OverallMastery = 0,
            LastEvidenceAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Users.Add(user);
            _dbContext.Students.Add(student);
            _dbContext.ClassStudents.AddRange(classStudents);
            _dbContext.StudentTwins.Add(studentTwin);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException != null && (ex.InnerException.Message.Contains("IX_") || ex.InnerException.Message.Contains("duplicate") || ex.InnerException.Message.Contains("unique")))
        {
            await transaction.RollbackAsync(cancellationToken);
            
            // Check if it's the username constraint
            var stillDuplicate = await _dbContext.Users.AnyAsync(u => u.CenterId == centerId && u.Username == request.Username, cancellationToken);
            if (stillDuplicate)
                return CreateStudentResult.Failure(ErrorCodes.DuplicateResource);

            throw; // Unrelated DB update error
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to create student for center {CenterId}", centerId);
            throw;
        }

        // Build Response
        var classDtos = classes.Select(c => new ClassDto
        {
            ClassId = c.ClassId.ToString("D"),
            ClassName = c.ClassName,
            AcademicYear = c.AcademicYear,
            Status = c.Status.ToString(),
            RowVersion = c.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Subject = new ClassSubjectDto { SubjectId = c.SubjectId.ToString("D"), SubjectName = string.Empty },
            Teacher = new ClassTeacherDto { TeacherId = c.TeacherId.ToString("D"), DisplayName = string.Empty },
            StudentCount = 0
        }).ToList();

        var studentDto = new StudentDetailDto
        {
            StudentId = studentId.ToString("D"),
            Username = user.Username,
            FullName = student.FullName,
            GradeLevel = student.GradeLevel,
            Status = user.Status.ToString(),
            ActiveClassCount = classDtos.Count,
            RowVersion = student.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Classes = classDtos,
            SubjectGoals = new List<StudentSubjectGoalDto>()
        };

        return CreateStudentResult.Success(studentDto);
    }
}
