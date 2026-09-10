using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Evidence;

public sealed class EvidenceAssessmentFactoryTests
{
    [Fact]
    public void Create_SameTenantAttemptAndAnalysis_MapsDecisionExactly()
    {
        var centerId = Guid.NewGuid();
        var attempt = new Attempt { CenterId = centerId, AttemptId = 17 };
        var analysis = new ReasoningAnalysis { CenterId = centerId, AttemptId = 17, AnalysisId = 23 };
        var now = new DateTime(2026, 9, 10, 4, 0, 0, DateTimeKind.Utc);
        var decision = new EvidenceGateDecision(
            EvidenceSourceType.AI,
            EvidenceTrustLevel.Reduced,
            EvidenceDecisionMode.AIWeighted,
            0.5m,
            [EvidenceReasonCodes.AIConfidence50To79],
            false,
            EvidenceGate.CurrentPolicyVersion,
            0);

        var result = new EvidenceAssessmentFactory().Create(
            attempt, analysis, null, decision, now, null);

        Assert.Equal(centerId, result.CenterId);
        Assert.Equal(17ul, result.AttemptId);
        Assert.Equal(23ul, result.AnalysisId);
        Assert.Equal(EvidenceTrustLevel.Reduced, result.TrustLevel);
        Assert.Equal(0.5m, result.ReasoningWeight);
        Assert.Equal(now, result.EvaluatedAt);
        Assert.Equal(now, result.CreatedAt);
        Assert.Equal(
            [EvidenceReasonCodes.AIConfidence50To79],
            result.ReasonCodes.RootElement.EnumerateArray().Select(item => item.GetString()!).ToArray());
    }

    [Fact]
    public void Create_CrossTenantAnalysis_Throws()
    {
        var attempt = new Attempt { CenterId = Guid.NewGuid(), AttemptId = 17 };
        var analysis = new ReasoningAnalysis { CenterId = Guid.NewGuid(), AttemptId = 17, AnalysisId = 23 };

        Assert.Throws<InvalidOperationException>(() => new EvidenceAssessmentFactory().Create(
            attempt, analysis, null, Decision(), DateTime.UtcNow, null));
    }

    [Fact]
    public void Create_AnalysisForDifferentAttempt_Throws()
    {
        var centerId = Guid.NewGuid();
        var attempt = new Attempt { CenterId = centerId, AttemptId = 17 };
        var analysis = new ReasoningAnalysis { CenterId = centerId, AttemptId = 18, AnalysisId = 23 };

        Assert.Throws<InvalidOperationException>(() => new EvidenceAssessmentFactory().Create(
            attempt, analysis, null, Decision(), DateTime.UtcNow, null));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Create_SupersededAssessmentOutsideAttemptBoundary_Throws(
        bool crossTenant,
        bool differentAttempt)
    {
        var centerId = Guid.NewGuid();
        var attempt = new Attempt { CenterId = centerId, AttemptId = 17 };
        var supersedes = new EvidenceAssessment
        {
            EvidenceAssessmentId = 9,
            CenterId = crossTenant ? Guid.NewGuid() : centerId,
            AttemptId = differentAttempt ? 18ul : 17ul
        };

        Assert.Throws<InvalidOperationException>(() => new EvidenceAssessmentFactory().Create(
            attempt, null, supersedes, Decision(), DateTime.UtcNow, null));
    }

    private static EvidenceGateDecision Decision() => new(
        EvidenceSourceType.AI,
        EvidenceTrustLevel.Trusted,
        EvidenceDecisionMode.AIWeighted,
        1m,
        [EvidenceReasonCodes.AIConfidence80To100],
        false,
        EvidenceGate.CurrentPolicyVersion,
        0);
}
