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
import { permissions } from "../auth/permissions";
import { extractProblemDetails, isConcurrencyConflict } from "../utils/problemDetails";

const STATUS_LABELS: Record<ClassStatus, string> = {
  Active: "Hoạt động",
  Archived: "Đã lưu trữ",
};

export const ClassListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreateClass =
    hasPermission(permissions.classesCreate) &&
    hasPermission(permissions.subjectsRead) &&
    hasPermission(permissions.teachersRead);
  const canUpdateClass =
    hasPermission(permissions.classesUpdate) &&
    hasPermission(permissions.teachersRead);
  const canAddMembers =
    hasPermission(permissions.classesManageMembers) &&
    hasPermission(permissions.studentsRead);
  const canRemoveMembers = hasPermission(permissions.classesManageMembers);
  const canViewDashboard =
    hasPermission(permissions.dashboardsCenterRead) ||
    hasPermission(permissions.dashboardsTeacherRead);

  // List filter state
  const [page, setPage] = useState<number>(1);
  const pageSize = 10;
  const [status, setStatus] = useState<ClassStatus | "">("");
  const [statusInput, setStatusInput] = useState<ClassStatus | "">("");

  // Feedback notifications
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

  // Detail modal state
  const [viewingClassId, setViewingClassId] = useState<string | null>(null);
  const [memberPage, setMemberPage] = useState<number>(1);
  const [memberSearchInput, setMemberSearchInput] = useState("");
  const [memberSearch, setMemberSearch] = useState("");

  // Add students modal state
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

  const { data, isLoading, isFetching, isError, refetch } = useQuery({
    queryKey: ["classes", queryParams.page, queryParams.pageSize, queryParams.status],
    queryFn: () => organizationApi.listClasses(queryParams),
  });

  // Subjects query for create modal
  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
    isFetching: isFetchingSubjects,
    isError: isErrorSubjects,
  } = useQuery({
    queryKey: ["subjects", "active"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: isCreateModalOpen && canCreateClass,
  });

  // Teachers query for create & edit modals
  const shouldFetchTeachers =
    (isCreateModalOpen && canCreateClass) || (!!editingClass && canUpdateClass);

  const {
    data: teachersData,
    isLoading: isLoadingTeachers,
    isFetching: isFetchingTeachers,
    isError: isErrorTeachers,
  } = useQuery({
    queryKey: ["teachers", "active"],
    queryFn: () => organizationApi.listTeachers({ page: 1, pageSize: 100, status: "Active" }),
    enabled: shouldFetchTeachers,
  });

  // Detail queries
  const {
    data: classDetail,
    isLoading: isDetailLoading,
    isError: isDetailError,
  } = useQuery({
    queryKey: ["classDetail", viewingClassId],
    queryFn: () => organizationApi.getClass(viewingClassId!),
    enabled: !!viewingClassId,
  });

  const {
    data: classStudentsData,
    isLoading: isLoadingStudents,
    isError: isErrorStudents,
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

  // Candidate students query for adding members
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

  // Form helpers
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
    setSelectedStudentIds((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id]
    );
  };

  const candidateList = candidateStudentsData?.data ?? [];
  const candidateMeta = candidateStudentsData?.meta;
  const currentPageCandidateIds = candidateList.map((c) => c.studentId);
  const allCurrentPageSelected =
    currentPageCandidateIds.length > 0 &&
    currentPageCandidateIds.every((id) => selectedStudentIds.includes(id));

  const handleToggleSelectCurrentPage = () => {
    if (allCurrentPageSelected) {
      setSelectedStudentIds((prev) =>
        prev.filter((id) => !currentPageCandidateIds.includes(id))
      );
    } else {
      setSelectedStudentIds((prev) =>
        Array.from(new Set([...prev, ...currentPageCandidateIds]))
      );
    }
  };

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
        {/* Page Header */}
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

        {/* Global Feedback Banner */}
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
                className="text-xs font-semibold text-gray-500 hover:text-gray-700"
              >
                Đóng
              </button>
            </div>
          </div>
        )}

        {/* Filter Toolbar */}
        <div className="mb-8 overflow-hidden rounded-lg bg-white shadow">
          <div className="p-6">
            <form onSubmit={handleFilter} className="flex flex-col gap-4 sm:flex-row sm:items-end">
              <div className="w-full sm:max-w-xs">
                <label htmlFor="status-filter" className="block text-sm font-medium leading-6 text-gray-900">
                  Trạng thái
                </label>
                <div className="mt-2">
                  <select
                    id="status-filter"
                    name="status"
                    value={statusInput}
                    onChange={(e) => setStatusInput(e.target.value as ClassStatus | "")}
                    disabled={isFetching}
                    className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <option value="">Tất cả trạng thái</option>
                    <option value="Active">Hoạt động</option>
                    <option value="Archived">Đã lưu trữ</option>
                  </select>
                </div>
              </div>

              <div>
                <button
                  type="submit"
                  id="btn-filter-classes"
                  disabled={isFetching}
                  className="inline-flex w-full items-center justify-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:bg-indigo-400 sm:w-auto"
                >
                  Lọc
                </button>
              </div>
            </form>
          </div>
        </div>

        {/* Error Alert */}
        {isError && (
          <div className="mb-6 rounded-md bg-red-50 p-4 border border-red-200" role="alert">
            <div className="flex">
              <div className="ml-3">
                <h3 className="text-sm font-medium text-red-800">Đã xảy ra lỗi</h3>
                <div className="mt-2 text-sm text-red-700">
                  <p>Không thể tải danh sách lớp học. Vui lòng kiểm tra lại kết nối và thử lại sau.</p>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Main Class Table */}
        <div className="overflow-hidden bg-white shadow sm:rounded-lg">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-300" id="table-classes">
              <thead className="bg-gray-50">
                <tr>
                  <th scope="col" className="py-3.5 pl-4 pr-3 text-left text-sm font-semibold text-gray-900 sm:pl-6">
                    Tên lớp
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Năm học
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Môn học
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Giáo viên
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Số học sinh
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
                    <td colSpan={7} className="py-10 text-center text-sm text-gray-500">
                      Đang tải danh sách lớp học...
                    </td>
                  </tr>
                ) : data?.data.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-10 text-center text-sm text-gray-500">
                      Không tìm thấy lớp học nào phù hợp.
                    </td>
                  </tr>
                ) : (
                  data?.data.map((cls) => (
                    <tr key={cls.classId} className="hover:bg-gray-50 transition-colors">
                      <td className="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-gray-900 sm:pl-6">
                        {cls.className}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        {cls.academicYear}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        {cls.subject.subjectName}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        {cls.teacher.displayName}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-1 text-xs font-medium text-blue-700 ring-1 ring-inset ring-blue-700/10">
                          {cls.studentCount} học sinh
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm">
                        <span
                          className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset ${
                            cls.status === "Active"
                              ? "bg-green-50 text-green-700 ring-green-600/20"
                              : "bg-yellow-50 text-yellow-800 ring-yellow-600/20"
                          }`}
                        >
                          {STATUS_LABELS[cls.status] || cls.status}
                        </span>
                      </td>
                      <td className="whitespace-nowrap py-4 pl-3 pr-4 text-right text-sm font-medium sm:pr-6">
                        <div className="flex justify-end gap-2 items-center">
                          <button
                            type="button"
                            id={`btn-view-class-${cls.classId}`}
                            onClick={() => handleOpenDetail(cls.classId)}
                            className="text-indigo-600 hover:text-indigo-900 text-xs font-semibold px-2 py-1 rounded hover:bg-indigo-50"
                          >
                            Chi tiết
                          </button>

                          {canUpdateClass && (
                            <button
                              type="button"
                              id={`btn-edit-class-${cls.classId}`}
                              onClick={() => handleOpenEdit(cls)}
                              className="text-slate-600 hover:text-slate-900 text-xs font-semibold px-2 py-1 rounded hover:bg-slate-100"
                            >
                              Sửa
                            </button>
                          )}

                          {canViewDashboard && (
                            <Link
                              to={`/quan-ly/lop-hoc/${cls.classId}/tong-quan`}
                              id={`link-class-dashboard-${cls.classId}`}
                              className="text-teal-600 hover:text-teal-900 text-xs font-semibold px-2 py-1 rounded hover:bg-teal-50"
                            >
                              Dashboard
                            </Link>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {data?.meta && data.meta.totalPages > 1 && (
            <div className="flex items-center justify-between border-t border-gray-200 bg-white px-4 py-3 sm:px-6">
              <div className="hidden sm:flex sm:flex-1 sm:items-center sm:justify-between">
                <div>
                  <p className="text-sm text-gray-700">
                    Hiển thị trang <span className="font-medium">{data.meta.page}</span> /{" "}
                    <span className="font-medium">{data.meta.totalPages}</span> (Tổng số{" "}
                    <span className="font-medium">{data.meta.totalItems}</span> lớp học)
                  </p>
                </div>
                <div>
                  <nav className="isolate inline-flex -space-x-px rounded-md shadow-sm" aria-label="Phân trang">
                    <button
                      type="button"
                      id="btn-prev-page"
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                      disabled={page === 1 || isFetching}
                      className="relative inline-flex items-center rounded-l-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 focus:z-20 focus:outline-offset-0 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <span className="sr-only">Trang trước</span>
                      <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                        <path
                          fillRule="evenodd"
                          d="M12.79 5.23a.75.75 0 01-.02 1.06L8.832 10l3.938 3.71a.75.75 0 11-1.04 1.08l-4.5-4.25a.75.75 0 010-1.08l4.5-4.25a.75.75 0 011.06.02z"
                          clipRule="evenodd"
                        />
                      </svg>
                    </button>
                    <button
                      type="button"
                      id="btn-next-page"
                      onClick={() => setPage((p) => Math.min(data.meta.totalPages, p + 1))}
                      disabled={page === data.meta.totalPages || isFetching}
                      className="relative inline-flex items-center rounded-r-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 focus:z-20 focus:outline-offset-0 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <span className="sr-only">Trang sau</span>
                      <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                        <path
                          fillRule="evenodd"
                          d="M7.21 14.77a.75.75 0 01.02-1.06L11.168 10 7.23 6.29a.75.75 0 111.04-1.08l4.5 4.25a.75.75 0 010 1.08l-4.5 4.25a.75.75 0 01-1.06-.02z"
                          clipRule="evenodd"
                        />
                      </svg>
                    </button>
                  </nav>
                </div>
              </div>
              <div className="flex flex-1 justify-between sm:hidden">
                <button
                  type="button"
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1 || isFetching}
                  className="relative inline-flex items-center rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
                >
                  Trước
                </button>
                <button
                  type="button"
                  onClick={() => setPage((p) => Math.min(data.meta.totalPages, p + 1))}
                  disabled={page === data.meta.totalPages || isFetching}
                  className="relative ml-3 inline-flex items-center rounded-md border border-gray-300 bg-white px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
                >
                  Sau
                </button>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* CREATE CLASS MODAL */}
      {isCreateModalOpen && canCreateClass && (
        <div className="fixed inset-0 z-20 overflow-y-auto" role="dialog" aria-modal="true" aria-labelledby="modal-create-title">
          <div className="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={createClassMutation.isPending ? undefined : handleCancelCreate}
            />

            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-lg sm:p-6">
              <div>
                <h3 id="modal-create-title" className="text-lg font-semibold leading-6 text-gray-900 mb-5">
                  Thêm lớp học mới
                </h3>

                {createError && (
                  <div className="mb-4 rounded-md bg-red-50 p-4 border border-red-200" role="alert">
                    <p className="text-sm font-medium text-red-800">{createError}</p>
                  </div>
                )}

                <form onSubmit={handleCreateSubmit} className="space-y-4">
                  <div>
                    <label htmlFor="create-className" className="block text-sm font-medium leading-6 text-gray-900">
                      Tên lớp <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <input
                        type="text"
                        id="create-className"
                        required
                        maxLength={150}
                        value={className}
                        onChange={(e) => setClassName(e.target.value)}
                        disabled={createClassMutation.isPending}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50 disabled:bg-gray-100"
                        placeholder="VD: Toán 12A"
                      />
                    </div>
                  </div>

                  <div>
                    <label htmlFor="create-academicYear" className="block text-sm font-medium leading-6 text-gray-900">
                      Năm học <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <input
                        type="text"
                        id="create-academicYear"
                        required
                        maxLength={20}
                        value={academicYear}
                        onChange={(e) => setAcademicYear(e.target.value)}
                        disabled={createClassMutation.isPending}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50 disabled:bg-gray-100"
                        placeholder="VD: 2026-2027"
                      />
                    </div>
                  </div>

                  <div>
                    <label htmlFor="create-subjectId" className="block text-sm font-medium leading-6 text-gray-900">
                      Môn học <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <select
                        id="create-subjectId"
                        required
                        value={subjectId}
                        onChange={(e) => setSubjectId(e.target.value)}
                        disabled={createClassMutation.isPending || isLoadingSubjects || isFetchingSubjects || isErrorSubjects}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50 disabled:bg-gray-100"
                      >
                        <option value="">Chọn môn học</option>
                        {(isLoadingSubjects || isFetchingSubjects) && (
                          <option value="" disabled>Đang tải môn học...</option>
                        )}
                        {isErrorSubjects && !isLoadingSubjects && !isFetchingSubjects && (
                          <option value="" disabled>Lỗi tải môn học</option>
                        )}
                        {!isLoadingSubjects &&
                          !isFetchingSubjects &&
                          !isErrorSubjects &&
                          (subjectsData?.data?.length ?? 0) === 0 && (
                            <option value="" disabled>Không có môn học hoạt động</option>
                          )}
                        {subjectsData?.data?.map((sub) => (
                          <option key={sub.subjectId} value={sub.subjectId}>
                            {sub.subjectCode} - {sub.subjectName}
                          </option>
                        ))}
                      </select>
                    </div>
                    <p className="mt-1 text-xs text-gray-500">
                      Lưu ý: Môn học không thể thay đổi sau khi tạo để bảo toàn lịch sử giao bài và làm bài.
                    </p>
                  </div>

                  <div>
                    <label htmlFor="create-teacherId" className="block text-sm font-medium leading-6 text-gray-900">
                      Giáo viên <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <select
                        id="create-teacherId"
                        required
                        value={teacherId}
                        onChange={(e) => setTeacherId(e.target.value)}
                        disabled={createClassMutation.isPending || isLoadingTeachers || isFetchingTeachers || isErrorTeachers}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50 disabled:bg-gray-100"
                      >
                        <option value="">Chọn giáo viên</option>
                        {(isLoadingTeachers || isFetchingTeachers) && (
                          <option value="" disabled>Đang tải giáo viên...</option>
                        )}
                        {isErrorTeachers && !isLoadingTeachers && !isFetchingTeachers && (
                          <option value="" disabled>Lỗi tải giáo viên</option>
                        )}
                        {!isLoadingTeachers &&
                          !isFetchingTeachers &&
                          !isErrorTeachers &&
                          (teachersData?.data?.length ?? 0) === 0 && (
                            <option value="" disabled>Không có giáo viên hoạt động</option>
                          )}
                        {teachersData?.data?.map((teacher) => (
                          <option key={teacher.teacherId} value={teacher.teacherId}>
                            {teacher.displayName} ({teacher.username})
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>

                  <div className="mt-5 sm:mt-6 sm:grid sm:grid-flow-row-dense sm:grid-cols-2 sm:gap-3">
                    <button
                      type="submit"
                      id="btn-submit-create-class"
                      disabled={createClassMutation.isPending}
                      className="inline-flex w-full justify-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 sm:col-start-2 disabled:bg-indigo-400 disabled:cursor-not-allowed"
                    >
                      {createClassMutation.isPending ? "Đang tạo..." : "Lưu lớp học"}
                    </button>
                    <button
                      type="button"
                      id="btn-cancel-create-class"
                      onClick={handleCancelCreate}
                      disabled={createClassMutation.isPending}
                      className="mt-3 inline-flex w-full justify-center rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50 sm:col-start-1 sm:mt-0 disabled:opacity-50"
                    >
                      Hủy
                    </button>
                  </div>
                </form>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* EDIT CLASS MODAL */}
      {editingClass && canUpdateClass && (
        <div className="fixed inset-0 z-20 overflow-y-auto" role="dialog" aria-modal="true" aria-labelledby="modal-edit-title">
          <div className="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={updateClassMutation.isPending ? undefined : () => setEditingClass(null)}
            />

            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-lg sm:p-6">
              <div>
                <h3 id="modal-edit-title" className="text-lg font-semibold leading-6 text-gray-900 mb-5">
                  Cập nhật thông tin lớp học
                </h3>

                {editError && (
                  <div className="mb-4 rounded-md bg-red-50 p-4 border border-red-200" role="alert">
                    <p className="text-sm font-medium text-red-800">{editError}</p>
                  </div>
                )}

                <form onSubmit={handleEditSubmit} className="space-y-4">
                  <div>
                    <label htmlFor="edit-className" className="block text-sm font-medium leading-6 text-gray-900">
                      Tên lớp <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <input
                        type="text"
                        id="edit-className"
                        required
                        maxLength={150}
                        value={editClassName}
                        onChange={(e) => setEditClassName(e.target.value)}
                        disabled={updateClassMutation.isPending}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50 disabled:bg-gray-100"
                      />
                    </div>
                  </div>

                  <div>
                    <label className="block text-sm font-medium leading-6 text-gray-500">
                      Năm học (cố định)
                    </label>
                    <div className="mt-2">
                      <input
                        type="text"
                        disabled
                        value={editingClass.academicYear}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-500 bg-gray-100 shadow-sm ring-1 ring-inset ring-gray-200 sm:text-sm sm:leading-6 cursor-not-allowed"
                      />
                    </div>
                  </div>

                  <div>
                    <label className="block text-sm font-medium leading-6 text-gray-500">
                      Môn học (bất biến theo Contract 32)
                    </label>
                    <div className="mt-2">
                      <input
                        type="text"
                        disabled
                        value={editingClass.subject.subjectName}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-500 bg-gray-100 shadow-sm ring-1 ring-inset ring-gray-200 sm:text-sm sm:leading-6 cursor-not-allowed"
                      />
                    </div>
                    <p className="mt-1 text-xs text-gray-500">
                      Môn học không thể thay đổi sau khi tạo để bảo toàn lịch sử giao bài và evidence.
                    </p>
                  </div>

                  <div>
                    <label htmlFor="edit-teacherId" className="block text-sm font-medium leading-6 text-gray-900">
                      Giáo viên phụ trách <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <select
                        id="edit-teacherId"
                        required
                        value={editTeacherId}
                        onChange={(e) => setEditTeacherId(e.target.value)}
                        disabled={updateClassMutation.isPending || isLoadingTeachers}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50 disabled:bg-gray-100"
                      >
                        {isLoadingTeachers && <option value="" disabled>Đang tải giáo viên...</option>}
                        {teachersData?.data?.map((teacher) => (
                          <option key={teacher.teacherId} value={teacher.teacherId}>
                            {teacher.displayName} ({teacher.username})
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>

                  <div>
                    <label htmlFor="edit-status" className="block text-sm font-medium leading-6 text-gray-900">
                      Trạng thái lớp học <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <select
                        id="edit-status"
                        required
                        value={editStatus}
                        onChange={(e) => setEditStatus(e.target.value as ClassStatus)}
                        disabled={updateClassMutation.isPending}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50 disabled:bg-gray-100"
                      >
                        <option value="Active">Hoạt động (Active)</option>
                        <option value="Archived">Đã lưu trữ (Archived)</option>
                      </select>
                    </div>
                  </div>

                  <div className="mt-5 sm:mt-6 sm:grid sm:grid-flow-row-dense sm:grid-cols-2 sm:gap-3">
                    <button
                      type="submit"
                      id="btn-submit-edit-class"
                      disabled={updateClassMutation.isPending}
                      className="inline-flex w-full justify-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 sm:col-start-2 disabled:bg-indigo-400 disabled:cursor-not-allowed"
                    >
                      {updateClassMutation.isPending ? "Đang lưu..." : "Cập nhật"}
                    </button>
                    <button
                      type="button"
                      id="btn-cancel-edit-class"
                      onClick={() => setEditingClass(null)}
                      disabled={updateClassMutation.isPending}
                      className="mt-3 inline-flex w-full justify-center rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50 sm:col-start-1 sm:mt-0 disabled:opacity-50"
                    >
                      Hủy
                    </button>
                  </div>
                </form>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* CLASS DETAIL & MEMBERS MODAL */}
      {viewingClassId && (
        <div className="fixed inset-0 z-20 overflow-y-auto" role="dialog" aria-modal="true" aria-labelledby="modal-detail-title">
          <div className="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={() => setViewingClassId(null)}
            />

            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-4xl sm:p-6">
              {isDetailLoading ? (
                <div className="py-12 text-center text-sm text-gray-500">
                  Đang tải thông tin chi tiết lớp học...
                </div>
              ) : isDetailError || !classDetail ? (
                <div className="py-8 text-center text-sm text-red-600">
                  Không thể tải thông tin lớp học. Vui lòng thử lại.
                </div>
              ) : (
                <div>
                  {/* Header & Badges */}
                  <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between border-b border-gray-200 pb-4 mb-5 gap-3">
                    <div>
                      <div className="flex items-center gap-3">
                        <h3 id="modal-detail-title" className="text-xl font-bold leading-6 text-gray-900">
                          {classDetail.className}
                        </h3>
                        <span
                          className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${
                            classDetail.status === "Active"
                              ? "bg-green-50 text-green-700 ring-green-600/20"
                              : "bg-yellow-50 text-yellow-800 ring-yellow-600/20"
                          }`}
                        >
                          {STATUS_LABELS[classDetail.status] || classDetail.status}
                        </span>
                      </div>
                      <p className="mt-1 text-sm text-gray-500">
                        Năm học: {classDetail.academicYear} | Môn: {classDetail.subject.subjectName}
                      </p>
                    </div>

                    <div className="flex items-center gap-2">
                      {canViewDashboard && (
                        <Link
                          to={`/quan-ly/lop-hoc/${classDetail.classId}/tong-quan`}
                          id="btn-detail-view-dashboard"
                          className="inline-flex items-center rounded-md bg-teal-50 px-3 py-2 text-sm font-semibold text-teal-700 ring-1 ring-inset ring-teal-600/20 hover:bg-teal-100"
                        >
                          Xem Dashboard lớp
                        </Link>
                      )}
                      <button
                        type="button"
                        id="btn-close-class-detail"
                        onClick={() => setViewingClassId(null)}
                        className="rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
                      >
                        Đóng
                      </button>
                    </div>
                  </div>

                  {/* Summary Properties Grid */}
                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 bg-slate-50 p-4 rounded-lg border border-slate-200 mb-6">
                    <div>
                      <span className="text-xs font-medium text-slate-500 uppercase tracking-wider">Giáo viên phụ trách</span>
                      <p className="mt-1 text-sm font-semibold text-slate-900">{classDetail.teacher.displayName}</p>
                    </div>
                    <div>
                      <span className="text-xs font-medium text-slate-500 uppercase tracking-wider">Số lượng học sinh</span>
                      <p className="mt-1 text-sm font-semibold text-slate-900">{classDetail.studentCount} học sinh</p>
                    </div>
                    <div>
                      <span className="text-xs font-medium text-slate-500 uppercase tracking-wider">Phiên bản dữ liệu (RowVersion)</span>
                      <p className="mt-1 text-xs font-mono text-slate-600 truncate">{classDetail.rowVersion}</p>
                    </div>
                  </div>

                  {/* Members Section Header */}
                  <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between mb-4 gap-3">
                    <div>
                      <h4 className="text-base font-semibold text-gray-900">
                        Danh sách Học sinh trong Lớp
                      </h4>
                      <p className="text-xs text-gray-500">
                        Thành viên được quản lý và bảo toàn lịch sử làm bài, chấm điểm và đánh giá năng lực.
                      </p>
                    </div>

                    <div className="flex items-center gap-2">
                      {canAddMembers && (
                        <button
                          type="button"
                          id="btn-open-add-students"
                          onClick={handleOpenAddStudents}
                          className="inline-flex items-center rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white shadow-sm hover:bg-indigo-500"
                        >
                          + Thêm học sinh
                        </button>
                      )}
                    </div>
                  </div>

                  {/* Member Search Form */}
                  <form onSubmit={handleMemberSearchSubmit} className="mb-4 flex gap-2">
                    <input
                      type="text"
                      id="input-search-class-member"
                      placeholder="Tìm kiếm học sinh theo họ tên hoặc tên đăng nhập..."
                      value={memberSearchInput}
                      onChange={(e) => setMemberSearchInput(e.target.value)}
                      className="block w-full rounded-md border-0 py-1.5 text-sm text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600"
                    />
                    <button
                      type="submit"
                      id="btn-search-class-member"
                      className="rounded-md bg-white px-3 py-1.5 text-xs font-semibold text-gray-700 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
                    >
                      Tìm
                    </button>
                    {memberSearch && (
                      <button
                        type="button"
                        onClick={() => {
                          setMemberSearchInput("");
                          setMemberSearch("");
                          setMemberPage(1);
                        }}
                        className="rounded-md bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-200"
                      >
                        Bỏ lọc
                      </button>
                    )}
                  </form>

                  {/* Class Students Table */}
                  <div className="overflow-hidden border border-gray-200 rounded-lg">
                    <table className="min-w-full divide-y divide-gray-200">
                      <thead className="bg-gray-50">
                        <tr>
                          <th scope="col" className="py-2.5 pl-4 pr-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Họ và tên
                          </th>
                          <th scope="col" className="px-3 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Tên đăng nhập
                          </th>
                          <th scope="col" className="px-3 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Khối lớp
                          </th>
                          <th scope="col" className="px-3 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                            Trạng thái
                          </th>
                          {canRemoveMembers && (
                            <th scope="col" className="relative py-2.5 pl-3 pr-4 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                              Thao tác
                            </th>
                          )}
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-200 bg-white">
                        {isLoadingStudents ? (
                          <tr>
                            <td colSpan={canRemoveMembers ? 5 : 4} className="py-6 text-center text-sm text-gray-500">
                              Đang tải danh sách học sinh...
                            </td>
                          </tr>
                        ) : isErrorStudents ? (
                          <tr>
                            <td colSpan={canRemoveMembers ? 5 : 4} className="py-6 text-center text-sm text-red-500">
                              Không thể tải danh sách học sinh. Vui lòng thử lại.
                            </td>
                          </tr>
                        ) : classStudentsData?.data.length === 0 ? (
                          <tr>
                            <td colSpan={canRemoveMembers ? 5 : 4} className="py-6 text-center text-sm text-gray-500">
                              Chưa có học sinh nào trong lớp học này.
                            </td>
                          </tr>
                        ) : (
                          classStudentsData?.data.map((student: StudentDto) => (
                            <tr key={student.studentId} className="hover:bg-gray-50">
                              <td className="whitespace-nowrap py-3 pl-4 pr-3 text-sm font-medium text-gray-900">
                                {student.fullName}
                              </td>
                              <td className="whitespace-nowrap px-3 py-3 text-sm text-gray-500">
                                {student.username}
                              </td>
                              <td className="whitespace-nowrap px-3 py-3 text-sm text-gray-500">
                                Khối {student.gradeLevel}
                              </td>
                              <td className="whitespace-nowrap px-3 py-3 text-sm">
                                <span
                                  className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${
                                    student.status === "Active"
                                      ? "bg-green-50 text-green-700 ring-green-600/20"
                                      : "bg-gray-50 text-gray-700 ring-gray-600/20"
                                  }`}
                                >
                                  {student.status === "Active" ? "Hoạt động" : student.status}
                                </span>
                              </td>
                              {canRemoveMembers && (
                                <td className="whitespace-nowrap py-3 pl-3 pr-4 text-right text-sm font-medium">
                                  <button
                                    type="button"
                                    id={`btn-remove-student-${student.studentId}`}
                                    onClick={() =>
                                      setRemovingStudent({
                                        studentId: student.studentId,
                                        fullName: student.fullName,
                                      })
                                    }
                                    className="text-xs font-semibold text-red-600 hover:text-red-900 px-2 py-1 rounded hover:bg-red-50"
                                  >
                                    Xóa khỏi lớp
                                  </button>
                                </td>
                              )}
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>

                  {/* Member Pagination */}
                  {classStudentsData?.meta && classStudentsData.meta.totalPages > 1 && (
                    <div className="mt-3 flex items-center justify-between text-xs text-gray-500">
                      <div>
                        Trang {classStudentsData.meta.page} / {classStudentsData.meta.totalPages} (Tổng số {classStudentsData.meta.totalItems} học sinh)
                      </div>
                      <div className="flex gap-2">
                        <button
                          type="button"
                          id="btn-prev-member-page"
                          onClick={() => setMemberPage((p) => Math.max(1, p - 1))}
                          disabled={memberPage === 1}
                          className="rounded border border-gray-300 bg-white px-2 py-1 font-semibold text-gray-700 disabled:opacity-50"
                        >
                          Trước
                        </button>
                        <button
                          type="button"
                          id="btn-next-member-page"
                          onClick={() => setMemberPage((p) => Math.min(classStudentsData.meta.totalPages, p + 1))}
                          disabled={memberPage === classStudentsData.meta.totalPages}
                          className="rounded border border-gray-300 bg-white px-2 py-1 font-semibold text-gray-700 disabled:opacity-50"
                        >
                          Sau
                        </button>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ADD STUDENTS MODAL */}
      {isAddStudentsModalOpen && canAddMembers && viewingClassId && (
        <div className="fixed inset-0 z-30 overflow-y-auto" role="dialog" aria-modal="true" aria-labelledby="modal-add-students-title">
          <div className="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={addStudentsMutation.isPending ? undefined : () => setIsAddStudentsModalOpen(false)}
            />

            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-2xl sm:p-6">
              <div>
                <h3 id="modal-add-students-title" className="text-lg font-semibold leading-6 text-gray-900 mb-2">
                  Thêm học sinh vào lớp học
                </h3>
                <p className="text-xs text-gray-500 mb-4">
                  Chọn học sinh đang hoạt động trong trung tâm để thêm vào danh sách lớp.
                </p>

                {addStudentsError && (
                  <div className="mb-4 rounded-md bg-red-50 p-3 border border-red-200" role="alert">
                    <p className="text-xs font-medium text-red-800">{addStudentsError}</p>
                  </div>
                )}

                {/* Search input for candidates */}
                <form onSubmit={handleCandidateSearchSubmit} className="mb-4 flex gap-2">
                  <input
                    type="text"
                    id="input-filter-candidate-students"
                    placeholder="Tìm kiếm học sinh theo tên hoặc tên đăng nhập..."
                    value={candidateSearchInput}
                    onChange={(e) => setCandidateSearchInput(e.target.value)}
                    className="block w-full rounded-md border-0 py-1.5 text-sm text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600"
                  />
                  <button
                    type="submit"
                    id="btn-search-candidate-students"
                    className="rounded-md bg-white px-3 py-1.5 text-xs font-semibold text-gray-700 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
                  >
                    Tìm
                  </button>
                  {candidateSearch && (
                    <button
                      type="button"
                      id="btn-clear-candidate-search"
                      onClick={handleClearCandidateSearch}
                      className="rounded-md bg-slate-100 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-200"
                    >
                      Bỏ lọc
                    </button>
                  )}
                </form>

                {/* Candidate Selection List */}
                <div className="max-h-64 overflow-y-auto border border-gray-200 rounded-md divide-y divide-gray-100">
                  {isErrorCandidates ? (
                    <div className="p-6 text-center" role="alert">
                      <p className="text-sm font-medium text-red-700 mb-2">
                        Không thể tải danh sách học sinh khả dụng.
                      </p>
                      <p className="text-xs text-red-600 mb-3">
                        Vui lòng kiểm tra quyền truy cập (yêu cầu quyền quản lý thành viên và xem học sinh) hoặc thử lại sau.
                      </p>
                      <button
                        type="button"
                        id="btn-retry-candidate-students"
                        onClick={() => refetchCandidates()}
                        className="inline-flex items-center rounded bg-red-100 px-3 py-1.5 text-xs font-semibold text-red-800 hover:bg-red-200"
                      >
                        Thử lại
                      </button>
                    </div>
                  ) : isLoadingCandidates || isFetchingCandidates ? (
                    <div className="p-4 text-center text-xs text-gray-500">Đang tải danh sách học sinh khả dụng...</div>
                  ) : candidateList.length === 0 ? (
                    <div className="p-4 text-center text-xs text-gray-500">
                      Không có học sinh khả dụng để thêm (tất cả học sinh hoạt động đã vào lớp hoặc không khớp tìm kiếm).
                    </div>
                  ) : (
                    candidateList.map((s) => {
                      const isSelected = selectedStudentIds.includes(s.studentId);
                      return (
                        <div
                          key={s.studentId}
                          onClick={() => handleToggleSelectStudent(s.studentId)}
                          className={`flex items-center justify-between p-3 cursor-pointer transition-colors ${
                            isSelected ? "bg-indigo-50" : "hover:bg-gray-50"
                          }`}
                        >
                          <div className="flex items-center gap-3">
                            <input
                              type="checkbox"
                              id={`checkbox-student-${s.studentId}`}
                              checked={isSelected}
                              onChange={() => {}}
                              onClick={(e) => {
                                e.stopPropagation();
                                handleToggleSelectStudent(s.studentId);
                              }}
                              className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-600 cursor-pointer"
                            />
                            <div>
                              <p className="text-sm font-medium text-gray-900">{s.fullName}</p>
                              <p className="text-xs text-gray-500">@{s.username} • Khối {s.gradeLevel}</p>
                            </div>
                          </div>
                          <span className="text-xs text-slate-500">
                            {s.activeClassCount} lớp đang học
                          </span>
                        </div>
                      );
                    })
                  )}
                </div>

                {/* Candidate Pagination */}
                {candidateMeta && candidateMeta.totalPages > 1 && (
                  <div className="mt-3 flex items-center justify-between text-xs text-gray-500">
                    <div>
                      Trang {candidateMeta.page} / {candidateMeta.totalPages} (Tổng số {candidateMeta.totalItems} học sinh khả dụng)
                    </div>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        id="btn-prev-candidate-page"
                        onClick={() => setCandidatePage((p) => Math.max(1, p - 1))}
                        disabled={candidatePage === 1 || isFetchingCandidates}
                        className="rounded border border-gray-300 bg-white px-2 py-1 font-semibold text-gray-700 disabled:opacity-50"
                      >
                        Trước
                      </button>
                      <button
                        type="button"
                        id="btn-next-candidate-page"
                        onClick={() => setCandidatePage((p) => Math.min(candidateMeta.totalPages, p + 1))}
                        disabled={candidatePage === candidateMeta.totalPages || isFetchingCandidates}
                        className="rounded border border-gray-300 bg-white px-2 py-1 font-semibold text-gray-700 disabled:opacity-50"
                      >
                        Sau
                      </button>
                    </div>
                  </div>
                )}

                {/* Selected Count & Actions */}
                <div className="mt-4 flex items-center justify-between">
                  <span className="text-xs font-medium text-gray-600">
                    Đã chọn: <span className="text-indigo-600 font-bold">{selectedStudentIds.length}</span> học sinh
                  </span>

                  <div className="flex gap-2">
                    {candidateList.length > 0 && (
                      <button
                        type="button"
                        id="btn-select-all-candidates"
                        onClick={handleToggleSelectCurrentPage}
                        className="rounded bg-white px-2 py-1 text-xs font-medium text-gray-700 ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
                      >
                        {allCurrentPageSelected ? "Bỏ chọn trang này" : "Chọn tất cả trang này"}
                      </button>
                    )}
                    {selectedStudentIds.length > 0 && (
                      <button
                        type="button"
                        id="btn-clear-all-selected-candidates"
                        onClick={() => setSelectedStudentIds([])}
                        className="rounded bg-white px-2 py-1 text-xs font-medium text-gray-700 ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
                      >
                        Bỏ chọn tất cả
                      </button>
                    )}
                  </div>
                </div>

                <div className="mt-5 sm:mt-6 sm:grid sm:grid-flow-row-dense sm:grid-cols-2 sm:gap-3">
                  <button
                    type="button"
                    id="btn-confirm-add-students"
                    disabled={selectedStudentIds.length === 0 || addStudentsMutation.isPending}
                    onClick={handleAddStudentsSubmit}
                    className="inline-flex w-full justify-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 sm:col-start-2 disabled:bg-indigo-400 disabled:cursor-not-allowed"
                  >
                    {addStudentsMutation.isPending ? "Đang thêm..." : `Xác nhận thêm (${selectedStudentIds.length})`}
                  </button>
                  <button
                    type="button"
                    id="btn-cancel-add-students"
                    disabled={addStudentsMutation.isPending}
                    onClick={() => setIsAddStudentsModalOpen(false)}
                    className="mt-3 inline-flex w-full justify-center rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50 sm:col-start-1 sm:mt-0 disabled:opacity-50"
                  >
                    Hủy
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* REMOVE STUDENT CONFIRMATION MODAL */}
      {removingStudent && canRemoveMembers && viewingClassId && (
        <div className="fixed inset-0 z-30 overflow-y-auto" role="dialog" aria-modal="true" aria-labelledby="modal-remove-title">
          <div className="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
            <div
              className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
              onClick={removeStudentMutation.isPending ? undefined : () => setRemovingStudent(null)}
            />

            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-md sm:p-6">
              <div>
                <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-red-100">
                  <svg className="h-6 w-6 text-red-600" fill="none" viewBox="0 0 24 24" strokeWidth="1.5" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
                  </svg>
                </div>
                <div className="mt-3 text-center sm:mt-5">
                  <h3 id="modal-remove-title" className="text-base font-semibold leading-6 text-gray-900">
                    Xác nhận xóa học sinh khỏi lớp
                  </h3>
                  <div className="mt-2 text-left bg-amber-50 p-3 rounded border border-amber-200">
                    <p className="text-xs text-amber-800">
                      Bạn có chắc chắn muốn xóa học sinh <span className="font-bold">{removingStudent.fullName}</span> khỏi lớp này?
                    </p>
                    <p className="mt-2 text-xs text-amber-700">
                      Hành động này sẽ chuyển trạng thái tham gia lớp sang <span className="font-semibold">Removed</span>. Toàn bộ lịch sử bài làm (Attempts), điểm số và evidence học tập trước đó vẫn được bảo toàn nguyên vẹn.
                    </p>
                  </div>
                </div>

                {removeError && (
                  <div className="mt-3 rounded-md bg-red-50 p-2 border border-red-200" role="alert">
                    <p className="text-xs font-medium text-red-800">{removeError}</p>
                  </div>
                )}

                <div className="mt-5 sm:mt-6 sm:grid sm:grid-flow-row-dense sm:grid-cols-2 sm:gap-3">
                  <button
                    type="button"
                    id="btn-confirm-remove-student"
                    disabled={removeStudentMutation.isPending}
                    onClick={handleConfirmRemoveStudent}
                    className="inline-flex w-full justify-center rounded-md bg-red-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-red-500 sm:col-start-2 disabled:bg-red-400"
                  >
                    {removeStudentMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
                  </button>
                  <button
                    type="button"
                    id="btn-cancel-remove-student"
                    disabled={removeStudentMutation.isPending}
                    onClick={() => setRemovingStudent(null)}
                    className="mt-3 inline-flex w-full justify-center rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50 sm:col-start-1 sm:mt-0 disabled:opacity-50"
                  >
                    Hủy
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
