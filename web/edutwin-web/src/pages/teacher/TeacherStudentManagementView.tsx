import { useState, useMemo, useEffect, useRef } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { teacherNotesApi } from "../../api/teacherNotesApi";
import { organizationApi } from "../../api/organizationApi";
import { httpClient } from "../../api/httpClient";
import { getTeacherStudentTwin } from "../../api/digitalTwinApi";
import { useAuthStore } from "../../stores/authStore";
import { legacyTeacherNotes } from "../../utils/teacherNotesStorage";
import { isWeakTopic, twinPercent } from "../../utils/topicMastery";
import { permissions } from "../../auth/permissions";
import {
  TeacherPageHeader,
  TeacherMetricCard,
  TeacherStatusBadge,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import { TeacherModal } from "../../components/teacher/TeacherOverlays";
import { ClassHistoryPanel } from "../../components/ClassHistoryPanel";
import type { StudentDto, StudentSubjectGoalDto } from "../../types/organization";
import type { StudentTwinDataDto } from "../../types/digitalTwin";

import {
  downloadCsv,
  exportStudentReportXlsx,
  exportClassReportXlsx,
  buildStudentReportCsv,
  buildClassReportCsv,
  assessmentLabel,
  isAcademicHighRisk,
  reportScoreText,
  filterReportPeriod,
  maxRegularGrades,
  simulateSubjectGrade,
  type StudentAcademicSummary,
} from "./teacherReportsHelpers";

interface ClassAcademicReport {
  totalAssignments: number;
  generatedAt: string;
  students: { student: StudentDto; summary: StudentAcademicSummary }[];
}

export function TeacherStudentManagementView({ reports = false }: { reports?: boolean }) {
  const { studentId: routeStudentId } = useParams<{ studentId?: string }>();
  const navigate = useNavigate();
  const hasPermission = useAuthStore((state) => state.hasPermission);
  const actor = useAuthStore((state) => state.user);
  const canCreateAssignment = hasPermission(permissions.assignmentsCreate);

  // Filter States
  const [selectedClassId, setSelectedClassId] = useState<string>("");
  const [selectedReportStudentIds, setSelectedReportStudentIds] = useState<Set<string>>(() => new Set());
  const [regularSelection, setRegularSelection] = useState<Set<string> | null>(null);
  const [periodFrom, setPeriodFrom] = useState("");
  const [periodTo, setPeriodTo] = useState("");
  const [searchQuery, setSearchQuery] = useState<string>("");
  const [statusTab, setStatusTab] = useState<"all" | "high_risk" | "good" | "needs_attention">("all");
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>("");
  const [selectedGrade, setSelectedGrade] = useState<string>("");

  // Drawer / Detail state
  const [selectedStudent, setSelectedStudent] = useState<StudentDto | null>(null);
  const [activeTab, setActiveTab] = useState<"goals" | "assignments" | "twin" | "notes">("assignments");

  // Print Preview Modals
  const [isStudentPrintModalOpen, setIsStudentPrintModalOpen] = useState<boolean>(false);
  const [isClassPrintModalOpen, setIsClassPrintModalOpen] = useState<boolean>(false);

  // Excel Export Async / Loading & Error State
  const [isExportingExcel, setIsExportingExcel] = useState<boolean>(false);
  const [exportError, setExportError] = useState<string | null>(null);

  // Notes live on the server: edits show at once and save when typing pauses.
  const queryClient = useQueryClient();
  const notesQueryKey = ["teacherStudentNotes", actor?.centerId, actor?.userId];
  const { data: savedNotes } = useQuery({
    queryKey: notesQueryKey,
    enabled: !!actor?.userId,
    queryFn: async () => {
      const notes = await teacherNotesApi.getMyNotes();
      // One-time upload of notes this browser kept before the server had them.
      const legacy = legacyTeacherNotes(actor?.centerId, actor?.userId);
      const known = new Set(notes.map((item) => item.studentId));
      const pending = Object.entries(legacy.notes).filter(([studentId, content]) => content.trim() && !known.has(studentId));
      const moved = await Promise.allSettled(pending.map(([studentId, content]) => teacherNotesApi.upsertNoteForStudent(studentId, { content })));
      if (moved.every((result) => result.status === "fulfilled")) legacy.clear();
      return [...notes, ...moved.flatMap((result) => (result.status === "fulfilled" ? [result.value] : []))];
    },
  });
  const [noteDrafts, setNoteDrafts] = useState<Record<string, string>>({});
  const [noteSaveFailed, setNoteSaveFailed] = useState(false);
  const noteSaveTimers = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const teacherNotes = useMemo<Record<string, string>>(
    () => ({ ...Object.fromEntries((savedNotes ?? []).map((item) => [item.studentId, item.content])), ...noteDrafts }),
    [savedNotes, noteDrafts]
  );

  const handleSaveNote = (studentId: string, note: string) => {
    setNoteDrafts((previous) => ({ ...previous, [studentId]: note }));
    clearTimeout(noteSaveTimers.current.get(studentId));
    noteSaveTimers.current.set(studentId, setTimeout(() => {
      teacherNotesApi.upsertNoteForStudent(studentId, { content: note })
        .then(() => {
          setNoteSaveFailed(false);
          void queryClient.invalidateQueries({ queryKey: notesQueryKey });
        })
        .catch(() => setNoteSaveFailed(true));
    }, 600));
  };

  // 1. Fetch Teacher Assigned Classes
  const { data: classesData, isLoading: isClassesLoading } = useQuery({
    queryKey: ["teacherClassesListForStudents", actor?.centerId, actor?.userId],
    queryFn: async () => {
      const first = await organizationApi.listClasses({ page: 1, pageSize: 100 });
      const all = [...first.data];
      for (let page=2; page<=first.meta.totalPages; page++)
        all.push(...(await organizationApi.listClasses({ page, pageSize:100 })).data);
      return { ...first, data:all };
    },
  });

  const classes = classesData?.data || [];

  // Auto-select first class if none selected
  useEffect(() => {
    if (classes.length > 0 && !selectedClassId) {
      setSelectedClassId(classes[0].classId);
    }
  }, [classes, selectedClassId]);

  const selectedClass = useMemo(
    () => classes.find((c) => c.classId === selectedClassId),
    [classes, selectedClassId]
  );

  // 2. Fetch Subjects list
  const { data: subjectsData } = useQuery({
    queryKey: ["teacherSubjectsListForReports"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const subjects = subjectsData?.data || [];
  const subjectsMap = useMemo(() => {
    const map = new Map<string, string>();
    subjects.forEach((s) => map.set(s.subjectId, s.subjectName));
    return map;
  }, [subjects]);

  // One complete authorized snapshot: server grades, targets and roster from the same read.
  // Pagination below is only for rendering; exports use all rows, never the first page.
  const { data: reportData, isLoading: isStudentsLoading, isError: isStudentsError,
    error: studentsError, refetch: refetchStudents } = useQuery({
    queryKey: ["class-academic-report", actor?.centerId, actor?.userId, selectedClassId],
    queryFn: async () => (await httpClient.get<{ data: ClassAcademicReport }>(
      `/classes/${selectedClassId}/academic-report`)).data.data,
    enabled: Boolean(selectedClassId), refetchOnWindowFocus: true,
  });
  const studentsList = useMemo(() => reportData?.students.map(r => r.student) ?? [], [reportData]);
  const studentSummaries = useMemo(() => new Map(reportData?.students.map(r =>
    [r.student.studentId, r.summary] as const) ?? []), [reportData]);
  const periodRows = useMemo(() => reports ? reportData?.students.map(row => ({
    ...row, records: filterReportPeriod(row.summary.records, periodFrom, periodTo),
  })) ?? [] : [], [reports, reportData, periodFrom, periodTo]);
  const regularOptions = [...new Map(periodRows.flatMap(row => row.records).filter(record => record.kind === "Regular")
    .map(record => [record.assignmentId, record])).values()];
  const regularIds = new Set(regularOptions.filter(record => regularSelection?.has(record.assignmentId) ??
    regularOptions.length <= maxRegularGrades).map(record => record.assignmentId));
  const selectedReportCount = studentsList.filter(student => selectedReportStudentIds.has(student.studentId)).length;
  const allReportStudentsSelected = studentsList.length > 0 && selectedReportCount === studentsList.length;
  const reportReady = !!reportData && !isStudentsError && !isStudentsLoading;
  const [studentPage, setStudentPage] = useState(1);
  useEffect(() => setStudentPage(1), [selectedClassId, searchQuery, selectedGrade, statusTab]);

  // 7. Student Detail Query (When drawer is opened)
  const { data: studentDetailData, isLoading: isDetailLoading } = useQuery({
    queryKey: ["studentDetailDataForTeacher", selectedStudent?.studentId],
    queryFn: () => (selectedStudent ? organizationApi.getStudent(selectedStudent.studentId) : null),
    enabled: Boolean(selectedStudent),
  });

  const studentSubjectGoals: StudentSubjectGoalDto[] = studentDetailData?.subjectGoals || [];

  // 8. Student Digital Twin Query (When Digital Twin tab is selected)
  const activeSubjectId = selectedSubjectId || selectedClass?.subject?.subjectId || (subjects.length > 0 ? subjects[0].subjectId : "");
  const { data: studentTwinData, isLoading: isTwinLoading } = useQuery<StudentTwinDataDto | null>({
    queryKey: ["studentTwinForTeacherStudentView", selectedStudent?.studentId, activeSubjectId],
    queryFn: () =>
      selectedStudent && activeSubjectId
        ? getTeacherStudentTwin(selectedStudent.studentId, activeSubjectId)
        : null,
    enabled: Boolean(selectedStudent && activeSubjectId && activeTab === "twin"),
  });

  // Handle URL route param studentId
  useEffect(() => {
    if (routeStudentId && studentsList.length > 0) {
      const found = studentsList.find((s) => s.studentId === routeStudentId);
      if (found) {
        setSelectedStudent(found);
      }
    }
  }, [routeStudentId, studentsList]);

  // Filtered Students List
  const filteredStudents = useMemo(() => {
    return studentsList.filter((student) => {
      // Search
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const matchName = student.fullName.toLowerCase().includes(q);
        const matchUsername = student.username.toLowerCase().includes(q);
        const matchId = student.studentId.toLowerCase().includes(q);
        if (!matchName && !matchUsername && !matchId) return false;
      }

      // Grade
      if (selectedGrade && student.gradeLevel !== Number(selectedGrade)) {
        return false;
      }

      // Status tab
      const summary = studentSummaries.get(student.studentId);
      if (statusTab === "high_risk") {
        return isAcademicHighRisk(summary);
      }

      if (statusTab === "good") {
        return summary?.assessmentStatus === "Good";
      }

      if (statusTab === "needs_attention") {
        const hasOverdue = summary && summary.overdueCount > 0;
        return hasOverdue;
      }

      return true;
    });
  }, [studentsList, searchQuery, selectedGrade, statusTab, studentSummaries]);

  // Summary Metrics
  const classMetrics = useMemo(() => {
    const total = studentsList.length;
    if (total === 0) return { total: 0, avgComp: 0, avgScore: null, highRisk: 0 };

    let totalComp = 0;
    let totalScore = 0;
    let scoredCount = 0;
    let highRiskCount = 0;

    studentsList.forEach((s) => {
      const sum = studentSummaries.get(s.studentId);
      if (sum) {
        totalComp += sum.completionRate;
        if (sum.averageScore !== null) {
          totalScore += sum.averageScore;
          scoredCount++;
        }
        if (isAcademicHighRisk(sum)) {
          highRiskCount++;
        }
      }
    });

    return {
      total,
      avgComp: Math.round(totalComp / total),
      avgScore: scoredCount > 0 ? (totalScore / scoredCount).toFixed(1) : "-",
      highRisk: highRiskCount,
    };
  }, [studentsList, studentSummaries]);

  // Selected Student Academic Summary
  const currentStudentSummary = selectedStudent
    ? studentSummaries.get(selectedStudent.studentId)
    : null;

  // CSV & Excel Export Handlers
  const handleExportIndividualExcel = async (student: StudentDto) => {
    if (isExportingExcel || !reportReady) return;
    setIsExportingExcel(true);
    setExportError(null);
    try {
      const summary = studentSummaries.get(student.studentId) || {
        totalAssigned: 0,
        completedCount: 0,
        inProgressCount: 0,
        notStartedCount: 0,
        overdueCount: 0,
        completionRate: 0,
        averageScore: null,
        minScore: null,
        maxScore: null,
        records: [],
      };
      const detail = await organizationApi.getStudent(student.studentId);
      await exportStudentReportXlsx(
        student,
        selectedClass,
        detail.subjectGoals ?? [],
        subjectsMap,
        summary,
        teacherNotes[student.studentId]
      );
    } catch (err) {
      console.error("Failed to export student excel report:", err);
      setExportError("Không thể xuất file Excel học viên. Vui lòng kiểm tra mạng và thử lại.");
    } finally {
      setIsExportingExcel(false);
    }
  };

  const handleExportIndividualCsv = async (student: StudentDto) => {
    if (!reportReady || isExportingExcel) return;
    const summary = studentSummaries.get(student.studentId) || {
      totalAssigned: 0,
      completedCount: 0,
      inProgressCount: 0,
      notStartedCount: 0,
      overdueCount: 0,
      completionRate: 0,
      averageScore: null,
      minScore: null,
      maxScore: null,
      records: [],
    };
    setExportError(null);
    try {
      const detail = await organizationApi.getStudent(student.studentId);
      const rows = buildStudentReportCsv(student, selectedClass, detail.subjectGoals ?? [], subjectsMap, summary);
      const filename = `Bao_cao_hoc_vien_${student.username}_${new Date().toISOString().slice(0, 10)}.csv`;
      downloadCsv(filename, rows);
    } catch {
      setExportError("Không thể xuất CSV học sinh. Vui lòng thử lại.");
    }
  };

  const handleExportClassExcel = async (selectedOnly = false) => {
    if (!selectedClass || isExportingExcel || !reportReady || (selectedOnly && selectedReportCount === 0)) return;
    setIsExportingExcel(true);
    setExportError(null);
    try {
      await exportClassReportXlsx(selectedClass, studentsList, studentSummaries, {
        from: periodFrom,
        to: periodTo,
        selectedRegularIds: regularIds,
        selectedStudentIds: selectedOnly ? selectedReportStudentIds : undefined,
      });
    } catch (err) {
      console.error("Failed to export class excel report:", err);
      setExportError("Không thể xuất bảng điểm Excel. Vui lòng thử lại.");
    } finally {
      setIsExportingExcel(false);
    }
  };

  const handleExportClassCsv = () => {
    if (!selectedClass || !reportReady) return;
    const goalsMap = new Map<string, StudentSubjectGoalDto[]>();
    const rows = buildClassReportCsv(selectedClass, studentsList, studentSummaries, goalsMap);
    const filename = `Bang_diem_lop_${selectedClass.className}_${new Date().toISOString().slice(0, 10)}.csv`;
    downloadCsv(filename, rows);
  };

  return (
    <div className="th-page-container">
      {/* 1. Header with Class Switcher & Report Actions */}
      <TeacherPageHeader
        eyebrow="GIẢNG DẠY & ĐÁNH GIÁ NĂNG LỰC"
        title={reports ? "Thống kê và báo cáo" : "Quản lý học sinh"}
        description={reports ? "Xem số liệu theo lớp và mô phỏng điểm môn theo kỳ." : "Theo dõi hồ sơ, bài làm, mục tiêu và ghi chú của học sinh."}
        breadcrumbs={[
          { label: "Giáo viên", href: "/giao-vien/lop-hoc" },
          { label: reports ? "Thống kê và báo cáo" : "Quản lý học sinh" },
        ]}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            {isClassesLoading ? (
              <span className="text-xs text-[var(--th-text-muted)]">Đang tải danh sách lớp...</span>
            ) : (
              <select
                value={selectedClassId}
                onChange={(e) => {
                  setSelectedClassId(e.target.value);
                  setSelectedReportStudentIds(new Set());
                  setRegularSelection(null);
                  setSelectedStudent(null);
                }}
                className="th-select text-xs w-full min-w-0 max-w-full sm:w-auto sm:min-w-[190px]"
                aria-label="Chọn lớp học phụ trách"
              >
                {classes.map((c) => (
                  <option key={c.classId} value={c.classId}>
                    {c.className} ({c.academicYear})
                  </option>
                ))}
              </select>
            )}

            {reports && <>
            <button
              type="button"
              onClick={() => void handleExportClassExcel()}
              disabled={!reportReady || studentsList.length === 0 || isExportingExcel}
              className={`th-primary-button text-xs py-2 px-3 flex items-center gap-1.5 ${
                isExportingExcel ? "opacity-60 cursor-wait" : ""
              }`}
              title="Xuất bảng điểm và tiến độ toàn bộ học sinh trong lớp ra file Microsoft Excel (.xlsx) với các cột riêng biệt"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="7 10 12 15 17 10" />
                <line x1="12" y1="15" x2="12" y2="3" />
              </svg>
              <span>{isExportingExcel ? "Đang xuất Excel..." : "Xuất Excel Cả Lớp (.xlsx)"}</span>
            </button>

            <button
              type="button"
              onClick={handleExportClassCsv}
              disabled={!reportReady || studentsList.length === 0}
              className="th-secondary-button text-xs py-2 px-2.5 flex items-center gap-1"
              title="Xuất bảng điểm lớp định dạng CSV (.csv)"
            >
              <span>📥</span>
              <span>CSV</span>
            </button>

            <button
              type="button"
              onClick={() => setIsClassPrintModalOpen(true)}
              disabled={!reportReady || studentsList.length === 0}
              className="th-secondary-button text-xs py-2 px-3 flex items-center gap-1.5"
              title="Xem và in báo cáo tổng kết lớp học dạng phiếu chuẩn A4"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <polyline points="6 9 6 2 18 2 18 9" />
                <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
                <rect x="6" y="14" width="12" height="8" />
              </svg>
              <span>In Báo Cáo Lớp</span>
            </button>
            </>}
          </div>
        }
      />

      {exportError && (
        <div className="rounded-xl border border-rose-300 bg-rose-50 p-3.5 text-xs text-rose-800 dark:border-rose-800 dark:bg-rose-950/40 dark:text-rose-200 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span>⚠</span>
            <span>{exportError}</span>
          </div>
          <button
            type="button"
            onClick={() => setExportError(null)}
            className="font-bold underline text-rose-900 dark:text-rose-100 hover:text-rose-700 ml-4 cursor-pointer"
          >
            Đóng
          </button>
        </div>
      )}

      {/* 2. Overview Metric Cards */}
      {reports && reportReady && <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <TeacherMetricCard
          label="Sĩ số lớp"
          value={classMetrics.total}
          unit="học sinh"
          supportingText={selectedClass ? `Lớp ${selectedClass.className}` : "Đang chọn"}
          icon="👨‍🎓"
        />
        <TeacherMetricCard
          label="Tỷ lệ nộp bài tập"
          value={`${classMetrics.avgComp}%`}
          supportingText={`Tổng ${reportData?.totalAssignments ?? 0} bài tập đã giao`}
          icon="📑"
          trend={{
            label: !reportData?.totalAssignments ? "Chưa giao bài" : "Tiến độ nộp, không phải điểm",
            tone: "neutral",
          }}
        />
        <TeacherMetricCard
          label="Điểm trung bình lớp"
          value={classMetrics.avgScore}
          unit="/ 10.0"
          supportingText="Chỉ tính điểm bài đã được giáo viên chốt"
          icon="🎯"
          trend={{
            label: classMetrics.avgScore === "-" || classMetrics.avgScore == null ? "Chưa có điểm đã chốt" : "Chỉ tính bài đã chốt",
            tone: Number(classMetrics.avgScore) >= 7.0 ? "positive" : "neutral",
          }}
        />
        <TeacherMetricCard
          label="Học sinh nguy cơ cao"
          value={classMetrics.highRisk}
          unit="học sinh"
          supportingText="Điểm đã chốt < 5.0 hoặc có bài quá hạn"
          icon="⚠️"
          trend={{
            label: classMetrics.highRisk === 0 ? "Chưa phát hiện từ dữ liệu đã đánh giá" : "Cần hỗ trợ sớm",
            tone: classMetrics.highRisk === 0 ? "neutral" : "negative",
          }}
        />
      </div>}

      {reports && <>
        {isStudentsLoading && selectedClassId && <TeacherSkeleton className="h-48 w-full" />}
        {isStudentsError && <TeacherSafeErrorPanel error={studentsError} onRetry={() => void refetchStudents()} />}
        {!selectedClassId && !isClassesLoading && <p>Chưa có lớp trong phạm vi báo cáo.</p>}
        {reportReady && <section className="th-surface space-y-4 p-4" aria-labelledby="simulated-grade-title">
          <h2 id="simulated-grade-title" className="text-lg font-semibold">Mô phỏng điểm môn</h2>
          <p className="text-sm text-[var(--th-text-muted)]">Mỗi bài thường xuyên được chọn có hệ số 1, giữa kỳ hệ số 2 và cuối kỳ hệ số 3. Hãy chọn kỳ có đúng một bài giữa kỳ và một bài cuối kỳ. Giỏi từ 8, Khá từ 6,5 và Trung bình từ 5 là các mức mô phỏng theo điểm môn.</p>
          <div className="flex flex-wrap gap-3">
            <label className="grid gap-1 text-sm">Từ ngày<input type="date" className="th-input" value={periodFrom} onChange={e => { setPeriodFrom(e.target.value); setRegularSelection(null); }} /></label>
            <label className="grid gap-1 text-sm">Đến ngày<input type="date" className="th-input" value={periodTo} onChange={e => { setPeriodTo(e.target.value); setRegularSelection(null); }} /></label>
          </div>
          {!!regularOptions.length && <details open={regularOptions.length > maxRegularGrades}>
            <summary className="cursor-pointer text-sm font-medium">Bài thường xuyên tính điểm ({regularIds.size}/{regularOptions.length} đã chọn)</summary>
            <p className="my-2 text-xs text-[var(--th-text-muted)]">Chọn tối đa 4 bài kiểm tra nhỏ. Nếu có nhiều bài, hãy chọn các bài dự kiến lấy điểm cho cả lớp.</p>
            <div className="grid max-h-48 gap-2 overflow-y-auto sm:grid-cols-2">
              {regularOptions.map(record => <label key={record.assignmentId} className="flex gap-2 text-sm">
                <input type="checkbox" checked={regularIds.has(record.assignmentId)} disabled={!regularIds.has(record.assignmentId) && regularIds.size >= maxRegularGrades}
                  onChange={event => {
                    const selected = new Set(regularIds);
                    if (event.target.checked) selected.add(record.assignmentId); else selected.delete(record.assignmentId);
                    setRegularSelection(selected);
                  }} />
                {record.title}
              </label>)}
            </div>
          </details>}
          {periodFrom && periodTo && periodFrom > periodTo && <p role="status">Ngày kết thúc phải từ ngày bắt đầu trở đi.</p>}
          {regularOptions.length > maxRegularGrades && !regularIds.size && <p role="status">Chọn các bài thường xuyên để tính điểm mô phỏng.</p>}
          <div className="flex flex-wrap items-center gap-3">
            <button type="button" className="th-primary-button px-3 py-2 text-sm"
              disabled={selectedReportCount === 0 || isExportingExcel || Boolean(periodTo && periodFrom > periodTo)}
              onClick={() => void handleExportClassExcel(true)}>
              Xuất Excel học sinh đã chọn ({selectedReportCount})
            </button>
          </div>
          <div className="overflow-x-auto"><table className="w-full text-left text-sm">
              <thead><tr>
                <th scope="col" className="p-3">
                  <input type="checkbox" aria-label="Chọn tất cả học sinh trong bảng điểm"
                    checked={allReportStudentsSelected} disabled={studentsList.length === 0}
                    ref={input => { if (input) input.indeterminate = selectedReportCount > 0 && !allReportStudentsSelected; }}
                    onChange={event => setSelectedReportStudentIds(event.target.checked
                      ? new Set(studentsList.map(student => student.studentId)) : new Set())} />
                </th>
                {["Học sinh", "Thường xuyên", "Giữa kỳ", "Cuối kỳ", "Điểm mô phỏng", "Xếp mức", "Báo cáo cá nhân"].map(label => <th key={label} scope="col" className="p-3 whitespace-nowrap">{label}</th>)}
              </tr></thead>
              <tbody>{periodRows.map(({ student, records }) => {
                const result = simulateSubjectGrade(records, regularIds);
                return <tr key={student.studentId} className="border-t">
                  <td className="p-3">
                    <input type="checkbox" aria-label={`Chọn ${student.fullName} (@${student.username})`}
                      checked={selectedReportStudentIds.has(student.studentId)}
                      onChange={event => {
                        const checked = event.target.checked;
                        setSelectedReportStudentIds(current => {
                          const next = new Set(current);
                          if (checked) next.add(student.studentId);
                          else next.delete(student.studentId);
                          return next;
                        });
                      }} />
                  </td>
                  <th scope="row" className="p-3 text-left font-medium">{student.fullName}</th>
                  {result.byGroup.map((group, index) => <td key={index} className="whitespace-nowrap p-3">{group.average?.toFixed(1) ?? "—"} <span className="text-xs text-[var(--th-text-muted)]">({group.graded}/{group.total} đã chốt)</span></td>)}
                  <td className="p-3">{result.score?.toFixed(1) ?? "—"}</td><td className="p-3">{result.label}</td>
                  <td className="p-3"><div className="flex flex-wrap gap-2">
                    <button type="button" className="th-secondary-button px-2 py-1 text-xs" disabled={isExportingExcel}
                      onClick={() => void handleExportIndividualExcel(student)} aria-label={`Xuất Excel cá nhân cho ${student.fullName}`}>Excel</button>
                    <button type="button" className="th-secondary-button px-2 py-1 text-xs" disabled={isExportingExcel}
                      onClick={() => void handleExportIndividualCsv(student)} aria-label={`Xuất CSV cá nhân cho ${student.fullName}`}>CSV</button>
                    <button type="button" className="th-secondary-button px-2 py-1 text-xs"
                      onClick={() => { setSelectedStudent(student); setIsStudentPrintModalOpen(true); }}
                      aria-label={`In phiếu đánh giá cho ${student.fullName}`}>In</button>
                  </div></td>
                </tr>;
              })}</tbody>
            </table>{!studentsList.length && <p className="p-3">Chưa có học sinh trong phạm vi báo cáo.</p>}</div>
        </section>}
      </>}
      {!reports && <>
      {/* 3. Filter Bar & Quick Status Tabs */}
      <div className="th-surface p-4 space-y-3">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
          {/* Search box */}
          <div className="flex-1 max-w-md relative">
            <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-slate-400">
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
            </div>
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Tìm kiếm học sinh theo họ tên, tên đăng nhập..."
              className="th-input w-full text-sm py-2 !pl-10 pr-4 placeholder:text-slate-400"
            />
          </div>

          {/* Grade filter */}
          <div className="flex items-center gap-2">
            <span className="text-xs text-[var(--th-text-muted)] font-semibold">Khối:</span>
            <select
              value={selectedGrade}
              onChange={(e) => setSelectedGrade(e.target.value)}
              className="th-select text-sm py-1.5 px-3"
            >
              <option value="">Tất cả khối</option>
              <option value="10">Khối 10</option>
              <option value="11">Khối 11</option>
              <option value="12">Khối 12</option>
            </select>
          </div>
        </div>

        {/* Status Tab Buttons */}
        <div className="flex flex-wrap items-center gap-2 border-t border-[var(--th-border-subtle)] pt-3">
          <button
            type="button"
            onClick={() => setStatusTab("all")}
            className={`text-xs font-bold px-3 py-1.5 rounded-lg transition-colors ${
              statusTab === "all"
                ? "bg-[var(--th-teal)] text-white shadow-sm"
                : "bg-[var(--th-surface-muted)] text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
            }`}
          >
            Tất cả học sinh ({studentsList.length})
          </button>
          <button
            type="button"
            onClick={() => setStatusTab("high_risk")}
            className={`text-xs font-bold px-3 py-1.5 rounded-lg transition-colors flex items-center gap-1 ${
              statusTab === "high_risk"
                ? "bg-rose-600 text-white shadow-sm"
                : "bg-[var(--th-surface-muted)] text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
            }`}
          >
            <span>⚠️</span>
            <span>Nguy cơ tụt hậu ({classMetrics.highRisk})</span>
          </button>
          <button
            type="button"
            onClick={() => setStatusTab("good")}
            className={`text-xs font-bold px-3 py-1.5 rounded-lg transition-colors flex items-center gap-1 ${
              statusTab === "good"
                ? "bg-emerald-600 text-white shadow-sm"
                : "bg-[var(--th-surface-muted)] text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
            }`}
          >
            <span>🌟</span>
            <span>Hoàn thành tốt</span>
          </button>
          <button
            type="button"
            onClick={() => setStatusTab("needs_attention")}
            className={`text-xs font-bold px-3 py-1.5 rounded-lg transition-colors flex items-center gap-1 ${
              statusTab === "needs_attention"
                ? "bg-amber-600 text-white shadow-sm"
                : "bg-[var(--th-surface-muted)] text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
            }`}
          >
            <span>⏳</span>
            <span>Có bài quá hạn</span>
          </button>
        </div>
      </div>

      {/* 4. Students Table */}
      {isStudentsLoading ? (
        <div className="space-y-3">
          <TeacherSkeleton className="h-16 rounded-xl" />
          <TeacherSkeleton className="h-16 rounded-xl" />
          <TeacherSkeleton className="h-16 rounded-xl" />
        </div>
      ) : isStudentsError ? (
        <TeacherSafeErrorPanel
          error={studentsError}
          fallback="Không thể tải danh sách học sinh của lớp học."
          onRetry={() => refetchStudents()}
        />
      ) : (
        <div className="th-surface overflow-x-auto">
          <table className="w-full text-left text-xs border-collapse">
            <thead>
              <tr className="border-b border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)] text-[var(--th-text-muted)] font-semibold uppercase tracking-wider">
                <th className="px-5 py-3.5">Học sinh</th>
                <th className="px-5 py-3.5">Khối</th>
                <th className="px-5 py-3.5">Tiến độ bài tập</th>
                <th className="px-5 py-3.5">% Hoàn thành</th>
                <th className="px-5 py-3.5">Điểm TB</th>
                <th className="px-5 py-3.5">Đánh giá</th>
                <th className="px-5 py-3.5 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--th-border-subtle)]">
              {filteredStudents.length === 0 ? (
                <tr>
                  <td colSpan={7} className="p-10 text-center text-xs text-[var(--th-text-muted)]">
                    Không tìm thấy học sinh nào phù hợp với bộ lọc hiện tại.
                  </td>
                </tr>
              ) : (
                filteredStudents.slice((studentPage-1)*25, studentPage*25).map((student) => {
                  const summary = studentSummaries.get(student.studentId);
                  const completion = summary ? summary.completionRate : 0;
                  const avgScore = summary?.averageScore;
                  const isHighRisk = isAcademicHighRisk(summary);

                  return (
                    <tr
                      key={student.studentId}
                      className="hover:bg-[var(--th-surface-subtle)] transition-colors group cursor-pointer"
                      onClick={() => setSelectedStudent(student)}
                    >
                      {/* Name & Username (Read-Only) */}
                      <td className="px-5 py-4">
                        <div className="flex items-center gap-3">
                          <div className="h-8 w-8 rounded-full bg-teal-500/10 dark:bg-teal-500/20 border border-teal-500/25 flex items-center justify-center font-bold text-xs text-teal-700 dark:text-teal-300 shrink-0">
                            {student.fullName.slice(0, 2).toUpperCase()}
                          </div>
                          <div>
                            <p className="font-bold text-sm text-[var(--th-text)] group-hover:text-[var(--th-teal)] transition-colors">
                              {student.fullName}
                            </p>
                            <p className="text-[11px] text-[var(--th-text-muted)] font-mono">
                              @{student.username}
                            </p>
                          </div>
                        </div>
                      </td>

                      {/* Grade Level */}
                      <td className="px-5 py-4 text-[var(--th-text-secondary)] font-semibold">
                        Khối {student.gradeLevel}
                      </td>

                      {/* Completed / Total */}
                      <td className="px-5 py-4 text-[var(--th-text-secondary)]">
                        <span className="font-semibold text-[var(--th-text)]">
                          {summary?.completedCount || 0}
                        </span>{" "}
                        / {summary?.totalAssigned || 0} bài đã nộp
                        {summary && summary.overdueCount > 0 && (
                          <span className="ml-1 text-[11px] text-rose-600 font-bold">
                            ({summary.overdueCount} quá hạn)
                          </span>
                        )}
                      </td>

                      {/* % Completion with Visual Bar */}
                      <td className="px-5 py-4">
                        <div className="flex items-center gap-2">
                          <div className="w-16 h-2 rounded-full bg-[var(--th-surface-muted)] overflow-hidden">
                            <div
                              className={`h-full rounded-full ${
                                completion >= 80 ? "bg-emerald-500" : completion >= 50 ? "bg-sky-500" : "bg-rose-500"
                              }`}
                              style={{ width: `${Math.min(100, completion)}%` }}
                            />
                          </div>
                          <span className="font-mono font-bold text-xs text-[var(--th-text)]">
                            {completion.toFixed(0)}%
                          </span>
                        </div>
                      </td>

                      {/* Average Score */}
                      <td className="px-5 py-4">
                        {avgScore !== null && avgScore !== undefined ? (
                          <span
                            className={`font-mono font-black text-sm ${
                              avgScore >= 8.0
                                ? "text-emerald-600 dark:text-emerald-400"
                                : avgScore >= 6.5
                                ? "text-sky-600 dark:text-sky-400"
                                : avgScore >= 5.0
                                ? "text-amber-600 dark:text-amber-400"
                                : "text-rose-600 dark:text-rose-400"
                            }`}
                          >
                            {avgScore.toFixed(1)}
                          </span>
                        ) : (
                          <span className="text-stone-400 text-xs">-</span>
                        )}
                      </td>

                      {/* Risk Badge */}
                      <td className="px-5 py-4">
                        {isHighRisk ? (
                          <TeacherStatusBadge status="failed" label="Nguy cơ cao" tone="danger" />
                        ) : completion >= 80 ? (
                          <TeacherStatusBadge status="completed" label={assessmentLabel(summary)} tone="success" />
                        ) : (
                          <TeacherStatusBadge status="pending" label={assessmentLabel(summary)} tone="info" />
                        )}
                      </td>

                      {/* Actions */}
                      <td
                        className="px-5 py-4 text-right"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <div className="flex items-center justify-end gap-1.5">
                          <button
                            type="button"
                            onClick={() => setSelectedStudent(student)}
                            className="th-secondary-button text-xs py-1 px-2.5 rounded-lg"
                            title="Xem chi tiết hồ sơ học tập và bảng điểm"
                          >
                            Hồ sơ →
                          </button>
                          <Link
                            to={`/giao-vien/hoc-sinh/${student.studentId}/twin?subjectId=${selectedClass?.subject?.subjectId || ""}`}
                            className="th-secondary-button text-xs py-1 px-2.5 rounded-lg text-slate-700 dark:text-slate-200 hover:text-teal-600"
                            title="Xem bản sao số năng lực (Digital Twin)"
                          >
                            Twin
                          </Link>
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      )}

      {filteredStudents.length > 25 && <nav className="flex gap-4 justify-end mt-3" aria-label="Phân trang học sinh">
        <button className="th-secondary-button" disabled={studentPage <= 1} onClick={() => setStudentPage(studentPage-1)}>Trước</button>
        <span>Trang {studentPage}/{Math.ceil(filteredStudents.length/25)} · {filteredStudents.length} học sinh</span>
        <button className="th-secondary-button" disabled={studentPage*25 >= filteredStudents.length} onClick={() => setStudentPage(studentPage+1)}>Sau</button>
      </nav>}
      {selectedClassId && <div className="mt-4"><ClassHistoryPanel key={selectedClassId} classId={selectedClassId} /></div>}

      {/* 5. Comprehensive Individual Student Modal (Centered & Responsive) */}
      <TeacherModal
        isOpen={Boolean(selectedStudent)}
        onClose={() => setSelectedStudent(null)}
        title={selectedStudent ? selectedStudent.fullName : "Hồ sơ học sinh"}
        description={selectedStudent ? `Tên đăng nhập: @${selectedStudent.username} · Khối ${selectedStudent.gradeLevel}` : ""}
        size="lg"
        footer={
          selectedStudent && (
            <div className="flex w-full justify-end">
              <button
                type="button"
                onClick={() => setSelectedStudent(null)}
                className="th-secondary-button text-xs py-1.5 px-3"
              >
                Đóng
              </button>
            </div>
          )
        }
      >
        {selectedStudent && (
          <div className="space-y-6">
            {/* Read-only Student Profile Card */}
            <div className="rounded-xl border border-[var(--th-border)] bg-[var(--th-surface-muted)] p-4">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="h-12 w-12 rounded-full bg-[var(--th-surface)] border-2 border-[var(--th-terracotta)] flex items-center justify-center font-black text-base text-[var(--th-text)]">
                    {selectedStudent.fullName.slice(0, 2).toUpperCase()}
                  </div>
                  <div>
                    <h3 className="font-bold text-base text-[var(--th-text)]">{selectedStudent.fullName}</h3>
                    <p className="text-xs text-[var(--th-text-muted)] font-mono">
                      Mã tài khoản: {selectedStudent.studentId.slice(0, 12)}...
                    </p>
                  </div>
                </div>
                <div className="text-right">
                  <TeacherStatusBadge status="active" label="Tài khoản hoạt động" tone="success" />
                  <p className="mt-1 text-[11px] text-[var(--th-text-muted)]">
                    Chế độ: Chỉ đọc học vụ
                  </p>
                </div>
              </div>

              {/* Security info note */}
              <div className="mt-3 pt-3 border-t border-[var(--th-border-subtle)] flex items-center justify-between text-[11px] text-[var(--th-text-muted)]">
                <span>🔒 Mật khẩu & thông tin tài khoản được quản lý bởi Quản trị viên Trung tâm.</span>
                <span className="font-semibold text-[var(--th-teal)]">Khối {selectedStudent.gradeLevel}</span>
              </div>
            </div>

            {/* Navigation Tabs */}
            <div className="flex border-b border-[var(--th-border)] text-xs font-bold">
              <button
                type="button"
                onClick={() => setActiveTab("assignments")}
                className={`py-2.5 px-3 border-b-2 transition-colors ${
                  activeTab === "assignments"
                    ? "border-[var(--th-teal)] text-[var(--th-teal)]"
                    : "border-transparent text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
                }`}
              >
                📝 Bài tập & Bảng điểm
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("goals")}
                className={`py-2.5 px-3 border-b-2 transition-colors ${
                  activeTab === "goals"
                    ? "border-[var(--th-teal)] text-[var(--th-teal)]"
                    : "border-transparent text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
                }`}
              >
                🎯 Mục tiêu học tập ({studentSubjectGoals.length})
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("twin")}
                className={`py-2.5 px-3 border-b-2 transition-colors ${
                  activeTab === "twin"
                    ? "border-[var(--th-teal)] text-[var(--th-teal)]"
                    : "border-transparent text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
                }`}
              >
                🧠 Bản sao số (Twin)
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("notes")}
                className={`py-2.5 px-3 border-b-2 transition-colors ${
                  activeTab === "notes"
                    ? "border-[var(--th-teal)] text-[var(--th-teal)]"
                    : "border-transparent text-[var(--th-text-secondary)] hover:text-[var(--th-text)]"
                }`}
              >
                📝 Sổ tay sư phạm
              </button>
            </div>

            {/* TAB 1: BÀI TẬP VÀ BẢNG ĐIỂM */}
            {activeTab === "assignments" && (
              <div className="space-y-4">
                {/* Academic Quick Stats */}
                <div className="grid grid-cols-3 gap-3">
                  <div className="p-3 rounded-xl bg-[var(--th-surface-muted)] border border-[var(--th-border-subtle)] text-center">
                    <p className="text-[11px] font-bold text-[var(--th-text-muted)] uppercase">Tỷ lệ hoàn thành</p>
                    <p className="text-xl font-black text-[var(--th-teal)] mt-1">
                      {currentStudentSummary?.completionRate.toFixed(0)}%
                    </p>
                    <p className="text-[10px] text-[var(--th-text-secondary)] mt-0.5">
                      {currentStudentSummary?.completedCount} / {currentStudentSummary?.totalAssigned} bài đã nộp
                    </p>
                  </div>
                  <div className="p-3 rounded-xl bg-[var(--th-surface-muted)] border border-[var(--th-border-subtle)] text-center">
                    <p className="text-[11px] font-bold text-[var(--th-text-muted)] uppercase">Điểm trung bình</p>
                    <p className="text-xl font-black text-[var(--th-terracotta)] mt-1">
                      {currentStudentSummary?.averageScore !== null && currentStudentSummary?.averageScore !== undefined
                        ? currentStudentSummary.averageScore.toFixed(1)
                        : "-"}
                    </p>
                    <p className="text-[10px] text-[var(--th-text-secondary)] mt-0.5">Thang điểm 10.0</p>
                  </div>
                  <div className="p-3 rounded-xl bg-[var(--th-surface-muted)] border border-[var(--th-border-subtle)] text-center">
                    <p className="text-[11px] font-bold text-[var(--th-text-muted)] uppercase">Chưa làm / Quá hạn</p>
                    <p className="text-xl font-black text-rose-600 mt-1">
                      {(currentStudentSummary?.notStartedCount || 0) + (currentStudentSummary?.overdueCount || 0)}
                    </p>
                    <p className="text-[10px] text-[var(--th-text-secondary)] mt-0.5">Cần nhắc nhở</p>
                  </div>
                </div>

                {/* Assignment List */}
                <div className="space-y-2">
                  <div className="flex items-center justify-between pb-1">
                    <h4 className="font-bold text-xs text-[var(--th-text)] uppercase tracking-wider">
                      Danh sách bài tập chi tiết ({currentStudentSummary?.records.length || 0})
                    </h4>
                  </div>

                  {!currentStudentSummary || currentStudentSummary.records.length === 0 ? (
                    <div className="p-6 text-center text-xs text-[var(--th-text-muted)] border border-dashed border-[var(--th-border)] rounded-xl">
                      Chưa có bài tập nào được giao cho học sinh trong lớp này.
                    </div>
                  ) : (
                    <div className="space-y-2">
                      {currentStudentSummary.records.map((rec) => (
                        <div
                          key={rec.assignmentId}
                          className="p-3.5 rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface)] hover:border-[var(--th-teal)] transition-colors flex flex-col sm:flex-row sm:items-center justify-between gap-3"
                        >
                          <div className="min-w-0 flex-1">
                            <div className="flex items-center gap-2">
                              <h5 className="font-bold text-sm text-[var(--th-text)] truncate">{rec.title}</h5>
                              {rec.status === "Completed" && (
                                <span className="th-badge th-badge-success text-[10px] shrink-0">Đã nộp</span>
                              )}
                              {rec.status === "InProgress" && (
                                <span className="th-badge th-badge-info text-[10px] shrink-0">Đang làm</span>
                              )}
                              {rec.status === "Overdue" && (
                                <span className="th-badge th-badge-danger text-[10px] shrink-0">Quá hạn</span>
                              )}
                              {rec.status === "NotStarted" && (
                                <span className="th-badge th-badge-neutral text-[10px] shrink-0">Chưa làm</span>
                              )}
                            </div>
                            <p className="mt-1 text-[11px] text-[var(--th-text-secondary)]">
                              Hạn nộp: {rec.dueAt ? new Date(rec.dueAt).toLocaleDateString("vi-VN") : "Không giới hạn"} · Tiến độ: {rec.completedQuestionCount}/{rec.totalQuestionCount} câu
                            </p>
                          </div>

                          <div className="flex items-center justify-between sm:justify-end gap-3 shrink-0">
                            {rec.score !== null && rec.score !== undefined ? (
                              <div className="text-right">
                                <span className="font-mono font-black text-sm text-[var(--th-teal)]">
                                  {reportScoreText(rec)}
                                </span>
                              </div>
                            ) : (
                              <span className="text-xs text-[var(--th-text-muted)] italic">Chưa có điểm</span>
                            )}
                            <Link
                              to={`/giao-vien/bai-tap/${rec.assignmentId}/tien-do`}
                              className="th-secondary-button text-[11px] py-1 px-2"
                            >
                              Tiến độ lớp →
                            </Link>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* TAB 2: MỤC TIÊU HỌC TẬP */}
            {activeTab === "goals" && (
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <h4 className="font-bold text-xs text-[var(--th-text)] uppercase tracking-wider">
                    Mục tiêu điểm thi & Điểm dự báo năng lực
                  </h4>
                </div>

                {isDetailLoading ? (
                  <TeacherSkeleton className="h-32 rounded-xl" />
                ) : studentSubjectGoals.length === 0 ? (
                  <div className="p-8 text-center text-xs text-[var(--th-text-muted)] border border-dashed border-[var(--th-border)] rounded-xl">
                    Học sinh chưa thiết lập mục tiêu môn học nào.
                  </div>
                ) : (
                  <div className="space-y-3">
                    {studentSubjectGoals.map((g) => {
                      const subjectName = subjectsMap.get(g.subjectId) || "Môn học";
                      const gap = (g.targetScore - g.currentPredictedScore).toFixed(1);
                      const isHighRisk = g.riskScore > 0.6;

                      return (
                        <div
                          key={g.goalId}
                          className="p-4 rounded-xl border border-[var(--th-border)] bg-[var(--th-surface)] space-y-3"
                        >
                          <div className="flex items-center justify-between">
                            <div>
                              <h5 className="font-bold text-sm text-[var(--th-text)]">{subjectName}</h5>
                              <p className="text-xs text-[var(--th-text-muted)]">
                                Còn {g.remainingDays} ngày đếm ngược đến kỳ thi
                              </p>
                            </div>
                            <span
                              className={`th-badge ${
                                isHighRisk ? "th-badge-danger" : g.riskScore > 0.3 ? "th-badge-warning" : "th-badge-success"
                              }`}
                            >
                              {isHighRisk ? "Nguy cơ cao" : g.riskScore > 0.3 ? "Cần theo dõi" : "Tiến độ an toàn"}
                            </span>
                          </div>

                          <div className="grid grid-cols-3 gap-2 pt-2 border-t border-[var(--th-border-subtle)] text-center">
                            <div>
                              <p className="text-[10px] text-[var(--th-text-muted)] uppercase font-semibold">Điểm mục tiêu</p>
                              <p className="font-mono font-black text-base text-[var(--th-text)] mt-0.5">
                                {g.targetScore.toFixed(1)}
                              </p>
                            </div>
                            <div>
                              <p className="text-[10px] text-[var(--th-text-muted)] uppercase font-semibold">Dự báo hiện tại (AI)</p>
                              <p className="font-mono font-black text-base text-[var(--th-teal)] mt-0.5">
                                {g.currentPredictedScore.toFixed(1)}
                              </p>
                            </div>
                            <div>
                              <p className="text-[10px] text-[var(--th-text-muted)] uppercase font-semibold">Khoảng cách (Gap)</p>
                              <p className={`font-mono font-black text-base mt-0.5 ${Number(gap) > 0 ? "text-rose-600" : "text-emerald-600"}`}>
                                {Number(gap) > 0 ? `-${gap}` : `+${Math.abs(Number(gap)).toFixed(1)}`}
                              </p>
                            </div>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            )}

            {/* TAB 3: BẢN SAO SỐ DIGITAL TWIN */}
            {activeTab === "twin" && (
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <h4 className="font-bold text-xs text-[var(--th-text)] uppercase tracking-wider">
                    Phổ Năng Lực & Chuyên Đề Kiến Thức
                  </h4>
                  <select
                    value={activeSubjectId}
                    onChange={(e) => setSelectedSubjectId(e.target.value)}
                    className="th-select text-xs py-1"
                  >
                    {subjects.map((sub) => (
                      <option key={sub.subjectId} value={sub.subjectId}>
                        {sub.subjectName}
                      </option>
                    ))}
                  </select>
                </div>

                {isTwinLoading ? (
                  <TeacherSkeleton className="h-48 rounded-xl" />
                ) : !studentTwinData || !studentTwinData.topics || studentTwinData.topics.length === 0 ? (
                  <div className="p-8 text-center text-xs text-[var(--th-text-muted)] border border-dashed border-[var(--th-border)] rounded-xl">
                    {studentTwinData?.scopeMessage || "Chưa có dữ liệu Bản sao số (Digital Twin) cho môn học này."}
                  </div>
                ) : (
                  <div className="space-y-4">
                    {/* Weak topics alert */}
                    {studentTwinData.topics.some((t) => isWeakTopic(t)) && (
                      <div className="p-3.5 rounded-xl bg-rose-500/10 border border-rose-500/30 flex items-start justify-between gap-3">
                        <div>
                          <p className="font-bold text-xs text-rose-700 dark:text-rose-300">
                            Phát hiện lỗ hổng kiến thức (&lt; 60% độ thuần thục)
                          </p>
                          <p className="mt-1 text-[11px] text-rose-600 dark:text-rose-400">
                            Học sinh đang gặp khó khăn ở các chuyên đề:{" "}
                            <strong>
                              {studentTwinData.topics
                                .filter((t) => isWeakTopic(t))
                                .map((t) => t.topicName)
                                .join(", ")}
                            </strong>
                          </p>
                        </div>
                        {canCreateAssignment && (
                          <button
                            type="button"
                            onClick={() => {
                              const weak = studentTwinData.topics.filter((t) => isWeakTopic(t));
                              const weakTopicId = weak.length > 0 ? weak[0].topicNodeId : "";
                              navigate(
                                `/giao-vien/bai-tap/tao-moi?studentIds=${selectedStudent.studentId}&subjectId=${activeSubjectId}${
                                  weakTopicId ? `&topicNodeId=${weakTopicId}` : ""
                                }`
                              );
                            }}
                            className="th-primary-button text-[11px] py-1.5 px-3 shrink-0"
                          >
                            Giao bài bổ trợ →
                          </button>
                        )}
                      </div>
                    )}

                    {/* Topics Mastery List */}
                    <div className="space-y-2.5">
                      {studentTwinData.topics.map((topic) => {
                        const pct = twinPercent(topic.masteryPercentage);
                        return (
                          <div
                            key={topic.topicNodeId}
                            className="p-3 rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface)] space-y-1.5"
                          >
                            <div className="flex items-center justify-between text-xs">
                              <span className="font-semibold text-[var(--th-text)]">{topic.topicName}</span>
                              <span className="font-mono font-bold text-[var(--th-text)]">{topic.evidenceCount > 0 ? `${pct}%` : "Chưa có dữ liệu"}</span>
                            </div>
                            <div className="h-2 rounded-full bg-[var(--th-surface-muted)] overflow-hidden">
                              <div
                                className={`h-full rounded-full ${
                                  topic.evidenceCount <= 0 ? "bg-transparent" : pct >= 80 ? "bg-emerald-500" : pct >= 60 ? "bg-sky-500" : "bg-rose-500"
                                }`}
                                style={{ width: `${pct}%` }}
                              />
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* TAB 4: SỔ TAY GHI CHÚ SƯ PHẠM */}
            {activeTab === "notes" && (
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <h4 className="font-bold text-xs text-[var(--th-text)] uppercase tracking-wider">
                      Sổ Tay Nhận Xét & Kế Hoạch Bồi Dưỡng
                    </h4>
                    <p className="text-[11px] text-[var(--th-text-secondary)] mt-0.5">
                      Ghi chú cá nhân của giáo viên nhằm theo dõi sự tiến bộ hoặc lưu ý phương pháp sư phạm cho học sinh này.
                    </p>
                  </div>
                </div>

                <textarea
                  value={teacherNotes[selectedStudent.studentId] || ""}
                  onChange={(e) => handleSaveNote(selectedStudent.studentId, e.target.value)}
                  placeholder="Nhập ghi chú nhận xét học lực, thái độ học tập, phương pháp kèm cặp hoặc dặn dò cho học sinh này..."
                  rows={6}
                  maxLength={4000}
                  className="th-input w-full text-xs p-3 leading-relaxed"
                />
                {noteSaveFailed && (
                  <p role="alert" className="text-xs text-rose-600">Chưa lưu được ghi chú. Hãy kiểm tra kết nối rồi sửa lại ghi chú để lưu lại.</p>
                )}

                <div className="flex justify-between items-center text-[11px] text-[var(--th-text-muted)]">
                  <span>💾 Ghi chú được tự động lưu trên trình duyệt của bạn.</span>
                  <span className="font-semibold text-emerald-600">Đã lưu tự động</span>
                </div>
              </div>
            )}
          </div>
        )}
      </TeacherModal>
      </>}

      {/* 6. Individual Student Printable Evaluation Sheet Modal */}
      {reports && selectedStudent && (
        <TeacherModal
          isOpen={isStudentPrintModalOpen}
          onClose={() => setIsStudentPrintModalOpen(false)}
          title="Phiếu Đánh Giá Năng Lực Học Sinh"
          description="Bản in chuẩn A4 kèm bảng điểm, mục tiêu và chuyên đề cần bổ trợ"
          size="lg"
          footer={
            <div className="flex justify-between items-center w-full">
              <span className="text-xs text-[var(--th-text-muted)]">
                Nhấn "In phiếu đánh giá" để in ra giấy hoặc lưu thành tệp PDF.
              </span>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => window.print()}
                  className="th-primary-button text-xs py-2 px-4 flex items-center gap-1.5"
                >
                  <span>🖨️</span>
                  <span>In phiếu đánh giá</span>
                </button>
                <button
                  type="button"
                  onClick={() => setIsStudentPrintModalOpen(false)}
                  className="th-secondary-button text-xs py-2 px-3"
                >
                  Đóng
                </button>
              </div>
            </div>
          }
        >
          <div className="p-6 bg-white text-slate-900 font-sans space-y-6 rounded-xl border border-slate-200">
            {/* Report Header */}
            <div className="flex justify-between items-start border-b-2 border-slate-900 pb-4">
              <div>
                <h2 className="text-lg font-black uppercase text-slate-900 tracking-tight">
                  PHIẾU ĐÁNH GIÁ NĂNG LỰC HỌC VIÊN
                </h2>
                <p className="text-xs text-slate-600 mt-1">Hệ Thống Đào Tạo & Khảo Thí Thích Ứng EduTwin</p>
              </div>
              <div className="text-right text-xs text-slate-500">
                <p>Ngày xuất: {new Date().toLocaleDateString("vi-VN")}</p>
                <p>Lớp: {selectedClass ? selectedClass.className : "Toàn bộ"}</p>
              </div>
            </div>

            {/* Student Info Box */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-slate-50 p-4 rounded-lg border border-slate-200 text-xs">
              <div>
                <span className="text-slate-500 block">Họ và tên:</span>
                <strong className="text-slate-900">{selectedStudent.fullName}</strong>
              </div>
              <div>
                <span className="text-slate-500 block">Tên đăng nhập:</span>
                <span className="text-slate-900 font-mono">@{selectedStudent.username}</span>
              </div>
              <div>
                <span className="text-slate-500 block">Khối lớp:</span>
                <span className="text-slate-900 font-semibold">Khối {selectedStudent.gradeLevel}</span>
              </div>
              <div>
                <span className="text-slate-500 block">Điểm trung bình:</span>
                <strong className="text-teal-700 text-sm">
                  {currentStudentSummary?.averageScore !== null && currentStudentSummary?.averageScore !== undefined
                    ? `${currentStudentSummary.averageScore.toFixed(1)} / 10.0`
                    : "Chưa có"}
                </strong>
              </div>
            </div>

            {/* Goals Table */}
            <div>
              <h3 className="text-xs font-bold uppercase text-slate-900 mb-2">1. Mục Tiêu Môn Học</h3>
              <table className="w-full text-left text-xs border border-slate-300">
                <thead>
                  <tr className="bg-slate-100 border-b border-slate-300 font-bold text-slate-700">
                    <th className="p-2 border-r border-slate-300">Môn học</th>
                    <th className="p-2 border-r border-slate-300">Điểm mục tiêu</th>
                    <th className="p-2 border-r border-slate-300">Điểm dự báo (AI)</th>
                    <th className="p-2 border-r border-slate-300">Khoảng cách</th>
                    <th className="p-2">Đánh giá rủi ro</th>
                  </tr>
                </thead>
                <tbody>
                  {studentSubjectGoals.length === 0 ? (
                    <tr>
                      <td colSpan={5} className="p-3 text-center text-slate-400">
                        Chưa thiết lập mục tiêu
                      </td>
                    </tr>
                  ) : (
                    studentSubjectGoals.map((g) => (
                      <tr key={g.goalId} className="border-b border-slate-200">
                        <td className="p-2 border-r border-slate-200 font-semibold">{subjectsMap.get(g.subjectId) || "Môn học"}</td>
                        <td className="p-2 border-r border-slate-200">{g.targetScore.toFixed(1)}</td>
                        <td className="p-2 border-r border-slate-200">{g.currentPredictedScore.toFixed(1)}</td>
                        <td className="p-2 border-r border-slate-200">
                          {(g.targetScore - g.currentPredictedScore).toFixed(1)}
                        </td>
                        <td className="p-2">{g.riskScore > 0.6 ? "Nguy cơ cao" : "An toàn"}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* Assignments Summary Table */}
            <div>
              <h3 className="text-xs font-bold uppercase text-slate-900 mb-2">2. Kết Quả Làm Bài Tập</h3>
              <table className="w-full text-left text-xs border border-slate-300">
                <thead>
                  <tr className="bg-slate-100 border-b border-slate-300 font-bold text-slate-700">
                    <th className="p-2 border-r border-slate-300">Tên bài tập</th>
                    <th className="p-2 border-r border-slate-300">Trạng thái</th>
                    <th className="p-2 border-r border-slate-300">Tiến độ câu</th>
                    <th className="p-2">Điểm số</th>
                  </tr>
                </thead>
                <tbody>
                  {!currentStudentSummary || currentStudentSummary.records.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="p-3 text-center text-slate-400">
                        Chưa có bài tập nào
                      </td>
                    </tr>
                  ) : (
                    currentStudentSummary.records.map((rec) => (
                      <tr key={rec.assignmentId} className="border-b border-slate-200">
                        <td className="p-2 border-r border-slate-200 font-medium">{rec.title}</td>
                        <td className="p-2 border-r border-slate-200">
                          {rec.status === "Completed"
                            ? "Đã nộp bài"
                            : rec.status === "InProgress"
                            ? "Đang làm"
                            : rec.status === "Overdue"
                            ? "Quá hạn"
                            : "Chưa làm"}
                        </td>
                        <td className="p-2 border-r border-slate-200">
                          {rec.completedQuestionCount}/{rec.totalQuestionCount} câu
                        </td>
                        <td className="p-2 font-mono font-bold">
                          {reportScoreText(rec)}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* Pedagogical Notes in Print */}
            {teacherNotes[selectedStudent.studentId] && (
              <div className="p-3 rounded bg-amber-50 border border-amber-200 text-xs">
                <span className="font-bold text-amber-900 block mb-1">Nhận xét của Giáo viên:</span>
                <p className="text-amber-800 leading-relaxed">{teacherNotes[selectedStudent.studentId]}</p>
              </div>
            )}

            {/* Signature Block */}
            <div className="flex justify-between pt-8 text-xs text-center">
              <div>
                <p className="font-bold text-slate-800">Học viên xác nhận</p>
                <p className="text-slate-400 italic mt-8">(Ký và ghi rõ họ tên)</p>
              </div>
              <div>
                <p className="font-bold text-slate-800">Giáo viên phụ trách</p>
                <p className="text-slate-400 italic mt-8">(Ký và ghi rõ họ tên)</p>
              </div>
            </div>
          </div>
        </TeacherModal>
      )}

      {/* 7. Class Summary Printable Report Modal */}
      {reports && selectedClass && (
        <TeacherModal
          isOpen={isClassPrintModalOpen}
          onClose={() => setIsClassPrintModalOpen(false)}
          title="Báo Cáo Tổng Quan & Bảng Điểm Cả Lớp"
          description={`Lớp: ${selectedClass.className} · Niên khóa ${selectedClass.academicYear}`}
          size="xl"
          footer={
            <div className="flex justify-between items-center w-full">
              <span className="text-xs text-[var(--th-text-muted)]">
                Nhấn "In bảng điểm lớp" để in hoặc lưu file PDF.
              </span>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => window.print()}
                  className="th-primary-button text-xs py-2 px-4 flex items-center gap-1.5"
                >
                  <span>🖨️</span>
                  <span>In bảng điểm lớp</span>
                </button>
                <button
                  type="button"
                  onClick={() => setIsClassPrintModalOpen(false)}
                  className="th-secondary-button text-xs py-2 px-3"
                >
                  Đóng
                </button>
              </div>
            </div>
          }
        >
          <div className="p-6 bg-white text-slate-900 font-sans space-y-6 rounded-xl border border-slate-200">
            {/* Header */}
            <div className="flex justify-between items-start border-b-2 border-slate-900 pb-4">
              <div>
                <h2 className="text-lg font-black uppercase text-slate-900 tracking-tight">
                  BÁO CÁO TỔNG KẾT HỌC TẬP LỚP HỌC
                </h2>
                <p className="text-xs text-slate-600 mt-1">
                  Lớp: <strong>{selectedClass.className}</strong> ({selectedClass.academicYear})
                </p>
              </div>
              <div className="text-right text-xs text-slate-500">
                <p>Sĩ số: {studentsList.length} học sinh</p>
                <p>Ngày xuất: {new Date().toLocaleDateString("vi-VN")}</p>
              </div>
            </div>

            {/* Metrics Row */}
            <div className="grid grid-cols-3 gap-3 bg-slate-50 p-4 rounded-lg border border-slate-200 text-center text-xs">
              <div>
                <span className="text-slate-500 block">Tỷ lệ nộp bài tập chung:</span>
                <strong className="text-base text-teal-700">{classMetrics.avgComp}%</strong>
              </div>
              <div>
                <span className="text-slate-500 block">Điểm trung bình cả lớp:</span>
                <strong className="text-base text-slate-900">{classMetrics.avgScore} / 10.0</strong>
              </div>
              <div>
                <span className="text-slate-500 block">Học sinh cần kèm cặp:</span>
                <strong className="text-base text-rose-600">{classMetrics.highRisk} học sinh</strong>
              </div>
            </div>

            {/* Class Roster Table */}
            <div>
              <h3 className="text-xs font-bold uppercase text-slate-900 mb-2">Danh Sách Bảng Điểm Học Sinh</h3>
              <table className="w-full text-left text-xs border border-slate-300">
                <thead>
                  <tr className="bg-slate-100 border-b border-slate-300 font-bold text-slate-700">
                    <th className="p-2 border-r border-slate-300">STT</th>
                    <th className="p-2 border-r border-slate-300">Họ và tên</th>
                    <th className="p-2 border-r border-slate-300">Tên đăng nhập</th>
                    <th className="p-2 border-r border-slate-300">Bài đã nộp</th>
                    <th className="p-2 border-r border-slate-300">% Hoàn thành</th>
                    <th className="p-2 border-r border-slate-300">Điểm TB</th>
                    <th className="p-2">Xếp loại</th>
                  </tr>
                </thead>
                <tbody>
                  {studentsList.map((s, idx) => {
                    const sum = studentSummaries.get(s.studentId);
                    const comp = sum?.completionRate || 0;
                    const avg = sum?.averageScore;
                    const classification =
                      avg !== null && avg !== undefined
                        ? avg >= 8.0
                          ? "Giỏi"
                          : avg >= 6.5
                          ? "Khá"
                          : avg >= 5.0
                          ? "Trung bình"
                          : "Yếu / Nguy cơ"
                        : "Chưa có điểm";

                    return (
                      <tr key={s.studentId} className="border-b border-slate-200">
                        <td className="p-2 border-r border-slate-200 text-center">{idx + 1}</td>
                        <td className="p-2 border-r border-slate-200 font-semibold">{s.fullName}</td>
                        <td className="p-2 border-r border-slate-200 font-mono text-slate-600">@{s.username}</td>
                        <td className="p-2 border-r border-slate-200">
                          {sum?.completedCount || 0}/{sum?.totalAssigned || 0}
                        </td>
                        <td className="p-2 border-r border-slate-200 font-bold text-teal-700">{comp.toFixed(0)}%</td>
                        <td className="p-2 border-r border-slate-200 font-mono font-bold">
                          {avg !== null && avg !== undefined ? avg.toFixed(1) : "-"}
                        </td>
                        <td className="p-2">{classification}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        </TeacherModal>
      )}
    </div>
  );
}
