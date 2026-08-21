using EduTwin.API.AssessmentAndReasoning.AI;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class GeminiOptionsTests
{
    [Fact]
    public void GeminiOptions_DefaultTimeout_IsThirtySeconds()
    {
        var options = new GeminiOptions();

        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
    }

    [Fact]
    public void GeminiOptions_ValidConfiguration_PassesValidation()
    {
        var options = CreateValidOptions();

        options.Validate();
    }

    [Fact]
    public void GeminiOptions_MissingApiKey_ThrowsSanitizedConfigurationError()
    {
        var options = CreateValidOptions();
        options.ApiKey = null;
        options.Model = "MODEL_VALUE_MUST_NOT_LEAK";

        var exception = Assert.Throws<GeminiAdapterException>(options.Validate);

        AssertSanitizedConfigurationError(exception, "MODEL_VALUE_MUST_NOT_LEAK");
    }

    [Fact]
    public void GeminiOptions_WhitespaceModel_ThrowsSanitizedConfigurationError()
    {
        var options = CreateValidOptions();
        options.ApiKey = "API_KEY_VALUE_MUST_NOT_LEAK";
        options.Model = "   ";

        var exception = Assert.Throws<GeminiAdapterException>(options.Validate);

        AssertSanitizedConfigurationError(exception, "API_KEY_VALUE_MUST_NOT_LEAK");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GeminiOptions_ZeroOrNegativeTimeout_ThrowsSanitizedConfigurationError(int seconds)
    {
        var options = CreateValidOptions();
        options.ApiKey = "TIMEOUT_SECRET_MUST_NOT_LEAK";
        options.Timeout = TimeSpan.FromSeconds(seconds);

        var exception = Assert.Throws<GeminiAdapterException>(options.Validate);

        AssertSanitizedConfigurationError(exception, "TIMEOUT_SECRET_MUST_NOT_LEAK");
    }

    [Fact]
    public void GeminiOptions_TimeoutAboveMaximum_ThrowsSanitizedConfigurationError()
    {
        var options = CreateValidOptions();
        options.Model = "OVERSIZED_TIMEOUT_MODEL_MUST_NOT_LEAK";
        options.Timeout = TimeSpan.FromSeconds(121);

        var exception = Assert.Throws<GeminiAdapterException>(options.Validate);

        AssertSanitizedConfigurationError(exception, "OVERSIZED_TIMEOUT_MODEL_MUST_NOT_LEAK");
    }

    private static GeminiOptions CreateValidOptions() =>
        new()
        {
            ApiKey = "test-api-key",
            Model = "test-model",
            Timeout = TimeSpan.FromSeconds(30)
        };

    private static void AssertSanitizedConfigurationError(
        GeminiAdapterException exception,
        string forbiddenValue)
    {
        Assert.Equal("AI_PROVIDER_CONFIGURATION_INVALID", exception.ErrorCode);
        Assert.Equal("AI provider configuration is invalid.", exception.Message);
        Assert.DoesNotContain(forbiddenValue, exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.InnerException);
    }
}
