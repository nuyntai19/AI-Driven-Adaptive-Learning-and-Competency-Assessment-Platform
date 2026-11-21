import { AssignmentGradingWorkspace } from "../../src/components/reviews/AssignmentGradingWorkspace";
import { StudentFocusStudio } from "../../src/components/student/StudentFocusStudio";
import { NotificationsPage } from "../../src/pages/NotificationsPage";
import "../../src/styles/student-theme.css";
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
import { StudentAssignmentsPage } from '../../src/pages/StudentAssignmentsPage';
import { TeacherAssignmentEditorView } from '../../src/pages/teacher/TeacherAssignmentEditorView';
import { TeacherStudentManagementView } from '../../src/pages/teacher/TeacherStudentManagementView';
import { TeacherThemeScope } from '../../src/components/teacher/TeacherThemeScope';
import '../../src/index.css';

const scenario = new URLSearchParams(location.search).get('scenario') ?? 'goal';
const submissionCase = new URLSearchParams(location.search).get('submissions');
const teacher = scenario.startsWith('teacher');
useAuthStore.getState().setSession('fixture-only-not-a-real-token', {
  userId: 'fixture-user', centerId: 'fixture-center', centerName: 'Fixture', username: 'fixture', displayName: 'Fixture',
  role: teacher ? 'Teacher' : 'Student', accountType: teacher ? 'Teacher' : 'Student', roles: [],
  permissions: scenario === 'teacher-denied' ? [] : Object.values(permissions), authorizationVersion: 1,
});
const record: any = { requests: [], accepted: false, closed: false, unknown: [] };
(window as any).__verification = record;
const skills = ['base', 'target'].map((id, i) => ({ skillId: id, skillName: i ? 'Kỹ năng phụ thuộc' : 'Kỹ năng nền', topicId: 'topic', topicName: 'Chủ đề kiểm thử',
  description: 'Nội dung kiểm thử giao diện', state: i ? 'Unknown' : 'Developing', availability: 'Available', prerequisiteSkillIds: scenario === 'goal-graph' && i ? ['base'] : [], evidenceCount: 0,
  requiresProbe: true, rationale: 'Bằng chứng fixture', masteryScore: null }));
const schedule = { studyDays: [0,1,2,3,4,5,6], excludedDates: [], timeZoneId: 'Asia/Ho_Chi_Minh', reviewReservePercent: 20,
  shortCheckDays: 45, mockExamDays: 14, questionReuseDelayDays: 2 };
const mission: any = { missionId: 'mission', milestoneOrder: 1, milestoneTitle: 'Kỹ năng nền', skillId: 'base', skillName: 'Kỹ năng nền', title: 'Kiểm tra đầu vào: Kỹ năng nền',
  purpose: 'IndependentCheck', objective: 'Kiểm tra', estimatedDurationMinutes: 10, questionCount: 1, activityTypes: ['IndependentCheck'], status: 'NotStarted',
  canStart: true, recommendationReason: 'Kiểm tra bằng câu mới', plannedDate: '2026-10-26', runs: [] };
const activity: any = { activityRunId: 1, sessionRunId: 1, activityOrder: 1, activityType: 'IndependentCheck', status: 'InProgress', stage: 'Tự kiểm tra',
  draftRevision: 1, assistanceRequestedCount: 0, startedAt: new Date().toISOString(), question: { question: {
    questionId: '10000', questionText: 'Câu kiểm thử không chấm được: có thể bỏ qua.', questionType: 'ShortAnswer', options: [], hasImage: false, reasoningRequired: false } } };
const placement = scenario === 'placement';
const activities = placement ? [
  activity,
  { ...structuredClone(activity), activityRunId: 2, activityOrder: 2 },
  { ...structuredClone(activity), activityRunId: 3, activityOrder: 3 },
] : [activity];
const run: any = { requiresSubmissionConfirmation: placement, sessionRunId: 1, sessionId: 'mission', status: 'InProgress', outcome: 'None', runNumber: 1, activities,
  startedAt: new Date().toISOString(), ...(scenario === 'expired' ? { expiresAt: new Date(Date.now() + 1500).toISOString() } : {}) };
let assignment: any = { assignmentId: 'exam', classId: 'class', title: 'Thi giữa kỳ kiểm thử', instructions: '', status: 'Published', kind: 'Midterm',
  examAt: new Date(Date.now() + 4 * 86400000).toISOString(), dueAt: new Date(Date.now() + 4 * 86400000 + 1800000).toISOString(), timeLimitMinutes: 30,
  rowVersion: '11', targetMode: 'WholeClass', questions: [], targets: [], questionCount: 0, targetStudentCount: 0 };
const cls = { classId: 'class', className: 'Lớp kiểm thử', subjectId: 'subject', subjectName: 'Toán', subject: { subjectId: 'subject', subjectName: 'Toán' }, academicYear: '2026–2027', teacherId: 'fixture-user', gradeLevel: 10, status: 'Active' };
const reportStudent = { studentId: 'report-student', fullName: 'Học sinh kiểm thử', username: 'fixture-student', gradeLevel: 10, status: 'Active' };
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
const timeline = scenario.startsWith("timeline");
if (timeline) {
  agenda.missions.push({ ...mission, missionId: "review", milestoneOrder: 4, milestoneTitle: "Ôn xác nhận",
    title: "Ôn xác nhận", purpose: "Review", isSelfAssessedCheck: true });
  agenda.missions.push({ ...mission, missionId: "later-study", milestoneOrder: 5, milestoneTitle: "Chặng học tiếp theo",
    title: "Học kỹ năng tiếp theo", purpose: "Study" });
  agenda.exams.push({ assignmentId: "published-midterm", classId: "class", title: "Giữa kỳ đã công bố", kind: "Midterm",
    status: "Completed", supportSkillIds: ["target"], examAt: "2026-10-27T18:00:00Z", timeLimitMinutes: 45 });
  agenda.missions.push({ ...mission, missionId: "repair", milestoneOrder: 2, skillId: "target", skillName: "Kỹ năng phụ thuộc",
    title: "Ôn tập sau thi giữa kỳ: Kỹ năng phụ thuộc", sourceExamAssignmentId: "published-midterm", purpose: "Practice" });
  const finalPublished = scenario === "timeline-final";
  agenda.exams.push({ assignmentId: "term-final", classId: "class", title: "Đề cuối kỳ tiếng Anh", kind: "Final",
    status: finalPublished ? "Completed" : "PendingGrading", supportSkillIds: ["target"], examAt: "2026-11-10T18:00:00Z", timeLimitMinutes: 45 });
  if (finalPublished) agenda.missions.push({ ...mission, missionId: "repair-final", milestoneOrder: 2, skillId: "target", skillName: "Kỹ năng phụ thuộc",
    title: "Ôn tập cuối kỳ: Kỹ năng phụ thuộc", sourceExamAssignmentId: "term-final", purpose: "Practice" });
}
const examSchedule = scenario.startsWith("exam-schedule");
if (examSchedule) {
  agenda.exams = [{ assignmentId: "upcoming", classId: "class", title: "Thi giữa kỳ kiểm thử", kind: "Midterm",
    examAt: new Date(Date.now() + 86400000).toISOString(), timeLimitMinutes: 45, status: "Scheduled", supportSkillIds: [] }];
  if (scenario === "exam-schedule") agenda.scheduleSummary = {
    phase: "ShortChecks", message: "Ôn 30 phút hôm nay.", deferredSkillIds: [], missingMinutes: 0,
  };
  else agenda.learningPathId = null;
}
const notices = Array.from({ length: 21 }, (_, i) => ({
  deliveryId: String(i + 1), eventType: "AssignmentPublished", title: `Thông báo kiểm thử ${i + 1}`,
  body: "Bạn được giao bài tập này.", deliveredAt: "2026-11-18T10:00:00Z", readAt: "2026-11-18T10:01:00Z",
  priority: "Normal", category: "Information", lifecycleStatus: "Active", senderName: "Giảng viên",
}));
if (scenario === "notifications" || scenario === "focus") document.documentElement.classList.add("student-theme", "dark");
httpClient.defaults.adapter = async config => {
  const url = config.url ?? ''; const method = config.method ?? 'get';
  const body = typeof config.data === 'string' ? JSON.parse(config.data) : config.data;
  record.requests.push({ url, method, body, params: config.params } as any);
  let data: any;
  if (url === "/notifications") {
    const page = Number(config.params.page);
    data = { data: notices.slice((page - 1) * 20, page * 20), meta: { page, pageSize: 20, totalItems: notices.length, totalPages: Math.ceil(notices.length / 20) } };
  }
  else if (url === "/notifications/summary") data = envelope({ unreadCount: 0 });
  else if (url.endsWith("/hide") && url.startsWith("/notifications/")) {
    notices.splice(notices.findIndex(item => item.deliveryId === url.split("/")[2]), 1);
    data = {};
  }
  else if (url.startsWith("/notifications/")) {
    const notice = notices.find(item => item.deliveryId === url.split("/")[2]);
    if (!notice) throw new Error("Notification no longer available");
    data = envelope(notice);
  }
  else if (url === '/students/me/question-bookmarks') data = [];
  else if (url === '/subjects') data = envelope([{ subjectId: 'subject', subjectName: 'Toán', isActive: true }]);
  else if (url === '/classes') data = envelope([cls]);
  else if (url === '/classes/class') data = envelope(cls);
  else if (url === '/classes/class/history' || url === '/teacher-student-notes/my-notes') data = envelope([]);
  else if (url === '/students/report-student') data = envelope({ ...reportStudent, subjectGoals: [] });
  else if (url === '/classes/class/academic-report' && submissionCase) {
    const empty = { totalAssigned: 0, completedCount: 0, inProgressCount: 0, notStartedCount: 0, overdueCount: 0,
      completionRate: 0, averageScore: null, minScore: null, maxScore: null, records: [] };
    const submitted = submissionCase === 'none' ? empty : { ...empty, totalAssigned: 1, completedCount: 1, completionRate: 100 };
    const other = submissionCase === 'weighted' ? { ...empty, totalAssigned: 3, notStartedCount: 3 }
      : submissionCase === 'pending' ? { ...empty, totalAssigned: 1, inProgressCount: 1 } : empty;
    data = envelope({ totalAssignments: submissionCase === 'none' ? 0 : submissionCase === 'weighted' ? 3 : 1,
      students: [{ student: reportStudent, summary: submitted },
        { student: { ...reportStudent, studentId: 'unassigned-student', fullName: 'Học sinh chưa được giao', username: 'unassigned' }, summary: other }] });
  }
  else if (url === '/classes/class/academic-report') data = envelope({
    students: [{ student: reportStudent, summary: { totalAssigned: 0, completedCount: 0, inProgressCount: 0, notStartedCount: 0, overdueCount: 0, completionRate: 0, records: [] } }],
    totalAssignments: 0,
  });
  else if (url === '/classes/class/students' || url === '/questions' || url === '/knowledge/nodes') data = envelope([]);
  else if (url === '/teachers/me/review-queue') data = envelope([]);
  else if (url === '/assignments') data = envelope([
    { ...assignment, title: "Đề giữa kỳ", kind: "Midterm" },
    { ...assignment, assignmentId: "final", title: "Đề cuối kỳ", kind: "Final" },
    { ...assignment, assignmentId: "regular", title: "Bài luyện thường", kind: "Regular" },
    { ...assignment, assignmentId: "mock", title: "Đề thi thử", kind: "MockExam" },
  ]);
  else if (url === '/assignments/exam') {
    if (method === 'patch') assignment = { ...assignment, ...body, rowVersion: '12' };
    data = envelope(assignment);
  }
  else if (url === '/assignments/exam/progress') data = envelope([]);
  else if (url === '/students/me/assignments') {
    const page = config.params.page ?? 1;
    const items = page === 1 ? [{ assignmentId: 'regular', title: 'Bài luyện của em', kind: 'Regular' }] : [
      { assignmentId: 'midterm', title: 'Kiểm tra giữa kỳ A', kind: 'Midterm' },
      { assignmentId: 'final', title: 'Kiểm tra cuối kỳ B', kind: 'Final' },
    ];
    data = envelope(items.map(item => ({ ...item, subjectId: 'subject', classId: 'class', progress: { status: 'NotStarted' }, questionCount: 1 })));
    data.meta = { ...data.meta, page, totalPages: 2, totalItems: 3 };
  }
  else if (url === '/students/me/academic-context') data = envelope({ isHistory: false, selectedClassId: 'class', classes: [{ ...cls, isHistorical: false }],
    curriculums: [{ curriculumId: 'curriculum', classId: 'class', title: 'Fixture', applicationRole: 'Primary' }], message: null });
  else if (url.includes('/capability-map')) data = { subjectId: 'subject', studentId: 'fixture-user', subjectName: 'Toán', skills, provenCapabilities: [], areasNeedingSupport: [], reviewDueSkills: [], externalPrerequisiteMastery: {} };
  else if (url === '/students/me/learning-agenda') data = agenda;
  else if (url === '/students/me/learning-goals') { record.goal = body; data = { ...body, goalId: 'goal' }; }
  else if (url === '/students/me/path-proposals') data = { ...proposal, isPlacement: record.goal?.startWithPlacement ?? false };
  else if (url.endsWith('/accept')) { record.accepted = true; data = { learningPathId: 'path', version: 1, message: 'Đã chấp nhận' }; }
  else if (url.endsWith('/start')) data = run;
  else if (url.endsWith('/draft')) {
    const target = activities.find(a => a.activityRunId === Number(url.split('/').slice(-2)[0]));
    Object.assign(target, { draftPayload: body.draftPayload, draftRevision: target.draftRevision + 1 });
    data = { draftRevision: target.draftRevision, hasConflict: false };
  }
  else if (url.endsWith('/submissions')) {
    const target = activities.find(a => a.activityRunId === Number(url.split('/').slice(-2)[0]));
    target.status = 'Submitted'; target.submittedResponse = body.finalAnswer;
    data = { activityRunId: target.activityRunId, activityStatus: 'Submitted', isRunFinished: true };
  }
  else if (url.endsWith('/evaluate-v4')) { run.status = 'Completed'; data = { sessionRunId: 1, outcome: scenario === 'expired' ? 'TimedOut' : 'Inconclusive', decisionReasons: [scenario === 'expired' ? 'Đã hết giờ. Giữ câu đã nộp.' : 'Câu đã bỏ qua: chưa đủ bằng chứng.'], nextMissionOptions: [], provenSkills: [], skillsNeedingSupport: [] }; }
  else { record.unknown.push(url); throw new Error('Unhandled fixture request: ' + url); }
  return { data: structuredClone(data), status: 200, statusText: 'OK', headers: {}, config };
};
const query = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
const component = scenario.startsWith('goal') ? <GoalAndProposalModal isOpen subjectId="subject" subjectName="Toán" skills={skills} entryMode="Start" onClose={() => { record.closed = true; }} onAccepted={() => { record.accepted = true; }} />
  : scenario === 'student-assignments' ? <StudentAssignmentsPage />
  : scenario === 'teacher-students' || scenario === 'teacher-reports' ? <TeacherThemeScope className="p-4"><TeacherStudentManagementView reports={scenario === 'teacher-reports'} /></TeacherThemeScope>
  : scenario === "teacher-grading" ? <TeacherThemeScope className="p-4"><AssignmentGradingWorkspace actor="Teacher" /></TeacherThemeScope>
  : teacher ? <TeacherThemeScope className="p-4"><Routes><Route path="/giao-vien/bai-tap/:id" element={<TeacherAssignmentEditorView />} /></Routes></TeacherThemeScope>
  : (timeline || examSchedule) ? <StudentLearningPathPage />
  : scenario === "notifications" ? <NotificationsPage />
  : scenario === "focus" ? <StudentFocusStudio />
  : <MissionPlayerModal isOpen mission={mission} subjectId="subject" onClose={() => { record.closed = true; }} onResumeMission={() => {}} />;
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={query}><MemoryRouter initialEntries={[teacher ? '/giao-vien/bai-tap/exam' : '/hoc-tap/lo-trinh-hoc-tap?subjectId=subject&classId=class' + (examSchedule ? '&examId=upcoming' : '')]}>{component}</MemoryRouter></QueryClientProvider>);
