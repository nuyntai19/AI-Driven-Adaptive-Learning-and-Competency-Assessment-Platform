// Production page and API client, isolated HTTP adapter; no live API calls.
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CanceledError } from 'axios';
import { PlatformCentersPage } from '../../src/pages/PlatformCentersPage';
import { httpClient } from '../../src/api/httpClient';
import { useAuthStore } from '../../src/stores/authStore';
import '../../src/index.css';

useAuthStore.getState().setSession('fixture-only-not-a-real-token', {
  userId:'fixture-admin', centerId:'fixture-platform', centerName:'Fixture', username:'fixture', displayName:'Fixture',
  role:'PlatformAdmin', accountType:'PlatformAdmin', roles:[], authorizationVersion:1, permissions:['platform.centers.read'],
});
const centers = Array.from({length:25}, (_,i) => ({
  centerId:`center-${i+1}`, centerCode:`CENTER_${i+1}`, centerName:`Trung tâm kiểm thử ${i+1}`,
  status:i<21 ? 'Active' : 'Suspended', timezone:'Asia/Bangkok', createdAt:'2026-11-18T10:00:00Z', rowVersion:'1',
  primaryManagerUserId:`manager-${i+1}`, primaryManagerUsername:`manager_${i+1}`, primaryManagerDisplayName:`Quản lý ${i+1}`,
}));
Object.assign(centers[0], {centerCode:'HANOI_A', centerName:'Trung tâm Hà Nội', primaryManagerUsername:'manager_lan', primaryManagerDisplayName:'Nguyễn Lan'});
Object.assign(centers[24], {centerCode:'HANOI_B', centerName:'Trung tâm Hà Nội 2', primaryManagerUsername:'manager_lan_b', primaryManagerDisplayName:'Nguyễn Lan B'});
const record: any = {requests:[], cancelled:[], delays:{}};
(window as any).__verification = record;
httpClient.defaults.adapter = async config => {
  if (config.url !== '/platform/centers') throw new Error(`Unexpected request: ${config.url}`);
  const {page=1, pageSize=20, search='', status} = config.params;
  record.requests.push({url:config.url, method:config.method, params:config.params});
  if (record.delays[search]) await new Promise<void>((resolve,reject) => {
    const timeout = setTimeout(resolve,record.delays[search]);
    config.signal?.addEventListener?.('abort', () => { clearTimeout(timeout); record.cancelled.push(search); reject(new CanceledError()); }, {once:true});
  });
  const needle = search.toLocaleLowerCase('vi');
  const filtered = centers.filter(c => (!status || status===c.status) &&
    [c.centerCode,c.centerName,c.primaryManagerUsername,c.primaryManagerDisplayName].some(value=>value.toLocaleLowerCase('vi').includes(needle)));
  return {data:{data:{items:filtered.slice((page-1)*pageSize,page*pageSize),totalCount:filtered.length,page,pageSize}},
    status:200,statusText:'OK',headers:{},config};
};
const query = new QueryClient({defaultOptions:{queries:{retry:false}}});
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={query}>
  <MemoryRouter initialEntries={['/quan-tri-nen-tang/trung-tam']}><PlatformCentersPage /></MemoryRouter>
</QueryClientProvider>);
