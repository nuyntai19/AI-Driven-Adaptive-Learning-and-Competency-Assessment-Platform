// Real production components, deterministic HTTP fixtures; this is not live API/MySQL E2E.
import { createRoot } from 'react-dom/client';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { httpClient } from '../../src/api/httpClient';
import { useAuthStore } from '../../src/stores/authStore';
import { permissions } from '../../src/auth/permissions';
import { GoalAndProposalModal } from '../../src/components/learningPathV4/GoalAndProposalModal';
import { MissionPlayerModal } from '../../src/components/learningPathV4/MissionPlayerModal';
import { StudentLearningPathPage } from '../../src/pages/StudentLearningPathPage';
import { TeacherAssignmentEditorView } from '../../src/pages/teacher/TeacherAssignmentEditorView';
import { TeacherThemeScope } from '../../src/components/teacher/TeacherThemeScope';
import '../../src/index.css';

const scenario = new URLSearchParams(location.search).get('scenario') ?? 'goal';
const teacher = scenario.startsWith('teacher');
useAuthStore.getState().setSession('fixture-only-not-a-real-token', {
  userId: 'fixture-user', centerId: 'fixture-center', centerName: 'Fixture', username: 'fixture', displayName: 'Fixture',
  role: teacher ? 'Teacher' : 'Student', accountType: teacher ? 'Teacher' : 'Student', roles: [],
  permissions: scenario === 'teacher-denied' ? [] : Object.values(permissions), authorizationVersion: 1,
});
const record: any = { requests: [], accepted: false, closed: false, unknown: [] };
(window as any).__verification = record;
const skills = ['base', 'target'].map((id, i) => ({ skillId: id, skillName: i ? 'Kỹ năng phụ thuộc' : 'Kỹ năng nền', topicId: 'topic', topicName: 'Chủ đề kiểm thử',
  description: 'Nội dung kiểm thử giao diện', state: i ? 'Unknown' : 'Developing', availability: 'Available', prerequisiteSkillIds: [], evidenceCount: 0,
  requiresProbe: true, rationale: 'Bằng chứng fixture', masteryScore: null }));
const schedule = { studyDays: [0,1,2,3,4,5,6], excludedDates: [], timeZoneId: 'Asia/Ho_Chi_Minh', reviewReservePercent: 20,
  shortCheckDays: 45, mockExamDays: 14, questionReuseDelayDays: 2 };
const mission: any = { missionId: 'mission', milestoneOrder: 1, milestoneTitle: 'Kỹ năng nền', skillId: 'base', skillName: 'Kỹ năng nền', title: 'Kiểm tra đầu vào: Kỹ năng nền',
  purpose: 'IndependentCheck', objective: 'Kiểm tra', estimatedDurationMinutes: 10, questionCount: 1, activityTypes: ['IndependentCheck'], status: 'NotStarted',
  canStart: true, recommendationReason: 'Kiểm tra bằng câu mới', plannedDate: '2026-10-26', runs: [] };
const activity: any = { activityRunId: 1, sessionRunId: 1, activityOrder: 1, activityType: 'IndependentCheck', status: 'InProgress', stage: 'Tự kiểm tra',
  draftRevision: 1, assistanceRequestedCount: 0, startedAt: new Date().toISOString(), question: { question: {
    questionId: '10000', questionText: 'Câu kiểm thử không chấm được: có thể bỏ qua.', questionType: 'ShortAnswer', options: [], hasImage: false, reasoningRequired: false } } };
const run: any = { sessionRunId: 1, sessionId: 'mission', status: 'InProgress', outcome: 'None', runNumber: 1, activities: [activity],
  startedAt: new Date().toISOString(), ...(scenario === 'expired' ? { expiresAt: new Date(Date.now() + 1500).toISOString() } : {}) };
let assignment: any = { assignmentId: 'exam', classId: 'class', title: 'Thi giữa kỳ kiểm thử', instructions: '', status: 'Published', kind: 'Midterm',
  examAt: new Date(Date.now() + 4 * 86400000).toISOString(), dueAt: new Date(Date.now() + 4 * 86400000 + 1800000).toISOString(), timeLimitMinutes: 30,
  rowVersion: '11', targetMode: 'WholeClass', questions: [], targets: [], questionCount: 0, targetStudentCount: 0 };
const cls = { classId: 'class', className: 'Lớp kiểm thử', subjectId: 'subject', subjectName: 'Toán', subject: { subjectId: 'subject', subjectName: 'Toán' }, academicYear: '2026–2027', teacherId: 'fixture-user', gradeLevel: 10, status: 'Active' };
const envelope = (data: any) => ({ data, meta: { totalItems: Array.isArray(data) ? data.length : 1, totalPages: 1, page: 1, pageSize: 20 } });
const proposal: any = { proposalId: 'proposal', goalId: 'goal', subjectId: 'subject', goalTitle: 'Mục tiêu kiểm thử', goalType: 'ExamPrep', isPlacement: false,
  startingBaselineSummary: 'Đề xuất kiểm thử giao diện', confirmedSkills: [], developingSkills: [], unknownSkills: ['base'], requestedSkillIds: ['base'],
  selfAssessedSkillIds: [], selfReportedPrerequisiteSkillIds: [], availableMinutesPerDay: 30, availableDaysPerWeek: 7, schedule,
  feasibilityStatus: 'Feasible', feasibilityMessage: 'Đã xếp trong ngân sách 30 phút/ngày.', estimatedWeeks: 1,
  milestones: [{ order: 1, title: 'Kỹ năng nền', skillIds: ['base'], prerequisiteSkillIds: [], outcomeDescription: 'Kiểm thử', missions: [mission] }] };
const insight = { answerCount: 0, windowDays: 30, gradedQuestionCount: 0, assignmentCount: 0, pendingCount: 0, skills: [], suggestedSkillIds: [], summary: '' };
const agenda: any = { learningPathId: 'path', date: new Date().toISOString(), availableMinutes: 30, availableMinutesPerDay: 30, availableDaysPerWeek: 7,
  goalType: 'ExamPrep', targetSkillIds: ['base','target'], schedule, insights: insight, recentChanges: [], pathHistory: [], agendaMessage: '',
  missions: [mission, { ...mission, missionId: 'blocked', milestoneOrder: 2, milestoneTitle: 'Kỹ năng phụ thuộc', skillId: 'target', skillName: 'Kỹ năng phụ thuộc',
    title: 'Rèn phần phụ thuộc', purpose: 'Practice', canStart: false, recommendationReason: 'Mốc kiểm tra nền tảng chưa đạt', plannedDate: '2026-10-29' },
    { ...mission, missionId: 'mock', milestoneOrder: 3, milestoneTitle: 'Thi thử tổng hợp', title: 'Thi thử tổng hợp', isMockExam: true, isMilestoneExam: true, plannedDate: '2026-11-01' }],
  alternativeOptions: [], ongoingRuns: [], reviewDueTasks: [], todayPlan: [],
  exams: [{ assignmentId: 'class-exam', classId: 'class', title: 'Mốc thi lớp đã hủy', kind: 'Final', examAt: '2026-10-27T18:00:00Z', timeLimitMinutes: 30, status: 'Cancelled', supportSkillIds: [] }] };
httpClient.defaults.adapter = async config => {
  const url = config.url ?? ''; const method = config.method ?? 'get';
  const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
  record.requests.push({ url, method, body });
  let data: any;
  if (url === '/subjects') data = envelope([{ subjectId: 'subject', subjectName: 'Toán', isActive: true }]);
  else if (url === '/classes') data = envelope([cls]);
  else if (url === '/classes/class') data = envelope(cls);
  else if (url === '/classes/class/students' || url === '/questions' || url === '/knowledge/nodes') data = envelope([]);
  else if (url === '/assignments/exam') {
    if (method === 'patch') assignment = { ...assignment, ...body, rowVersion: '12' };
    data = envelope(assignment);
  }
  else if (url === '/assignments/exam/progress') data = envelope([]);
  else if (url === '/students/me/academic-context') data = envelope({ isHistory: false, selectedClassId: 'class', classes: [{ ...cls, isHistorical: false }],
    curriculums: [{ curriculumId: 'curriculum', classId: 'class', title: 'Fixture', applicationRole: 'Primary' }], message: null });
  else if (url.includes('/capability-map')) data = { subjectId: 'subject', studentId: 'fixture-user', subjectName: 'Toán', skills, provenCapabilities: [], areasNeedingSupport: [], reviewDueSkills: [], externalPrerequisiteMastery: {} };
  else if (url === '/students/me/learning-agenda') data = agenda;
  else if (url === '/students/me/learning-goals') { record.goal = body; data = { ...body, goalId: 'goal' }; }
  else if (url === '/students/me/path-proposals') data = { ...proposal, isPlacement: record.goal?.startWithPlacement ?? false };
  else if (url.endsWith('/accept')) { record.accepted = true; data = { learningPathId: 'path', version: 1, message: 'Đã chấp nhận' }; }
  else if (url.endsWith('/start')) data = run;
  else if (url.endsWith('/submissions')) { activity.status = 'Submitted'; data = { activityRunId: 1, activityStatus: 'Submitted', isRunFinished: true, outcomeFeedback: 'Đã bỏ qua' }; }
  else if (url.endsWith('/evaluate-v4')) data = { sessionRunId: 1, outcome: scenario === 'expired' ? 'TimedOut' : 'Inconclusive', decisionReasons: [scenario === 'expired' ? 'Đã hết giờ. Giữ câu đã nộp.' : 'Câu đã bỏ qua: chưa đủ bằng chứng.'], nextMissionOptions: [], provenSkills: [], skillsNeedingSupport: [] };
  else { record.unknown.push(url); throw new Error('Unhandled fixture request: ' + url); }
  return { data: structuredClone(data), status: 200, statusText: 'OK', headers: {}, config };
};
const query = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
const component = scenario === 'goal' ? <GoalAndProposalModal isOpen subjectId="subject" subjectName="Toán" skills={skills} entryMode="Start" onClose={() => { record.closed = true; }} onAccepted={() => { record.accepted = true; }} />
  : teacher ? <TeacherThemeScope className="p-4"><Routes><Route path="/giao-vien/bai-tap/:id" element={<TeacherAssignmentEditorView />} /></Routes></TeacherThemeScope>
  : scenario === 'timeline' ? <StudentLearningPathPage />
  : <MissionPlayerModal isOpen mission={mission} subjectId="subject" onClose={() => { record.closed = true; }} onResumeMission={() => {}} />;
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={query}><MemoryRouter initialEntries={[teacher ? '/giao-vien/bai-tap/exam' : '/hoc-tap/lo-trinh-hoc-tap?subjectId=subject&classId=class']}>{component}</MemoryRouter></QueryClientProvider>);
