using System;

namespace EduTwin.Contracts.Organization;

public class AddStudentsToClassDto
{
    public string ClassId { get; init; } = string.Empty;
    public int AddedCount { get; init; }
    public int AlreadyMemberCount { get; init; }
}
