import {createRoot} from 'react-dom/client';
import {QueryClient,QueryClientProvider} from '@tanstack/react-query';
import {MemoryRouter} from 'react-router-dom';
import {StudentDashboardPage} from '../../src/pages/StudentDashboardPage';
import {StudentFocusStudio} from '../../src/components/student/StudentFocusStudio';
import {useAuthStore} from '../../src/stores/authStore';
import '../../src/index.css';
const qc=new QueryClient({defaultOptions:{queries:{staleTime:Infinity,retry:false,refetchOnWindowFocus:false,refetchOnReconnect:false}}});
useAuthStore.setState({user:{userId:'qa',centerId:'qa',centerName:'Trung tâm kiểm thử',username:'qa',displayName:'Minh Anh',accountType:'Student',role:'Student',roles:[],permissions:[],authorizationVersion:1}});
qc.setQueryData(['studentDashboard','math','',false],{student:{fullName:'Nguyễn Minh Anh'},subject:{subjectName:'Toán học'},goal:{hasGoal:true,hasPrediction:true,targetScore:8.5,currentPredictedScore:7.2,riskScore:24,remainingDays:32},masteryRadar:[{topicNodeId:'1',topicName:'Hàm số và đồ thị',mastery:82,evidenceCount:12},{topicNodeId:'2',topicName:'Đạo hàm',mastery:61,evidenceCount:7},{topicNodeId:'3',topicName:'Tích phân',mastery:43,evidenceCount:4}],progressLine:[]});
qc.setQueryData(['studentAssignmentsToday','math','',false],{data:[{assignmentId:'qa',title:'Chinh phục đạo hàm, từng bước nhỏ.',dueAt:new Date(Date.now()+86400000).toISOString(),progress:{status:'InProgress',completedQuestionCount:3,totalQuestionCount:10},summary:{estimatedMinutes:15,knowledgeAreas:[]}}]});
qc.setQueryData(['student-workspace-summary','dashboard','math','',false],{dailyStreak:5,studiedToday:true,assignmentCount:3});
qc.setQueryData(['learning-agenda','dashboard','math',''],{ongoingRuns:[],todayPlan:[{missionId:'1',title:'Ôn quy tắc đạo hàm',purpose:'Review',status:'Completed',questionCount:5,estimatedDurationMinutes:10},{missionId:'2',title:'Vận dụng đạo hàm',purpose:'Study',status:'Ready',questionCount:8,estimatedDurationMinutes:15}],exams:[]});
qc.setQueryData(['studentTwinHistory','math',''],[]);
qc.setQueryData(['question-bookmarks','qa','qa'],[{questionId:'1',subjectId:'math',answered:true,questionText:'Tìm đạo hàm của hàm số f(x) = x² + 3x.',topicName:'Đạo hàm',correctAnswer:'f′(x) = 2x + 3',solution:'Áp dụng quy tắc đạo hàm của tổng.'}]);
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={qc}><MemoryRouter initialEntries={['/hoc-tap/tong-quan?subjectId=math']}><div className="student-shell min-h-screen pb-32"><header style={{padding:'18px 32px',borderBottom:'1px solid var(--student-border)',display:'flex',justifyContent:'space-between'}}><b>EduTwin / Learning Studio</b><span style={{fontSize:12}}>KIỂM TRA GIAO DIỆN · DỮ LIỆU MẪU</span></header><StudentDashboardPage/><StudentFocusStudio/></div></MemoryRouter></QueryClientProvider>);

