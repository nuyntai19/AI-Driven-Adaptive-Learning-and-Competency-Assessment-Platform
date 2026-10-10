using EduTwin.BLL.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

internal static class QuestionImageFixture
{
    // Synthetic 1x1 RGBA PNG. No student or teacher data.
    public static byte[] Bytes => Convert.FromHexString("89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000A49444154789C63000100000500010D0A2DB40000000049454E44AE426082");
    public static string DataUrl => "data:image/png;base64," + Convert.ToBase64String(Bytes);
}

public sealed class QuestionImageContentTests
{
    [Fact]
    public void NormalizedPng_IsAcceptedWithoutChangingBytes()
    {
        Assert.True(QuestionImageContent.TryDecode(QuestionImageFixture.DataUrl, out var bytes));
        Assert.Equal(QuestionImageFixture.Bytes, bytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("data:image/png;base64,not base64")]
    [InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
    [InlineData("https://example.com/problem.png")]
    public void InvalidDataUrls_AreRejected(string? dataUrl)
    {
        Assert.False(QuestionImageContent.TryDecode(dataUrl, out var bytes));
        Assert.Empty(bytes);
    }

    [Fact]
    public void CorruptOrTruncatedOrAppendedPng_IsRejected()
    {
        var corrupt = QuestionImageFixture.Bytes; corrupt[20] ^= 1;
        foreach (var bytes in new[] { corrupt, QuestionImageFixture.Bytes[..^1], QuestionImageFixture.Bytes.Concat(new byte[] { 0 }).ToArray() })
            Assert.False(QuestionImageContent.TryDecode("data:image/png;base64," + Convert.ToBase64String(bytes), out _));
    }

    [Fact]
    public void OversizedUpload_IsRejectedBeforeDecode()
    {
        Assert.False(QuestionImageContent.TryDecode("data:image/png;base64," + new string('A', QuestionImageContent.MaxBytes * 2), out _));
    }
}
