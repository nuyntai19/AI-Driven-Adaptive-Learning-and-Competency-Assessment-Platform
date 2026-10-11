import { httpClient } from "./httpClient";
import type { ApiCollectionResponse, ApiResponse } from "../types/api";
import type { NotificationItem, NotificationAudienceOption, NotificationDraft, NotificationPreview, NotificationSent, NotificationFilters, NotificationReply } from "../types/notifications";

export const notificationsApi = {
  summary: async (signal?: AbortSignal) => (await httpClient.get<ApiResponse<{ unreadCount: number }>>("/notifications/summary", { signal })).data,
  list: async (page = 1, pageSize = 20, unreadOnly = false, signal?: AbortSignal, filters: NotificationFilters = {}) =>
    (await httpClient.get<ApiCollectionResponse<NotificationItem>>("/notifications", { params: { page, pageSize, unreadOnly, ...filters }, signal })).data,
  detail: async (id: string, signal?: AbortSignal) =>
    (await httpClient.get<ApiResponse<NotificationItem>>(`/notifications/${encodeURIComponent(id)}`, { signal })).data,
  read: async (ids: string[]) => { await httpClient.post("/notifications/read-batch", { deliveryIds: ids }); },
  options: async (signal?: AbortSignal) => (await httpClient.get<ApiResponse<NotificationAudienceOption[]>>("/notifications/compose-options", { signal })).data,
  preview: async (draft: NotificationDraft) => (await httpClient.post<ApiResponse<NotificationPreview>>("/notifications/preview", draft)).data,
  send: async (draft: NotificationDraft, previewToken: string, clientMessageId: string) =>
    (await httpClient.post<ApiResponse<NotificationSent>>("/notifications/send", { ...draft, previewToken, clientMessageId })).data,
  sent: async (page = 1, signal?: AbortSignal) => (await httpClient.get<ApiCollectionResponse<NotificationSent>>("/notifications/sent", { params: { page, pageSize: 20 }, signal })).data,
  sentDetail: async (id: string, signal?: AbortSignal) => (await httpClient.get<ApiResponse<NotificationSent>>(`/notifications/sent/${encodeURIComponent(id)}`, { signal })).data,
  hide: async (id: string) => { await httpClient.post(`/notifications/${encodeURIComponent(id)}/hide`); },
  acknowledge: async (id: string) => { await httpClient.post(`/notifications/${encodeURIComponent(id)}/acknowledge`); },
  reply: async (id: string, clientReplyId: string, body: string) => (await httpClient.post<ApiResponse<NotificationReply>>(`/notifications/${encodeURIComponent(id)}/reply`, { clientReplyId, body })).data,
  retract: async (id: string, clientActionId: string, reason: string) => { await httpClient.post(`/notifications/sent/${encodeURIComponent(id)}/retract`, { clientActionId, reason }); },
  replies: async (id: string, page = 1, signal?: AbortSignal) => (await httpClient.get<ApiCollectionResponse<NotificationReply>>(`/notifications/sent/${encodeURIComponent(id)}/replies`, { params: { page, pageSize: 20 }, signal })).data,
};
