import { useState, useMemo, useEffect } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { organizationApi } from "../../api/organizationApi";
import { getAssignments, getAssignmentProgress } from "../../api/assignmentsApi";
import { getTeacherStudentTwin } from "../../api/digitalTwinApi";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import {
  TeacherPageHeader,
  TeacherMetricCard,
  TeacherStatusBadge,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import { TeacherModal } from "../../components/teacher/TeacherOverlays";
import type { StudentDto, StudentSubjectGoalDto } from "../../types/organization";
import type { AssignmentDto, AssignmentProgressItemDto, ProgressStatus } from "../../types/assignments";
import type { StudentTwinDataDto } from "../../types/digitalTwin";

import {
  downloadCsv,
  exportStudentReportXlsx,
  exportClassReportXlsx,
  buildStudentReportCsv,
  buildClassReportCsv,
  type StudentAcademicSummary,
  type StudentAssignmentRecord,
} from "./teacherReportsHelpers";

export function TeacherStudentManagementView() {
  const { studentId: routeStudentId } = useParams<{ studentId?: string }>();
  const navigate = useNavigate();
  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canCreateAssignment = hasPermission(permissions.assignmentsCreate);

  // Filter States
  const [selectedClassId, setSelectedClassId] = useState<string>("");
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

  // Pedagogical notes state (stored by studentId in localStorage for teacher persistence)
  const [teacherNotes, setTeacherNotes] = useState<Record<string, string>>(() => {
    try {
      const saved = localStorage.getItem("teacher_pedagogical_notes");
      return saved ? JSON.parse(saved) : {};
    } catch {
      return {};
    }
  });

  const handleSaveNote = (studentId: string, note: string) => {
    const updated = { ...teacherNotes, [studentId]: note };
    setTeacherNotes(updated);
    try {
      localStorage.setItem("teacher_pedagogical_notes", JSON.stringify(updated));
    } catch {
      // ignore
    }
  };

  // 1. Fetch Teacher Assigned Classes
  const { data: classesData, isLoading: isClassesLoading } = useQuery({
    queryKey: ["teacherClassesListForStudents"],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 50, status: "Active" }),
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

  // 3. Fetch Students in the selected class (or all students if class selected)
  const {
    data: studentsData,
    isLoading: isStudentsLoading,
    isError: isStudentsError,
    error: studentsError,
    refetch: refetchStudents,
  } = useQuery({
    queryKey: ["classStudentsForTeacher", selectedClassId],
    queryFn: () =>
      selectedClassId
        ? organizationApi.getClassStudents(selectedClassId, { page: 1, pageSize: 100 })
        : organizationApi.listStudents({ page: 1, pageSize: 100 }),
    enabled: Boolean(selectedClassId) || classes.length === 0,
  });

  const studentsList: StudentDto[] = studentsData?.data || [];

  // 4. Fetch Assignments for the selected class to calculate student completion & scores
  const { data: assignmentsData } = useQuery({
    queryKey: ["classAssignmentsForTeacher", selectedClassId],
    queryFn: () => getAssignments({ classId: selectedClassId, pageSize: 50 }),
    enabled: Boolean(selectedClassId),
  });

  const classAssignments: AssignmentDto[] = assignmentsData?.data || [];

  // 5. Query progress of all assignments in this class
  // To keep UI fast and responsive, we can query progress for the assignments
  const assignmentIds = useMemo(() => classAssignments.map((a) => a.assignmentId), [classAssignments]);

  const { data: assignmentsProgressMap } = useQuery({
    queryKey: ["classAssignmentsProgressBatch", selectedClassId, assignmentIds],
    queryFn: async () => {
      const map = new Map<string, AssignmentProgressItemDto[]>();
      await Promise.all(
        classAssignments.map(async (assignment) => {
          try {
            const res = await getAssignmentProgress(assignment.assignmentId);
            map.set(assignment.assignmentId, res.data || []);
          } catch {
            map.set(assignment.assignmentId, []);
          }
        })
      );
      return map;
    },
    enabled: classAssignments.length > 0,
  });

  // 6. Calculate Academic Summary for Each Student
  const studentSummaries = useMemo(() => {
    const summaries = new Map<string, StudentAcademicSummary>();

    studentsList.forEach((student) => {
      const records: StudentAssignmentRecord[] = [];
      let completedCount = 0;
      let inProgressCount = 0;
      let notStartedCount = 0;
      let overdueCount = 0;
      let scoreSum = 0;
      let scoredAssignmentsCount = 0;
      let minScore: number | null = null;
      let maxScore: number | null = null;

      classAssignments.forEach((assignment) => {
        const progressList = assignmentsProgressMap?.get(assignment.assignmentId) || [];
        const studentProgress = progressList.find((p) => p.studentId === student.studentId);

        let status: ProgressStatus = studentProgress?.status || "NotStarted";
        const completedQuestions = studentProgress?.completedQuestionCount || 0;
        const totalQuestions = studentProgress?.totalQuestionCount || assignment.questionCount || 10;

        // Check if overdue
        if (status !== "Completed" && assignment.dueAt && new Date(assignment.dueAt) < new Date()) {
          status = "Overdue";
        }

        if (status === "Completed") completedCount++;
        else if (status === "InProgress") inProgressCount++;
        else if (status === "Overdue") overdueCount++;
        else notStartedCount++;

        // Calculate score (approximate from question points or evaluation)
        let calculatedScore: number | null = null;
        if (status === "Completed") {
          // Normalized 10 scale score based on completed questions ratio or actual score
          const ratio = totalQuestions > 0 ? completedQuestions / totalQuestions : 1;
          calculatedScore = Math.round(ratio * 10 * 10) / 10;
          scoreSum += calculatedScore;
          scoredAssignmentsCount++;

          if (minScore === null || calculatedScore < minScore) minScore = calculatedScore;
          if (maxScore === null || calculatedScore > maxScore) maxScore = calculatedScore;
        }

        const subjName = selectedClass?.subject?.subjectName || (selectedClass?.subject?.subjectId ? subjectsMap.get(selectedClass.subject.subjectId) : undefined);

        records.push({
          assignmentId: assignment.assignmentId,
          title: assignment.title,
          subjectId: selectedClass?.subject?.subjectId,
          subjectName: subjName,
          dueAt: assignment.dueAt,
          status,
          completedQuestionCount: completedQuestions,
          totalQuestionCount: totalQuestions,
          score: calculatedScore,
          maxScore: 10,
          feedbackNote: status === "Completed" ? "Đã nộp bài đầy đủ" : status === "Overdue" ? "Chưa hoàn thành đúng hạn" : undefined,
        });
      });

      const totalAssigned = classAssignments.length;
      const completionRate = totalAssigned > 0 ? (completedCount / totalAssigned) * 100 : 0;
      const averageScore = scoredAssignmentsCount > 0 ? scoreSum / scoredAssignmentsCount : null;

      summaries.set(student.studentId, {
        totalAssigned,
        completedCount,
        inProgressCount,
        notStartedCount,
        overdueCount,
        completionRate,
        averageScore,
        minScore,
        maxScore,
        records,
      });
    });

    return summaries;
  }, [studentsList, classAssignments, assignmentsProgressMap, selectedClass, subjectsMap]);

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
        const isLowCompletion = summary && summary.completionRate < 50;
        const isLowScore = summary && summary.averageScore !== null && summary.averageScore < 5.0;
        return isLowCompletion || isLowScore;
      }

      if (statusTab === "good") {
        const isGoodCompletion = summary && summary.completionRate >= 80;
        const isGoodScore = summary && summary.averageScore !== null && summary.averageScore >= 8.0;
        return isGoodCompletion || isGoodScore;
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
        if (sum.completionRate < 50 || (sum.averageScore !== null && sum.averageScore < 5.0)) {
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
  const handleExportIndividualExcel = (student: StudentDto) => {
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
    exportStudentReportXlsx(
      student,
      selectedClass,
      studentSubjectGoals,
      subjectsMap,
      summary,
      teacherNotes[student.studentId]
    );
  };

  const handleExportIndividualCsv = (student: StudentDto) => {
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
    const rows = buildStudentReportCsv(student, selectedClass, studentSubjectGoals, subjectsMap, summary);
    const filename = `Bao_cao_hoc_vien_${student.username}_${new Date().toISOString().slice(0, 10)}.csv`;
    downloadCsv(filename, rows);
  };

  const handleExportClassExcel = () => {
    if (!selectedClass) return;
    const goalsMap = new Map<string, StudentSubjectGoalDto[]>();
    exportClassReportXlsx(selectedClass, studentsList, studentSummaries, goalsMap);
  };

  const handleExportClassCsv = () => {
    if (!selectedClass) return;
    const goalsMap = new Map<string, StudentSubjectGoalDto[]>();
    const rows = buildClassReportCsv(selectedClass, studentsList, studentSummaries, goalsMap);
    const filename = `Bang_diem_lop_${selectedClass.className}_${new Date().toISOString().slice(0, 10)}.csv`;
    downloadCsv(filename, rows);
  };

  return (
    <div className="space-y-6">
      {/* 1. Header with Class Switcher & Report Actions */}
      <TeacherPageHeader
        eyebrow="GIẢNG DẠY & ĐÁNH GIÁ NĂNG LỰC"
        title="Quản Lý Học Viên & Báo Cáo Học Tập"
        description="Theo dõi hồ sơ cá nhân, tiến độ nộp bài, phân tích bảng điểm, mục tiêu điểm thi và xuất báo cáo đánh giá học lực định kỳ."
        breadcrumbs={[
          { label: "Giáo viên", href: "/giao-vien/lop-hoc" },
          { label: "Quản lý học sinh" },
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
                  setSelectedStudent(null);
                }}
                className="th-select text-xs min-w-[190px]"
                aria-label="Chọn lớp học phụ trách"
              >
                {classes.map((c) => (
                  <option key={c.classId} value={c.classId}>
                    {c.className} ({c.academicYear})
                  </option>
                ))}
              </select>
            )}

            <button
              type="button"
              onClick={handleExportClassExcel}
              disabled={studentsList.length === 0}
              className="th-primary-button text-xs py-2 px-3 flex items-center gap-1.5"
              title="Xuất bảng điểm và tiến độ toàn bộ học sinh trong lớp ra file Microsoft Excel (.xlsx) với các cột riêng biệt"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                <polyline points="7 10 12 15 17 10" />
                <line x1="12" y1="15" x2="12" y2="3" />
              </svg>
              <span>Xuất Excel Cả Lớp (.xlsx)</span>
            </button>

            <button
              type="button"
              onClick={handleExportClassCsv}
              disabled={studentsList.length === 0}
              className="th-secondary-button text-xs py-2 px-2.5 flex items-center gap-1"
              title="Xuất bảng điểm lớp định dạng CSV (.csv)"
            >
              <span>📥</span>
              <span>CSV</span>
            </button>

            <button
              type="button"
              onClick={() => setIsClassPrintModalOpen(true)}
              disabled={studentsList.length === 0}
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
          </div>
        }
      />

      {/* 2. Overview Metric Cards */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
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
          supportingText={`Tổng ${classAssignments.length} bài tập đã giao`}
          icon="📑"
          trend={{
            label: classMetrics.avgComp >= 80 ? "Đạt chỉ tiêu" : "Cần đôn đốc",
            tone: classMetrics.avgComp >= 80 ? "positive" : "negative",
          }}
        />
        <TeacherMetricCard
          label="Điểm trung bình lớp"
          value={classMetrics.avgScore}
          unit="/ 10.0"
          supportingText="Tính trên bài tập đã nộp"
          icon="🎯"
          trend={{
            label: Number(classMetrics.avgScore) >= 7.0 ? "Khá giỏi" : "Cần củng cố",
            tone: Number(classMetrics.avgScore) >= 7.0 ? "positive" : "neutral",
          }}
        />
        <TeacherMetricCard
          label="Học sinh nguy cơ cao"
          value={classMetrics.highRisk}
          unit="học sinh"
          supportingText="Điểm < 5.0 hoặc nộp < 50%"
          icon="⚠️"
          trend={{
            label: classMetrics.highRisk === 0 ? "Lớp an toàn" : "Cần hỗ trợ sớm",
            tone: classMetrics.highRisk === 0 ? "positive" : "negative",
          }}
        />
      </div>

      {/* 3. Filter Bar & Quick Status Tabs */}
      <div className="th-surface p-4 space-y-3">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
          {/* Search box */}
          <div className="flex-1 max-w-md relative">
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Tìm kiếm học sinh theo họ tên, tên đăng nhập..."
              className="th-input w-full text-xs py-2 pl-8"
            />
            <span className="absolute left-2.5 top-2.5 text-stone-400 text-xs">🔍</span>
          </div>

          {/* Grade filter */}
          <div className="flex items-center gap-2">
            <span className="text-xs text-[var(--th-text-muted)] font-semibold">Khối:</span>
            <select
              value={selectedGrade}
              onChange={(e) => setSelectedGrade(e.target.value)}
              className="th-select text-xs py-1.5"
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
                filteredStudents.map((student) => {
                  const summary = studentSummaries.get(student.studentId);
                  const completion = summary ? summary.completionRate : 0;
                  const avgScore = summary?.averageScore;
                  const isHighRisk = completion < 50 || (avgScore !== null && avgScore !== undefined && avgScore < 5.0);

                  return (
                    <tr
                      key={student.studentId}
                      className="hover:bg-[var(--th-surface-subtle)] transition-colors group cursor-pointer"
                      onClick={() => setSelectedStudent(student)}
                    >
                      {/* Name & Username (Read-Only) */}
                      <td className="px-5 py-4">
                        <div className="flex items-center gap-3">
                          <div className="h-8 w-8 rounded-full bg-[var(--th-surface-muted)] border border-[var(--th-border)] flex items-center justify-center font-bold text-xs text-[var(--th-text)] shrink-0">
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
                          <TeacherStatusBadge status="completed" label="Tiến độ tốt" tone="success" />
                        ) : (
                          <TeacherStatusBadge status="pending" label="Bình thường" tone="info" />
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
                            className="th-secondary-button text-xs py-1 px-2.5"
                            title="Xem chi tiết hồ sơ học tập và bảng điểm"
                          >
                            Hồ sơ →
                          </button>
                          <Link
                            to={`/giao-vien/hoc-sinh/${student.studentId}/twin?subjectId=${selectedClass?.subject?.subjectId || ""}`}
                            className="th-secondary-button text-xs py-1 px-2 text-stone-600 hover:text-[var(--th-teal)]"
                            title="Xem bản sao số năng lực (Digital Twin)"
                          >
                            Twin
                          </Link>
                          <button
                            type="button"
                            onClick={() => handleExportIndividualExcel(student)}
                            className="th-icon-button h-7 w-7 text-xs text-teal-700 dark:text-teal-400 hover:bg-teal-500/10"
                            title="Tải bảng điểm cá nhân định dạng Microsoft Excel (.xlsx)"
                          >
                            📊
                          </button>
                          <button
                            type="button"
                            onClick={() => handleExportIndividualCsv(student)}
                            className="th-icon-button h-7 w-7 text-xs text-stone-600 hover:bg-stone-500/10"
                            title="Tải bảng điểm cá nhân định dạng CSV (.csv)"
                          >
                            📥
                          </button>
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

      {/* 5. Comprehensive Individual Student Modal (Centered & Responsive) */}
      <TeacherModal
        isOpen={Boolean(selectedStudent)}
        onClose={() => setSelectedStudent(null)}
        title={selectedStudent ? selectedStudent.fullName : "Hồ sơ học sinh"}
        description={selectedStudent ? `Tên đăng nhập: @${selectedStudent.username} · Khối ${selectedStudent.gradeLevel}` : ""}
        size="lg"
        footer={
          selectedStudent && (
            <div className="flex flex-wrap items-center justify-between w-full gap-2.5">
              <div className="flex flex-wrap items-center gap-2">
                <button
                  type="button"
                  onClick={() => selectedStudent && handleExportIndividualExcel(selectedStudent)}
                  className="th-primary-button text-xs py-1.5 px-3 flex items-center gap-1.5"
                  title="Tải bảng điểm và mục tiêu học sinh định dạng Microsoft Excel (.xlsx) với các cột và hàng riêng biệt"
                >
                  <span>📊</span>
                  <span>Xuất Excel (.xlsx)</span>
                </button>
                <button
                  type="button"
                  onClick={() => selectedStudent && handleExportIndividualCsv(selectedStudent)}
                  className="th-secondary-button text-xs py-1.5 px-2.5 flex items-center gap-1.5"
                  title="Tải bảng điểm học sinh định dạng CSV (.csv)"
                >
                  <span>📥</span>
                  <span>Xuất CSV</span>
                </button>
                <button
                  type="button"
                  onClick={() => setIsStudentPrintModalOpen(true)}
                  className="th-secondary-button text-xs py-1.5 px-3 flex items-center gap-1.5"
                  title="Mở giao diện in phiếu đánh giá chuẩn A4"
                >
                  <span>🖨️</span>
                  <span>In Phiếu Đánh Giá</span>
                </button>
              </div>
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
                                  {rec.score.toFixed(1)}
                                </span>
                                <span className="text-[10px] text-stone-400"> / 10.0</span>
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
                    Chưa có dữ liệu Bản sao số (Digital Twin) cho môn học này.
                  </div>
                ) : (
                  <div className="space-y-4">
                    {/* Weak topics alert */}
                    {studentTwinData.topics.some((t) => t.masteryPercentage < 0.6) && (
                      <div className="p-3.5 rounded-xl bg-rose-500/10 border border-rose-500/30 flex items-start justify-between gap-3">
                        <div>
                          <p className="font-bold text-xs text-rose-700 dark:text-rose-300">
                            Phát hiện lỗ hổng kiến thức (&lt; 60% độ thuần thục)
                          </p>
                          <p className="mt-1 text-[11px] text-rose-600 dark:text-rose-400">
                            Học sinh đang gặp khó khăn ở các chuyên đề:{" "}
                            <strong>
                              {studentTwinData.topics
                                .filter((t) => t.masteryPercentage < 0.6)
                                .map((t) => t.topicName)
                                .join(", ")}
                            </strong>
                          </p>
                        </div>
                        {canCreateAssignment && (
                          <button
                            type="button"
                            onClick={() => {
                              const weak = studentTwinData.topics.filter((t) => t.masteryPercentage < 0.6);
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
                        const pct = Math.round(topic.masteryPercentage * 100);
                        return (
                          <div
                            key={topic.topicNodeId}
                            className="p-3 rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface)] space-y-1.5"
                          >
                            <div className="flex items-center justify-between text-xs">
                              <span className="font-semibold text-[var(--th-text)]">{topic.topicName}</span>
                              <span className="font-mono font-bold text-[var(--th-text)]">{pct}%</span>
                            </div>
                            <div className="h-2 rounded-full bg-[var(--th-surface-muted)] overflow-hidden">
                              <div
                                className={`h-full rounded-full ${
                                  pct >= 80 ? "bg-emerald-500" : pct >= 60 ? "bg-sky-500" : "bg-rose-500"
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
                  className="th-input w-full text-xs p-3 leading-relaxed"
                />

                <div className="flex justify-between items-center text-[11px] text-[var(--th-text-muted)]">
                  <span>💾 Ghi chú được tự động lưu trên trình duyệt của bạn.</span>
                  <span className="font-semibold text-emerald-600">Đã lưu tự động</span>
                </div>
              </div>
            )}
          </div>
        )}
      </TeacherModal>

      {/* 6. Individual Student Printable Evaluation Sheet Modal */}
      {selectedStudent && (
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
                          {rec.score !== null && rec.score !== undefined ? `${rec.score.toFixed(1)} / 10` : "-"}
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
      {selectedClass && (
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
