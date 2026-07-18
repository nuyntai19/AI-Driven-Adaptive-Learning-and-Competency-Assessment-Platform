namespace EduTwin.BLL.IdentityAndTenancy;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
            throw new System.InvalidOperationException("JWT SigningKey is missing or empty.");
        if (SigningKey == "YOUR_DEVELOPMENT_SIGNING_KEY_HERE")
            throw new System.InvalidOperationException("JWT SigningKey placeholder is not allowed.");
        if (System.Text.Encoding.UTF8.GetByteCount(SigningKey) < 32)
            throw new System.InvalidOperationException("JWT SigningKey must be at least 32 bytes.");
        if (string.IsNullOrWhiteSpace(Issuer))
            throw new System.InvalidOperationException("JWT Issuer is required.");
        if (string.IsNullOrWhiteSpace(Audience))
            throw new System.InvalidOperationException("JWT Audience is required.");
    }
}
