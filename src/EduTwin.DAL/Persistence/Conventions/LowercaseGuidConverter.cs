using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EduTwin.DAL.Persistence.Conventions;

public class LowercaseGuidConverter : ValueConverter<Guid, string>
{
    public LowercaseGuidConverter() : base(
        v => v.ToString("D").ToLowerInvariant(),
        v => Guid.Parse(v))
    {
    }
}
