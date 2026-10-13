using System.Linq.Expressions;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.AssessmentAndReasoning.Attachments;

public static class AttemptAttachmentNonceQuery
{
    public static IQueryable<AttemptAttachment> ForUploadNonces(
        IQueryable<AttemptAttachment> attachments,
        Guid centerId,
        IEnumerable<string> nonces)
    {
        // MySQL EF 10 cannot map a parameterized string collection in Contains,
        // even with EF.Constant. Build scalar equalities instead, still one scoped
        // query and with values escaped by EF, never by interpolating raw SQL.
        var parameter = Expression.Parameter(typeof(AttemptAttachment), "attachment");
        var property = Expression.Property(parameter, nameof(AttemptAttachment.UploadNonce));
        Expression? matches = null;
        foreach (var nonce in nonces.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            var equality = Expression.Equal(property, Expression.Constant(nonce, typeof(string)));
            matches = matches is null ? equality : Expression.OrElse(matches, equality);
        }
        var predicate = Expression.Lambda<Func<AttemptAttachment, bool>>(
            matches ?? Expression.Constant(false), parameter);
        return attachments.Where(attachment => attachment.CenterId == centerId).Where(predicate);
    }
}
