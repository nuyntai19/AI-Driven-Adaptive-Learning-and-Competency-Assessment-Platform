using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.Controllers;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

public sealed class AttemptAttachmentsHttpIntegrationTests
{
    private static readonly byte[] ValidPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    private sealed class TestServerContext : IDisposable
    {
        public IHost Host { get; }
        public HttpClient Client { get; }

        public TestServerContext(IHost host, HttpClient client)
        {
            Host = host;
            Client = client;
        }

        public void Dispose()
        {
            Client.Dispose();
            Host.Dispose();
        }
    }

    private async Task<TestServerContext> CreateTestServerAsync(
        IPrepareAttemptAttachmentUploadUseCase? prepareUseCase = null)
    {
        var mockPrepare = prepareUseCase ?? Mock.Of<IPrepareAttemptAttachmentUploadUseCase>(u =>
            u.ExecuteAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) ==
            Task.FromResult(PrepareAttemptAttachmentUploadResult.Success("valid-upload-token", DateTime.UtcNow.AddHours(24))));

        var mockGet = Mock.Of<IGetAttemptAttachmentUseCase>();
        var mockStorage = Mock.Of<IAttemptAttachmentStorage>();

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddControllers()
                        .AddApplicationPart(typeof(AttemptAttachmentsController).Assembly);

                    services.AddSingleton(mockPrepare);
                    services.AddSingleton(mockGet);
                    services.AddSingleton(mockStorage);
                    services.AddSingleton(TimeProvider.System);

                    services.AddAuthentication("TestStudent")
                        .AddScheme<AuthenticationSchemeOptions, TestStudentAuthHandler>("TestStudent", _ => { });

                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = new AuthorizationPolicyBuilder("TestStudent")
                            .RequireAuthenticatedUser()
                            .Build();
                        options.AddPolicy(AuthorizationPolicies.StudentOnly, policy =>
                            policy.RequireAssertion(_ => true));
                        options.AddPolicy("learning.attempts.submit", policy =>
                            policy.RequireAssertion(_ => true));
                        options.AddPolicy("learning.attempts.read_scoped", policy =>
                            policy.RequireAssertion(_ => true));
                    });
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            });

        var host = await hostBuilder.StartAsync();
        var client = host.GetTestClient();
        return new TestServerContext(host, client);
    }

    [Fact]
    public async Task PrepareUpload_ValidMultipart_Returns200WithToken()
    {
        using var testContext = await CreateTestServerAsync();
        var content = new MultipartFormDataContent("test-boundary-123");
        var fileContent = new ByteArrayContent(ValidPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "scratchpad.png");

        var response = await testContext.Client.PostAsync(
            "/api/v1/learning/attempts/attachments/prepare-upload", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("valid-upload-token", body);
    }

    [Fact]
    public async Task PrepareUpload_TwoFileSections_Returns400BadRequest()
    {
        using var testContext = await CreateTestServerAsync();
        var content = new MultipartFormDataContent("test-boundary-123");
        var file1 = new ByteArrayContent(ValidPngBytes);
        file1.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file1, "file", "first.png");

        var file2 = new ByteArrayContent(ValidPngBytes);
        file2.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file2, "file", "second.png");

        var response = await testContext.Client.PostAsync(
            "/api/v1/learning/attempts/attachments/prepare-upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("tối đa một tệp", body);
    }

    [Fact]
    public async Task PrepareUpload_TruncatedBody_Returns400BadRequest()
    {
        using var testContext = await CreateTestServerAsync();
        const string boundary = "truncate-boundary";
        var rawMultipart = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"draw.png\"\r\nContent-Type: image/png\r\n\r\n";
        // Send body that is cut off abruptly in the middle of stream without boundary
        var truncatedBytes = Encoding.UTF8.GetBytes(rawMultipart);

        using var content = new ByteArrayContent(truncatedBytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");

        var response = await testContext.Client.PostAsync(
            "/api/v1/learning/attempts/attachments/prepare-upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PrepareUpload_MissingFinalBoundary_Returns400BadRequest()
    {
        using var testContext = await CreateTestServerAsync();
        const string boundary = "missing-final-boundary";
        var raw = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"draw.png\"\r\nContent-Type: image/png\r\n\r\nPNGDATA\r\n";
        // Missing the closing --missing-final-boundary--
        var bytes = Encoding.UTF8.GetBytes(raw);

        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={boundary}");

        var response = await testContext.Client.PostAsync(
            "/api/v1/learning/attempts/attachments/prepare-upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PrepareUpload_OversizedBoundary_Returns400BadRequest()
    {
        using var testContext = await CreateTestServerAsync();
        var oversizedBoundary = new string('B', 200); // 200 chars exceeds 128 chars limit
        using var content = new StringContent("some body");
        content.Headers.ContentType = MediaTypeHeaderValue.Parse($"multipart/form-data; boundary={oversizedBoundary}");

        var response = await testContext.Client.PostAsync(
            "/api/v1/learning/attempts/attachments/prepare-upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("boundary", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareUpload_OversizedNonFileSection_Returns400BadRequest()
    {
        using var testContext = await CreateTestServerAsync();
        var content = new MultipartFormDataContent("test-boundary-123");
        // Non-file section with 100 KB text (exceeds 64 KB limit)
        var oversizedText = new string('A', 100 * 1024);
        content.Add(new StringContent(oversizedText), "comment");

        var fileContent = new ByteArrayContent(ValidPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "scratchpad.png");

        var response = await testContext.Client.PostAsync(
            "/api/v1/learning/attempts/attachments/prepare-upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("vượt quá giới hạn", body);
    }

    [Fact]
    public async Task PrepareUpload_NonFileNamedFile_Returns400BadRequest()
    {
        using var testContext = await CreateTestServerAsync();
        var content = new MultipartFormDataContent("test-boundary-123");
        var fileContent = new ByteArrayContent(ValidPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        // Field name is "avatar" instead of "file"
        content.Add(fileContent, "avatar", "scratchpad.png");

        var response = await testContext.Client.PostAsync(
            "/api/v1/learning/attempts/attachments/prepare-upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("trường 'file'", body);
    }

    [Fact]
    public async Task PrepareUpload_NoFileSection_Returns400BadRequest()
    {
        using var testContext = await CreateTestServerAsync();
        var content = new MultipartFormDataContent("test-boundary-123");
        content.Add(new StringContent("Just some text"), "notes");

        var response = await testContext.Client.PostAsync(
            "/api/v1/learning/attempts/attachments/prepare-upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("trường 'file'", body);
    }

    private sealed class TestStudentAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestStudentAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "student01"),
                new Claim("CenterId", Guid.NewGuid().ToString()),
                new Claim("AccountType", "CenterUser"),
                new Claim("Role", "Student"),
                new Claim("Permission", "learning.attempts.submit")
            };
            var identity = new ClaimsIdentity(claims, "TestStudent");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestStudent");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
