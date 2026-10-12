export interface NotificationItem {
  deliveryId: string;
  eventType: string;
  title: string;
  body: string;
  deliveredAt: string;
  readAt: string | null;
  senderName?: string | null;
  priority: "Normal" | "Important" | "Urgent";
  category: "Information" | "Request" | "System";
  requiresAcknowledgement: boolean;
  allowReply: boolean;
  acknowledgedAt: string | null;
  lifecycleStatus: "Active" | "Retracted" | "Corrected";
  lifecycleReason: string | null;
  correctionMessageId: string | null;
  ownReply: string | null;
  relatedLink?: string | null;
}
export interface NotificationDraft { title: string; body: string; audienceKind: string; targetIds: string[];
  priority?: "Normal" | "Important" | "Urgent"; category?: "Information" | "Request";
  requiresAcknowledgement?: boolean; allowReply?: boolean; correctionOfMessageId?: string | null; correctionReason?: string | null }
export interface NotificationFilters { priority?: string; category?: string; from?: string; until?: string; needsAcknowledgement?: boolean }
export interface NotificationReply { replyId: string; body: string; senderName: string; createdAt: string }
export interface NotificationAudienceOption { kind: string; label: string; choices: { id: string; label: string }[] }
export interface NotificationPreview { token: string; recipientCount: number; recipientNames: string[]; expiresAt: string }
export interface NotificationSent { messageId: string; title: string; body: string; senderName: string; createdAt: string;
  recipientCount: number; deliveredCount: number; suppressedCount: number; readCount: number; dispatchStatus: "Pending" | "Completed" | "Failed";
  priority: "Normal" | "Important" | "Urgent"; category: "Information" | "Request"; requiresAcknowledgement: boolean; allowReply: boolean;
  acknowledgedCount: number; lifecycleStatus: "Active" | "Retracted" | "Corrected"; lifecycleReason: string | null;
  replacementMessageId: string | null; correctionOfMessageId: string | null }
