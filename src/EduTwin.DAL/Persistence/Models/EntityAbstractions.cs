namespace EduTwin.DAL.Persistence.Models;

public interface ITenantOwnedEntity
{
    Guid CenterId { get; set; }
}

public interface IHasRowVersion
{
    ulong RowVersion { get; set; }
}

public interface IAuditEntity : IHasRowVersion
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

public interface ISoftDeletableEntity
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

public interface IMutableTenantAggregate : ITenantOwnedEntity, IAuditEntity, ISoftDeletableEntity
{
}

public interface IMutableRootEntity : IHasRowVersion
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

public interface ITenantAppendOnlyEntity : ITenantOwnedEntity
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
}

public interface ITenantJoinEntity : ITenantOwnedEntity
{
}
