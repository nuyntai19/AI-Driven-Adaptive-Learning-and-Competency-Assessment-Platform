import React, { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { organizationApi } from "../api/organizationApi";
import type {
  ClassListParams,
  ClassStatus,
  ClassDto,
  CreateClassRequest,
  UpdateClassRequest,
  StudentDto,
} from "../types/organization";
import { useAuthStore } from "../stores/authStore";
import { extractProblemDetails, isConcurrencyConflict, mapSafeOperationalError } from "../utils/problemDetails";
import {
  evaluateClassCapabilities,
  toggleStudentSelection,
  mergePageSelection,
  unmergePageSelection,
  getCandidateListState,
} from "./classListHelpers";
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
} from "../components/centerManager";

const STATUS_LABELS: Record<ClassStatus, string> = {
  Active: "Hoạt động",
  Archived: "Đã lưu trữ",
};

/**
 * Modern CenterManager Dark Enterprise SaaS view for ClassListPage
 */
const CenterManagerClassListView: React.FC = () => {
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);

  const {
    canCreateClass,
    canUpdateClass,
    canAddMembers,
    canRemoveMembers,
    canViewDashboard,
  } = evaluateClassCapabilities(user);

  // Primary list state
  const [page, setPage] = useState<number>(1);
  const pageSize = 10;
  const [status, setStatus] = useState<ClassStatus | "">("");
  const [statusInput, setStatusInput] = useState<ClassStatus | "">("");

  // Feedback notifications
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

  // Create form state
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [className, setClassName] = useState("");
  const [academicYear, setAcademicYear] = useState("");
  const [subjectId, setSubjectId] = useState("");
  const [teacherId, setTeacherId] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);

  // Edit form state
  const [editingClass, setEditingClass] = useState<ClassDto | null>(null);
  const [editClassName, setEditClassName] = useState("");
  const [editTeacherId, setEditTeacherId] = useState("");
  const [editStatus, setEditStatus] = useState<ClassStatus>("Active");
  const [editError, setEditError] = useState<string | null>(null);

  // Detail modal/drawer state
  const [viewingClassId, setViewingClassId] = useState<string | null>(null);
  const [memberPage, setMemberPage] = useState<number>(1);
  const [memberSearchInput, setMemberSearchInput] = useState("");
  const [memberSearch, setMemberSearch] = useState("");

  // Add students modal state (SQL anti-join candidate list)
  const [isAddStudentsModalOpen, setIsAddStudentsModalOpen] = useState(false);
  const [selectedStudentIds, setSelectedStudentIds] = useState<string[]>([]);
  const [candidatePage, setCandidatePage] = useState<number>(1);
  const [candidateSearchInput, setCandidateSearchInput] = useState("");
  const [candidateSearch, setCandidateSearch] = useState("");
  const [addStudentsError, setAddStudentsError] = useState<string | null>(null);

  // Remove student confirmation modal state
  const [removingStudent, setRemovingStudent] = useState<{
    studentId: string;
    fullName: string;
  } | null>(null);
  const [removeError, setRemoveError] = useState<string | null>(null);

  // Primary list query
  const queryParams: ClassListParams = {
    page,
    pageSize,
    status: status !== "" ? status : undefined,
  };

  const { data, isLoading, isFetching, isError, error: listError, refetch } = useQuery({
    queryKey: ["classes", queryParams.page, queryParams.pageSize, queryParams.status],
    queryFn: () => organizationApi.listClasses(queryParams),
  });

  // Subjects query for create modal
  const { data: subjectsData, isLoading: isLoadingSubjects } = useQuery({
    queryKey: ["subjects", "active"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: isCreateModalOpen && canCreateClass,
  });

  // Teachers query for create & edit modals
  const shouldFetchTeachers =
    (isCreateModalOpen && canCreateClass) || (!!editingClass && canUpdateClass);

  const { data: teachersData, isLoading: isLoadingTeachers } = useQuery({
    queryKey: ["teachers", "active"],
    queryFn: () => organizationApi.listTeachers({ page: 1, pageSize: 100, status: "Active" }),
    enabled: shouldFetchTeachers,
  });

  // Detail queries
  const { data: classDetail, isLoading: isDetailLoading } = useQuery({
    queryKey: ["classDetail", viewingClassId],
    queryFn: () => organizationApi.getClass(viewingClassId!),
    enabled: !!viewingClassId,
  });

  const { data: classStudentsData, isLoading: isLoadingStudents } = useQuery({
    queryKey: ["classStudents", viewingClassId, memberPage, memberSearch],
    queryFn: () =>
      organizationApi.getClassStudents(viewingClassId!, {
        page: memberPage,
        pageSize: 10,
        search: memberSearch.trim() || undefined,
      }),
    enabled: !!viewingClassId,
  });

  // Candidate students query (SQL anti-join server pagination)
  const {
    data: candidateStudentsData,
    isLoading: isLoadingCandidates,
    isFetching: isFetchingCandidates,
    isError: isErrorCandidates,
    refetch: refetchCandidates,
  } = useQuery({
    queryKey: ["candidateStudents", viewingClassId, candidatePage, candidateSearch],
    queryFn: () =>
      organizationApi.getClassCandidateStudents(viewingClassId!, {
        page: candidatePage,
        pageSize: 10,
        search: candidateSearch.trim() || undefined,
      }),
    enabled: isAddStudentsModalOpen && !!viewingClassId && canAddMembers,
  });

  // Mutations
  const createClassMutation = useMutation({
    mutationFn: (req: CreateClassRequest) => organizationApi.createClass(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["classes"] });
      showFeedback("success", "Tạo lớp học thành công!");
      resetCreateForm();
      setIsCreateModalOpen(false);
    },
    onError: (error: unknown) => {
      const details = extractProblemDetails(error);
      if (details.errorCode === "DUPLICATE_RESOURCE") {
        setCreateError("Tên lớp và năm học này đã tồn tại trong trung tâm.");
      } else if (details.errorCode === "VALIDATION_FAILED") {
        setCreateError("Thông tin lớp học không hợp lệ.");
      } else if (details.errorCode === "RESOURCE_NOT_FOUND") {
        setCreateError("Giáo viên hoặc môn học đã chọn không tồn tại hoặc không còn khả dụng.");
      } else {
        setCreateError(mapSafeOperationalError(error, "Không thể tạo lớp học. Vui lòng thử lại."));
      }
    },
  });

  const updateClassMutation = useMutation({
    mutationFn: ({ classId, request }: { classId: string; request: UpdateClassRequest }) =>
      organizationApi.updateClass(classId, request),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ["classes"] });
      queryClient.invalidateQueries({ queryKey: ["classDetail", updated.classId] });
      showFeedback("success", `Đã cập nhật lớp "${updated.className}" thành công!`);
      setEditingClass(null);
    },
    onError: (error: unknown) => {
      const details = extractProblemDetails(error);
      if (details.errorCode === "DUPLICATE_RESOURCE") {
        setEditError("Tên lớp và năm học này đã tồn tại trong trung tâm.");
      } else if (details.errorCode === "CONCURRENCY_CONFLICT" || isConcurrencyConflict(error)) {
        showFeedback(
          "conflict",
          "Dữ liệu lớp học đã bị thay đổi bởi phiên làm việc khác. Vui lòng nạp lại dữ liệu mới nhất.",
          details.traceId
        );
        refetch();
        if (viewingClassId) {
          queryClient.invalidateQueries({ queryKey: ["classDetail", viewingClassId] });
        }
        setEditingClass(null);
      } else if (details.errorCode === "VALIDATION_FAILED") {
        setEditError("Thông tin cập nhật không hợp lệ.");
      } else {
        setEditError(mapSafeOperationalError(error, "Không thể cập nhật lớp học. Vui lòng thử lại."));
      }
    },
  });

  const addStudentsMutation = useMutation({
    mutationFn: ({ classId, studentIds }: { classId: string; studentIds: string[] }) =>
      organizationApi.addStudentsToClass(classId, { studentIds }),
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["classes"] });
      queryClient.invalidateQueries({ queryKey: ["classDetail", res.classId] });
      queryClient.invalidateQueries({ queryKey: ["classStudents", res.classId] });
      queryClient.invalidateQueries({ queryKey: ["candidateStudents"] });
      const duplicateMsg =
        res.alreadyMemberCount > 0
          ? ` (${res.alreadyMemberCount} học sinh đã là thành viên)`
          : "";
      showFeedback("success", `Đã thêm ${res.addedCount} học sinh vào lớp học${duplicateMsg}.`);
      setIsAddStudentsModalOpen(false);
      setSelectedStudentIds([]);
    },
    onError: (error: unknown) => {
      setAddStudentsError(mapSafeOperationalError(error, "Không thể thêm học sinh vào lớp. Vui lòng thử lại."));
    },
  });

  const removeStudentMutation = useMutation({
    mutationFn: ({ classId, studentId }: { classId: string; studentId: string }) =>
      organizationApi.removeStudentFromClass(classId, studentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["classes"] });
      if (viewingClassId) {
        queryClient.invalidateQueries({ queryKey: ["classDetail", viewingClassId] });
        queryClient.invalidateQueries({ queryKey: ["classStudents", viewingClassId] });
        queryClient.invalidateQueries({ queryKey: ["candidateStudents"] });
      }
      showFeedback("success", "Đã rút học sinh khỏi lớp học thành công. Bằng chứng làm bài được giữ nguyên vẹn.");
      setRemovingStudent(null);
    },
    onError: (error: unknown) => {
      if (isConcurrencyConflict(error)) {
        showFeedback(
          "conflict",
          "Dữ liệu thành viên đã thay đổi. Đang tải lại danh sách mới nhất..."
        );
        if (viewingClassId) {
          queryClient.invalidateQueries({ queryKey: ["classStudents", viewingClassId] });
        }
        setRemovingStudent(null);
      } else {
        setRemoveError(mapSafeOperationalError(error, "Không thể rút học sinh khỏi lớp. Vui lòng thử lại."));
      }
    },
  });

  const resetCreateForm = () => {
    setClassName("");
    setAcademicYear("");
    setSubjectId("");
    setTeacherId("");
    setCreateError(null);
  };

  const handleCancelCreate = () => {
    setIsCreateModalOpen(false);
    resetCreateForm();
  };

  const handleOpenEdit = (cls: ClassDto) => {
    setEditingClass(cls);
    setEditClassName(cls.className);
    setEditTeacherId(cls.teacher.teacherId);
    setEditStatus(cls.status);
    setEditError(null);
  };

  const handleOpenDetail = (classId: string) => {
    setViewingClassId(classId);
    setMemberPage(1);
    setMemberSearchInput("");
    setMemberSearch("");
  };

  const handleMemberSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setMemberSearch(memberSearchInput);
    setMemberPage(1);
  };

  const handleFilter = (e: React.FormEvent) => {
    e.preventDefault();
    setStatus(statusInput);
    setPage(1);
  };

  const handleCreateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCreateError(null);

    const normClassName = className.trim();
    const normAcademicYear = academicYear.trim();
    const normSubjectId = subjectId.trim();
    const normTeacherId = teacherId.trim();

    if (
      !normClassName ||
      normClassName.length > 150 ||
      !normAcademicYear ||
      normAcademicYear.length > 20 ||
      !normSubjectId ||
      !normTeacherId
    ) {
      setCreateError("Vui lòng điền đầy đủ và chính xác các thông tin bắt buộc.");
      return;
    }

    createClassMutation.mutate({
      className: normClassName,
      academicYear: normAcademicYear,
      subjectId: normSubjectId,
      teacherId: normTeacherId,
    });
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingClass) return;
    setEditError(null);

    const normClassName = editClassName.trim();
    const normTeacherId = editTeacherId.trim();

    if (!normClassName || normClassName.length > 150) {
      setEditError("Tên lớp học không được để trống và không vượt quá 150 ký tự.");
      return;
    }
    if (!normTeacherId) {
      setEditError("Vui lòng chọn giáo viên phụ trách.");
      return;
    }

    updateClassMutation.mutate({
      classId: editingClass.classId,
      request: {
        className: normClassName,
        teacherId: normTeacherId,
        status: editStatus,
        rowVersion: editingClass.rowVersion,
      },
    });
  };

  const handleOpenAddStudents = () => {
    setSelectedStudentIds([]);
    setCandidatePage(1);
    setCandidateSearchInput("");
    setCandidateSearch("");
    setAddStudentsError(null);
    setIsAddStudentsModalOpen(true);
  };

  const handleCandidateSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCandidatePage(1);
    setCandidateSearch(candidateSearchInput);
  };

  const handleClearCandidateSearch = () => {
    setCandidateSearchInput("");
    setCandidateSearch("");
    setCandidatePage(1);
  };

  const handleToggleSelectStudent = (id: string) => {
    setSelectedStudentIds((prev) => toggleStudentSelection(prev, id));
  };

  const candidateList = candidateStudentsData?.data ?? [];
  const candidateMeta = candidateStudentsData?.meta;
  const currentPageCandidateIds = candidateList.map((c) => c.studentId);
  const allCurrentPageSelected =
    currentPageCandidateIds.length > 0 &&
    currentPageCandidateIds.every((id) => selectedStudentIds.includes(id));

  const handleToggleSelectCurrentPage = () => {
    if (allCurrentPageSelected) {
      setSelectedStudentIds((prev) => unmergePageSelection(prev, currentPageCandidateIds));
    } else {
      setSelectedStudentIds((prev) => mergePageSelection(prev, currentPageCandidateIds));
    }
  };

  const candidateState = getCandidateListState({
    isError: isErrorCandidates,
    isLoading: isLoadingCandidates,
    isFetching: isFetchingCandidates,
    candidateCount: candidateList.length,
  });

  const handleAddStudentsSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!viewingClassId || selectedStudentIds.length === 0) return;
    setAddStudentsError(null);

    addStudentsMutation.mutate({
      classId: viewingClassId,
      studentIds: selectedStudentIds,
    });
  };

  const handleConfirmRemoveStudent = () => {
    if (!viewingClassId || !removingStudent) return;
    setRemoveError(null);

    removeStudentMutation.mutate({
      classId: viewingClassId,
      studentId: removingStudent.studentId,
    });
  };

  const columns: DataTableColumn<ClassDto>[] = [
    {
      id: "className",
      header: "Tên lớp học",
      render: (cls) => (
        <div>
          <span className="font-semibold text-[var(--cm-text)]">{cls.className}</span>
          <span className="block text-xs text-[var(--cm-text-secondary)]">{cls.academicYear}</span>
        </div>
      ),
    },
    {
      id: "subject",
      header: "Môn học",
      render: (cls) => (
        <span className="font-medium text-[var(--cm-text)]">
          {cls.subject.subjectName}
        </span>
      ),
    },
    {
      id: "teacher",
      header: "Giáo viên phụ trách",
      render: (cls) => (
        <span className="text-[var(--cm-text-secondary)]">
          {cls.teacher?.displayName || "Chưa phân công"}
        </span>
      ),
    },
    {
      id: "studentCount",
      header: "Sĩ số",
      align: "center",
      render: (cls) => (
        <span className="font-semibold text-[var(--cm-text-secondary)]">
          {cls.studentCount} học sinh
        </span>
      ),
    },
    {
      id: "status",
      header: "Trạng thái",
      render: (cls) => (
        <StatusBadge
          status={cls.status}
          label={STATUS_LABELS[cls.status] || cls.status}
        />
      ),
    },
    {
      id: "actions",
      header: "Thao tác",
      align: "right",
      render: (cls) => (
        <div className="flex items-center justify-end gap-1.5">
          {canViewDashboard && (
            <Link
              to={`/quan-ly/lop-hoc/${cls.classId}/tong-quan`}
              className="cm-secondary-button h-8 px-2.5 py-1 text-xs text-indigo-300 hover:text-white"
            >
              Báo cáo lớp
            </Link>
          )}
          <button
            type="button"
            id={`btn-view-class-${cls.classId}`}
            onClick={() => handleOpenDetail(cls.classId)}
            className="cm-secondary-button h-8 px-2.5 py-1 text-xs"
          >
            Chi tiết
          </button>
          {canUpdateClass && (
            <button
              type="button"
              id={`btn-edit-class-${cls.classId}`}
              onClick={() => handleOpenEdit(cls)}
              className="cm-secondary-button h-8 px-2.5 py-1 text-xs"
            >
              Sửa
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
            eyebrow="Quản trị lớp học"
            title="Danh sách Lớp học"
            description="Tổ chức lớp học theo môn và năm học, chỉ định giáo viên phụ trách và quản lý thành viên học viên."
            actions={
              canCreateClass && (
                <button
                  type="button"
                  id="btn-open-create-class"
                  onClick={() => setIsCreateModalOpen(true)}
                  className="cm-primary-button"
                >
                  + Thêm lớp học
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
              id="class-feedback-alert"
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
            filters={
              <div className="flex items-center gap-2">
                <label htmlFor="filter-class-status" className="text-xs text-[var(--cm-text-secondary)]">
                  Trạng thái:
                </label>
                <select
                  id="filter-class-status"
                  value={statusInput}
                  onChange={(e) => setStatusInput(e.target.value as ClassStatus | "")}
                  className="cm-field px-3 py-1.5 text-xs"
                >
                  <option value="">Tất cả trạng thái</option>
                  <option value="Active">Hoạt động</option>
                  <option value="Archived">Đã lưu trữ</option>
                </select>
              </div>
            }
            actions={
              <button
                type="button"
                id="btn-filter-classes"
                onClick={handleFilter}
                disabled={isFetching}
                className="cm-secondary-button text-xs"
              >
                Lọc danh sách
              </button>
            }
          />

          {isError && (
            <SafeErrorPanel
              error={listError}
              fallback="Không thể tải danh sách lớp học. Vui lòng thử lại."
              onRetry={() => refetch()}
            />
          )}

          <DataTable<ClassDto>
            caption="Danh sách lớp học trung tâm"
            columns={columns}
            rows={data?.data ?? []}
            rowKey={(cls) => cls.classId}
            isLoading={isLoading}
            emptyTitle="Không tìm thấy lớp học nào"
            emptyDescription="Chưa có lớp học phù hợp với bộ lọc hiện tại."
            page={page}
            totalPages={data?.meta?.totalPages ?? 1}
            totalItems={data?.meta?.totalItems}
            onPageChange={setPage}
          />

          {/* Create Class Modal */}
          {isCreateModalOpen && canCreateClass && (
            <Modal
              isOpen={isCreateModalOpen}
              title="Thêm lớp học mới"
              description="Tạo mới lớp học, phân công môn học và giáo viên phụ trách."
              onClose={handleCancelCreate}
            >
              {createError && (
                <div id="create-class-error" role="alert" className="mb-4 rounded-xl border border-rose-400/30 bg-rose-400/10 p-3 text-xs text-rose-200">
                  {createError}
                </div>
              )}

              <form onSubmit={handleCreateSubmit} className="space-y-4">
                <div>
                  <label htmlFor="input-class-name" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Tên lớp học <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-class-name"
                    value={className}
                    onChange={(e) => setClassName(e.target.value)}
                    required
                    maxLength={150}
                    placeholder="Ví dụ: 12A1 - Toán Nâng Cao"
                    disabled={createClassMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="input-academic-year" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Năm học <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-academic-year"
                    value={academicYear}
                    onChange={(e) => setAcademicYear(e.target.value)}
                    required
                    maxLength={20}
                    placeholder="Ví dụ: 2026-2027"
                    disabled={createClassMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="select-subject" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Môn học <span className="text-rose-400">*</span>
                  </label>
                  <select
                    id="select-subject"
                    value={subjectId}
                    onChange={(e) => setSubjectId(e.target.value)}
                    required
                    disabled={isLoadingSubjects || createClassMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  >
                    <option value="">-- Chọn môn học --</option>
                    {subjectsData?.data.map((s) => (
                      <option key={s.subjectId} value={s.subjectId}>
                        {s.subjectName} ({s.subjectCode})
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label htmlFor="select-teacher" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Giáo viên phụ trách <span className="text-rose-400">*</span>
                  </label>
                  <select
                    id="select-teacher"
                    value={teacherId}
                    onChange={(e) => setTeacherId(e.target.value)}
                    required
                    disabled={isLoadingTeachers || createClassMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  >
                    <option value="">-- Chọn giáo viên --</option>
                    {teachersData?.data.map((t) => (
                      <option key={t.teacherId} value={t.teacherId}>
                        {t.displayName} ({t.username})
                      </option>
                    ))}
                  </select>
                </div>

                <div className="flex justify-end gap-3 pt-4">
                  <button
                    type="button"
                    onClick={handleCancelCreate}
                    disabled={createClassMutation.isPending}
                    className="cm-secondary-button text-sm"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-create-class"
                    disabled={createClassMutation.isPending}
                    className="cm-primary-button text-sm"
                  >
                    {createClassMutation.isPending ? "Đang tạo..." : "Tạo lớp học"}
                  </button>
                </div>
              </form>
            </Modal>
          )}

          {/* Edit Class Modal */}
          {editingClass && canUpdateClass && (
            <Modal
              isOpen={!!editingClass}
              title="Chỉnh sửa lớp học"
              description={`Cập nhật thông tin cho lớp ${editingClass.className}`}
              onClose={() => setEditingClass(null)}
            >
              {editError && (
                <div id="edit-class-error" role="alert" className="mb-4 rounded-xl border border-rose-400/30 bg-rose-400/10 p-3 text-xs text-rose-200">
                  {editError}
                </div>
              )}

              <form onSubmit={handleEditSubmit} className="space-y-4">
                <div>
                  <label htmlFor="input-edit-class-name" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Tên lớp học <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-edit-class-name"
                    value={editClassName}
                    onChange={(e) => setEditClassName(e.target.value)}
                    required
                    maxLength={150}
                    disabled={updateClassMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="select-edit-teacher" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Giáo viên phụ trách <span className="text-rose-400">*</span>
                  </label>
                  <select
                    id="select-edit-teacher"
                    value={editTeacherId}
                    onChange={(e) => setEditTeacherId(e.target.value)}
                    required
                    disabled={isLoadingTeachers || updateClassMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  >
                    {teachersData?.data.map((t) => (
                      <option key={t.teacherId} value={t.teacherId}>
                        {t.displayName} ({t.username})
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label htmlFor="select-edit-status" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Trạng thái
                  </label>
                  <select
                    id="select-edit-status"
                    value={editStatus}
                    onChange={(e) => setEditStatus(e.target.value as ClassStatus)}
                    disabled={updateClassMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  >
                    <option value="Active">Hoạt động</option>
                    <option value="Archived">Đã lưu trữ</option>
                  </select>
                </div>

                <div className="flex justify-end gap-3 pt-4">
                  <button
                    type="button"
                    onClick={() => setEditingClass(null)}
                    disabled={updateClassMutation.isPending}
                    className="cm-secondary-button text-sm"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-save-edit-class"
                    disabled={updateClassMutation.isPending}
                    className="cm-primary-button text-sm"
                  >
                    {updateClassMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </Modal>
          )}

          {/* Class Detail Drawer (Enrolled Students & Class Info) */}
          {viewingClassId && (
            <Drawer
              isOpen={!!viewingClassId}
              title={classDetail ? `Lớp ${classDetail.className}` : "Chi tiết lớp học"}
              description={classDetail ? `Môn: ${classDetail.subject.subjectName} · Niên khóa: ${classDetail.academicYear}` : undefined}
              onClose={() => setViewingClassId(null)}
              footer={
                <div className="flex justify-end">
                  <button
                    type="button"
                    onClick={() => setViewingClassId(null)}
                    className="cm-secondary-button text-sm"
                  >
                    Đóng
                  </button>
                </div>
              }
            >
              {isDetailLoading ? (
                <div className="py-12 text-center text-sm text-[var(--cm-text-muted)]">
                  Đang tải thông tin lớp học...
                </div>
              ) : classDetail ? (
                <div className="space-y-6 text-sm">
                  {/* Summary card */}
                  <div className="grid grid-cols-2 gap-4 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-4">
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Giáo viên phụ trách</span>
                      <span className="font-semibold text-[var(--cm-text)]">
                        {classDetail.teacher?.displayName || "Chưa phân công"}
                      </span>
                    </div>
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Trạng thái</span>
                      <StatusBadge
                        status={classDetail.status}
                        label={STATUS_LABELS[classDetail.status] || classDetail.status}
                      />
                    </div>
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Sĩ số hiện tại</span>
                      <span className="font-bold text-indigo-400">{classDetail.studentCount} học sinh</span>
                    </div>
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Phiên bản (RowVersion)</span>
                      <span className="font-mono text-xs text-[var(--cm-text-secondary)]">{classDetail.rowVersion}</span>
                    </div>
                  </div>

                  {/* Enrolled students */}
                  <div>
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between mb-3">
                      <h3 className="text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-secondary)]">
                        Danh sách học sinh trong lớp ({classStudentsData?.meta?.totalItems ?? 0})
                      </h3>
                      {canAddMembers && classDetail.status === "Active" && (
                        <button
                          type="button"
                          id="btn-open-add-students"
                          onClick={handleOpenAddStudents}
                          className="cm-primary-button h-8 px-3 text-xs"
                        >
                          + Thêm học sinh vào lớp
                        </button>
                      )}
                    </div>

                    <form onSubmit={handleMemberSearchSubmit} className="flex gap-2 mb-3">
                      <input
                        type="search"
                        placeholder="Tìm học sinh trong lớp..."
                        value={memberSearchInput}
                        onChange={(e) => setMemberSearchInput(e.target.value)}
                        className="cm-field flex-1 px-3 text-xs"
                      />
                      <button
                        type="submit"
                        id="btn-search-members"
                        className="cm-secondary-button text-xs"
                      >
                        Tìm
                      </button>
                    </form>

                    {isLoadingStudents ? (
                      <div className="py-8 text-center text-xs text-[var(--cm-text-muted)]">Đang tải danh sách...</div>
                    ) : !classStudentsData?.data || classStudentsData.data.length === 0 ? (
                      <p className="py-6 text-center text-xs italic text-[var(--cm-text-muted)]">
                        {memberSearch ? "Không tìm thấy học sinh phù hợp." : "Lớp chưa có học sinh nào."}
                      </p>
                    ) : (
                      <div className="divide-y divide-[var(--cm-border-subtle)] rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)]">
                        {classStudentsData.data.map((student: StudentDto) => (
                          <div key={student.studentId} className="flex items-center justify-between p-3 text-xs">
                            <div>
                              <span className="font-medium text-[var(--cm-text)]">{student.fullName}</span>
                              <span className="ml-2 font-mono text-[var(--cm-cyan)]">@{student.username}</span>
                              <span className="ml-2 text-[var(--cm-text-muted)]">· Khối {student.gradeLevel}</span>
                            </div>
                            {canRemoveMembers && classDetail.status === "Active" && (
                              <button
                                type="button"
                                id={`btn-remove-student-${student.studentId}`}
                                onClick={() => setRemovingStudent({ studentId: student.studentId, fullName: student.fullName })}
                                className="cm-danger-button h-7 px-2 text-xs"
                              >
                                Rút khỏi lớp
                              </button>
                            )}
                          </div>
                        ))}
                      </div>
                    )}

                    {/* Member pagination */}
                    {classStudentsData?.meta && classStudentsData.meta.totalPages > 1 && (
                      <div className="flex items-center justify-between pt-3 text-xs text-[var(--cm-text-secondary)]">
                        <span>Trang {memberPage} / {classStudentsData.meta.totalPages}</span>
                        <div className="flex gap-2">
                          <button
                            type="button"
                            disabled={memberPage <= 1}
                            onClick={() => setMemberPage((p) => p - 1)}
                            className="cm-secondary-button h-7 px-2 text-xs"
                          >
                            Trước
                          </button>
                          <button
                            type="button"
                            disabled={memberPage >= classStudentsData.meta.totalPages}
                            onClick={() => setMemberPage((p) => p + 1)}
                            className="cm-secondary-button h-7 px-2 text-xs"
                          >
                            Sau
                          </button>
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              ) : null}
            </Drawer>
          )}

          {/* Add Students Modal (SQL anti-join server pagination preserved) */}
          {isAddStudentsModalOpen && viewingClassId && canAddMembers && (
            <Modal
              isOpen={isAddStudentsModalOpen}
              title="Thêm học sinh vào lớp học"
              description="Chọn học sinh từ danh sách ứng viên (chưa tham gia lớp học này)."
              onClose={() => setIsAddStudentsModalOpen(false)}
              maxWidth="max-w-2xl"
            >
              {addStudentsError && (
                <div id="add-students-error" role="alert" className="mb-4 rounded-xl border border-rose-400/30 bg-rose-400/10 p-3 text-xs text-rose-200">
                  {addStudentsError}
                </div>
              )}

              <form onSubmit={handleCandidateSearchSubmit} className="flex gap-2 mb-4">
                <input
                  type="search"
                  placeholder="Tìm theo tên đăng nhập hoặc họ tên học sinh..."
                  value={candidateSearchInput}
                  onChange={(e) => setCandidateSearchInput(e.target.value)}
                  className="cm-field flex-1 px-3 text-xs"
                />
                <button
                  type="submit"
                  id="btn-search-candidates"
                  className="cm-secondary-button text-xs"
                >
                  Tìm kiếm
                </button>
                {candidateSearch && (
                  <button
                    type="button"
                    onClick={handleClearCandidateSearch}
                    className="cm-secondary-button text-xs"
                  >
                    Xóa tìm
                  </button>
                )}
              </form>

              {/* Multi-page selection indicator */}
              <div className="flex items-center justify-between rounded-lg bg-indigo-500/10 p-2.5 mb-3 text-xs text-indigo-300 border border-indigo-400/20">
                <span>Đã chọn: <strong className="text-white">{selectedStudentIds.length}</strong> học sinh</span>
                {candidateList.length > 0 && (
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      id="checkbox-select-all-candidates"
                      checked={allCurrentPageSelected}
                      onChange={handleToggleSelectCurrentPage}
                      className="rounded border-[var(--cm-border)] text-indigo-500"
                    />
                    <span>Chọn tất cả trang này</span>
                  </label>
                )}
              </div>

              {candidateState === "loading" && (
                <div className="py-12 text-center text-xs text-[var(--cm-text-muted)]">Đang tải danh sách ứng viên...</div>
              )}
              {candidateState === "error" && (
                <div className="py-8 text-center text-xs text-rose-300">
                  Không thể tải ứng viên.{" "}
                  <button
                    type="button"
                    onClick={() => refetchCandidates()}
                    className="underline font-semibold hover:text-white"
                  >
                    Thử lại
                  </button>
                </div>
              )}
              {candidateState === "empty" && (
                <div className="py-8 text-center text-xs text-[var(--cm-text-muted)]">
                  {candidateSearch ? "Không có ứng viên nào khớp với từ khóa." : "Tất cả học sinh hợp lệ đã có mặt trong lớp này."}
                </div>
              )}

              {candidateState === "ready" && (
                <div className="max-h-60 overflow-y-auto divide-y divide-[var(--cm-border-subtle)] rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)]">
                  {candidateList.map((c) => {
                    const isSelected = selectedStudentIds.includes(c.studentId);
                    return (
                      <label
                        key={c.studentId}
                        className={`flex items-center justify-between p-3 text-xs cursor-pointer hover:bg-white/5 transition-colors ${
                          isSelected ? "bg-indigo-500/10" : ""
                        }`}
                      >
                        <div className="flex items-center gap-3">
                          <input
                            type="checkbox"
                            id={`candidate-checkbox-${c.studentId}`}
                            checked={isSelected}
                            onChange={() => handleToggleSelectStudent(c.studentId)}
                            className="rounded border-[var(--cm-border)] text-indigo-500"
                          />
                          <div>
                            <span className="font-semibold text-[var(--cm-text)]">{c.fullName}</span>
                            <span className="ml-2 font-mono text-[var(--cm-cyan)]">@{c.username}</span>
                          </div>
                        </div>
                        <span className="text-[var(--cm-text-muted)]">Khối {c.gradeLevel}</span>
                      </label>
                    );
                  })}
                </div>
              )}

              {/* Candidate pagination */}
              {candidateMeta && candidateMeta.totalPages > 1 && (
                <div className="flex items-center justify-between pt-3 text-xs text-[var(--cm-text-secondary)]">
                  <span>Trang {candidatePage} / {candidateMeta.totalPages} ({candidateMeta.totalItems} học sinh)</span>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      id="btn-prev-candidates"
                      disabled={candidatePage <= 1 || isFetchingCandidates}
                      onClick={() => setCandidatePage((p) => p - 1)}
                      className="cm-secondary-button h-7 px-2 text-xs"
                    >
                      Trước
                    </button>
                    <button
                      type="button"
                      id="btn-next-candidates"
                      disabled={candidatePage >= candidateMeta.totalPages || isFetchingCandidates}
                      onClick={() => setCandidatePage((p) => p + 1)}
                      className="cm-secondary-button h-7 px-2 text-xs"
                    >
                      Sau
                    </button>
                  </div>
                </div>
              )}

              <div className="flex justify-end gap-3 pt-4 border-t border-[var(--cm-border-subtle)] mt-4">
                <button
                  type="button"
                  onClick={() => setIsAddStudentsModalOpen(false)}
                  disabled={addStudentsMutation.isPending}
                  className="cm-secondary-button text-sm"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  id="btn-submit-add-students"
                  onClick={handleAddStudentsSubmit}
                  disabled={selectedStudentIds.length === 0 || addStudentsMutation.isPending}
                  className="cm-primary-button text-sm"
                >
                  {addStudentsMutation.isPending ? "Đang thêm..." : `Thêm (${selectedStudentIds.length}) học sinh`}
                </button>
              </div>
            </Modal>
          )}

          {/* Remove Student Confirmation Modal */}
          {removingStudent && canRemoveMembers && (
            <Modal
              isOpen={!!removingStudent}
              title="Rút học sinh khỏi lớp"
              onClose={() => setRemovingStudent(null)}
            >
              {removeError && (
                <div id="remove-student-error" role="alert" className="mb-4 rounded-xl border border-rose-400/30 bg-rose-400/10 p-3 text-xs text-rose-200">
                  {removeError}
                </div>
              )}

              <div className="space-y-4 text-sm">
                <p className="text-[var(--cm-text-secondary)]">
                  Bạn có chắc chắn muốn rút học sinh{" "}
                  <strong className="text-[var(--cm-text)]">{removingStudent.fullName}</strong> khỏi lớp học này?
                </p>

                <div className="rounded-xl border border-cyan-400/30 bg-cyan-400/10 p-3 text-xs text-cyan-200 space-y-1">
                  <p className="font-bold">BẢO TOÀN LỊCH SỬ HỌC TẬP:</p>
                  <p>• Trạng thái của học sinh trong lớp sẽ được chuyển sang &quot;Đã rút&quot; (Removed).</p>
                  <p>• Lịch sử làm bài tập, bài kiểm tra và điểm số đã hoàn thành vẫn được bảo toàn trọn vẹn.</p>
                </div>

                <div className="flex justify-end gap-3 pt-2">
                  <button
                    type="button"
                    onClick={() => setRemovingStudent(null)}
                    disabled={removeStudentMutation.isPending}
                    className="cm-secondary-button text-sm"
                  >
                    Hủy
                  </button>
                  <button
                    type="button"
                    id="btn-confirm-remove-student"
                    onClick={handleConfirmRemoveStudent}
                    disabled={removeStudentMutation.isPending}
                    className="cm-danger-button text-sm"
                  >
                    {removeStudentMutation.isPending ? "Đang xử lý..." : "Xác nhận rút khỏi lớp"}
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

export const ClassListPage: React.FC = () => {
  const user = useAuthStore((state) => state.user);
  return user?.accountType === "CenterManager" ? (
    <CenterManagerClassListView />
  ) : (
    <LegacyClassListPage />
  );
};

/**
 * Legacy ClassListPage implementation preserved for non-CenterManager users
 */
const LegacyClassListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);

  const {
    canCreateClass,
    canUpdateClass,
    canAddMembers,
    canRemoveMembers,
    canViewDashboard,
  } = evaluateClassCapabilities(user);

  const [page, setPage] = useState<number>(1);
  const pageSize = 10;
  const [status, setStatus] = useState<ClassStatus | "">("");
  const [statusInput, setStatusInput] = useState<ClassStatus | "">("");

  const [feedback, setFeedback] = useState<{
    type: "success" | "error" | "conflict";
    message: string;
  } | null>(null);

  const showFeedback = (type: "success" | "error" | "conflict", message: string) => {
    setFeedback({ type, message });
    if (type === "success") {
      setTimeout(() => setFeedback(null), 5000);
    }
  };

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [className, setClassName] = useState("");
  const [academicYear, setAcademicYear] = useState("");
  const [subjectId, setSubjectId] = useState("");
  const [teacherId, setTeacherId] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);

  const [editingClass, setEditingClass] = useState<ClassDto | null>(null);
  const [editClassName, setEditClassName] = useState("");
  const [editTeacherId, setEditTeacherId] = useState("");
  const [editStatus, setEditStatus] = useState<ClassStatus>("Active");
  const [editError, setEditError] = useState<string | null>(null);

  const [viewingClassId, setViewingClassId] = useState<string | null>(null);
  const [memberPage, setMemberPage] = useState<number>(1);
  const [memberSearchInput, setMemberSearchInput] = useState("");
  const [memberSearch, setMemberSearch] = useState("");

  const [isAddStudentsModalOpen, setIsAddStudentsModalOpen] = useState(false);
  const [selectedStudentIds, setSelectedStudentIds] = useState<string[]>([]);
  const [candidatePage, setCandidatePage] = useState<number>(1);
  const [candidateSearchInput, setCandidateSearchInput] = useState("");
  const [candidateSearch, setCandidateSearch] = useState("");
  const [addStudentsError, setAddStudentsError] = useState<string | null>(null);

  const [removingStudent, setRemovingStudent] = useState<{
    studentId: string;
    fullName: string;
  } | null>(null);
  const [removeError, setRemoveError] = useState<string | null>(null);

  const queryParams: ClassListParams = {
    page,
    pageSize,
    status: status !== "" ? status : undefined,
  };

  const { data, isLoading, isFetching, isError, refetch } = useQuery({
    queryKey: ["classes", queryParams.page, queryParams.pageSize, queryParams.status],
    queryFn: () => organizationApi.listClasses(queryParams),
  });

  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
  } = useQuery({
    queryKey: ["subjects", "active"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: isCreateModalOpen && canCreateClass,
  });

  const shouldFetchTeachers =
    (isCreateModalOpen && canCreateClass) || (!!editingClass && canUpdateClass);

  const {
    data: teachersData,
    isLoading: isLoadingTeachers,
  } = useQuery({
    queryKey: ["teachers", "active"],
    queryFn: () => organizationApi.listTeachers({ page: 1, pageSize: 100, status: "Active" }),
    enabled: shouldFetchTeachers,
  });

  const {
    data: classDetail,
  } = useQuery({
    queryKey: ["classDetail", viewingClassId],
    queryFn: () => organizationApi.getClass(viewingClassId!),
    enabled: !!viewingClassId,
  });

  const {
    data: classStudentsData,
    isLoading: isLoadingStudents,
  } = useQuery({
    queryKey: ["classStudents", viewingClassId, memberPage, memberSearch],
    queryFn: () =>
      organizationApi.getClassStudents(viewingClassId!, {
        page: memberPage,
        pageSize: 10,
        search: memberSearch.trim() || undefined,
      }),
    enabled: !!viewingClassId,
  });

  const {
    data: candidateStudentsData,
    isLoading: isLoadingCandidates,
    isFetching: isFetchingCandidates,
    isError: isErrorCandidates,
    refetch: refetchCandidates,
  } = useQuery({
    queryKey: ["candidateStudents", viewingClassId, candidatePage, candidateSearch],
    queryFn: () =>
      organizationApi.getClassCandidateStudents(viewingClassId!, {
        page: candidatePage,
        pageSize: 10,
        search: candidateSearch.trim() || undefined,
      }),
    enabled: isAddStudentsModalOpen && !!viewingClassId && canAddMembers,
  });

  const createClassMutation = useMutation({
    mutationFn: (req: CreateClassRequest) => organizationApi.createClass(req),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["classes"] });
      showFeedback("success", "Tạo lớp học thành công!");
      resetCreateForm();
      setIsCreateModalOpen(false);
    },
    onError: (error: unknown) => {
      const details = extractProblemDetails(error);
      if (details.errorCode === "DUPLICATE_RESOURCE") {
        setCreateError("Tên lớp và năm học này đã tồn tại trong trung tâm.");
      } else if (details.errorCode === "VALIDATION_FAILED") {
        setCreateError("Thông tin lớp học không hợp lệ.");
      } else if (details.errorCode === "RESOURCE_NOT_FOUND") {
        setCreateError("Giáo viên hoặc môn học đã chọn không tồn tại hoặc không còn khả dụng.");
      } else {
        setCreateError(details.message || "Không thể tạo lớp học. Vui lòng thử lại.");
      }
    },
  });

  const updateClassMutation = useMutation({
    mutationFn: ({ classId, request }: { classId: string; request: UpdateClassRequest }) =>
      organizationApi.updateClass(classId, request),
    onSuccess: (updated) => {
      queryClient.invalidateQueries({ queryKey: ["classes"] });
      queryClient.invalidateQueries({ queryKey: ["classDetail", updated.classId] });
      showFeedback("success", `Đã cập nhật lớp "${updated.className}" thành công!`);
      setEditingClass(null);
    },
    onError: (error: unknown) => {
      const details = extractProblemDetails(error);
      if (details.errorCode === "DUPLICATE_RESOURCE") {
        setEditError("Tên lớp và năm học này đã tồn tại trong trung tâm.");
      } else if (details.errorCode === "CONCURRENCY_CONFLICT" || isConcurrencyConflict(error)) {
        showFeedback(
          "conflict",
          "Dữ liệu lớp học đã bị thay đổi bởi phiên làm việc khác. Đang tải lại dữ liệu mới nhất..."
        );
        refetch();
        if (viewingClassId) {
          queryClient.invalidateQueries({ queryKey: ["classDetail", viewingClassId] });
        }
        setEditingClass(null);
      } else if (details.errorCode === "VALIDATION_FAILED") {
        setEditError("Thông tin cập nhật không hợp lệ.");
      } else {
        setEditError(details.message || "Không thể cập nhật lớp học. Vui lòng thử lại.");
      }
    },
  });

  const addStudentsMutation = useMutation({
    mutationFn: ({ classId, studentIds }: { classId: string; studentIds: string[] }) =>
      organizationApi.addStudentsToClass(classId, { studentIds }),
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: ["classes"] });
      queryClient.invalidateQueries({ queryKey: ["classDetail", res.classId] });
      queryClient.invalidateQueries({ queryKey: ["classStudents", res.classId] });
      queryClient.invalidateQueries({ queryKey: ["candidateStudents"] });
      const duplicateMsg =
        res.alreadyMemberCount > 0
          ? ` (${res.alreadyMemberCount} học sinh đã là thành viên)`
          : "";
      showFeedback("success", `Đã thêm ${res.addedCount} học sinh vào lớp học${duplicateMsg}.`);
      setIsAddStudentsModalOpen(false);
      setSelectedStudentIds([]);
    },
    onError: (error: unknown) => {
      const details = extractProblemDetails(error);
      setAddStudentsError(details.message || "Không thể thêm học sinh vào lớp. Vui lòng thử lại.");
    },
  });

  const removeStudentMutation = useMutation({
    mutationFn: ({ classId, studentId }: { classId: string; studentId: string }) =>
      organizationApi.removeStudentFromClass(classId, studentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["classes"] });
      if (viewingClassId) {
        queryClient.invalidateQueries({ queryKey: ["classDetail", viewingClassId] });
        queryClient.invalidateQueries({ queryKey: ["classStudents", viewingClassId] });
        queryClient.invalidateQueries({ queryKey: ["candidateStudents"] });
      }
      showFeedback("success", "Đã xóa học sinh khỏi lớp học thành công.");
      setRemovingStudent(null);
    },
    onError: (error: unknown) => {
      if (isConcurrencyConflict(error)) {
        showFeedback(
          "conflict",
          "Dữ liệu thành viên đã thay đổi. Đang tải lại danh sách mới nhất..."
        );
        if (viewingClassId) {
          queryClient.invalidateQueries({ queryKey: ["classStudents", viewingClassId] });
        }
        setRemovingStudent(null);
      } else {
        const details = extractProblemDetails(error);
        setRemoveError(details.message || "Không thể xóa học sinh khỏi lớp. Vui lòng thử lại.");
      }
    },
  });

  const resetCreateForm = () => {
    setClassName("");
    setAcademicYear("");
    setSubjectId("");
    setTeacherId("");
    setCreateError(null);
  };

  const handleCancelCreate = () => {
    resetCreateForm();
    setIsCreateModalOpen(false);
  };

  const handleOpenEdit = (cls: ClassDto) => {
    setEditingClass(cls);
    setEditClassName(cls.className);
    setEditTeacherId(cls.teacher.teacherId);
    setEditStatus(cls.status);
    setEditError(null);
  };

  const handleOpenDetail = (classId: string) => {
    setViewingClassId(classId);
    setMemberPage(1);
    setMemberSearchInput("");
    setMemberSearch("");
  };

  const handleMemberSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setMemberSearch(memberSearchInput);
    setMemberPage(1);
  };

  const handleFilter = (e: React.FormEvent) => {
    e.preventDefault();
    setStatus(statusInput);
    setPage(1);
  };

  const handleCreateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCreateError(null);

    const normClassName = className.trim();
    const normAcademicYear = academicYear.trim();
    const normSubjectId = subjectId.trim();
    const normTeacherId = teacherId.trim();

    if (
      !normClassName ||
      normClassName.length > 150 ||
      !normAcademicYear ||
      normAcademicYear.length > 20 ||
      !normSubjectId ||
      !normTeacherId
    ) {
      setCreateError("Vui lòng điền đầy đủ và chính xác các thông tin bắt buộc.");
      return;
    }

    createClassMutation.mutate({
      className: normClassName,
      academicYear: normAcademicYear,
      subjectId: normSubjectId,
      teacherId: normTeacherId,
    });
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingClass) return;
    setEditError(null);

    const normClassName = editClassName.trim();
    const normTeacherId = editTeacherId.trim();

    if (!normClassName || normClassName.length > 150) {
      setEditError("Tên lớp học không được để trống và không vượt quá 150 ký tự.");
      return;
    }
    if (!normTeacherId) {
      setEditError("Vui lòng chọn giáo viên phụ trách.");
      return;
    }

    updateClassMutation.mutate({
      classId: editingClass.classId,
      request: {
        className: normClassName,
        teacherId: normTeacherId,
        status: editStatus,
        rowVersion: editingClass.rowVersion,
      },
    });
  };

  const handleOpenAddStudents = () => {
    setSelectedStudentIds([]);
    setCandidatePage(1);
    setCandidateSearchInput("");
    setCandidateSearch("");
    setAddStudentsError(null);
    setIsAddStudentsModalOpen(true);
  };

  const handleCandidateSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCandidatePage(1);
    setCandidateSearch(candidateSearchInput);
  };

  const handleClearCandidateSearch = () => {
    setCandidateSearchInput("");
    setCandidateSearch("");
    setCandidatePage(1);
  };

  const handleToggleSelectStudent = (id: string) => {
    setSelectedStudentIds((prev) => toggleStudentSelection(prev, id));
  };

  const candidateList = candidateStudentsData?.data ?? [];
  const candidateMeta = candidateStudentsData?.meta;
  const currentPageCandidateIds = candidateList.map((c) => c.studentId);
  const allCurrentPageSelected =
    currentPageCandidateIds.length > 0 &&
    currentPageCandidateIds.every((id) => selectedStudentIds.includes(id));

  const handleToggleSelectCurrentPage = () => {
    if (allCurrentPageSelected) {
      setSelectedStudentIds((prev) => unmergePageSelection(prev, currentPageCandidateIds));
    } else {
      setSelectedStudentIds((prev) => mergePageSelection(prev, currentPageCandidateIds));
    }
  };

  const candidateState = getCandidateListState({
    isError: isErrorCandidates,
    isLoading: isLoadingCandidates,
    isFetching: isFetchingCandidates,
    candidateCount: candidateList.length,
  });

  const handleAddStudentsSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!viewingClassId || selectedStudentIds.length === 0) return;
    setAddStudentsError(null);

    addStudentsMutation.mutate({
      classId: viewingClassId,
      studentIds: selectedStudentIds,
    });
  };

  const handleConfirmRemoveStudent = () => {
    if (!viewingClassId || !removingStudent) return;
    setRemoveError(null);

    removeStudentMutation.mutate({
      classId: viewingClassId,
      studentId: removingStudent.studentId,
    });
  };

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold leading-7 text-gray-900 sm:truncate sm:text-3xl sm:tracking-tight">
              Quản lý Lớp học & Danh sách Thành viên
            </h1>
            <p className="mt-1 text-sm text-gray-500">
              Quản lý danh sách lớp học, cập nhật thông tin, gán giáo viên và quản lý danh sách học sinh theo từng lớp.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Link
              to="/"
              className="inline-flex items-center rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
            >
              Về trang chủ
            </Link>
            {canCreateClass && (
              <button
                type="button"
                id="btn-open-create-class"
                onClick={() => setIsCreateModalOpen(true)}
                className="inline-flex items-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
              >
                Thêm lớp học
              </button>
            )}
          </div>
        </div>

        {feedback && (
          <div
            className={`mb-6 rounded-md p-4 transition-all duration-300 ${
              feedback.type === "success"
                ? "bg-green-50 border border-green-200"
                : feedback.type === "conflict"
                ? "bg-amber-50 border border-amber-200"
                : "bg-red-50 border border-red-200"
            }`}
            role={feedback.type === "success" ? "status" : "alert"}
          >
            <div className="flex items-center justify-between">
              <div className="flex items-center">
                <p
                  className={`text-sm font-medium ${
                    feedback.type === "success"
                      ? "text-green-800"
                      : feedback.type === "conflict"
                      ? "text-amber-800"
                      : "text-red-800"
                  }`}
                >
                  {feedback.message}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setFeedback(null)}
                className="text-gray-400 hover:text-gray-500 focus:outline-none"
              >
                <span className="sr-only">Đóng</span>
                <span className="text-lg">×</span>
              </button>
            </div>
          </div>
        )}

        <div className="mb-6 rounded-lg bg-white p-4 shadow-sm border border-gray-100">
          <form onSubmit={handleFilter} className="flex flex-wrap items-end gap-4">
            <div className="min-w-[200px]">
              <label htmlFor="select-filter-status" className="block text-xs font-semibold text-gray-700 uppercase tracking-wider mb-1">
                Trạng thái lớp
              </label>
              <select
                id="select-filter-status"
                value={statusInput}
                onChange={(e) => setStatusInput(e.target.value as ClassStatus | "")}
                className="block w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              >
                <option value="">Tất cả trạng thái</option>
                <option value="Active">Hoạt động (Active)</option>
                <option value="Archived">Đã lưu trữ (Archived)</option>
              </select>
            </div>
            <button
              type="submit"
              id="btn-apply-filter"
              disabled={isFetching}
              className="inline-flex items-center rounded-md bg-gray-100 px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-200 focus:outline-none disabled:opacity-50"
            >
              Lọc danh sách
            </button>
          </form>
        </div>

        {isError && (
          <div className="mb-6 rounded-md bg-red-50 p-4 border border-red-200">
            <p className="text-sm font-medium text-red-800">
              Không thể tải danh sách lớp học. Vui lòng kiểm tra lại kết nối mạng hoặc thử lại.
            </p>
          </div>
        )}

        <div className="overflow-hidden rounded-lg bg-white shadow">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Tên lớp
                  </th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Môn học
                  </th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Giáo viên phụ trách
                  </th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Năm học
                  </th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Sĩ số
                  </th>
                  <th scope="col" className="px-6 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">
                    Trạng thái
                  </th>
                  <th scope="col" className="relative px-6 py-3 text-right text-xs font-medium uppercase tracking-wider text-gray-500">
                    Thao tác
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {isLoading ? (
                  <tr>
                    <td colSpan={7} className="px-6 py-10 text-center text-sm text-gray-500">
                      Đang tải dữ liệu lớp học...
                    </td>
                  </tr>
                ) : !data || data.data.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="px-6 py-10 text-center text-sm text-gray-500">
                      Không có lớp học nào phù hợp với bộ lọc hiện tại.
                    </td>
                  </tr>
                ) : (
                  data.data.map((cls) => (
                    <tr key={cls.classId} className="hover:bg-gray-50">
                      <td className="whitespace-nowrap px-6 py-4 text-sm font-bold text-gray-900">
                        {cls.className}
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 text-sm text-gray-700">
                        {cls.subject.subjectName}
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 text-sm text-gray-700">
                        {cls.teacher.displayName}
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 text-sm text-gray-500 font-mono">
                        {cls.academicYear}
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 text-sm font-semibold text-gray-900">
                        {cls.studentCount}
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 text-sm">
                        <span
                          className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${
                            cls.status === "Active"
                              ? "bg-green-100 text-green-800"
                              : "bg-gray-100 text-gray-800"
                          }`}
                        >
                          {STATUS_LABELS[cls.status]}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-6 py-4 text-right text-sm font-medium space-x-2">
                        {canViewDashboard && (
                          <Link
                            to={`/quan-ly/lop-hoc/${cls.classId}/tong-quan`}
                            className="inline-flex items-center text-xs font-semibold text-indigo-600 hover:text-indigo-900 mr-2"
                          >
                            Báo cáo lớp
                          </Link>
                        )}
                        <button
                          type="button"
                          id={`btn-view-class-${cls.classId}`}
                          onClick={() => handleOpenDetail(cls.classId)}
                          className="text-xs font-semibold text-teal-600 hover:text-teal-900"
                        >
                          Chi tiết & Thành viên
                        </button>
                        {canUpdateClass && (
                          <button
                            type="button"
                            id={`btn-edit-class-${cls.classId}`}
                            onClick={() => handleOpenEdit(cls)}
                            className="text-xs font-semibold text-blue-600 hover:text-blue-900"
                          >
                            Sửa
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
              <div className="flex flex-1 justify-between sm:hidden">
                <button
                  type="button"
                  onClick={() => setPage((p) => Math.max(p - 1, 1))}
                  disabled={page === 1}
                  className="relative inline-flex items-center rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
                >
                  Trước
                </button>
                <button
                  type="button"
                  onClick={() => setPage((p) => Math.min(p + 1, data.meta.totalPages))}
                  disabled={page === data.meta.totalPages}
                  className="relative ml-3 inline-flex items-center rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
                >
                  Sau
                </button>
              </div>
              <div className="hidden sm:flex sm:flex-1 sm:items-center sm:justify-between">
                <div>
                  <p className="text-sm text-gray-700">
                    Hiển thị trang <span className="font-medium">{page}</span> trên{" "}
                    <span className="font-medium">{data.meta.totalPages}</span> trang (Tổng cộng{" "}
                    <span className="font-medium">{data.meta.totalItems}</span> lớp)
                  </p>
                </div>
                <div>
                  <nav className="isolate inline-flex -space-x-px rounded-md shadow-sm" aria-label="Pagination">
                    <button
                      type="button"
                      id="btn-prev-page"
                      onClick={() => setPage((p) => Math.max(p - 1, 1))}
                      disabled={page === 1}
                      className="relative inline-flex items-center rounded-l-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 focus:z-20 focus:outline-offset-0 disabled:opacity-50"
                    >
                      ‹
                    </button>
                    <button
                      type="button"
                      id="btn-next-page"
                      onClick={() => setPage((p) => Math.min(p + 1, data.meta.totalPages))}
                      disabled={page === data.meta.totalPages}
                      className="relative inline-flex items-center rounded-r-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 focus:z-20 focus:outline-offset-0 disabled:opacity-50"
                    >
                      ›
                    </button>
                  </nav>
                </div>
              </div>
            </div>
          )}
        </div>

        {isCreateModalOpen && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-gray-500 bg-opacity-75 flex items-center justify-center p-4">
            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-lg sm:p-6">
              <h3 className="text-base font-semibold leading-6 text-gray-900 mb-4">
                Tạo lớp học mới
              </h3>
              {createError && (
                <div id="create-class-error" className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700">
                  {createError}
                </div>
              )}
              <form onSubmit={handleCreateSubmit} className="space-y-4">
                <div>
                  <label htmlFor="input-class-name" className="block text-sm font-medium text-gray-700">
                    Tên lớp học <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-class-name"
                    required
                    maxLength={150}
                    value={className}
                    onChange={(e) => setClassName(e.target.value)}
                    placeholder="Ví dụ: 12A1 - Toán Nâng Cao"
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>
                <div>
                  <label htmlFor="input-academic-year" className="block text-sm font-medium text-gray-700">
                    Năm học <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-academic-year"
                    required
                    maxLength={20}
                    value={academicYear}
                    onChange={(e) => setAcademicYear(e.target.value)}
                    placeholder="Ví dụ: 2026-2027"
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>
                <div>
                  <label htmlFor="select-subject" className="block text-sm font-medium text-gray-700">
                    Môn học <span className="text-red-500">*</span>
                  </label>
                  <select
                    id="select-subject"
                    required
                    value={subjectId}
                    onChange={(e) => setSubjectId(e.target.value)}
                    disabled={isLoadingSubjects}
                    className="mt-1 block w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  >
                    <option value="">-- Chọn môn học --</option>
                    {subjectsData?.data.map((s) => (
                      <option key={s.subjectId} value={s.subjectId}>
                        {s.subjectName} ({s.subjectCode})
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label htmlFor="select-teacher" className="block text-sm font-medium text-gray-700">
                    Giáo viên phụ trách <span className="text-red-500">*</span>
                  </label>
                  <select
                    id="select-teacher"
                    required
                    value={teacherId}
                    onChange={(e) => setTeacherId(e.target.value)}
                    disabled={isLoadingTeachers}
                    className="mt-1 block w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  >
                    <option value="">-- Chọn giáo viên --</option>
                    {teachersData?.data.map((t) => (
                      <option key={t.teacherId} value={t.teacherId}>
                        {t.displayName} ({t.username})
                      </option>
                    ))}
                  </select>
                </div>
                <div className="mt-5 sm:mt-6 flex justify-end gap-3">
                  <button
                    type="button"
                    onClick={handleCancelCreate}
                    className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm hover:bg-gray-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-create-class"
                    disabled={createClassMutation.isPending}
                    className="rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {createClassMutation.isPending ? "Đang xử lý..." : "Tạo lớp học"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {editingClass && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-gray-500 bg-opacity-75 flex items-center justify-center p-4">
            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-lg sm:p-6">
              <h3 className="text-base font-semibold leading-6 text-gray-900 mb-4">
                Chỉnh sửa lớp học
              </h3>
              {editError && (
                <div id="edit-class-error" className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700">
                  {editError}
                </div>
              )}
              <form onSubmit={handleEditSubmit} className="space-y-4">
                <div>
                  <label htmlFor="input-edit-class-name" className="block text-sm font-medium text-gray-700">
                    Tên lớp học <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-edit-class-name"
                    required
                    maxLength={150}
                    value={editClassName}
                    onChange={(e) => setEditClassName(e.target.value)}
                    className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  />
                </div>
                <div>
                  <label htmlFor="select-edit-teacher" className="block text-sm font-medium text-gray-700">
                    Giáo viên phụ trách <span className="text-red-500">*</span>
                  </label>
                  <select
                    id="select-edit-teacher"
                    required
                    value={editTeacherId}
                    onChange={(e) => setEditTeacherId(e.target.value)}
                    disabled={isLoadingTeachers}
                    className="mt-1 block w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  >
                    {teachersData?.data.map((t) => (
                      <option key={t.teacherId} value={t.teacherId}>
                        {t.displayName} ({t.username})
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label htmlFor="select-edit-status" className="block text-sm font-medium text-gray-700">
                    Trạng thái
                  </label>
                  <select
                    id="select-edit-status"
                    value={editStatus}
                    onChange={(e) => setEditStatus(e.target.value as ClassStatus)}
                    className="mt-1 block w-full rounded-md border border-gray-300 bg-white px-3 py-2 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                  >
                    <option value="Active">Hoạt động (Active)</option>
                    <option value="Archived">Đã lưu trữ (Archived)</option>
                  </select>
                </div>
                <div className="mt-5 sm:mt-6 flex justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setEditingClass(null)}
                    className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm hover:bg-gray-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-save-edit-class"
                    disabled={updateClassMutation.isPending}
                    className="rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {updateClassMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {viewingClassId && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-gray-500 bg-opacity-75 flex items-center justify-center p-4">
            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-4xl sm:p-6">
              <div className="flex justify-between items-start mb-4 border-b pb-3">
                <div>
                  <h3 className="text-lg font-bold text-gray-900">
                    {classDetail ? `Lớp: ${classDetail.className}` : "Chi tiết lớp học"}
                  </h3>
                  {classDetail && (
                    <p className="text-xs text-gray-500 mt-1">
                      Môn học: <span className="font-semibold">{classDetail.subject.subjectName}</span> | Giáo viên:{" "}
                      <span className="font-semibold">{classDetail.teacher.displayName}</span> | Năm học:{" "}
                      <span className="font-semibold">{classDetail.academicYear}</span>
                    </p>
                  )}
                </div>
                <button
                  type="button"
                  onClick={() => setViewingClassId(null)}
                  className="text-gray-400 hover:text-gray-500 text-2xl font-bold leading-none"
                >
                  ×
                </button>
              </div>

              <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3 mb-4">
                <form onSubmit={handleMemberSearchSubmit} className="flex gap-2 w-full sm:w-auto">
                  <input
                    type="text"
                    placeholder="Tìm theo tên học sinh..."
                    value={memberSearchInput}
                    onChange={(e) => setMemberSearchInput(e.target.value)}
                    className="rounded-md border border-gray-300 px-3 py-1.5 text-xs focus:border-indigo-500 focus:outline-none w-full sm:w-60"
                  />
                  <button
                    type="submit"
                    id="btn-search-members"
                    className="rounded-md bg-gray-100 px-3 py-1.5 text-xs font-semibold text-gray-700 hover:bg-gray-200"
                  >
                    Tìm
                  </button>
                </form>

                {canAddMembers && classDetail?.status === "Active" && (
                  <button
                    type="button"
                    id="btn-open-add-students"
                    onClick={handleOpenAddStudents}
                    className="inline-flex items-center rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white shadow-sm hover:bg-indigo-500 w-full sm:w-auto justify-center"
                  >
                    + Thêm học sinh vào lớp
                  </button>
                )}
              </div>

              <div className="border rounded-md overflow-hidden mb-4">
                <table className="min-w-full divide-y divide-gray-200">
                  <thead className="bg-gray-50">
                    <tr>
                      <th scope="col" className="px-4 py-2 text-left text-xs font-medium uppercase text-gray-500">
                        Tên đăng nhập
                      </th>
                      <th scope="col" className="px-4 py-2 text-left text-xs font-medium uppercase text-gray-500">
                        Họ và tên
                      </th>
                      <th scope="col" className="px-4 py-2 text-left text-xs font-medium uppercase text-gray-500">
                        Khối
                      </th>
                      <th scope="col" className="px-4 py-2 text-left text-xs font-medium uppercase text-gray-500">
                        Trạng thái
                      </th>
                      <th scope="col" className="relative px-4 py-2 text-right text-xs font-medium uppercase text-gray-500">
                        Thao tác
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200 bg-white">
                    {isLoadingStudents ? (
                      <tr>
                        <td colSpan={5} className="px-4 py-6 text-center text-xs text-gray-500">
                          Đang tải danh sách học sinh...
                        </td>
                      </tr>
                    ) : !classStudentsData || classStudentsData.data.length === 0 ? (
                      <tr>
                        <td colSpan={5} className="px-4 py-6 text-center text-xs text-gray-500">
                          {memberSearch
                            ? "Không tìm thấy học sinh phù hợp với từ khóa."
                            : "Lớp học chưa có học sinh nào."}
                        </td>
                      </tr>
                    ) : (
                      classStudentsData.data.map((student: StudentDto) => (
                        <tr key={student.studentId} className="hover:bg-gray-50">
                          <td className="px-4 py-2.5 text-xs font-mono font-medium text-gray-900">
                            {student.username}
                          </td>
                          <td className="px-4 py-2.5 text-xs font-medium text-gray-900">
                            {student.fullName}
                          </td>
                          <td className="px-4 py-2.5 text-xs text-gray-500">
                            Khối {student.gradeLevel}
                          </td>
                          <td className="px-4 py-2.5 text-xs">
                            <span className="inline-flex rounded-full px-2 py-0.5 text-[10px] font-medium bg-green-100 text-green-800">
                              {student.status}
                            </span>
                          </td>
                          <td className="px-4 py-2.5 text-right text-xs">
                            {canRemoveMembers && classDetail?.status === "Active" && (
                              <button
                                type="button"
                                id={`btn-remove-student-${student.studentId}`}
                                onClick={() =>
                                  setRemovingStudent({
                                    studentId: student.studentId,
                                    fullName: student.fullName,
                                  })
                                }
                                className="text-red-600 hover:text-red-900 font-medium"
                              >
                                Xóa khỏi lớp
                              </button>
                            )}
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>

              {classStudentsData?.meta && classStudentsData.meta.totalPages > 1 && (
                <div className="flex items-center justify-between text-xs text-gray-600 mb-4">
                  <span>
                    Trang {memberPage} / {classStudentsData.meta.totalPages} (Tổng {classStudentsData.meta.totalItems} học sinh)
                  </span>
                  <div className="space-x-1">
                    <button
                      type="button"
                      disabled={memberPage <= 1}
                      onClick={() => setMemberPage((p) => p - 1)}
                      className="px-2.5 py-1 border rounded bg-white hover:bg-gray-50 disabled:opacity-50"
                    >
                      Trước
                    </button>
                    <button
                      type="button"
                      disabled={memberPage >= classStudentsData.meta.totalPages}
                      onClick={() => setMemberPage((p) => p + 1)}
                      className="px-2.5 py-1 border rounded bg-white hover:bg-gray-50 disabled:opacity-50"
                    >
                      Sau
                    </button>
                  </div>
                </div>
              )}

              <div className="flex justify-end">
                <button
                  type="button"
                  onClick={() => setViewingClassId(null)}
                  className="rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        )}

        {isAddStudentsModalOpen && viewingClassId && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-gray-500 bg-opacity-75 flex items-center justify-center p-4">
            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-2xl sm:p-6">
              <h3 className="text-base font-semibold leading-6 text-gray-900 mb-2">
                Thêm học sinh vào lớp học
              </h3>
              <p className="text-xs text-gray-500 mb-4">
                Chỉ hiển thị các học sinh đang hoạt động trong trung tâm và chưa tham gia lớp học này.
              </p>

              {addStudentsError && (
                <div id="add-students-error" className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700">
                  {addStudentsError}
                </div>
              )}

              <form onSubmit={handleCandidateSearchSubmit} className="flex gap-2 mb-3">
                <input
                  type="text"
                  placeholder="Tìm học sinh theo họ tên hoặc username..."
                  value={candidateSearchInput}
                  onChange={(e) => setCandidateSearchInput(e.target.value)}
                  className="rounded-md border border-gray-300 px-3 py-1.5 text-xs focus:border-indigo-500 focus:outline-none flex-1"
                />
                <button
                  type="submit"
                  id="btn-search-candidates"
                  className="rounded-md bg-gray-100 px-3 py-1.5 text-xs font-semibold text-gray-700 hover:bg-gray-200"
                >
                  Tìm kiếm
                </button>
                {candidateSearch && (
                  <button
                    type="button"
                    onClick={handleClearCandidateSearch}
                    className="rounded-md border border-gray-300 px-3 py-1.5 text-xs text-gray-600 hover:bg-gray-50"
                  >
                    Xóa tìm kiếm
                  </button>
                )}
              </form>

              <div className="flex items-center justify-between text-xs text-gray-600 bg-indigo-50 p-2 rounded mb-3">
                <span>
                  Đã chọn: <strong className="text-indigo-700">{selectedStudentIds.length}</strong> học sinh
                </span>
                {candidateList.length > 0 && (
                  <label className="flex items-center gap-1 cursor-pointer">
                    <input
                      type="checkbox"
                      id="checkbox-select-all-candidates"
                      checked={allCurrentPageSelected}
                      onChange={handleToggleSelectCurrentPage}
                      className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500 h-3.5 w-3.5"
                    />
                    <span>Chọn tất cả trang này</span>
                  </label>
                )}
              </div>

              <div className="border rounded-md overflow-hidden max-h-60 overflow-y-auto mb-3">
                {candidateState === "loading" && (
                  <div className="py-8 text-center text-xs text-gray-500">Đang tải danh sách học sinh...</div>
                )}
                {candidateState === "error" && (
                  <div className="py-8 text-center text-xs text-red-600">
                    Lỗi tải dữ liệu.{" "}
                    <button type="button" onClick={() => refetchCandidates()} className="underline font-semibold">
                      Thử lại
                    </button>
                  </div>
                )}
                {candidateState === "empty" && (
                  <div className="py-8 text-center text-xs text-gray-500">
                    {candidateSearch
                      ? "Không tìm thấy học sinh nào phù hợp với từ khóa."
                      : "Tất cả học sinh trong trung tâm đã có mặt trong lớp này."}
                  </div>
                )}
                {candidateState === "ready" && (
                  <table className="min-w-full divide-y divide-gray-200">
                    <thead className="bg-gray-50 sticky top-0">
                      <tr>
                        <th scope="col" className="w-8 px-3 py-2"></th>
                        <th scope="col" className="px-3 py-2 text-left text-xs font-medium uppercase text-gray-500">
                          Tên đăng nhập
                        </th>
                        <th scope="col" className="px-3 py-2 text-left text-xs font-medium uppercase text-gray-500">
                          Họ và tên
                        </th>
                        <th scope="col" className="px-3 py-2 text-left text-xs font-medium uppercase text-gray-500">
                          Khối
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-200 bg-white">
                      {candidateList.map((c) => {
                        const isSelected = selectedStudentIds.includes(c.studentId);
                        return (
                          <tr
                            key={c.studentId}
                            onClick={() => handleToggleSelectStudent(c.studentId)}
                            className={`cursor-pointer transition-colors ${
                              isSelected ? "bg-indigo-50" : "hover:bg-gray-50"
                            }`}
                          >
                            <td className="px-3 py-2 text-center" onClick={(e) => e.stopPropagation()}>
                              <input
                                type="checkbox"
                                id={`candidate-checkbox-${c.studentId}`}
                                checked={isSelected}
                                onChange={() => handleToggleSelectStudent(c.studentId)}
                                className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500 h-3.5 w-3.5"
                              />
                            </td>
                            <td className="px-3 py-2 text-xs font-mono font-medium text-gray-900">{c.username}</td>
                            <td className="px-3 py-2 text-xs text-gray-900 font-medium">{c.fullName}</td>
                            <td className="px-3 py-2 text-xs text-gray-500">Khối {c.gradeLevel}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                )}
              </div>

              {candidateMeta && candidateMeta.totalPages > 1 && (
                <div className="flex items-center justify-between text-xs text-gray-600 mb-4">
                  <span>
                    Trang {candidatePage} / {candidateMeta.totalPages} ({candidateMeta.totalItems} học sinh khả dụng)
                  </span>
                  <div className="space-x-1">
                    <button
                      type="button"
                      disabled={candidatePage <= 1 || isFetchingCandidates}
                      onClick={() => setCandidatePage((p) => p - 1)}
                      className="px-2.5 py-1 border rounded bg-white hover:bg-gray-50 disabled:opacity-50"
                    >
                      Trước
                    </button>
                    <button
                      type="button"
                      disabled={candidatePage >= candidateMeta.totalPages || isFetchingCandidates}
                      onClick={() => setCandidatePage((p) => p + 1)}
                      className="px-2.5 py-1 border rounded bg-white hover:bg-gray-50 disabled:opacity-50"
                    >
                      Sau
                    </button>
                  </div>
                </div>
              )}

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setIsAddStudentsModalOpen(false)}
                  className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  id="btn-submit-add-students"
                  onClick={handleAddStudentsSubmit}
                  disabled={selectedStudentIds.length === 0 || addStudentsMutation.isPending}
                  className="rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white hover:bg-indigo-500 disabled:opacity-50"
                >
                  {addStudentsMutation.isPending
                    ? "Đang thêm..."
                    : `Thêm (${selectedStudentIds.length}) học sinh`}
                </button>
              </div>
            </div>
          </div>
        )}

        {removingStudent && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-gray-500 bg-opacity-75 flex items-center justify-center p-4">
            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-md sm:p-6">
              <h3 className="text-base font-semibold leading-6 text-gray-900 mb-2">
                Xác nhận xóa học sinh khỏi lớp
              </h3>
              <p className="text-sm text-gray-500 mb-4">
                Bạn có chắc chắn muốn xóa học sinh <span className="font-bold text-gray-900">{removingStudent.fullName}</span> khỏi lớp học này?
              </p>
              {removeError && (
                <div id="remove-student-error" className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700">
                  {removeError}
                </div>
              )}
              <div className="flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setRemovingStudent(null)}
                  className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  id="btn-confirm-remove-student"
                  onClick={handleConfirmRemoveStudent}
                  disabled={removeStudentMutation.isPending}
                  className="rounded-md bg-red-600 px-3 py-2 text-sm font-semibold text-white hover:bg-red-500 disabled:opacity-50"
                >
                  {removeStudentMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
