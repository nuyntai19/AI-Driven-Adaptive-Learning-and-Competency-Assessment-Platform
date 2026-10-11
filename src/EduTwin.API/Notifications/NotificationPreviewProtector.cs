using EduTwin.BLL.Notifications;
using Microsoft.AspNetCore.DataProtection;

namespace EduTwin.API.Notifications;

public sealed class NotificationPreviewProtector(IDataProtectionProvider provider) : INotificationPreviewProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("EduTwin.Notifications.Preview.v1");
    public string Protect(string value) => protector.Protect(value);
    public string? Unprotect(string token)
    {
        try { return protector.Unprotect(token); }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
        catch (ArgumentException) { return null; }
    }
}
