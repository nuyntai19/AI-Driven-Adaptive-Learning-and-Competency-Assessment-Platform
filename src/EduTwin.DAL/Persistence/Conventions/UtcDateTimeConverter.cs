using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EduTwin.DAL.Persistence.Conventions;

public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => ConvertToUtc(v),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }

    private static DateTime ConvertToUtc(DateTime v)
    {
        if (v.Kind == DateTimeKind.Utc) return v;
        if (v.Kind == DateTimeKind.Local) return v.ToUniversalTime();
        throw new InvalidOperationException("DateTimeKind.Unspecified is not allowed. Must be UTC or Local.");
    }
}
