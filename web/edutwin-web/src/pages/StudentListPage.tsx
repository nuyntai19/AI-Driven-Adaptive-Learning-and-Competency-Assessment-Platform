import React, { useMemo, useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { organizationApi } from "../api/organizationApi";
import type { UserStatus } from "../types/auth";
import type {
  StudentListParams,
  CreateStudentRequest,
  UpdateStudentRequest,
  ResetAccountPasswordRequest,
  StudentDto,
  StudentDetailDto,
} from "../types/organization";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { extractProblemDetails, isConcurrencyConflict, mapSafeOperationalError } from "../utils/problemDetails";
import {
  CenterManagerThemeScope,
  PageHeader,
  FilterBar,
  DataTable,
  type DataTableColumn,
  StatusBadge,
  Modal,
  Drawer,
  ConcurrencyBanner,
  SafeErrorPanel,
  SubjectGoalsModal,
} from "../components/centerManager";

const STATUS_LABELS: Record<string, string> = {
  Active: "Hoạt động",
  Locked: "Bị khóa",
  Disabled: "Vô hiệu hóa",
};

/**
 * Modern CenterManager Dark Enterprise SaaS view for StudentListPage
 */
const CenterManagerStudentListView: React.FC = () => {
  const queryClient = useQueryClient();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreateStudent = hasPermission(permissions.studentsCreate);
  const canUpdateStudent = hasPermission(permissions.studentsUpdate);
  const canDeleteStudent = hasPermission(permissions.studentsDelete);
  const canResetPassword = hasPermission(permissions.studentsResetPassword);
  const canUpdateTwinScoped = hasPermission(permissions.twinStudentUpdateScoped);

  const [page, setPage] = useState<number>(1);
  const pageSize = 20;

  // Filter state
  const [search, setSearch] = useState<string>("");
  const [status, setStatus] = useState<UserStatus | "">("");
  const [gradeLevel, setGradeLevel] = useState<number | "">("");

  // Input states before search submit
  const [searchInput, setSearchInput] = useState<string>("");
  const [statusInput, setStatusInput] = useState<UserStatus | "">("");
  const [gradeLevelInput, setGradeLevelInput] = useState<number | "">("");

  // Create modal state
  const [isCreating, setIsCreating] = useState(false);
  const [createUsername, setCreateUsername] = useState("");
  const [createPassword, setCreatePassword] = useState("");
  const [createFullName, setCreateFullName] = useState("");
  const [createGradeLevel, setCreateGradeLevel] = useState<10 | 11 | 12>(10);
  const [createClassIds, setCreateClassIds] = useState<string[]>([]);

  // Detail drawer state
  const [viewingStudentId, setViewingStudentId] = useState<string | null>(null);

  // Subject goals modal state
  const [goalStudentId, setGoalStudentId] = useState<string | null>(null);
  const [goalStudentName, setGoalStudentName] = useState<string>("");

  // Query active subjects for resolving subjectId into subjectName in drawer and modal
  const { data: subjectsData } = useQuery({
    queryKey: ["subjects", "active-for-student-list"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const subjectsMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const sub of subjectsData?.data ?? []) {
      map.set(sub.subjectId, `${sub.subjectName} (${sub.subjectCode})`);
    }
    return map;
  }, [subjectsData?.data]);

  // Edit modal state
  const [editingStudent, setEditingStudent] = useState<StudentDto | null>(null);
  const [editFullName, setEditFullName] = useState("");
  const [editGradeLevel, setEditGradeLevel] = useState<number>(10);
  const [editStatus, setEditStatus] = useState<UserStatus>("Active");

  // Reset password modal state
  const [resetStudent, setResetStudent] = useState<StudentDto | null>(null);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [resetReason, setResetReason] = useState("");

  // Delete modal state
  const [deletingStudent, setDeletingStudent] = useState<StudentDto | null>(null);

  // Notifications
  const [feedback, setFeedback] = useState<{
    type: "success" | "error" | "conflict";
    message: string;
    traceId?: string;
  } | null>(null);

  const showFeedback = (type: "success" | "error" | "conflict", message: string, traceId?: string | null) => {
    setFeedback({ type, message, traceId: traceId ?? undefined });
    if (type === "success") {
      setTimeout(() => setFeedback(null), 5000);
    }
  };

  const queryParams: StudentListParams = {
    page,
    pageSize,
    search: search.trim() !== "" ? search.trim() : undefined,
    status: status !== "" ? status : undefined,
    gradeLevel: gradeLevel !== "" ? gradeLevel : undefined,
  };

  const { data, isLoading, isFetching, isError: isListError, error: listError, refetch } = useQuery({
    queryKey: ["students", queryParams.page, queryParams.pageSize, queryParams.search, queryParams.status, queryParams.gradeLevel],
    queryFn: () => organizationApi.listStudents(queryParams),
  });

  const { data: classesData } = useQuery({
    queryKey: ["classes", "active-for-student-create"],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 100, status: "Active" }),
    enabled: isCreating,
  });

  const { data: studentDetail, isLoading: isDetailLoading } = useQuery<StudentDetailDto>({
    queryKey: ["studentDetail", viewingStudentId],
    queryFn: () => organizationApi.getStudent(viewingStudentId!),
    enabled: !!viewingStudentId,
  });

  // Create mutation
  const createMutation = useMutation({
    mutationFn: async (request: CreateStudentRequest) => {
      try {
        return await organizationApi.createStudent(request);
      } finally {
        request.temporaryPassword = "";
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["students"] });
      setIsCreating(false);
      setCreateUsername("");
      setCreatePassword("");
      setCreateFullName("");
      setCreateGradeLevel(10);
      setCreateClassIds([]);
      showFeedback("success", "Đã tạo tài khoản học viên thành công.");
    },
    onError: (error) => {
      const details = extractProblemDetails(error);
      if (details.errorCode === "DUPLICATE_RESOURCE") {
        showFeedback("error", "Tên đăng nhập đã tồn tại trong trung tâm.", details.traceId);
      } else {
        showFeedback("error", mapSafeOperationalError(error, "Không thể tạo học viên."), details.traceId);
      }
    },
  });

  // Update mutation
  const updateMutation = useMutation({
    mutationFn: async ({ studentId, request }: { studentId: string; request: UpdateStudentRequest }) => {
      return await organizationApi.updateStudent(studentId, request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["students"] });
      setEditingStudent(null);
      showFeedback("success", "Đã cập nhật thông tin học viên thành công.");
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) {
        showFeedback("conflict", "Dữ liệu học viên đã bị thay đổi bởi phiên làm việc khác. Vui lòng tải lại dữ liệu mới nhất.");
      } else {
        const details = extractProblemDetails(error);
        showFeedback("error", mapSafeOperationalError(error, "Không thể cập nhật học viên."), details.traceId);
      }
    },
  });

  // Reset password mutation
  const resetPasswordMutation = useMutation({
    mutationFn: async ({ studentId, request }: { studentId: string; request: ResetAccountPasswordRequest }) => {
      try {
        return await organizationApi.resetStudentPassword(studentId, request);
      } finally {
        request.newPassword = "";
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["students"] });
      setResetStudent(null);
      setNewPassword("");
      setConfirmPassword("");
      setResetReason("");
      showFeedback("success", "Đã đặt lại mật khẩu học viên thành công. Mọi phiên đăng nhập cũ đã được thu hồi.");
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) {
        showFeedback("conflict", "Dữ liệu người dùng đã bị thay đổi bởi phiên làm việc khác. Vui lòng tải lại.");
      } else {
        const details = extractProblemDetails(error);
        showFeedback("error", mapSafeOperationalError(error, "Không thể đặt lại mật khẩu học viên."), details.traceId);
      }
    },
  });

  // Delete mutation
  const deleteMutation = useMutation({
    mutationFn: async (studentId: string) => {
      return await organizationApi.deleteStudent(studentId);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["students"] });
      setDeletingStudent(null);
      showFeedback("success", "Đã xóa học viên thành công. Toàn bộ bằng chứng học tập và Digital Twin được bảo toàn.");
    },
    onError: (error) => {
      const details = extractProblemDetails(error);
      showFeedback("error", mapSafeOperationalError(error, "Không thể xóa học viên. Vui lòng thử lại."), details.traceId);
    },
  });

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput);
    setStatus(statusInput);
    setGradeLevel(gradeLevelInput);
    setPage(1);
  };

  const openEditModal = (student: StudentDto) => {
    setEditingStudent(student);
    setEditFullName(student.fullName);
    setEditGradeLevel(student.gradeLevel);
    setEditStatus(student.status);
    setFeedback(null);
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingStudent) return;

    const trimmed = editFullName.trim();
    if (!trimmed) {
      showFeedback("error", "Họ tên học viên không được để trống.");
      return;
    }

    updateMutation.mutate({
      studentId: editingStudent.studentId,
      request: {
        fullName: trimmed,
        gradeLevel: editGradeLevel,
        status: editStatus,
        rowVersion: editingStudent.rowVersion,
      },
    });
  };

  const openResetPasswordModal = (student: StudentDto) => {
    setResetStudent(student);
    setNewPassword("");
    setConfirmPassword("");
    setResetReason("");
    setFeedback(null);
  };

  const handleResetPasswordSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!resetStudent) return;

    if (newPassword.length < 12 || newPassword.length > 200) {
      showFeedback("error", "Mật khẩu mới phải từ 12 đến 200 ký tự.");
      return;
    }

    if (newPassword !== confirmPassword) {
      showFeedback("error", "Mật khẩu xác nhận không khớp.");
      return;
    }

    if (resetReason.trim().length < 5 || resetReason.trim().length > 500) {
      showFeedback("error", "Lý do đặt lại mật khẩu là bắt buộc (từ 5 đến 500 ký tự).");
      return;
    }

    resetPasswordMutation.mutate({
      studentId: resetStudent.studentId,
      request: {
        newPassword,
        expectedUserRowVersion: resetStudent.rowVersion,
        reason: resetReason.trim(),
      },
    });
  };

  const openDeleteModal = (student: StudentDto) => {
    setDeletingStudent(student);
    setFeedback(null);
  };

  const handleDeleteSubmit = () => {
    if (!deletingStudent) return;
    deleteMutation.mutate(deletingStudent.studentId);
  };

  // Open Subject Goals modal directly
  const handleOpenSubjectGoals = (student: StudentDto) => {
    setGoalStudentId(student.studentId);
    setGoalStudentName(`${student.fullName} (@${student.username})`);
  };

  const columns: DataTableColumn<StudentDto>[] = [
    {
      id: "username",
      header: "Tên đăng nhập",
      render: (student) => (
        <span className="font-mono font-medium text-[var(--cm-cyan)]">
          {student.username}
        </span>
      ),
    },
    {
      id: "fullName",
      header: "Họ và tên",
      render: (student) => (
        <span className="font-medium text-[var(--cm-text)]">
          {student.fullName}
        </span>
      ),
    },
    {
      id: "gradeLevel",
      header: "Khối lớp",
      render: (student) => (
        <span className="text-[var(--cm-text-secondary)]">
          Khối {student.gradeLevel}
        </span>
      ),
    },
    {
      id: "activeClassCount",
      header: "Số lớp",
      align: "center",
      render: (student) => (
        <span className="font-semibold text-[var(--cm-text-secondary)]">
          {student.activeClassCount}
        </span>
      ),
    },
    {
      id: "status",
      header: "Trạng thái",
      render: (student) => (
        <StatusBadge
          status={student.status}
          label={STATUS_LABELS[student.status] || student.status}
        />
      ),
    },
    {
      id: "actions",
      header: "Thao tác",
      align: "right",
      render: (student) => (
        <div className="flex items-center justify-end gap-1.5">
          <button
            type="button"
            id={`btn-view-student-${student.studentId}`}
            onClick={() => setViewingStudentId(student.studentId)}
            className="cm-secondary-button h-8 px-2.5 py-1 text-xs"
          >
            Chi tiết
          </button>
          {canUpdateTwinScoped && (
            <button
              type="button"
              id={`btn-goals-student-${student.studentId}`}
              onClick={() => handleOpenSubjectGoals(student)}
              className="cm-secondary-button h-8 px-2.5 py-1 text-xs text-cyan-300 hover:text-cyan-200"
              title="Mục tiêu môn học (Digital Twin)"
            >
              Mục tiêu
            </button>
          )}
          {canUpdateStudent && (
            <button
              type="button"
              id={`btn-edit-student-${student.studentId}`}
              onClick={() => openEditModal(student)}
              className="cm-secondary-button h-8 px-2.5 py-1 text-xs"
            >
              Sửa
            </button>
          )}
          {canResetPassword && (
            <button
              type="button"
              id={`btn-reset-password-student-${student.studentId}`}
              onClick={() => openResetPasswordModal(student)}
              className="cm-secondary-button h-8 px-2.5 py-1 text-xs"
            >
              Đổi MK
            </button>
          )}
          {canDeleteStudent && (
            <button
              type="button"
              id={`btn-delete-student-${student.studentId}`}
              onClick={() => openDeleteModal(student)}
              className="cm-danger-button h-8 px-2.5 py-1 text-xs"
            >
              Xóa
            </button>
          )}
        </div>
      ),
    },
  ];

  return (
    <CenterManagerThemeScope>
      <div className="px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
        <div className="mx-auto max-w-[96rem] space-y-6">
          <PageHeader
            eyebrow="Quản trị học viên"
            title="Quản lý Học viên"
            description="Quản lý hồ sơ học viên, phân khối lớp, trạng thái tài khoản và mục tiêu điểm số môn học (Digital Twin)."
            actions={
              canCreateStudent && (
                <button
                  type="button"
                  id="btn-create-student"
                  onClick={() => {
                    setIsCreating(true);
                    setFeedback(null);
                  }}
                  className="cm-primary-button"
                >
                  + Thêm học viên
                </button>
              )
            }
          />

          {feedback?.type === "conflict" && (
            <ConcurrencyBanner
              onReload={() => {
                refetch();
                setFeedback(null);
              }}
              isReloading={isFetching}
            />
          )}

          {feedback && feedback.type !== "conflict" && (
            <div
              id="student-feedback-alert"
              role="alert"
              className={`flex items-start justify-between rounded-xl border p-4 text-sm ${
                feedback.type === "success"
                  ? "border-emerald-400/30 bg-emerald-400/10 text-emerald-200"
                  : "border-rose-400/30 bg-rose-400/10 text-rose-200"
              }`}
            >
              <div>
                <p className="font-semibold">{feedback.message}</p>
                {feedback.traceId && (
                  <p className="mt-1 font-mono text-xs opacity-75">Trace ID: {feedback.traceId}</p>
                )}
              </div>
              <button
                type="button"
                onClick={() => setFeedback(null)}
                className="text-slate-400 hover:text-white"
                aria-label="Đóng thông báo"
              >
                ×
              </button>
            </div>
          )}

          <FilterBar
            searchValue={searchInput}
            searchLabel="Tìm kiếm học viên"
            searchPlaceholder="Tìm theo tên đăng nhập hoặc họ tên…"
            onSearchChange={setSearchInput}
            filters={
              <div className="flex flex-wrap items-center gap-2">
                <label htmlFor="filter-grade" className="sr-only">Khối lớp</label>
                <select
                  id="filter-grade"
                  value={gradeLevelInput}
                  onChange={(e) => setGradeLevelInput(e.target.value === "" ? "" : Number(e.target.value))}
                  className="cm-field px-3 py-1.5 text-xs"
                >
                  <option value="">Tất cả khối</option>
                  <option value="10">Khối 10</option>
                  <option value="11">Khối 11</option>
                  <option value="12">Khối 12</option>
                </select>

                <label htmlFor="filter-status" className="sr-only">Trạng thái học viên</label>
                <select
                  id="filter-status"
                  value={statusInput}
                  onChange={(e) => setStatusInput(e.target.value as UserStatus | "")}
                  className="cm-field px-3 py-1.5 text-xs"
                >
                  <option value="">Tất cả trạng thái</option>
                  <option value="Active">Hoạt động</option>
                  <option value="Locked">Bị khóa</option>
                  <option value="Disabled">Vô hiệu hóa</option>
                </select>
              </div>
            }
            actions={
              <button
                type="button"
                id="btn-search-students"
                onClick={handleSearch}
                disabled={isFetching}
                className="cm-secondary-button text-xs"
              >
                Tìm kiếm
              </button>
            }
          />

          {isListError && (
            <SafeErrorPanel
              error={listError}
              fallback="Không thể tải danh sách học viên. Vui lòng thử lại."
              onRetry={() => refetch()}
            />
          )}

          <DataTable<StudentDto>
            caption="Danh sách học viên trung tâm"
            columns={columns}
            rows={data?.data ?? []}
            rowKey={(student) => student.studentId}
            isLoading={isLoading}
            emptyTitle="Không tìm thấy học viên nào"
            emptyDescription="Chưa có hồ sơ học viên phù hợp với bộ lọc tìm kiếm hiện tại."
            page={page}
            totalPages={data?.meta?.totalPages ?? 1}
            totalItems={data?.meta?.totalItems}
            onPageChange={setPage}
          />

          {/* Create Modal */}
          {isCreating && canCreateStudent && (
            <Modal
              isOpen={isCreating}
              title="Thêm học viên mới"
              description="Tạo tài khoản học viên, xếp khối và gán vào các lớp học ban đầu."
              onClose={() => setIsCreating(false)}
            >
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  createMutation.mutate({
                    username: createUsername.trim(),
                    temporaryPassword: createPassword,
                    fullName: createFullName.trim(),
                    gradeLevel: createGradeLevel,
                    classIds: createClassIds,
                  });
                }}
                className="space-y-4"
              >
                <div>
                  <label htmlFor="student-username" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Tên đăng nhập <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="student-username"
                    value={createUsername}
                    onChange={(e) => setCreateUsername(e.target.value)}
                    required
                    maxLength={100}
                    disabled={createMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="student-password" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Mật khẩu tạm thời (tối thiểu 12 ký tự) <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="password"
                    id="student-password"
                    autoComplete="new-password"
                    value={createPassword}
                    onChange={(e) => setCreatePassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={createMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="student-fullname" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Họ và tên <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="student-fullname"
                    value={createFullName}
                    onChange={(e) => setCreateFullName(e.target.value)}
                    required
                    maxLength={200}
                    disabled={createMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="student-grade" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Khối lớp <span className="text-rose-400">*</span>
                  </label>
                  <select
                    id="student-grade"
                    value={createGradeLevel}
                    onChange={(e) => setCreateGradeLevel(Number(e.target.value) as 10 | 11 | 12)}
                    disabled={createMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  >
                    <option value={10}>Khối 10</option>
                    <option value={11}>Khối 11</option>
                    <option value={12}>Khối 12</option>
                  </select>
                </div>

                {classesData?.data && classesData.data.length > 0 && (
                  <div>
                    <label className="block text-xs font-medium text-[var(--cm-text-secondary)] mb-1">
                      Gán vào lớp học ban đầu
                    </label>
                    <div className="max-h-36 overflow-y-auto rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-2 space-y-1">
                      {classesData.data.map((c) => (
                        <label key={c.classId} className="flex items-center text-xs text-[var(--cm-text)] gap-2 cursor-pointer hover:bg-white/5 p-1 rounded">
                          <input
                            type="checkbox"
                            checked={createClassIds.includes(c.classId)}
                            onChange={(e) => {
                              if (e.target.checked) {
                                setCreateClassIds((prev) => [...prev, c.classId]);
                              } else {
                                setCreateClassIds((prev) => prev.filter((id) => id !== c.classId));
                              }
                            }}
                            className="rounded border-[var(--cm-border)] text-indigo-500"
                          />
                          <span>{c.className} ({c.subject.subjectName})</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}

                <div className="flex justify-end gap-3 pt-4">
                  <button
                    type="button"
                    onClick={() => setIsCreating(false)}
                    disabled={createMutation.isPending}
                    className="cm-secondary-button text-sm"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-create-student"
                    disabled={createMutation.isPending}
                    className="cm-primary-button text-sm"
                  >
                    {createMutation.isPending ? "Đang tạo..." : "Tạo học viên"}
                  </button>
                </div>
              </form>
            </Modal>
          )}

          {/* Student Detail Drawer */}
          {viewingStudentId && (
            <Drawer
              isOpen={!!viewingStudentId}
              title="Hồ sơ chi tiết học viên"
              description="Thông tin tài khoản, danh sách lớp học và hồ sơ năng lực theo môn."
              onClose={() => setViewingStudentId(null)}
              footer={
                <div className="flex justify-end">
                  <button
                    type="button"
                    id="btn-close-student-detail"
                    onClick={() => setViewingStudentId(null)}
                    className="cm-secondary-button text-sm"
                  >
                    Đóng
                  </button>
                </div>
              }
            >
              {isDetailLoading ? (
                <div className="py-12 text-center text-sm text-[var(--cm-text-muted)]">
                  Đang tải thông tin chi tiết học viên...
                </div>
              ) : studentDetail ? (
                <div className="space-y-6 text-sm">
                  {/* Basic info */}
                  <div className="grid grid-cols-2 gap-4 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-4">
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Họ và tên</span>
                      <span className="font-semibold text-[var(--cm-text)]">{studentDetail.fullName}</span>
                    </div>
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Tên đăng nhập</span>
                      <span className="font-mono text-[var(--cm-cyan)]">{studentDetail.username}</span>
                    </div>
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Khối lớp</span>
                      <span className="font-medium text-[var(--cm-text)]">Khối {studentDetail.gradeLevel}</span>
                    </div>
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Trạng thái</span>
                      <StatusBadge
                        status={studentDetail.status}
                        label={STATUS_LABELS[studentDetail.status] || studentDetail.status}
                      />
                    </div>
                    <div className="col-span-2">
                      <span className="block text-xs text-[var(--cm-text-muted)]">Phiên bản dữ liệu (RowVersion)</span>
                      <span className="font-mono text-xs text-[var(--cm-text-secondary)]">{studentDetail.rowVersion}</span>
                    </div>
                  </div>

                  {/* Classes */}
                  <div>
                    <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-secondary)] mb-2">
                      Lớp học đang tham gia ({studentDetail.classes?.length || 0})
                    </h3>
                    {!studentDetail.classes || studentDetail.classes.length === 0 ? (
                      <p className="text-xs italic text-[var(--cm-text-muted)]">Chưa tham gia lớp học nào.</p>
                    ) : (
                      <div className="divide-y divide-[var(--cm-border-subtle)] rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)]">
                        {studentDetail.classes.map((cls) => (
                          <div key={cls.classId} className="flex items-center justify-between p-3 text-xs">
                            <div>
                              <span className="font-medium text-[var(--cm-text)]">{cls.className}</span>
                              <span className="ml-2 text-[var(--cm-text-secondary)]">({cls.subject?.subjectName})</span>
                            </div>
                            <span className="text-[var(--cm-text-muted)]">
                              GV: {cls.teacher?.displayName || "Chưa phân công"}
                            </span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                  {/* Subject Goals (Digital Twin) */}
                  <div>
                    <div className="flex items-center justify-between mb-2">
                      <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-cyan)]">
                        Mục tiêu điểm số môn học (Digital Twin) ({studentDetail.subjectGoals?.length || 0})
                      </h3>
                      {canUpdateTwinScoped && (
                        <button
                          type="button"
                          onClick={() => {
                            setGoalStudentId(studentDetail.studentId);
                            setGoalStudentName(`${studentDetail.fullName} (@${studentDetail.username})`);
                          }}
                          className="cm-secondary-button h-7 px-2 text-xs text-cyan-300 hover:text-cyan-200"
                        >
                          Thiết lập mục tiêu
                        </button>
                      )}
                    </div>

                    {!studentDetail.subjectGoals || studentDetail.subjectGoals.length === 0 ? (
                      <p className="text-xs italic text-[var(--cm-text-muted)]">Chưa thiết lập mục tiêu môn học nào.</p>
                    ) : (
                      <div className="divide-y divide-[var(--cm-border-subtle)] rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)]">
                        {studentDetail.subjectGoals.map((goal) => {
                          const subjectLabel = subjectsMap.get(goal.subjectId) || goal.subjectId;
                          return (
                            <div key={goal.subjectId} className="flex items-center justify-between p-3 text-xs">
                              <div>
                                <span className="font-semibold text-[var(--cm-text)]">
                                  {subjectLabel}
                                </span>
                                <span className="ml-2 text-[var(--cm-text-muted)]">· Còn {goal.remainingDays} ngày</span>
                                <span className="ml-2 text-indigo-300">· Dự báo: {goal.currentPredictedScore}</span>
                              </div>
                              <div className="flex items-center gap-2">
                                <span className="font-bold text-[var(--cm-cyan)]">
                                  {goal.targetScore} / 10
                                </span>
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                </div>
              ) : null}
            </Drawer>
          )}

          {/* Subject Goals Modal */}
          {goalStudentId && canUpdateTwinScoped && (
            <SubjectGoalsModal
              isOpen={!!goalStudentId}
              studentId={goalStudentId}
              studentName={goalStudentName}
              onClose={() => setGoalStudentId(null)}
              onSuccess={() => {
                queryClient.invalidateQueries({ queryKey: ["studentDetail", goalStudentId] });
              }}
            />
          )}

          {/* Edit Student Modal */}
          {editingStudent && canUpdateStudent && (
            <Modal
              isOpen={!!editingStudent}
              title="Chỉnh sửa thông tin học viên"
              description={`Tài khoản: ${editingStudent.username}`}
              onClose={() => setEditingStudent(null)}
            >
              <form onSubmit={handleEditSubmit} className="space-y-4">
                <div>
                  <label htmlFor="edit-student-fullname" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Họ và tên <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="edit-student-fullname"
                    value={editFullName}
                    onChange={(e) => setEditFullName(e.target.value)}
                    required
                    maxLength={200}
                    disabled={updateMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="edit-student-grade" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Khối lớp <span className="text-rose-400">*</span>
                  </label>
                  <select
                    id="edit-student-grade"
                    value={editGradeLevel}
                    onChange={(e) => setEditGradeLevel(Number(e.target.value))}
                    disabled={updateMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  >
                    <option value={10}>Khối 10</option>
                    <option value={11}>Khối 11</option>
                    <option value={12}>Khối 12</option>
                  </select>
                </div>

                <div>
                  <label htmlFor="edit-student-status" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Trạng thái tài khoản
                  </label>
                  <select
                    id="edit-student-status"
                    value={editStatus}
                    onChange={(e) => setEditStatus(e.target.value as UserStatus)}
                    disabled={updateMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  >
                    <option value="Active">Hoạt động</option>
                    <option value="Locked">Bị khóa</option>
                    <option value="Disabled">Vô hiệu hóa</option>
                  </select>
                </div>

                <div className="flex justify-end gap-3 pt-4">
                  <button
                    type="button"
                    onClick={() => setEditingStudent(null)}
                    disabled={updateMutation.isPending}
                    className="cm-secondary-button text-sm"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-save-edit-student"
                    disabled={updateMutation.isPending}
                    className="cm-primary-button text-sm"
                  >
                    {updateMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </Modal>
          )}

          {/* Reset Password Modal */}
          {resetStudent && canResetPassword && (
            <Modal
              isOpen={!!resetStudent}
              title="Đặt lại mật khẩu học viên"
              description={`Học viên: ${resetStudent.fullName} (@${resetStudent.username})`}
              onClose={() => setResetStudent(null)}
            >
              <div className="mb-4 rounded-xl border border-amber-400/30 bg-amber-400/10 p-3 text-xs text-amber-200">
                Lưu ý an toàn: Đặt lại mật khẩu sẽ lập tức thu hồi mọi phiên đăng nhập và refresh token đang hoạt động của học viên.
              </div>

              <form onSubmit={handleResetPasswordSubmit} className="space-y-4">
                <div>
                  <label htmlFor="reset-student-new-password" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Mật khẩu mới (tối thiểu 12 ký tự) <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="password"
                    id="reset-student-new-password"
                    autoComplete="new-password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={resetPasswordMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="reset-student-confirm-password" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Xác nhận mật khẩu mới <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="password"
                    id="reset-student-confirm-password"
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={resetPasswordMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="reset-student-reason" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Lý do đặt lại mật khẩu <span className="text-rose-400">*</span>
                  </label>
                  <textarea
                    id="reset-student-reason"
                    rows={3}
                    value={resetReason}
                    onChange={(e) => setResetReason(e.target.value)}
                    required
                    minLength={5}
                    maxLength={500}
                    placeholder="Ví dụ: Học viên yêu cầu cấp lại mật khẩu do quên"
                    disabled={resetPasswordMutation.isPending}
                    className="cm-field mt-1 w-full p-3 text-sm"
                  />
                </div>

                <div className="flex justify-end gap-3 pt-4">
                  <button
                    type="button"
                    onClick={() => setResetStudent(null)}
                    disabled={resetPasswordMutation.isPending}
                    className="cm-secondary-button text-sm"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-reset-student-password"
                    disabled={resetPasswordMutation.isPending}
                    className="cm-primary-button text-sm"
                  >
                    {resetPasswordMutation.isPending ? "Đang xử lý..." : "Xác nhận đặt lại"}
                  </button>
                </div>
              </form>
            </Modal>
          )}

          {/* Delete Confirmation Modal */}
          {deletingStudent && canDeleteStudent && (
            <Modal
              isOpen={!!deletingStudent}
              title="Xóa học viên"
              onClose={() => setDeletingStudent(null)}
            >
              <div className="space-y-4 text-sm">
                <p className="text-[var(--cm-text-secondary)]">
                  Bạn có chắc chắn muốn xóa học viên{" "}
                  <span className="font-semibold text-[var(--cm-text)]">{deletingStudent.fullName}</span> (
                  {deletingStudent.username})?
                </p>

                <div className="rounded-xl border border-cyan-400/30 bg-cyan-400/10 p-3 text-xs text-cyan-200 space-y-1">
                  <p className="font-bold">BẢO TOÀN DỮ LIỆU HỌC TẬP (100% EVIDENCE PRESERVATION):</p>
                  <p>• Tài khoản sẽ được chuyển sang trạng thái đã xóa và vô hiệu hóa.</p>
                  <p>• Học viên sẽ được gỡ khỏi danh sách thành viên các lớp học đang hoạt động.</p>
                  <p>• Mọi lịch sử bài tập, bằng chứng năng lực, điểm số và Digital Twin được giữ nguyên vẹn để phục vụ báo cáo.</p>
                </div>

                <div className="flex justify-end gap-3 pt-2">
                  <button
                    type="button"
                    onClick={() => setDeletingStudent(null)}
                    disabled={deleteMutation.isPending}
                    className="cm-secondary-button text-sm"
                  >
                    Hủy
                  </button>
                  <button
                    type="button"
                    id="btn-confirm-delete-student"
                    onClick={handleDeleteSubmit}
                    disabled={deleteMutation.isPending}
                    className="cm-danger-button text-sm"
                  >
                    {deleteMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
                  </button>
                </div>
              </div>
            </Modal>
          )}
        </div>
      </div>
    </CenterManagerThemeScope>
  );
};

/**
 * Legacy StudentListPage component preserved for non-CenterManager users
 */
const LegacyStudentListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreateStudent = hasPermission(permissions.studentsCreate);
  const canUpdateStudent = hasPermission(permissions.studentsUpdate);
  const canDeleteStudent = hasPermission(permissions.studentsDelete);
  const canResetPassword = hasPermission(permissions.studentsResetPassword);

  const [page, setPage] = useState<number>(1);
  const pageSize = 20;

  const [search, setSearch] = useState<string>("");
  const [status, setStatus] = useState<UserStatus | "">("");
  const [gradeLevel, setGradeLevel] = useState<number | "">("");

  const [searchInput, setSearchInput] = useState<string>("");
  const [statusInput, setStatusInput] = useState<UserStatus | "">("");
  const [gradeLevelInput, setGradeLevelInput] = useState<number | "">("");

  const [isCreating, setIsCreating] = useState(false);
  const [createUsername, setCreateUsername] = useState("");
  const [createPassword, setCreatePassword] = useState("");
  const [createFullName, setCreateFullName] = useState("");
  const [createGradeLevel, setCreateGradeLevel] = useState<10 | 11 | 12>(10);
  const [createClassIds, setCreateClassIds] = useState<string[]>([]);

  const [viewingStudentId, setViewingStudentId] = useState<string | null>(null);

  const [editingStudent, setEditingStudent] = useState<StudentDto | null>(null);
  const [editFullName, setEditFullName] = useState("");
  const [editGradeLevel, setEditGradeLevel] = useState<number>(10);
  const [editStatus, setEditStatus] = useState<UserStatus>("Active");

  const [resetStudent, setResetStudent] = useState<StudentDto | null>(null);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [resetReason, setResetReason] = useState("");

  const [deletingStudent, setDeletingStudent] = useState<StudentDto | null>(null);

  const [feedback, setFeedback] = useState<{ type: "success" | "error" | "conflict"; message: string } | null>(null);

  const showFeedback = (type: "success" | "error" | "conflict", message: string) => {
    setFeedback({ type, message });
    if (type === "success") {
      setTimeout(() => setFeedback(null), 5000);
    }
  };

  const queryParams: StudentListParams = {
    page,
    pageSize,
    search: search.trim() !== "" ? search.trim() : undefined,
    status: status !== "" ? status : undefined,
    gradeLevel: gradeLevel !== "" ? gradeLevel : undefined,
  };

  const { data, isLoading, isFetching, isError: isListError, refetch } = useQuery({
    queryKey: ["students", queryParams.page, queryParams.pageSize, queryParams.search, queryParams.status, queryParams.gradeLevel],
    queryFn: () => organizationApi.listStudents(queryParams),
  });

  const { data: classesData } = useQuery({
    queryKey: ["classes", "active-for-student-create"],
    queryFn: () => organizationApi.listClasses({ page: 1, pageSize: 100, status: "Active" }),
    enabled: isCreating,
  });

  const { data: legacySubjectsData } = useQuery({
    queryKey: ["subjects", "for-legacy-student-detail"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: !!viewingStudentId,
  });

  const legacySubjectsMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const sub of legacySubjectsData?.data ?? []) {
      map.set(sub.subjectId, sub.subjectName);
    }
    return map;
  }, [legacySubjectsData?.data]);

  const { data: studentDetail, isLoading: isDetailLoading } = useQuery<StudentDetailDto>({
    queryKey: ["studentDetail", viewingStudentId],
    queryFn: () => organizationApi.getStudent(viewingStudentId!),
    enabled: !!viewingStudentId,
  });

  const createMutation = useMutation({
    mutationFn: async (request: CreateStudentRequest) => {
      try {
        return await organizationApi.createStudent(request);
      } finally {
        request.temporaryPassword = "";
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["students"] });
      setIsCreating(false);
      setCreateUsername("");
      setCreatePassword("");
      setCreateFullName("");
      setCreateGradeLevel(10);
      setCreateClassIds([]);
      showFeedback("success", "Đã tạo tài khoản học viên thành công.");
    },
    onError: (error) => {
      const details = extractProblemDetails(error);
      if (details.errorCode === "DUPLICATE_RESOURCE") {
        showFeedback("error", "Tên đăng nhập đã tồn tại trong trung tâm.");
      } else {
        showFeedback("error", details.message || "Không thể tạo học viên.");
      }
    },
  });

  const updateMutation = useMutation({
    mutationFn: async ({ studentId, request }: { studentId: string; request: UpdateStudentRequest }) => {
      return await organizationApi.updateStudent(studentId, request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["students"] });
      setEditingStudent(null);
      showFeedback("success", "Đã cập nhật thông tin học viên thành công.");
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) {
        showFeedback("conflict", "Dữ liệu học viên đã bị thay đổi bởi phiên làm việc khác. Đang tải lại dữ liệu mới nhất...");
        refetch();
      } else {
        const details = extractProblemDetails(error);
        showFeedback("error", details.message || "Không thể cập nhật học viên.");
      }
    },
  });

  const resetPasswordMutation = useMutation({
    mutationFn: async ({ studentId, request }: { studentId: string; request: ResetAccountPasswordRequest }) => {
      try {
        return await organizationApi.resetStudentPassword(studentId, request);
      } finally {
        request.newPassword = "";
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["students"] });
      setResetStudent(null);
      setNewPassword("");
      setConfirmPassword("");
      setResetReason("");
      showFeedback("success", "Đã đặt lại mật khẩu học viên thành công. Mọi phiên đăng nhập cũ đã được thu hồi.");
    },
    onError: (error) => {
      if (isConcurrencyConflict(error)) {
        showFeedback("conflict", "Dữ liệu người dùng đã bị thay đổi bởi phiên làm việc khác. Vui lòng tải lại.");
      } else {
        const details = extractProblemDetails(error);
        showFeedback("error", details.message || "Không thể đặt lại mật khẩu học viên.");
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (studentId: string) => {
      return await organizationApi.deleteStudent(studentId);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["students"] });
      setDeletingStudent(null);
      showFeedback("success", "Đã xóa học viên thành công. Toàn bộ bằng chứng học tập và Digital Twin được bảo toàn.");
    },
    onError: (error) => {
      const details = extractProblemDetails(error);
      showFeedback("error", details.message || "Không thể xóa học viên. Vui lòng thử lại.");
    },
  });

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput);
    setStatus(statusInput);
    setGradeLevel(gradeLevelInput);
    setPage(1);
  };

  const openEditModal = (student: StudentDto) => {
    setEditingStudent(student);
    setEditFullName(student.fullName);
    setEditGradeLevel(student.gradeLevel);
    setEditStatus(student.status);
    setFeedback(null);
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingStudent) return;

    const trimmed = editFullName.trim();
    if (!trimmed) {
      showFeedback("error", "Họ tên học viên không được để trống.");
      return;
    }

    updateMutation.mutate({
      studentId: editingStudent.studentId,
      request: {
        fullName: trimmed,
        gradeLevel: editGradeLevel,
        status: editStatus,
        rowVersion: editingStudent.rowVersion,
      },
    });
  };

  const openResetPasswordModal = (student: StudentDto) => {
    setResetStudent(student);
    setNewPassword("");
    setConfirmPassword("");
    setResetReason("");
    setFeedback(null);
  };

  const handleResetPasswordSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!resetStudent) return;

    if (newPassword.length < 12 || newPassword.length > 200) {
      showFeedback("error", "Mật khẩu mới phải từ 12 đến 200 ký tự.");
      return;
    }

    if (newPassword !== confirmPassword) {
      showFeedback("error", "Mật khẩu xác nhận không khớp.");
      return;
    }

    if (resetReason.trim().length < 5 || resetReason.trim().length > 500) {
      showFeedback("error", "Lý do đặt lại mật khẩu là bắt buộc (từ 5 đến 500 ký tự).");
      return;
    }

    resetPasswordMutation.mutate({
      studentId: resetStudent.studentId,
      request: {
        newPassword,
        expectedUserRowVersion: resetStudent.rowVersion,
        reason: resetReason.trim(),
      },
    });
  };

  const openDeleteModal = (student: StudentDto) => {
    setDeletingStudent(student);
    setFeedback(null);
  };

  const handleDeleteSubmit = () => {
    if (!deletingStudent) return;
    deleteMutation.mutate(deletingStudent.studentId);
  };

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold leading-7 text-gray-900 sm:truncate sm:text-3xl sm:tracking-tight">
              Quản lý Học viên
            </h1>
            <p className="mt-1 text-sm text-gray-500">
              Quản lý hồ sơ học viên, mục tiêu học tập theo môn và bảo mật tài khoản.
            </p>
          </div>
          <div className="flex gap-3">
            {canCreateStudent && (
              <button
                type="button"
                id="btn-create-student"
                onClick={() => {
                  setIsCreating(true);
                  setFeedback(null);
                }}
                className="inline-flex items-center rounded-md bg-indigo-600 px-3.5 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
              >
                + Thêm học viên
              </button>
            )}
            <Link
              to="/"
              className="inline-flex items-center rounded-md bg-white px-3.5 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
            >
              Về trang chủ
            </Link>
          </div>
        </div>

        {feedback && (
          <div
            id="student-feedback-alert"
            role="alert"
            className={`mb-6 rounded-md p-4 flex items-start justify-between ${
              feedback.type === "success"
                ? "bg-green-50 text-green-800 border border-green-200"
                : feedback.type === "conflict"
                ? "bg-amber-50 text-amber-800 border border-amber-200"
                : "bg-red-50 text-red-800 border border-red-200"
            }`}
          >
            <div className="text-sm font-medium">{feedback.message}</div>
            <button
              type="button"
              onClick={() => setFeedback(null)}
              className="ml-4 text-gray-400 hover:text-gray-600"
              aria-label="Đóng thông báo"
            >
              ×
            </button>
          </div>
        )}

        {isCreating && canCreateStudent && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="modal-create-student-title">
            <div className="bg-white rounded-lg shadow-xl max-w-lg w-full p-6">
              <h2 id="modal-create-student-title" className="text-lg font-bold text-gray-900 mb-4">
                Thêm học viên mới
              </h2>
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  createMutation.mutate({
                    username: createUsername.trim(),
                    temporaryPassword: createPassword,
                    fullName: createFullName.trim(),
                    gradeLevel: createGradeLevel,
                    classIds: createClassIds,
                  });
                }}
                className="space-y-4"
              >
                <div>
                  <label htmlFor="student-username" className="block text-sm font-medium text-gray-700">
                    Tên đăng nhập <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="student-username"
                    value={createUsername}
                    onChange={(e) => setCreateUsername(e.target.value)}
                    required
                    maxLength={100}
                    disabled={createMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="student-password" className="block text-sm font-medium text-gray-700">
                    Mật khẩu tạm thời (tối thiểu 12 ký tự) <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="password"
                    id="student-password"
                    autoComplete="new-password"
                    value={createPassword}
                    onChange={(e) => setCreatePassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={createMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="student-fullname" className="block text-sm font-medium text-gray-700">
                    Họ và tên <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="student-fullname"
                    value={createFullName}
                    onChange={(e) => setCreateFullName(e.target.value)}
                    required
                    maxLength={200}
                    disabled={createMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="student-grade" className="block text-sm font-medium text-gray-700">
                    Khối lớp <span className="text-red-500">*</span>
                  </label>
                  <select
                    id="student-grade"
                    value={createGradeLevel}
                    onChange={(e) => setCreateGradeLevel(Number(e.target.value) as 10 | 11 | 12)}
                    disabled={createMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  >
                    <option value={10}>Khối 10</option>
                    <option value={11}>Khối 11</option>
                    <option value={12}>Khối 12</option>
                  </select>
                </div>

                {classesData?.data && classesData.data.length > 0 && (
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">Gán vào lớp học ban đầu</label>
                    <div className="max-h-36 overflow-y-auto border border-gray-200 rounded-md p-2 space-y-1">
                      {classesData.data.map((c) => (
                        <label key={c.classId} className="flex items-center text-xs text-gray-700 gap-2 cursor-pointer hover:bg-gray-50 p-1 rounded">
                          <input
                            type="checkbox"
                            checked={createClassIds.includes(c.classId)}
                            onChange={(e) => {
                              if (e.target.checked) {
                                setCreateClassIds((prev) => [...prev, c.classId]);
                              } else {
                                setCreateClassIds((prev) => prev.filter((id) => id !== c.classId));
                              }
                            }}
                            className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                          />
                          <span>{c.className} ({c.subject.subjectName})</span>
                        </label>
                      ))}
                    </div>
                  </div>
                )}

                <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
                  <button
                    type="button"
                    onClick={() => setIsCreating(false)}
                    disabled={createMutation.isPending}
                    className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-create-student"
                    disabled={createMutation.isPending}
                    className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {createMutation.isPending ? "Đang tạo..." : "Tạo học viên"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {viewingStudentId && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="modal-student-detail-title">
            <div className="bg-white rounded-lg shadow-xl max-w-2xl w-full p-6">
              <div className="flex justify-between items-start border-b border-gray-200 pb-3 mb-4">
                <h2 id="modal-student-detail-title" className="text-lg font-bold text-gray-900">
                  Hồ sơ học viên
                </h2>
                <button
                  type="button"
                  id="btn-close-student-detail"
                  onClick={() => setViewingStudentId(null)}
                  className="text-gray-400 hover:text-gray-600 text-xl font-bold"
                >
                  ×
                </button>
              </div>

              {isDetailLoading ? (
                <div className="py-8 text-center text-sm text-gray-500">Đang tải thông tin chi tiết...</div>
              ) : studentDetail ? (
                <div className="space-y-6">
                  <div className="grid grid-cols-2 gap-4 bg-gray-50 p-4 rounded-md">
                    <div>
                      <span className="text-xs text-gray-500 block">Họ và tên</span>
                      <span className="font-semibold text-gray-900">{studentDetail.fullName}</span>
                    </div>
                    <div>
                      <span className="text-xs text-gray-500 block">Tên đăng nhập</span>
                      <span className="font-mono text-gray-900">{studentDetail.username}</span>
                    </div>
                    <div>
                      <span className="text-xs text-gray-500 block">Khối lớp</span>
                      <span className="text-gray-900 font-medium">Khối {studentDetail.gradeLevel}</span>
                    </div>
                    <div>
                      <span className="text-xs text-gray-500 block">Trạng thái</span>
                      <span
                        className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${
                          studentDetail.status === "Active"
                            ? "bg-green-50 text-green-700 ring-green-600/20"
                            : studentDetail.status === "Locked"
                            ? "bg-yellow-50 text-yellow-800 ring-yellow-600/20"
                            : "bg-red-50 text-red-700 ring-red-600/10"
                        }`}
                      >
                        {STATUS_LABELS[studentDetail.status] || studentDetail.status}
                      </span>
                    </div>
                  </div>

                  <div>
                    <h3 className="text-sm font-semibold text-gray-900 mb-2">
                      Lớp học đang tham gia ({studentDetail.classes?.length || 0})
                    </h3>
                    {!studentDetail.classes || studentDetail.classes.length === 0 ? (
                      <p className="text-xs text-gray-500 italic">Chưa tham gia lớp học nào.</p>
                    ) : (
                      <div className="border border-gray-200 rounded-md overflow-hidden">
                        <table className="min-w-full divide-y divide-gray-200 text-xs">
                          <thead className="bg-gray-50">
                            <tr>
                              <th className="px-3 py-2 text-left font-medium text-gray-700">Tên lớp</th>
                              <th className="px-3 py-2 text-left font-medium text-gray-700">Môn học</th>
                              <th className="px-3 py-2 text-left font-medium text-gray-700">Giáo viên</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-gray-200">
                            {studentDetail.classes.map((cls) => (
                              <tr key={cls.classId}>
                                <td className="px-3 py-2 font-medium text-gray-900">{cls.className}</td>
                                <td className="px-3 py-2 text-gray-600">{cls.subject?.subjectName}</td>
                                <td className="px-3 py-2 text-gray-600">{cls.teacher?.displayName}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>

                  <div>
                    <h3 className="text-sm font-semibold text-gray-900 mb-2">
                      Mục tiêu điểm số theo môn ({studentDetail.subjectGoals?.length || 0})
                    </h3>
                    {!studentDetail.subjectGoals || studentDetail.subjectGoals.length === 0 ? (
                      <p className="text-xs text-gray-500 italic">Chưa thiết lập mục tiêu môn học nào.</p>
                    ) : (
                      <div className="border border-gray-200 rounded-md overflow-hidden">
                        <table className="min-w-full divide-y divide-gray-200 text-xs">
                          <thead className="bg-gray-50">
                            <tr>
                              <th className="px-3 py-2 text-left font-medium text-gray-700">Môn học</th>
                              <th className="px-3 py-2 text-left font-medium text-gray-700">Mục tiêu</th>
                              <th className="px-3 py-2 text-left font-medium text-gray-700">Thời hạn</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-gray-200">
                            {studentDetail.subjectGoals.map((goal) => (
                              <tr key={goal.subjectId}>
                                <td className="px-3 py-2 font-medium text-gray-900">
                                  {legacySubjectsMap.get(goal.subjectId) || goal.subjectId}
                                </td>
                                <td className="px-3 py-2 font-bold text-indigo-600">{goal.targetScore} đ</td>
                                <td className="px-3 py-2 text-gray-500">Còn {goal.remainingDays} ngày</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>
                </div>
              ) : null}

              <div className="flex justify-end pt-4 mt-4 border-t border-gray-200">
                <button
                  type="button"
                  onClick={() => setViewingStudentId(null)}
                  className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        )}

        {editingStudent && canUpdateStudent && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="modal-edit-student-title">
            <div className="bg-white rounded-lg shadow-xl max-w-lg w-full p-6">
              <h2 id="modal-edit-student-title" className="text-lg font-bold text-gray-900 mb-2">
                Chỉnh sửa thông tin học viên
              </h2>
              <p className="text-xs text-gray-500 mb-4">
                Tài khoản: <span className="font-semibold text-gray-700">{editingStudent.username}</span>
              </p>

              <form onSubmit={handleEditSubmit} className="space-y-4">
                <div>
                  <label htmlFor="edit-student-fullname" className="block text-sm font-medium text-gray-700">
                    Họ và tên <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="edit-student-fullname"
                    value={editFullName}
                    onChange={(e) => setEditFullName(e.target.value)}
                    required
                    maxLength={200}
                    disabled={updateMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="edit-student-grade" className="block text-sm font-medium text-gray-700">
                    Khối lớp <span className="text-red-500">*</span>
                  </label>
                  <select
                    id="edit-student-grade"
                    value={editGradeLevel}
                    onChange={(e) => setEditGradeLevel(Number(e.target.value))}
                    disabled={updateMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  >
                    <option value={10}>Khối 10</option>
                    <option value={11}>Khối 11</option>
                    <option value={12}>Khối 12</option>
                  </select>
                </div>

                <div>
                  <label htmlFor="edit-student-status" className="block text-sm font-medium text-gray-700">
                    Trạng thái tài khoản
                  </label>
                  <select
                    id="edit-student-status"
                    value={editStatus}
                    onChange={(e) => setEditStatus(e.target.value as UserStatus)}
                    disabled={updateMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  >
                    <option value="Active">Hoạt động</option>
                    <option value="Locked">Bị khóa</option>
                    <option value="Disabled">Vô hiệu hóa</option>
                  </select>
                </div>

                <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
                  <button
                    type="button"
                    onClick={() => setEditingStudent(null)}
                    disabled={updateMutation.isPending}
                    className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-save-edit-student"
                    disabled={updateMutation.isPending}
                    className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {updateMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {resetStudent && canResetPassword && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="modal-reset-student-password-title">
            <div className="bg-white rounded-lg shadow-xl max-w-lg w-full p-6">
              <h2 id="modal-reset-student-password-title" className="text-lg font-bold text-gray-900 mb-2">
                Đặt lại mật khẩu học viên
              </h2>
              <p className="text-sm text-gray-600 mb-4">
                Học viên: <span className="font-semibold text-gray-900">{resetStudent.fullName}</span> ({resetStudent.username})
              </p>

              <div className="rounded-md bg-amber-50 p-3 mb-4 text-xs text-amber-800 border border-amber-200">
                Lưu ý an toàn: Đặt lại mật khẩu sẽ lập tức thu hồi mọi phiên đăng nhập và refresh token đang hoạt động của học viên.
              </div>

              <form onSubmit={handleResetPasswordSubmit} className="space-y-4">
                <div>
                  <label htmlFor="reset-student-new-password" className="block text-sm font-medium text-gray-700">
                    Mật khẩu mới (tối thiểu 12 ký tự) <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="password"
                    id="reset-student-new-password"
                    autoComplete="new-password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={resetPasswordMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="reset-student-confirm-password" className="block text-sm font-medium text-gray-700">
                    Xác nhận mật khẩu mới <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="password"
                    id="reset-student-confirm-password"
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={resetPasswordMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div>
                  <label htmlFor="reset-student-reason" className="block text-sm font-medium text-gray-700">
                    Lý do đặt lại mật khẩu <span className="text-red-500">*</span>
                  </label>
                  <textarea
                    id="reset-student-reason"
                    rows={3}
                    value={resetReason}
                    onChange={(e) => setResetReason(e.target.value)}
                    required
                    minLength={5}
                    maxLength={500}
                    placeholder="Ví dụ: Học viên yêu cầu cấp lại mật khẩu do quên thông tin đăng nhập"
                    disabled={resetPasswordMutation.isPending}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>

                <div className="flex justify-end gap-3 pt-4 border-t border-gray-200">
                  <button
                    type="button"
                    onClick={() => setResetStudent(null)}
                    disabled={resetPasswordMutation.isPending}
                    className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-reset-student-password"
                    disabled={resetPasswordMutation.isPending}
                    className="rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-amber-500 disabled:opacity-50"
                  >
                    {resetPasswordMutation.isPending ? "Đang xử lý..." : "Xác nhận đặt lại mật khẩu"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {deletingStudent && canDeleteStudent && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black bg-opacity-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-labelledby="modal-delete-student-title">
            <div className="bg-white rounded-lg shadow-xl max-w-md w-full p-6">
              <h2 id="modal-delete-student-title" className="text-lg font-bold text-gray-900 mb-2">
                Xóa học viên
              </h2>
              <p className="text-sm text-gray-600 mb-4">
                Bạn có chắc chắn muốn xóa học viên <span className="font-semibold text-gray-900">{deletingStudent.fullName}</span> ({deletingStudent.username})?
              </p>

              <div className="rounded-md bg-blue-50 p-4 mb-4 text-xs text-blue-800 border border-blue-200 space-y-1.5">
                <p className="font-bold">BẢO TOÀN DỮ LIỆU HỌC TẬP (100% EVIDENCE PRESERVATION):</p>
                <p>• Tài khoản sẽ được chuyển sang trạng thái đã xóa và vô hiệu hóa.</p>
                <p>• Học viên sẽ được gỡ khỏi danh sách thành viên các lớp học đang hoạt động.</p>
                <p>• Mọi lịch sử bài tập, bằng chứng năng lực, điểm số và Digital Twin được giữ nguyên vẹn để phục vụ báo cáo trung tâm.</p>
              </div>

              <div className="flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setDeletingStudent(null)}
                  disabled={deleteMutation.isPending}
                  className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 shadow-sm hover:bg-gray-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  id="btn-confirm-delete-student"
                  onClick={handleDeleteSubmit}
                  disabled={deleteMutation.isPending}
                  className="rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-red-500 disabled:opacity-50"
                >
                  {deleteMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
                </button>
              </div>
            </div>
          </div>
        )}

        <div className="mb-8 overflow-hidden rounded-lg bg-white shadow">
          <div className="p-6">
            <form onSubmit={handleSearch} className="flex flex-col gap-4 sm:flex-row sm:items-end">
              <div className="w-full sm:max-w-xs">
                <label htmlFor="search-student" className="block text-sm font-medium leading-6 text-gray-900">
                  Tìm kiếm
                </label>
                <div className="mt-1">
                  <input
                    type="text"
                    id="search-student"
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    placeholder="Tên đăng nhập hoặc họ tên"
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-gray-900 shadow-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 sm:text-sm"
                  />
                </div>
              </div>

              <div className="w-full sm:max-w-xs">
                <label htmlFor="filter-grade" className="block text-sm font-medium leading-6 text-gray-900">
                  Khối lớp
                </label>
                <div className="mt-1">
                  <select
                    id="filter-grade"
                    value={gradeLevelInput}
                    onChange={(e) => setGradeLevelInput(e.target.value === "" ? "" : Number(e.target.value))}
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-gray-900 shadow-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 sm:text-sm"
                  >
                    <option value="">Tất cả khối</option>
                    <option value="10">Khối 10</option>
                    <option value="11">Khối 11</option>
                    <option value="12">Khối 12</option>
                  </select>
                </div>
              </div>

              <div className="w-full sm:max-w-xs">
                <label htmlFor="filter-status" className="block text-sm font-medium leading-6 text-gray-900">
                  Trạng thái
                </label>
                <div className="mt-1">
                  <select
                    id="filter-status"
                    value={statusInput}
                    onChange={(e) => setStatusInput(e.target.value as UserStatus | "")}
                    className="block w-full rounded-md border border-gray-300 px-3 py-1.5 text-gray-900 shadow-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 sm:text-sm"
                  >
                    <option value="">Tất cả trạng thái</option>
                    <option value="Active">Hoạt động</option>
                    <option value="Locked">Bị khóa</option>
                    <option value="Disabled">Vô hiệu hóa</option>
                  </select>
                </div>
              </div>

              <div>
                <button
                  type="submit"
                  id="btn-search-students"
                  disabled={isFetching}
                  className="inline-flex w-full items-center justify-center rounded-md bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-600 disabled:opacity-50 sm:w-auto"
                >
                  Tìm kiếm
                </button>
              </div>
            </form>
          </div>
        </div>

        {isListError && (
          <div className="mb-6 rounded-md bg-red-50 p-4 border border-red-200" role="alert">
            <h3 className="text-sm font-medium text-red-800">Không thể tải danh sách học viên</h3>
            <p className="mt-1 text-sm text-red-700">Vui lòng thử lại sau hoặc làm mới trang.</p>
          </div>
        )}

        <div className="overflow-hidden bg-white shadow sm:rounded-lg">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-300">
              <thead className="bg-gray-50">
                <tr>
                  <th scope="col" className="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6">
                    Tên đăng nhập
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Họ và tên
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Khối lớp
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Số lớp
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Trạng thái
                  </th>
                  <th scope="col" className="relative py-3.5 pl-3 pr-4 sm:pr-6 text-right text-sm font-semibold text-gray-900">
                    Thao tác
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {isLoading ? (
                  <tr>
                    <td colSpan={6} className="py-10 text-center text-sm text-gray-500">
                      Đang tải danh sách học viên...
                    </td>
                  </tr>
                ) : !data || data.data.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-10 text-center text-sm text-gray-500">
                      Không tìm thấy học viên nào phù hợp.
                    </td>
                  </tr>
                ) : (
                  data.data.map((student) => (
                    <tr key={student.studentId} className="hover:bg-gray-50">
                      <td className="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-gray-900 sm:pl-6">
                        {student.username}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-700">
                        {student.fullName}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        Khối {student.gradeLevel}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        {student.activeClassCount}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm">
                        <span
                          className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset ${
                            student.status === "Active"
                              ? "bg-green-50 text-green-700 ring-green-600/20"
                              : student.status === "Locked"
                              ? "bg-yellow-50 text-yellow-800 ring-yellow-600/20"
                              : "bg-red-50 text-red-700 ring-red-600/10"
                          }`}
                        >
                          {STATUS_LABELS[student.status] || student.status}
                        </span>
                      </td>
                      <td className="whitespace-nowrap py-4 pl-3 pr-4 sm:pr-6 text-right text-sm font-medium space-x-2">
                        <button
                          type="button"
                          id={`btn-view-student-${student.studentId}`}
                          onClick={() => setViewingStudentId(student.studentId)}
                          className="text-blue-600 hover:text-blue-900 text-xs font-semibold px-2 py-1 rounded hover:bg-blue-50"
                        >
                          Chi tiết
                        </button>
                        {canUpdateStudent && (
                          <button
                            type="button"
                            id={`btn-edit-student-${student.studentId}`}
                            onClick={() => openEditModal(student)}
                            className="text-indigo-600 hover:text-indigo-900 text-xs font-semibold px-2 py-1 rounded hover:bg-indigo-50"
                          >
                            Sửa
                          </button>
                        )}
                        {canResetPassword && (
                          <button
                            type="button"
                            id={`btn-reset-password-student-${student.studentId}`}
                            onClick={() => openResetPasswordModal(student)}
                            className="text-amber-600 hover:text-amber-900 text-xs font-semibold px-2 py-1 rounded hover:bg-amber-50"
                          >
                            Đổi mật khẩu
                          </button>
                        )}
                        {canDeleteStudent && (
                          <button
                            type="button"
                            id={`btn-delete-student-${student.studentId}`}
                            onClick={() => openDeleteModal(student)}
                            className="text-red-600 hover:text-red-900 text-xs font-semibold px-2 py-1 rounded hover:bg-red-50"
                          >
                            Xóa
                          </button>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {data?.meta && data.meta.totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-gray-200 bg-white px-4 py-3 sm:px-6">
              <div className="hidden sm:flex sm:flex-1 sm:items-center sm:justify-between">
                <div>
                  <p className="text-sm text-gray-700">
                    Trang <span className="font-medium">{data.meta.page}</span> /{" "}
                    <span className="font-medium">{data.meta.totalPages}</span> (Tổng cộng{" "}
                    <span className="font-medium">{data.meta.totalItems}</span> học viên)
                  </p>
                </div>
                <div>
                  <nav className="isolate inline-flex -space-x-px rounded-md shadow-sm" aria-label="Phân trang">
                    <button
                      type="button"
                      id="btn-prev-page-students"
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                      disabled={page === 1 || isFetching}
                      className="relative inline-flex items-center rounded-l-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 disabled:opacity-50"
                    >
                      ‹
                    </button>
                    <button
                      type="button"
                      id="btn-next-page-students"
                      onClick={() => setPage((p) => Math.min(data.meta.totalPages, p + 1))}
                      disabled={page === data.meta.totalPages || isFetching}
                      className="relative inline-flex items-center rounded-r-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 disabled:opacity-50"
                    >
                      ›
                    </button>
                  </nav>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export const StudentListPage: React.FC = () => {
  const user = useAuthStore((state) => state.user);
  return user?.accountType === "CenterManager" ? (
    <CenterManagerStudentListView />
  ) : (
    <LegacyStudentListPage />
  );
};
