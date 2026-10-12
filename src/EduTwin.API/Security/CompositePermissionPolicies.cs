using System.Collections.Generic;

namespace EduTwin.API.Security;

public static class CompositePermissionPolicies
{
    public const string AttemptsRead = "permissions:any:learning.attempts.read";
    public const string StudentTwinRead = "permissions:any:twin.student.read";
    public const string StudentTwinUpdate = "permissions:any:twin.student.update";
    public const string DashboardsClassRead = "permissions:any:dashboards.class.read";
    public const string QuestionImagesRead = "permissions:any:curriculum.question_images.read";
    public const string NotificationRead = "permissions:any:notifications.inbox.read_own";
    public const string NotificationUpdate = "permissions:any:notifications.inbox.update_own";
    public const string NotificationSend = "permissions:any:notifications.messages.send_scoped";
    public const string NotificationSentRead = "permissions:any:notifications.messages.read_sent_own";
    public const string NotificationRetract = "permissions:any:notifications.messages.retract_own";
    public const string NotificationAcknowledge = "permissions:any:notifications.inbox.acknowledge_own";
    public const string NotificationReply = "permissions:any:notifications.inbox.reply_own";
    public const string ActivityRead = "permissions:any:activity.read";
    public const string NotificationOperationsRead = "permissions:any:notifications.dispatches.read";
    public const string NotificationOperationsRedrive = "permissions:any:notifications.dispatches.redrive";

    public static IReadOnlyDictionary<string, string[]> GetAnyPermissionPolicies() =>
        new Dictionary<string, string[]>
        {
            [ActivityRead] = ["activity.history.read_own", "activity.history.read_scoped", "activity.history.read_administrative", "platform.activity.read"],
            [NotificationOperationsRead] = ["notifications.dispatches.read", "platform.notifications.read_dispatches"],
            [NotificationOperationsRedrive] = ["notifications.dispatches.redrive", "platform.notifications.redrive"],
            [NotificationRetract] = ["notifications.messages.retract_own", "platform.notifications.retract_own"],
            [NotificationAcknowledge] = ["notifications.inbox.acknowledge_own", "platform.notifications.acknowledge_own"],
            [NotificationReply] = ["notifications.inbox.reply_own", "platform.notifications.reply_own"],
            [NotificationRead] = ["notifications.inbox.read_own", "platform.notifications.read_own"],
            [NotificationSend] = ["notifications.messages.send_scoped", "platform.notifications.send_scoped"],
            [NotificationSentRead] = ["notifications.messages.read_sent_own", "platform.notifications.read_sent_own"],
            [NotificationUpdate] = ["notifications.inbox.update_own", "platform.notifications.update_own"],
            [QuestionImagesRead] = ["curriculum.questions.read", "learning.attempts.read_own", "learning.attempts.read_scoped"],
            [AttemptsRead] =
            [
                "learning.attempts.read_own",
                "learning.attempts.read_scoped"
            ],
            [StudentTwinRead] =
            [
                "twin.student.read_own",
                "twin.student.read_scoped"
            ],
            [StudentTwinUpdate] =
            [
                "twin.student.update_own",
                "twin.student.update_scoped"
            ],
            [DashboardsClassRead] =
            [
                "dashboards.teacher.read_scoped"
            ]
        };
}
