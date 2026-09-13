using System;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

public sealed class DataProtectionAttemptAttachmentTokenServiceTests
{
    private readonly DataProtectionAttemptAttachmentTokenService _service;

    public DataProtectionAttemptAttachmentTokenServiceTests()
    {
        var provider = new EphemeralDataProtectionProvider();
        _service = new DataProtectionAttemptAttachmentTokenService(provider);
    }

    [Fact]
    public void ProtectAndTryRead_RoundtripsSuccessfully()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var nonce = Guid.NewGuid().ToString("N");
        var sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var fileName = "scratchpad.png";
        var fileSize = 1024L;
        var expiresAtUtc = DateTime.UtcNow.AddHours(24);

        var payload = new AttachmentUploadTokenPayload(
            centerId,
            studentId,
            nonce,
            sha256,
            fileName,
            fileSize,
            expiresAtUtc);

        var token = _service.Protect(payload);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var success = _service.TryRead(token, out var readPayload);

        Assert.True(success);
        Assert.NotNull(readPayload);
        Assert.Equal(centerId, readPayload.CenterId);
        Assert.Equal(studentId, readPayload.StudentId);
        Assert.Equal(nonce, readPayload.UploadNonce);
        Assert.Equal(sha256, readPayload.Sha256Hex);
        Assert.Equal(fileName, readPayload.FileName);
        Assert.Equal(fileSize, readPayload.FileSizeBytes);
        Assert.Equal(expiresAtUtc, readPayload.ExpiresAtUtc);
    }

    [Fact]
    public void TryRead_TamperedToken_ReturnsFalse()
    {
        var payload = new AttachmentUploadTokenPayload(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "drawing.png",
            512L,
            DateTime.UtcNow.AddHours(24));

        var token = _service.Protect(payload);
        var tamperedToken = token + "xyz";

        var success = _service.TryRead(tamperedToken, out var readPayload);

        Assert.False(success);
        Assert.Null(readPayload);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-token")]
    public void TryRead_InvalidTokenString_ReturnsFalse(string? invalidToken)
    {
        var success = _service.TryRead(invalidToken!, out var readPayload);

        Assert.False(success);
        Assert.Null(readPayload);
    }
}
