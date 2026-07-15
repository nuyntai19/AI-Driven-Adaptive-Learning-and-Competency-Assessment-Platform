using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Organization;

public class Center : IMutableRootEntity
{
    public Guid CenterId { get; set; }
    public string CenterCode { get; set; } = null!;
    public string CenterName { get; set; } = null!;
    public CenterStatus Status { get; set; }
    public string Timezone { get; set; } = null!;
    
    // Mutable root fields (no center_id)
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ulong RowVersion { get; set; }
}
