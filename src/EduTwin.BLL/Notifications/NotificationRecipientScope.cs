using System.Linq.Expressions;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.BLL.Notifications;

internal static class NotificationRecipientScope
{
    public static IQueryable<T> WhereIds<T>(IQueryable<T> query, Expression<Func<T, Guid>> selector, IEnumerable<Guid> ids)
    {
        Expression body = Expression.Constant(false);
        foreach (var id in ids) body = Expression.OrElse(body, Expression.Equal(selector.Body, Expression.Constant(id)));
        return query.Where(Expression.Lambda<Func<T, bool>>(body, selector.Parameters));
    }
    // At most 500 scalar parameters, never load an entire tenant roster for one event.
    public static Expression<Func<User, bool>> Matches(IEnumerable<Guid> ids)
    {
        var user = Expression.Parameter(typeof(User), "user");
        Expression body = Expression.Constant(false);
        foreach (var id in ids)
            body = Expression.OrElse(body, Expression.Equal(Expression.Property(user, nameof(User.UserId)), Expression.Constant(id)));
        return Expression.Lambda<Func<User, bool>>(body, user);
    }
}
