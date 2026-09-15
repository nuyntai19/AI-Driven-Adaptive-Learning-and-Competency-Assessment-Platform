import React, { useState, useMemo } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { isAxiosError } from "axios";
import { organizationApi } from "../api/organizationApi";
import type {
  SubjectDto,
  CreateSubjectRequest,
  UpdateSubjectRequest,
} from "../types/organization";
import type { ProblemDetails } from "../types/auth";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { isConcurrencyConflict, mapSafeOperationalError } from "../utils/problemDetails";

export const SubjectListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreate = hasPermission(permissions.subjectsCreate);
  const canUpdate = hasPermission(permissions.subjectsUpdate);
  const canDelete = hasPermission(permissions.subjectsDelete);
  const canViewGraph =
    hasPermission(permissions.nodesRead) && hasPermission(permissions.edgesRead);

  // Filters
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all");
  const [searchTerm, setSearchTerm] = useState<string>("");

  // Modals state
  const [isCreating, setIsCreating] = useState(false);
  const [createCode, setCreateCode] = useState("");
  const [createName, setCreateName] = useState("");
  const [createDescription, setCreateDescription] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);

  const [detailSubject, setDetailSubject] = useState<SubjectDto | null>(null);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [editingSubject, setEditingSubject] = useState<SubjectDto | null>(null);
  const [editCode, setEditCode] = useState("");
  const [editName, setEditName] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editIsActive, setEditIsActive] = useState(true);
  const [editError, setEditError] = useState<string | null>(null);

  const [deletingSubject, setDeletingSubject] = useState<SubjectDto | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const [globalSuccessMessage, setGlobalSuccessMessage] = useState<string | null>(null);

  // Query subjects
  const isActiveParam = statusFilter === "all" ? undefined : statusFilter === "active";
  const {
    data: subjectsData,
    isLoading,
    isError,
    refetch,
  } = useQuery({
    queryKey: ["subjects", "list", statusFilter],
    queryFn: () => organizationApi.listSubjects(isActiveParam),
  });

  const subjects = useMemo(() => {
    const list = subjectsData?.data ?? [];
    if (!searchTerm.trim()) return list;
    const term = searchTerm.trim().toLowerCase();
    return list.filter(
      (s) =>
        s.subjectCode.toLowerCase().includes(term) ||
        s.subjectName.toLowerCase().includes(term) ||
        (s.description && s.description.toLowerCase().includes(term))
    );
  }, [subjectsData?.data, searchTerm]);

  // Invalidate queries helper
  const invalidateAllSubjectQueries = async () => {
    await queryClient.invalidateQueries({ queryKey: ["subjects"] });
  };

  // Create Mutation
  const createMutation = useMutation({
    mutationFn: (req: CreateSubjectRequest) => organizationApi.createSubject(req),
    onSuccess: async () => {
      await invalidateAllSubjectQueries();
      setIsCreating(false);
      setCreateCode("");
      setCreateName("");
      setCreateDescription("");
      setCreateError(null);
      setGlobalSuccessMessage("Tạo môn học mới thành công!");
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (err.response?.status === 409) {
          setCreateError(mapSafeOperationalError(err, "Mã môn học này đã tồn tại trong trung tâm. Vui lòng chọn mã khác."));
          return;
        }
        setCreateError(mapSafeOperationalError(err, "Không thể tạo môn học. Vui lòng kiểm tra lại thông tin."));
        return;
      }
      setCreateError("Đã xảy ra lỗi không mong muốn khi tạo môn học.");
    },
  });

  // Update Mutation
  const updateMutation = useMutation({
    mutationFn: ({ id, req }: { id: string; req: UpdateSubjectRequest }) =>
      organizationApi.updateSubject(id, req),
    onSuccess: async () => {
      await invalidateAllSubjectQueries();
      setEditingSubject(null);
      setEditError(null);
      setGlobalSuccessMessage("Cập nhật môn học thành công!");
    },
    onError: async (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (isConcurrencyConflict(err)) {
          await invalidateAllSubjectQueries();
          if (editingSubject) {
            try {
              const latest = await organizationApi.getSubject(editingSubject.subjectId);
              setEditingSubject(latest);
            } catch {
              // The canonical refetch below remains the source of truth if detail refresh fails.
            }
          }
          setEditError(
            "Dữ liệu môn học vừa thay đổi bởi người dùng khác. Hệ thống đã nạp phiên bản mới nhất; vui lòng kiểm tra lại rồi gửi lại."
          );
          return;
        }
        setEditError(mapSafeOperationalError(err, "Không thể cập nhật môn học. Vui lòng thử lại."));
        return;
      }
      setEditError("Đã xảy ra lỗi khi cập nhật môn học.");
    },
  });

  // Delete Mutation
  const deleteMutation = useMutation({
    mutationFn: (id: string) => organizationApi.deleteSubject(id),
    onSuccess: async () => {
      await invalidateAllSubjectQueries();
      setDeletingSubject(null);
      setDeleteError(null);
      setGlobalSuccessMessage("Đã xóa môn học thành công!");
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (err.response?.status === 409) {
          setDeleteError(mapSafeOperationalError(err, "Không thể xóa môn học này do đang có dữ liệu liên kết (lớp học, giáo trình, mục tiêu học tập, hoặc dữ liệu phân tích)."));
          return;
        }
        setDeleteError(mapSafeOperationalError(err, "Không thể xóa môn học. Vui lòng thử lại."));
        return;
      }
      setDeleteError("Đã xảy ra lỗi khi xóa môn học.");
    },
  });

  const handleOpenCreate = () => {
    setCreateCode("");
    setCreateName("");
    setCreateDescription("");
    setCreateError(null);
    setIsCreating(true);
  };

  const handleOpenDetail = async (subject: SubjectDto) => {
    setDetailSubject(subject);
    setDetailError(null);
    setIsDetailLoading(true);
    try {
      setDetailSubject(await organizationApi.getSubject(subject.subjectId));
    } catch (err) {
      setDetailError(mapSafeOperationalError(err, "Không thể tải chi tiết môn học mới nhất."));
    } finally {
      setIsDetailLoading(false);
    }
  };

  const handleCreateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCreateError(null);

    const trimmedCode = createCode.trim().toUpperCase();
    const trimmedName = createName.trim();

    if (!trimmedCode) {
      setCreateError("Mã môn học không được để trống.");
      return;
    }
    if (trimmedCode.length > 32) {
      setCreateError("Mã môn học không được vượt quá 32 ký tự.");
      return;
    }
    if (!trimmedName) {
      setCreateError("Tên môn học không được để trống.");
      return;
    }
    if (trimmedName.length > 100) {
      setCreateError("Tên môn học không được vượt quá 100 ký tự.");
      return;
    }
    if (createDescription.length > 500) {
      setCreateError("Mô tả không được vượt quá 500 ký tự.");
      return;
    }

    createMutation.mutate({
      subjectCode: trimmedCode,
      subjectName: trimmedName,
      description: createDescription.trim() || null,
    });
  };

  const handleOpenEdit = async (subject: SubjectDto) => {
    setEditError(null);
    try {
      const latest = await organizationApi.getSubject(subject.subjectId);
      setEditingSubject(latest);
      setEditCode(latest.subjectCode);
      setEditName(latest.subjectName);
      setEditDescription(latest.description || "");
      setEditIsActive(latest.isActive);
    } catch (err) {
      setGlobalSuccessMessage(null);
      setEditingSubject(subject);
      setEditCode(subject.subjectCode);
      setEditName(subject.subjectName);
      setEditDescription(subject.description || "");
      setEditIsActive(subject.isActive);
      setEditError(mapSafeOperationalError(err, "Không thể tải phiên bản môn học mới nhất để chỉnh sửa."));
    }
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingSubject) return;
    setEditError(null);

    const trimmedCode = editCode.trim().toUpperCase();
    const trimmedName = editName.trim();
    if (!trimmedCode || trimmedCode.length > 32) {
      setEditError("Mã môn học phải có từ 1 đến 32 ký tự.");
      return;
    }
    if (!trimmedName) {
      setEditError("Tên môn học không được để trống.");
      return;
    }
    if (trimmedName.length > 100) {
      setEditError("Tên môn học không được vượt quá 100 ký tự.");
      return;
    }
    if (editDescription.length > 500) {
      setEditError("Mô tả không được vượt quá 500 ký tự.");
      return;
    }

    updateMutation.mutate({
      id: editingSubject.subjectId,
      req: {
        subjectCode: trimmedCode,
        subjectName: trimmedName,
        description: editDescription.trim() || null,
        isActive: editIsActive,
        rowVersion: editingSubject.rowVersion,
      },
    });
  };

  const handleOpenDelete = (subject: SubjectDto) => {
    setDeletingSubject(subject);
    setDeleteError(null);
  };

  const handleDeleteConfirm = () => {
    if (!deletingSubject) return;
    setDeleteError(null);
    deleteMutation.mutate(deletingSubject.subjectId);
  };

  return (
    <div className="min-h-screen bg-slate-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold leading-7 text-slate-900 sm:truncate sm:text-3xl sm:tracking-tight">
              Quản lý môn học
            </h1>
            <p className="mt-1 text-sm text-slate-500">
              Quản trị danh mục môn học, trạng thái kích hoạt và cấu trúc sơ đồ kiến thức của trung tâm.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            {canViewGraph && (
              <Link
                to="/kien-thuc/do-thi"
                className="inline-flex items-center rounded-lg bg-white px-3 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
              >
                Xem đồ thị kiến thức →
              </Link>
            )}
            <Link
              to="/"
              className="inline-flex items-center rounded-lg bg-white px-3 py-2 text-sm font-semibold text-slate-700 shadow-sm ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
            >
              Trang chủ
            </Link>
          </div>
        </div>

        {/* Global Success Notification */}
        {globalSuccessMessage && (
          <div className="mb-6 rounded-lg bg-emerald-50 p-4 ring-1 ring-emerald-200">
            <div className="flex items-center justify-between">
              <p className="text-sm font-medium text-emerald-800">{globalSuccessMessage}</p>
              <button
                type="button"
                onClick={() => setGlobalSuccessMessage(null)}
                className="text-xs font-semibold text-emerald-700 hover:text-emerald-900"
              >
                Đóng
              </button>
            </div>
          </div>
        )}

        {/* Filter Card */}
        <div className="mb-6 rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
            <div className="flex flex-col sm:flex-row gap-4 flex-1">
              {/* Search */}
              <div className="flex-1 max-w-md">
                <label
                  htmlFor="subject-search-input"
                  className="block text-xs font-semibold uppercase tracking-wider text-slate-500 mb-1"
                >
                  Tìm kiếm môn học
                </label>
                <input
                  id="subject-search-input"
                  type="text"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  placeholder="Nhập mã hoặc tên môn học..."
                  className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                />
              </div>

              {/* Status Filter */}
              <div className="w-48">
                <label
                  htmlFor="subject-status-filter"
                  className="block text-xs font-semibold uppercase tracking-wider text-slate-500 mb-1"
                >
                  Trạng thái
                </label>
                <select
                  id="subject-status-filter"
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value as "all" | "active" | "inactive")}
                  className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                >
                  <option value="all">Tất cả</option>
                  <option value="active">Đang hoạt động</option>
                  <option value="inactive">Ngừng hoạt động</option>
                </select>
              </div>
            </div>

            {/* Create Button */}
            {canCreate && (
              <div>
                <button
                  type="button"
                  id="btn-open-create-subject"
                  onClick={handleOpenCreate}
                  className="inline-flex items-center justify-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
                >
                  + Thêm môn học
                </button>
              </div>
            )}
          </div>
        </div>

        {/* Subjects Table Card */}
        <div className="overflow-hidden rounded-xl bg-white shadow-sm ring-1 ring-slate-200">
          <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between">
            <h2 className="text-base font-bold text-slate-900">
              Danh sách môn học ({subjects.length})
            </h2>
            <button
              type="button"
              onClick={() => refetch()}
              className="text-xs font-semibold text-indigo-600 hover:text-indigo-500"
            >
              Làm mới
            </button>
          </div>

          {isLoading ? (
            <div className="p-12 text-center">
              <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-indigo-600 border-r-transparent align-[-0.125em]" />
              <p className="mt-4 text-sm font-medium text-slate-600">Đang tải danh sách môn học...</p>
            </div>
          ) : isError ? (
            <div className="p-6 text-center">
              <p className="text-sm font-medium text-red-600">
                Không thể tải danh sách môn học từ hệ thống.
              </p>
              <button
                type="button"
                onClick={() => refetch()}
                className="mt-2 text-xs font-bold text-indigo-600 hover:text-indigo-500 underline"
              >
                Thử lại
              </button>
            </div>
          ) : subjects.length === 0 ? (
            <div className="p-12 text-center">
              <p className="text-sm font-medium text-slate-500">
                {searchTerm
                  ? "Không tìm thấy môn học nào khớp với từ khóa tìm kiếm."
                  : "Chưa có môn học nào trong trung tâm."}
              </p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-slate-200" id="subjects-table">
                <thead className="bg-slate-50">
                  <tr>
                    <th scope="col" className="py-3.5 pl-6 pr-3 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                      Mã môn học
                    </th>
                    <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                      Tên môn học
                    </th>
                    <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                      Mô tả
                    </th>
                    <th scope="col" className="px-3 py-3.5 text-left text-xs font-bold uppercase tracking-wider text-slate-500">
                      Trạng thái
                    </th>
                    <th scope="col" className="py-3.5 pl-3 pr-6 text-right text-xs font-bold uppercase tracking-wider text-slate-500">
                      Thao tác
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 bg-white">
                  {subjects.map((subject) => (
                    <tr key={subject.subjectId} className="hover:bg-slate-50 transition-colors">
                      <td className="whitespace-nowrap py-4 pl-6 pr-3 text-sm font-bold text-indigo-700">
                        {subject.subjectCode}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm font-semibold text-slate-900">
                        {subject.subjectName}
                      </td>
                      <td className="px-3 py-4 text-sm text-slate-500 max-w-xs truncate">
                        {subject.description || "—"}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm">
                        {subject.isActive ? (
                          <span className="inline-flex items-center rounded-md bg-emerald-50 px-2 py-1 text-xs font-medium text-emerald-700 ring-1 ring-inset ring-emerald-600/20">
                            Đang hoạt động
                          </span>
                        ) : (
                          <span className="inline-flex items-center rounded-md bg-slate-100 px-2 py-1 text-xs font-medium text-slate-600 ring-1 ring-inset ring-slate-500/20">
                            Ngừng hoạt động
                          </span>
                        )}
                      </td>
                      <td className="whitespace-nowrap py-4 pl-3 pr-6 text-right text-sm font-medium space-x-2">
                        <button
                          type="button"
                          id={`btn-view-subject-${subject.subjectId}`}
                          onClick={() => void handleOpenDetail(subject)}
                          className="inline-flex items-center rounded bg-slate-50 px-2.5 py-1.5 text-xs font-semibold text-slate-700 ring-1 ring-inset ring-slate-300 hover:bg-slate-100"
                        >
                          Chi tiết
                        </button>

                        {canViewGraph && (
                          <Link
                            to={`/kien-thuc/do-thi?subjectId=${subject.subjectId}`}
                            id={`link-graph-subject-${subject.subjectId}`}
                            className="inline-flex items-center rounded bg-indigo-50 px-2.5 py-1.5 text-xs font-semibold text-indigo-700 ring-1 ring-inset ring-indigo-200 hover:bg-indigo-100"
                          >
                            Đồ thị
                          </Link>
                        )}

                        {canUpdate && (
                          <button
                            type="button"
                            id={`btn-edit-subject-${subject.subjectId}`}
                            onClick={() => void handleOpenEdit(subject)}
                            className="inline-flex items-center rounded bg-amber-50 px-2.5 py-1.5 text-xs font-semibold text-amber-700 ring-1 ring-inset ring-amber-300 hover:bg-amber-100"
                          >
                            Sửa
                          </button>
                        )}

                        {canDelete && (
                          <button
                            type="button"
                            id={`btn-delete-subject-${subject.subjectId}`}
                            onClick={() => handleOpenDelete(subject)}
                            className="inline-flex items-center rounded bg-red-50 px-2.5 py-1.5 text-xs font-semibold text-red-700 ring-1 ring-inset ring-red-300 hover:bg-red-100"
                          >
                            Xóa
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Modal 1: Create Subject Modal */}
        {isCreating && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl ring-1 ring-slate-200">
              <div className="flex items-center justify-between border-b border-slate-100 pb-4 mb-4">
                <h3 className="text-lg font-bold text-slate-900">Thêm môn học mới</h3>
                <button
                  type="button"
                  onClick={() => setIsCreating(false)}
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                >
                  ✕
                </button>
              </div>

              {createError && (
                <div className="mb-4 rounded-lg bg-red-50 p-3 ring-1 ring-red-200 text-xs font-medium text-red-700" role="alert">
                  {createError}
                </div>
              )}

              <form onSubmit={handleCreateSubmit} className="space-y-4">
                <div>
                  <label htmlFor="create-subject-code" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Mã môn học <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="create-subject-code"
                    type="text"
                    required
                    maxLength={32}
                    placeholder="VD: MATH, PHYS, CHEM"
                    value={createCode}
                    onChange={(e) => setCreateCode(e.target.value.toUpperCase())}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 uppercase focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm font-mono"
                  />
                  <p className="mt-1 text-xs text-slate-400">Tối đa 32 ký tự, viết hoa, không trùng lặp.</p>
                </div>

                <div>
                  <label htmlFor="create-subject-name" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Tên môn học <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="create-subject-name"
                    type="text"
                    required
                    maxLength={100}
                    placeholder="VD: Toán học THPT"
                    value={createName}
                    onChange={(e) => setCreateName(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="create-subject-desc" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Mô tả (tùy chọn)
                  </label>
                  <textarea
                    id="create-subject-desc"
                    rows={3}
                    maxLength={500}
                    placeholder="Thông tin tổng quan về môn học và chương trình..."
                    value={createDescription}
                    onChange={(e) => setCreateDescription(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                  <p className="mt-1 text-xs text-slate-400">{createDescription.length}/500 ký tự</p>
                </div>

                <div className="mt-6 flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
                  <button
                    type="button"
                    onClick={() => setIsCreating(false)}
                    disabled={createMutation.isPending}
                    className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-create-subject"
                    disabled={createMutation.isPending}
                    className="inline-flex items-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {createMutation.isPending ? "Đang tạo..." : "Xác nhận tạo"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Modal 2: Subject Detail Modal */}
        {detailSubject && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div
              className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl ring-1 ring-slate-200"
              role="dialog"
              aria-modal="true"
              aria-labelledby="subject-detail-title"
            >
              <div className="flex items-center justify-between border-b border-slate-100 pb-4 mb-4">
                <h3 id="subject-detail-title" className="text-lg font-bold text-slate-900">Chi tiết môn học</h3>
                <button
                  type="button"
                  onClick={() => setDetailSubject(null)}
                  aria-label="Đóng chi tiết môn học"
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                >
                  ✕
                </button>
              </div>

              {isDetailLoading && (
                <p className="mb-4 text-sm font-medium text-indigo-700" role="status">
                  Đang nạp chi tiết mới nhất...
                </p>
              )}
              {detailError && (
                <div className="mb-4 rounded-lg bg-red-50 p-3 text-xs font-medium text-red-700 ring-1 ring-red-200" role="alert">
                  {detailError}
                </div>
              )}

              <div className="space-y-4">
                <div>
                  <span className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                    Mã môn học
                  </span>
                  <p className="mt-0.5 text-base font-bold text-indigo-700 font-mono">
                    {detailSubject.subjectCode}
                  </p>
                </div>

                <div>
                  <span className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                    Tên môn học
                  </span>
                  <p className="mt-0.5 text-base font-semibold text-slate-900">
                    {detailSubject.subjectName}
                  </p>
                </div>

                <div>
                  <span className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                    Trạng thái
                  </span>
                  <div className="mt-1">
                    {detailSubject.isActive ? (
                      <span className="inline-flex items-center rounded-md bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700 ring-1 ring-inset ring-emerald-600/20">
                        Đang hoạt động
                      </span>
                    ) : (
                      <span className="inline-flex items-center rounded-md bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-600 ring-1 ring-inset ring-slate-500/20">
                        Ngừng hoạt động
                      </span>
                    )}
                  </div>
                </div>

                <div>
                  <span className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                    Mô tả
                  </span>
                  <p className="mt-0.5 text-sm text-slate-700 whitespace-pre-wrap">
                    {detailSubject.description || "Chưa có mô tả."}
                  </p>
                </div>

                <div className="grid grid-cols-2 gap-4 pt-2 border-t border-slate-100">
                  <div>
                    <span className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                      ID Môn học
                    </span>
                    <p className="mt-0.5 text-xs text-slate-500 font-mono break-all">
                      {detailSubject.subjectId}
                    </p>
                  </div>
                  <div>
                    <span className="block text-xs font-semibold uppercase tracking-wider text-slate-400">
                      Phiên bản (RowVersion)
                    </span>
                    <p className="mt-0.5 text-xs text-slate-500 font-mono">
                      {detailSubject.rowVersion}
                    </p>
                  </div>
                </div>
              </div>

              <div className="mt-6 flex items-center justify-between pt-4 border-t border-slate-100">
                {canViewGraph && (
                  <Link
                    to={`/kien-thuc/do-thi?subjectId=${detailSubject.subjectId}`}
                    className="inline-flex items-center rounded-lg bg-indigo-50 px-4 py-2 text-xs font-bold text-indigo-700 ring-1 ring-inset ring-indigo-200 hover:bg-indigo-100"
                  >
                    Sơ đồ kiến thức →
                  </Link>
                )}
                <button
                  type="button"
                  onClick={() => setDetailSubject(null)}
                  className="rounded-lg bg-slate-100 px-4 py-2 text-xs font-bold text-slate-700 hover:bg-slate-200"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Modal 3: Edit Subject Modal */}
        {editingSubject && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl ring-1 ring-slate-200">
              <div className="flex items-center justify-between border-b border-slate-100 pb-4 mb-4">
                <h3 className="text-lg font-bold text-slate-900">
                  Cập nhật môn học: {editingSubject.subjectCode}
                </h3>
                <button
                  type="button"
                  onClick={() => setEditingSubject(null)}
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                >
                  ✕
                </button>
              </div>

              {editError && (
                <div className="mb-4 rounded-lg bg-red-50 p-3 ring-1 ring-red-200 text-xs font-medium text-red-700" role="alert">
                  {editError}
                </div>
              )}

              <form onSubmit={handleEditSubmit} className="space-y-4">
                <div>
                  <label htmlFor="edit-subject-code" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Mã môn học <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="edit-subject-code"
                    type="text"
                    required
                    maxLength={32}
                    value={editCode}
                    onChange={(event) => setEditCode(event.target.value.toUpperCase())}
                    className="block w-full rounded-lg border-0 px-3 py-2 font-mono uppercase text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="edit-subject-name" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Tên môn học <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="edit-subject-name"
                    type="text"
                    required
                    maxLength={100}
                    value={editName}
                    onChange={(e) => setEditName(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="edit-subject-desc" className="block text-xs font-semibold uppercase tracking-wider text-slate-600 mb-1">
                    Mô tả
                  </label>
                  <textarea
                    id="edit-subject-desc"
                    rows={3}
                    maxLength={500}
                    value={editDescription}
                    onChange={(e) => setEditDescription(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                  <p className="mt-1 text-xs text-slate-400">{editDescription.length}/500 ký tự</p>
                </div>

                <div className="flex items-center gap-3 pt-2">
                  <input
                    id="edit-subject-active"
                    type="checkbox"
                    checked={editIsActive}
                    onChange={(e) => setEditIsActive(e.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-600"
                  />
                  <label htmlFor="edit-subject-active" className="text-sm font-medium text-slate-700 select-none">
                    Môn học đang hoạt động
                  </label>
                </div>

                <div className="mt-6 flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
                  <button
                    type="button"
                    onClick={() => setEditingSubject(null)}
                    disabled={updateMutation.isPending}
                    className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-edit-subject"
                    disabled={updateMutation.isPending}
                    className="inline-flex items-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {updateMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Modal 4: Delete Subject Confirmation Modal */}
        {deletingSubject && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl ring-1 ring-slate-200">
              <div className="flex items-center justify-between border-b border-slate-100 pb-4 mb-4">
                <h3 className="text-lg font-bold text-red-600">Xác nhận xóa môn học</h3>
                <button
                  type="button"
                  onClick={() => setDeletingSubject(null)}
                  className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                >
                  ✕
                </button>
              </div>

              {deleteError && (
                <div className="mb-4 rounded-lg bg-red-50 p-3 ring-1 ring-red-200 text-xs font-medium text-red-700" role="alert">
                  {deleteError}
                </div>
              )}

              <p className="text-sm text-slate-700 mb-3">
                Bạn có chắc chắn muốn xóa môn học{" "}
                <span className="font-bold text-slate-900">
                  {deletingSubject.subjectCode} — {deletingSubject.subjectName}
                </span>
                ?
              </p>

              <div className="rounded-lg bg-amber-50 p-3 ring-1 ring-amber-200 text-xs text-amber-800 mb-6">
                <strong>Lưu ý quan trọng:</strong> Thao tác này sẽ xóa mềm môn học. Nếu môn học đã có lớp học, giáo trình, câu hỏi hoặc dữ liệu phân tích liên quan, hệ thống sẽ từ chối xóa để đảm bảo toàn vẹn dữ liệu.
              </div>

              <div className="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setDeletingSubject(null)}
                  disabled={deleteMutation.isPending}
                  className="rounded-lg bg-white px-4 py-2 text-sm font-semibold text-slate-700 ring-1 ring-inset ring-slate-300 hover:bg-slate-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  id="btn-confirm-delete-subject"
                  onClick={handleDeleteConfirm}
                  disabled={deleteMutation.isPending}
                  className="inline-flex items-center rounded-lg bg-red-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-red-500 disabled:opacity-50"
                >
                  {deleteMutation.isPending ? "Đang xóa..." : "Xác nhận xóa"}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
