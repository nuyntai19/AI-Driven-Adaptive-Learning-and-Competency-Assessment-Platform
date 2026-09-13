using System.Security.Cryptography;
using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using Microsoft.AspNetCore.DataProtection;

namespace EduTwin.API.AssessmentAndReasoning.Attachments;

public sealed class DataProtectionAttemptAttachmentTokenService : IAttemptAttachmentTokenService
{
    private const string Purpose = "EduTwin.AttemptAttachment.v1";
    private readonly IDataProtector _protector;

    public DataProtectionAttemptAttachmentTokenService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(AttachmentUploadTokenPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return _protector.Protect(JsonSerializer.Serialize(payload));
    }

    public bool TryRead(string token, out AttachmentUploadTokenPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token)) return false;

        try
        {
            payload = JsonSerializer.Deserialize<AttachmentUploadTokenPayload>(_protector.Unprotect(token));
            return payload is not null;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
