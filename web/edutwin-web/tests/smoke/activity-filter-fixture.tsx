// Production journal with isolated HTTP data; never writes to the live API.
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ActivityPage } from '../../src/pages/ActivityPage';
import { httpClient } from '../../src/api/httpClient';
import { useAuthStore } from '../../src/stores/authStore';
import type { AccountType } from '../../src/types/auth';
import '../../src/index.css';

const options = {
  Teacher: [{source:'Audit', action:'AssignmentResultApproved'}, {source:'Assessment', action:'AttemptSubmitted'},
    {source:'TeacherReview', action:'Approved'}, {source:'ReviewRequest', action:'ReviewRequestCreated'},
    {source:'Curriculum', action:'CurriculumApplied'}, {source:'Business', action:'AssignmentPublished'}],
  CenterManager: [{source:'Audit', action:'ClassUpdated'}, {source:'Audit', action:'TeacherPasswordReset'},
    {source:'Business', action:'AssignmentCreated'}, {source:'Curriculum', action:'CurriculumApplied'}],
};
const row = (source: string, action: string, id: number) => ({source, action, sourceId:String(id),
  occurredAt:'2026-11-18T10:00:00Z', actorName:'Người thực hiện kiểm thử', targetType:'Class', targetId:'fixture-class',
  targetLabel:'Đối tượng kiểm thử', beforeStatus:null, afterStatus:null, version:null, reason:null, link:null});
const rows = Object.fromEntries(Object.entries(options).map(([role, choices]) => [role, [
  ...Array.from({length:21}, (_,i) => row(choices[0].source, choices[0].action, i + 1)),
  ...choices.slice(1).map((o,i) => row(o.source, o.action, 22 + i)),
]]));
function setRole(accountType: AccountType, authorizationVersion = 1) {
  useAuthStore.getState().setSession('fixture-only-not-a-real-token', {
    userId:'fixture-user', centerId:'fixture-center', centerName:'Fixture', username:'fixture', displayName:'Fixture',
    role:accountType, accountType, roles:[], authorizationVersion,
    permissions:[accountType === 'Teacher' ? 'activity.history.read_scoped' : 'activity.history.read_administrative'],
  });
}
const record: any = {requests:[], options, rows, failOptions:false, delayMs:0, setRole, row};
(window as any).__verification = record;
setRole('Teacher');
httpClient.defaults.adapter = async config => {
  const role = useAuthStore.getState().user!.accountType;
  const url = config.url;
  record.requests.push({url, role, method:config.method, params:config.params});
  if (record.delayMs) await new Promise(resolve => setTimeout(resolve, record.delayMs));
  let data;
  if (url === '/activity/filter-options') {
    if (record.failOptions) throw new Error('Simulated filter options failure');
    data = {data:record.options[role], meta:{}};
  } else if (url === '/activity') {
    const {page = 1, pageSize = 20, source, action, from, until} = config.params;
    const filtered = record.rows[role].filter((r: any) => (!source || r.source === source) && (!action || r.action === action) &&
      (!from || r.occurredAt >= from) && (!until || r.occurredAt <= until));
    data = {data:filtered.slice((page-1)*pageSize,page*pageSize), meta:{page,pageSize,totalItems:filtered.length,totalPages:Math.ceil(filtered.length/pageSize)}};
  } else throw new Error(`Unexpected activity fixture request: ${url}`);
  return {data:structuredClone(data), status:200, statusText:'OK', headers:{}, config};
};
const query = new QueryClient({defaultOptions:{queries:{retry:false}}});
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={query}>
  <MemoryRouter initialEntries={['/giao-vien/nhat-ky-hoat-dong']}><ActivityPage /></MemoryRouter>
</QueryClientProvider>);
