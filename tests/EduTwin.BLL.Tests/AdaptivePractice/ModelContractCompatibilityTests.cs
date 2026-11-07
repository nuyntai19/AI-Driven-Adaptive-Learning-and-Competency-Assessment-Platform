using System.Text.Json;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AdaptivePractice;

public sealed class ModelContractCompatibilityTests
{
    [Fact]
    public void SubjectResponse_ExposesCanonicalAndShortLabelsFromSameValues()
    {
        var subject = new SubjectDto { SubjectId = "subject", SubjectName = "Toán",
            SubjectCode = "MATH", RowVersion = "1", IsActive = true };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(subject, options));
        Assert.Equal("Toán", json.RootElement.GetProperty("subjectName").GetString());
        Assert.Equal("Toán", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("MATH", json.RootElement.GetProperty("subjectCode").GetString());
        Assert.Equal("MATH", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void NameCompatibilityAliases_DoNotIntroduceAdditionalDatabaseColumns()
    {
        using var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL("Server=localhost;Database=metadata_only;User=unused;Password=unused").Options);

        var topic = new KnowledgeNode { Name = "Chuyển vế" };
        var subject = new Subject { Name = "Toán" };
        Assert.Equal("Chuyển vế", topic.NodeName);
        Assert.Equal("Toán", subject.SubjectName);
        Assert.Null(db.Model.FindEntityType(typeof(KnowledgeNode))!.FindProperty(nameof(KnowledgeNode.Name)));
        Assert.Null(db.Model.FindEntityType(typeof(Subject))!.FindProperty(nameof(Subject.Name)));
        Assert.NotNull(db.Model.FindEntityType(typeof(KnowledgeNode))!.FindProperty(nameof(KnowledgeNode.NodeName)));
        Assert.NotNull(db.Model.FindEntityType(typeof(Subject))!.FindProperty(nameof(Subject.SubjectName)));

        // Proposal filtering must use a mapped property so the real provider can translate it.
        var sql = db.KnowledgeNodes.Where(node => node.IsActive).ToQueryString();
        Assert.Contains("is_active", sql);
    }
}
