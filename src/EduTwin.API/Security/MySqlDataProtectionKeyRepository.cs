using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace EduTwin.API.Security;

/// <summary>
/// Persists ASP.NET Core Data Protection key rings in MySQL so ephemeral container hosts
/// (e.g. Render Free) retain valid upload and notification preview tokens across restarts.
/// Supports optional AES-256-GCM encryption at rest when an encryption secret is configured.
/// </summary>
public sealed class MySqlDataProtectionKeyRepository : IXmlRepository
{
    private const string EncryptedEnvelopePrefix = "ENC:V1:";
    private readonly string _connectionString;
    private readonly byte[]? _encryptionKey;
    private readonly ILogger<MySqlDataProtectionKeyRepository> _logger;
    private volatile bool _tableEnsured;
    private readonly object _initLock = new();

    public MySqlDataProtectionKeyRepository(
        string connectionString,
        string? encryptionSecret,
        ILogger<MySqlDataProtectionKeyRepository> logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string is required for MySQL Data Protection key repository.", nameof(connectionString));
        }

        _connectionString = connectionString;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _encryptionKey = string.IsNullOrWhiteSpace(encryptionSecret)
            ? null
            : SHA256.HashData(Encoding.UTF8.GetBytes(encryptionSecret));
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        EnsureTableExists();

        var elements = new List<XElement>();
        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT friendly_name, xml_payload FROM data_protection_keys ORDER BY id ASC;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var friendlyName = reader.IsDBNull(0) ? "(unnamed)" : reader.GetString(0);
            var rawPayload = reader.GetString(1);
            try
            {
                var xml = UnprotectPayload(rawPayload);
                elements.Add(XElement.Parse(xml));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping unreadable Data Protection key entry '{FriendlyName}'.", friendlyName);
            }
        }

        return elements.AsReadOnly();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);
        EnsureTableExists();

        var xml = element.ToString(SaveOptions.DisableFormatting);
        var payload = ProtectPayload(xml);

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO data_protection_keys (friendly_name, xml_payload, created_at_utc)
            VALUES (@friendlyName, @xmlPayload, @createdAtUtc);
            """;
        command.Parameters.AddWithValue("@friendlyName", string.IsNullOrWhiteSpace(friendlyName) ? Guid.NewGuid().ToString("D") : friendlyName);
        command.Parameters.AddWithValue("@xmlPayload", payload);
        command.Parameters.AddWithValue("@createdAtUtc", DateTime.UtcNow);
        command.ExecuteNonQuery();
    }

    public string ProtectPayload(string plainXml)
    {
        if (_encryptionKey is null)
        {
            return plainXml;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainXml);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, nonce.Length + tag.Length, cipherBytes.Length);

        return EncryptedEnvelopePrefix + Convert.ToBase64String(combined);
    }

    public string UnprotectPayload(string storedPayload)
    {
        if (!storedPayload.StartsWith(EncryptedEnvelopePrefix, StringComparison.Ordinal))
        {
            return storedPayload;
        }

        if (_encryptionKey is null)
        {
            throw new CryptographicException("Data Protection key payload is encrypted, but no encryption secret is configured.");
        }

        var combined = Convert.FromBase64String(storedPayload[EncryptedEnvelopePrefix.Length..]);
        var nonceLen = AesGcm.NonceByteSizes.MaxSize;
        var tagLen = AesGcm.TagByteSizes.MaxSize;
        if (combined.Length < nonceLen + tagLen)
        {
            throw new CryptographicException("Encrypted Data Protection key payload is truncated.");
        }

        var nonce = combined.AsSpan(0, nonceLen);
        var tag = combined.AsSpan(nonceLen, tagLen);
        var cipher = combined.AsSpan(nonceLen + tagLen);
        var plainBytes = new byte[cipher.Length];

        using var aes = new AesGcm(_encryptionKey, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, cipher, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private void EnsureTableExists()
    {
        if (_tableEnsured)
        {
            return;
        }

        lock (_initLock)
        {
            if (_tableEnsured)
            {
                return;
            }

            using var connection = new MySqlConnection(_connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS data_protection_keys (
                    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                    friendly_name VARCHAR(255) NOT NULL,
                    xml_payload MEDIUMTEXT NOT NULL,
                    created_at_utc DATETIME(6) NOT NULL,
                    PRIMARY KEY (id)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                """;
            command.ExecuteNonQuery();
            _tableEnsured = true;
        }
    }
}
