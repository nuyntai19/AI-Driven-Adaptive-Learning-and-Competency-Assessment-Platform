// Production page with an isolated, mutable HTTP fixture. No live API writes.
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { httpClient } from '../../src/api/httpClient';
import { useAuthStore } from '../../src/stores/authStore';
import { NotificationsPage } from '../../src/pages/NotificationsPage';
import '../../src/index.css';

useAuthStore.getState().setSession('fixture-only-not-a-real-token', {
  userId: 'fixture-manager', centerId: 'fixture-center', centerName: 'Fixture', username: 'fixture', displayName: 'Fixture',
  role: 'CenterManager', accountType: 'CenterManager', roles: [], authorizationVersion: 1,
  permissions: ['notifications.inbox.read_own', 'notifications.inbox.update_own',
    'notifications.messages.read_sent_own', 'notifications.dispatches.read'],
});
const notices = Array.from({ length: 21 }, (_, i) => ({
  deliveryId: String(i + 1), eventType: 'AssignmentPublished', title: `Thông báo kiểm thử ${i + 1}`,
  body: 'Nội dung kiểm thử làm mới.', deliveredAt: '2026-11-18T10:00:00Z', readAt: '2026-11-18T10:01:00Z',
  priority: 'Important', category: 'Request', lifecycleStatus: 'Active', requiresAcknowledgement: false,
  allowReply: false, acknowledgedAt: null, lifecycleReason: null, correctionMessageId: null, ownReply: null,
}));
const sent = {
  messageId: 'sent-fixture', title: 'Thông báo đã gửi kiểm thử', body: 'Nội dung đã gửi.',
  createdAt: '2026-11-18T10:00:00Z', recipientCount: 2, deliveredCount: 1, readCount: 0,
  suppressedCount: 0, acknowledgedCount: 0, dispatchStatus: 'Pending',
  priority: 'Normal', category: 'Information', requiresAcknowledgement: false, allowReply: false, lifecycleStatus: 'Active',
};
const envelope = (data: unknown) => ({ data, meta: { page: 1, pageSize: 20, totalPages: 1, totalItems: 1 } });
const record: any = { notices, sent, requests: [], delayMs: 0, failPath: null, dispatchCompleted: 1 };
(window as any).__verification = record;
httpClient.defaults.adapter = async config => {
  const url = config.url ?? '';
  record.requests.push({ url, method: config.method, params: config.params });
  if (record.delayMs) await new Promise(resolve => setTimeout(resolve, record.delayMs));
  if (url === record.failPath) throw new Error('Simulated notification refresh failure');
  let data;
  if (url === '/notifications') {
    const { page = 1, search = '', unreadOnly, category, priority, from, until } = config.params;
    const filtered = notices.filter(n => `${n.title}\n${n.body}`.toLowerCase().includes(search.toLowerCase()) &&
      (!unreadOnly || !n.readAt) && (!category || n.category === category) && (!priority || n.priority === priority) &&
      (!from || n.deliveredAt >= from) && (!until || n.deliveredAt <= until));
    data = { data: filtered.slice((page - 1) * 20, page * 20), meta: { page, pageSize: 20, totalItems: filtered.length, totalPages: Math.ceil(filtered.length / 20) } };
  } else if (url === '/notifications/summary') data = envelope({ unreadCount: notices.filter(n => !n.readAt).length });
  else if (url === '/notifications/sent') data = envelope([sent]);
  else if (url === '/notifications/sent/sent-fixture') data = envelope(sent);
  else if (url === '/notification-operations') data = { ...envelope([]), summary: {
    pending: 0, processing: 0, deadLetter: 0, completed: record.dispatchCompleted,
    delivered: record.dispatchCompleted, suppressed: 0, oldestPendingAt: null,
  } };
  else throw new Error(`Unexpected fixture request: ${url}`);
  return { data: structuredClone(data), status: 200, statusText: 'OK', headers: {}, config };
};
const query = new QueryClient({ defaultOptions: { queries: { retry: false } } });
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={query}>
  <MemoryRouter initialEntries={['/quan-ly/thong-bao']}><NotificationsPage /></MemoryRouter>
</QueryClientProvider>);
