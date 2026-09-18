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

/**
 * Modern CenterManager Dark Enterprise SaaS view for SubjectListPage
 */
const CenterManagerSubjectListView: React.FC = () => {
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

  const [editingSubject, setEditingSubject] = useState<SubjectDto | null>(null);
  const [editCode, setEditCode] = useState("");
  const [editName, setEditName] = useState("");
  const [editDescription, setEditDescription] = useState("");
  const [editIsActive, setEditIsActive] = useState(true);
  const [editError, setEditError] = useState<string | null>(null);

  const [deletingSubject, setDeletingSubject] = useState<SubjectDto | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

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

  // Query subjects
  const isActiveParam = statusFilter === "all" ? undefined : statusFilter === "active";
  const {
    data: subjectsData,
    isLoading,
    isError,
    error: listError,
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
      showFeedback("success", "Tạo môn học mới thành công!");
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
      showFeedback("success", "Cập nhật môn học thành công!");
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
              // Ignore failure
            }
          }
          showFeedback(
            "conflict",
            "Dữ liệu môn học vừa thay đổi bởi phiên làm việc khác. Hệ thống đã nạp phiên bản mới nhất."
          );
          setEditError("Dữ liệu đã bị sửa đổi. Vui lòng kiểm tra lại nội dung và gửi lại.");
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
      showFeedback("success", "Đã xóa môn học thành công!");
    },
    onError: (err) => {
      if (isAxiosError<ProblemDetails>(err)) {
        if (err.response?.status === 409) {
          setDeleteError(
            mapSafeOperationalError(
              err,
              "Không thể xóa môn học này do đang có dữ liệu liên kết (lớp học, giáo trình, mục tiêu học tập, hoặc dữ liệu phân tích)."
            )
          );
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
    setIsDetailLoading(true);
    try {
      setDetailSubject(await organizationApi.getSubject(subject.subjectId));
    } catch {
      // Keep initial
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
    } catch {
      setEditingSubject(subject);
      setEditCode(subject.subjectCode);
      setEditName(subject.subjectName);
      setEditDescription(subject.description || "");
      setEditIsActive(subject.isActive);
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
    if (!trimmedName || trimmedName.length > 100) {
      setEditError("Tên môn học phải từ 1 đến 100 ký tự.");
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

  const columns: DataTableColumn<SubjectDto>[] = [
    {
      id: "subjectCode",
      header: "Mã môn học",
      render: (subject) => (
        <span className="font-mono font-bold text-[var(--cm-cyan)]">
          {subject.subjectCode}
        </span>
      ),
    },
    {
      id: "subjectName",
      header: "Tên môn học",
      render: (subject) => (
        <span className="font-medium text-[var(--cm-text)]">
          {subject.subjectName}
        </span>
      ),
    },
    {
      id: "description",
      header: "Mô tả",
      render: (subject) => (
        <span className="text-xs text-[var(--cm-text-secondary)] line-clamp-2">
          {subject.description || "—"}
        </span>
      ),
    },
    {
      id: "isActive",
      header: "Trạng thái",
      render: (subject) => (
        <StatusBadge
          status={subject.isActive ? "active" : "inactive"}
          label={subject.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
        />
      ),
    },
    {
      id: "actions",
      header: "Thao tác",
      align: "right",
      render: (subject) => (
        <div className="flex items-center justify-end gap-1.5">
          {canViewGraph && (
            <Link
              to={`/kien-thuc/do-thi?subjectId=${subject.subjectId}`}
              id={`btn-view-graph-${subject.subjectId}`}
              className="cm-secondary-button h-8 px-2.5 py-1 text-xs text-cyan-300 hover:text-white"
            >
              Đồ thị tri thức →
            </Link>
          )}
          <button
            type="button"
            id={`btn-open-detail-subject-${subject.subjectId}`}
            onClick={() => handleOpenDetail(subject)}
            className="cm-secondary-button h-8 px-2.5 py-1 text-xs"
          >
            Chi tiết
          </button>
          {canUpdate && (
            <button
              type="button"
              id={`btn-open-edit-subject-${subject.subjectId}`}
              onClick={() => handleOpenEdit(subject)}
              className="cm-secondary-button h-8 px-2.5 py-1 text-xs"
            >
              Sửa
            </button>
          )}
          {canDelete && (
            <button
              type="button"
              id={`btn-open-delete-subject-${subject.subjectId}`}
              onClick={() => handleOpenDelete(subject)}
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
            eyebrow="Quản lý môn học"
            title="Danh mục Môn học"
            description="Danh mục các môn học giảng dạy tại trung tâm, liên kết đồ thị tri thức và cấu hình hoạt động."
            actions={
              canCreate && (
                <button
                  type="button"
                  id="btn-open-create-subject"
                  onClick={handleOpenCreate}
                  className="cm-primary-button"
                >
                  + Thêm môn học
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
              isReloading={isLoading}
            />
          )}

          {feedback && feedback.type !== "conflict" && (
            <div
              id="global-success-message"
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
            searchValue={searchTerm}
            searchLabel="Tìm kiếm môn học"
            searchPlaceholder="Tìm theo mã môn hoặc tên môn học…"
            onSearchChange={setSearchTerm}
            filters={
              <div className="flex items-center gap-2">
                <label htmlFor="subject-status-filter" className="text-xs text-[var(--cm-text-secondary)]">
                  Trạng thái:
                </label>
                <select
                  id="subject-status-filter"
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value as "all" | "active" | "inactive")}
                  className="cm-field px-3 py-1.5 text-xs"
                >
                  <option value="all">Tất cả</option>
                  <option value="active">Đang hoạt động</option>
                  <option value="inactive">Ngừng hoạt động</option>
                </select>
              </div>
            }
            actions={
              (searchTerm !== "" || statusFilter !== "all") ? (
                <button
                  type="button"
                  id="btn-reset-subjects"
                  onClick={() => {
                    setSearchTerm("");
                    setStatusFilter("all");
                  }}
                  className="cm-ghost-button text-xs text-[var(--cm-text-secondary)] hover:text-white"
                >
                  Đặt lại
                </button>
              ) : undefined
            }
          />

          {isError && (
            <SafeErrorPanel
              error={listError}
              fallback="Không thể tải danh sách môn học từ hệ thống."
              onRetry={() => refetch()}
            />
          )}

          <DataTable<SubjectDto>
            caption="Danh mục môn học trung tâm"
            columns={columns}
            rows={subjects}
            rowKey={(subject) => subject.subjectId}
            isLoading={isLoading}
            emptyTitle="Không tìm thấy môn học nào"
            emptyDescription="Chưa có môn học nào khớp với điều kiện lọc hiện tại."
            totalItems={subjects.length}
          />

          {/* Create Subject Modal */}
          {isCreating && canCreate && (
            <Modal
              isOpen={isCreating}
              title="Thêm môn học mới"
              description="Khởi tạo mã và tên môn học trong danh mục của trung tâm."
              onClose={() => setIsCreating(false)}
            >
              {createError && (
                <div id="create-subject-error" role="alert" className="mb-4 rounded-xl border border-rose-400/30 bg-rose-400/10 p-3 text-xs text-rose-200">
                  {createError}
                </div>
              )}

              <form onSubmit={handleCreateSubmit} className="space-y-4">
                <div>
                  <label htmlFor="input-create-subject-code" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Mã môn học <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-create-subject-code"
                    value={createCode}
                    onChange={(e) => setCreateCode(e.target.value.toUpperCase())}
                    required
                    maxLength={32}
                    placeholder="Ví dụ: TOAN12"
                    disabled={createMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm uppercase font-mono"
                  />
                </div>

                <div>
                  <label htmlFor="input-create-subject-name" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Tên môn học <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-create-subject-name"
                    value={createName}
                    onChange={(e) => setCreateName(e.target.value)}
                    required
                    maxLength={100}
                    placeholder="Ví dụ: Toán học 12"
                    disabled={createMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="input-create-subject-desc" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Mô tả
                  </label>
                  <textarea
                    id="input-create-subject-desc"
                    rows={3}
                    value={createDescription}
                    onChange={(e) => setCreateDescription(e.target.value)}
                    maxLength={500}
                    placeholder="Mô tả nội dung môn học..."
                    disabled={createMutation.isPending}
                    className="cm-field mt-1 w-full p-3 text-sm"
                  />
                </div>

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
                    id="btn-submit-create-subject"
                    disabled={createMutation.isPending}
                    className="cm-primary-button text-sm"
                  >
                    {createMutation.isPending ? "Đang tạo..." : "Tạo môn học"}
                  </button>
                </div>
              </form>
            </Modal>
          )}

          {/* Edit Subject Modal */}
          {editingSubject && canUpdate && (
            <Modal
              isOpen={!!editingSubject}
              title="Chỉnh sửa môn học"
              description={`Cập nhật thông tin cho môn ${editingSubject.subjectCode}`}
              onClose={() => setEditingSubject(null)}
            >
              {editError && (
                <div id="edit-subject-error" role="alert" className="mb-4 rounded-xl border border-rose-400/30 bg-rose-400/10 p-3 text-xs text-rose-200">
                  {editError}
                </div>
              )}

              <form onSubmit={handleEditSubmit} className="space-y-4">
                <div>
                  <label htmlFor="input-edit-subject-code" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Mã môn học <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-edit-subject-code"
                    value={editCode}
                    onChange={(e) => setEditCode(e.target.value.toUpperCase())}
                    required
                    maxLength={32}
                    disabled={updateMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm uppercase font-mono"
                  />
                </div>

                <div>
                  <label htmlFor="input-edit-subject-name" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Tên môn học <span className="text-rose-400">*</span>
                  </label>
                  <input
                    type="text"
                    id="input-edit-subject-name"
                    value={editName}
                    onChange={(e) => setEditName(e.target.value)}
                    required
                    maxLength={100}
                    disabled={updateMutation.isPending}
                    className="cm-field mt-1 w-full px-3 text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="input-edit-subject-desc" className="block text-xs font-medium text-[var(--cm-text-secondary)]">
                    Mô tả
                  </label>
                  <textarea
                    id="input-edit-subject-desc"
                    rows={3}
                    value={editDescription}
                    onChange={(e) => setEditDescription(e.target.value)}
                    maxLength={500}
                    disabled={updateMutation.isPending}
                    className="cm-field mt-1 w-full p-3 text-sm"
                  />
                </div>

                <div>
                  <label className="flex items-center gap-2 text-xs text-[var(--cm-text)] cursor-pointer">
                    <input
                      type="checkbox"
                      id="checkbox-edit-subject-active"
                      checked={editIsActive}
                      onChange={(e) => setEditIsActive(e.target.checked)}
                      disabled={updateMutation.isPending}
                      className="rounded border-[var(--cm-border)] text-indigo-500"
                    />
                    <span>Kích hoạt giảng dạy môn học này</span>
                  </label>
                </div>

                <div className="flex justify-end gap-3 pt-4">
                  <button
                    type="button"
                    onClick={() => setEditingSubject(null)}
                    disabled={updateMutation.isPending}
                    className="cm-secondary-button text-sm"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-save-edit-subject"
                    disabled={updateMutation.isPending}
                    className="cm-primary-button text-sm"
                  >
                    {updateMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </Modal>
          )}

          {/* Detail Drawer */}
          {detailSubject && (
            <Drawer
              isOpen={!!detailSubject}
              title={`Môn: ${detailSubject.subjectName}`}
              description={`Mã môn: ${detailSubject.subjectCode}`}
              onClose={() => setDetailSubject(null)}
              footer={
                <div className="flex justify-end gap-2">
                  {canViewGraph && (
                    <Link
                      to={`/kien-thuc/do-thi?subjectId=${detailSubject.subjectId}`}
                      className="cm-primary-button text-xs"
                    >
                      Mở đồ thị tri thức →
                    </Link>
                  )}
                  <button
                    type="button"
                    onClick={() => setDetailSubject(null)}
                    className="cm-secondary-button text-xs"
                  >
                    Đóng
                  </button>
                </div>
              }
            >
              {isDetailLoading ? (
                <div className="py-12 text-center text-xs text-[var(--cm-text-muted)]">Đang tải chi tiết...</div>
              ) : (
                <div className="space-y-4 text-sm">
                  <div className="grid grid-cols-2 gap-4 rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-raised)] p-4">
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Mã môn</span>
                      <span className="font-mono font-bold text-[var(--cm-cyan)]">{detailSubject.subjectCode}</span>
                    </div>
                    <div>
                      <span className="block text-xs text-[var(--cm-text-muted)]">Trạng thái</span>
                      <StatusBadge
                        status={detailSubject.isActive ? "active" : "inactive"}
                        label={detailSubject.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
                      />
                    </div>
                    <div className="col-span-2">
                      <span className="block text-xs text-[var(--cm-text-muted)]">Tên môn học</span>
                      <span className="font-medium text-[var(--cm-text)]">{detailSubject.subjectName}</span>
                    </div>
                    <div className="col-span-2">
                      <span className="block text-xs text-[var(--cm-text-muted)]">Mô tả</span>
                      <span className="text-[var(--cm-text-secondary)]">{detailSubject.description || "Không có mô tả."}</span>
                    </div>
                    <div className="col-span-2">
                      <span className="block text-xs text-[var(--cm-text-muted)]">Phiên bản dữ liệu (RowVersion)</span>
                      <span className="font-mono text-xs text-[var(--cm-text-secondary)]">{detailSubject.rowVersion}</span>
                    </div>
                  </div>
                </div>
              )}
            </Drawer>
          )}

          {/* Delete Confirm Modal */}
          {deletingSubject && canDelete && (
            <Modal
              isOpen={!!deletingSubject}
              title="Xóa môn học"
              onClose={() => setDeletingSubject(null)}
            >
              {deleteError && (
                <div id="delete-subject-error" role="alert" className="mb-4 rounded-xl border border-rose-400/30 bg-rose-400/10 p-3 text-xs text-rose-200">
                  {deleteError}
                </div>
              )}

              <div className="space-y-4 text-sm">
                <p className="text-[var(--cm-text-secondary)]">
                  Bạn có chắc chắn muốn xóa môn học{" "}
                  <strong className="text-[var(--cm-text)]">{deletingSubject.subjectName}</strong> (
                  {deletingSubject.subjectCode})?
                </p>

                <div className="rounded-xl border border-amber-400/30 bg-amber-400/10 p-3 text-xs text-amber-200 space-y-1">
                  <p className="font-bold">LƯU Ý RÀNG BUỘC DỮ LIỆU:</p>
                  <p>Hệ thống sẽ từ chối xóa nếu môn học đang được sử dụng trong các lớp học, giáo trình, mục tiêu Digital Twin hoặc cây kiến thức.</p>
                </div>

                <div className="flex justify-end gap-3 pt-2">
                  <button
                    type="button"
                    onClick={() => setDeletingSubject(null)}
                    disabled={deleteMutation.isPending}
                    className="cm-secondary-button text-sm"
                  >
                    Hủy
                  </button>
                  <button
                    type="button"
                    id="btn-confirm-delete-subject"
                    onClick={handleDeleteConfirm}
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

export const SubjectListPage: React.FC = () => {
  const user = useAuthStore((state) => state.user);
  return user?.accountType === "CenterManager" ? (
    <CenterManagerSubjectListView />
  ) : (
    <LegacySubjectListPage />
  );
};

/**
 * Legacy SubjectListPage implementation preserved for non-CenterManager users
 */
const LegacySubjectListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreate = hasPermission(permissions.subjectsCreate);
  const canUpdate = hasPermission(permissions.subjectsUpdate);
  const canDelete = hasPermission(permissions.subjectsDelete);
  const canViewGraph =
    hasPermission(permissions.nodesRead) && hasPermission(permissions.edgesRead);

  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all");
  const [searchTerm, setSearchTerm] = useState<string>("");

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

  const invalidateAllSubjectQueries = async () => {
    await queryClient.invalidateQueries({ queryKey: ["subjects"] });
  };

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
              // Ignore failure
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

        <div className="mb-6 rounded-xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
            <div className="flex flex-col sm:flex-row gap-4 flex-1">
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
                      <td className="whitespace-nowrap py-4 pl-6 pr-3 text-sm font-bold text-indigo-600">
                        {subject.subjectCode}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm font-medium text-slate-900">
                        {subject.subjectName}
                      </td>
                      <td className="px-3 py-4 text-sm text-slate-500 max-w-xs truncate">
                        {subject.description || "-"}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm">
                        <span
                          className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                            subject.isActive
                              ? "bg-emerald-100 text-emerald-800"
                              : "bg-slate-100 text-slate-700"
                          }`}
                        >
                          {subject.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
                        </span>
                      </td>
                      <td className="whitespace-nowrap py-4 pl-3 pr-6 text-right text-sm font-medium space-x-2">
                        {canViewGraph && (
                          <Link
                            to={`/kien-thuc/do-thi?subjectId=${subject.subjectId}`}
                            className="text-xs font-semibold text-indigo-600 hover:text-indigo-900 mr-2"
                          >
                            Đồ thị tri thức →
                          </Link>
                        )}
                        <button
                          type="button"
                          onClick={() => handleOpenDetail(subject)}
                          className="text-xs font-semibold text-slate-600 hover:text-slate-900"
                        >
                          Chi tiết
                        </button>
                        {canUpdate && (
                          <button
                            type="button"
                            id={`btn-open-edit-subject-${subject.subjectId}`}
                            onClick={() => handleOpenEdit(subject)}
                            className="text-xs font-semibold text-blue-600 hover:text-blue-900"
                          >
                            Sửa
                          </button>
                        )}
                        {canDelete && (
                          <button
                            type="button"
                            id={`btn-open-delete-subject-${subject.subjectId}`}
                            onClick={() => handleOpenDelete(subject)}
                            className="text-xs font-semibold text-red-600 hover:text-red-900"
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

        {isCreating && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 flex items-center justify-center p-4">
            <div className="relative transform overflow-hidden rounded-2xl bg-white p-6 text-left shadow-xl transition-all sm:w-full sm:max-w-lg">
              <h3 className="text-lg font-bold text-slate-900 mb-4">Thêm môn học mới</h3>
              {createError && (
                <div id="create-subject-error" className="mb-4 rounded-lg bg-red-50 p-3 text-sm text-red-700 border border-red-200">
                  {createError}
                </div>
              )}
              <form onSubmit={handleCreateSubmit} className="space-y-4">
                <div>
                  <label htmlFor="input-create-subject-code" className="block text-xs font-semibold uppercase text-slate-600 mb-1">
                    Mã môn học <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="input-create-subject-code"
                    type="text"
                    required
                    maxLength={32}
                    value={createCode}
                    onChange={(e) => setCreateCode(e.target.value.toUpperCase())}
                    placeholder="Ví dụ: TOAN12"
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm font-mono uppercase"
                  />
                </div>
                <div>
                  <label htmlFor="input-create-subject-name" className="block text-xs font-semibold uppercase text-slate-600 mb-1">
                    Tên môn học <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="input-create-subject-name"
                    type="text"
                    required
                    maxLength={100}
                    value={createName}
                    onChange={(e) => setCreateName(e.target.value)}
                    placeholder="Ví dụ: Toán học 12"
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                </div>
                <div>
                  <label htmlFor="input-create-subject-desc" className="block text-xs font-semibold uppercase text-slate-600 mb-1">
                    Mô tả
                  </label>
                  <textarea
                    id="input-create-subject-desc"
                    rows={3}
                    maxLength={500}
                    value={createDescription}
                    onChange={(e) => setCreateDescription(e.target.value)}
                    placeholder="Nhập thông tin giới thiệu hoặc mô tả sơ bộ..."
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                </div>
                <div className="mt-6 flex justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setIsCreating(false)}
                    className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-submit-create-subject"
                    disabled={createMutation.isPending}
                    className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {createMutation.isPending ? "Đang tạo..." : "Tạo môn học"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {editingSubject && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 flex items-center justify-center p-4">
            <div className="relative transform overflow-hidden rounded-2xl bg-white p-6 text-left shadow-xl transition-all sm:w-full sm:max-w-lg">
              <h3 className="text-lg font-bold text-slate-900 mb-4">Chỉnh sửa môn học</h3>
              {editError && (
                <div id="edit-subject-error" className="mb-4 rounded-lg bg-red-50 p-3 text-sm text-red-700 border border-red-200">
                  {editError}
                </div>
              )}
              <form onSubmit={handleEditSubmit} className="space-y-4">
                <div>
                  <label htmlFor="input-edit-subject-code" className="block text-xs font-semibold uppercase text-slate-600 mb-1">
                    Mã môn học <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="input-edit-subject-code"
                    type="text"
                    required
                    maxLength={32}
                    value={editCode}
                    onChange={(e) => setEditCode(e.target.value.toUpperCase())}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm font-mono uppercase"
                  />
                </div>
                <div>
                  <label htmlFor="input-edit-subject-name" className="block text-xs font-semibold uppercase text-slate-600 mb-1">
                    Tên môn học <span className="text-red-500">*</span>
                  </label>
                  <input
                    id="input-edit-subject-name"
                    type="text"
                    required
                    maxLength={100}
                    value={editName}
                    onChange={(e) => setEditName(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                </div>
                <div>
                  <label htmlFor="input-edit-subject-desc" className="block text-xs font-semibold uppercase text-slate-600 mb-1">
                    Mô tả
                  </label>
                  <textarea
                    id="input-edit-subject-desc"
                    rows={3}
                    maxLength={500}
                    value={editDescription}
                    onChange={(e) => setEditDescription(e.target.value)}
                    className="block w-full rounded-lg border-0 py-2 px-3 text-slate-900 shadow-sm ring-1 ring-inset ring-slate-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm"
                  />
                </div>
                <div className="flex items-center">
                  <input
                    id="checkbox-edit-subject-active"
                    type="checkbox"
                    checked={editIsActive}
                    onChange={(e) => setEditIsActive(e.target.checked)}
                    className="h-4 w-4 rounded border-slate-300 text-indigo-600 focus:ring-indigo-600"
                  />
                  <label htmlFor="checkbox-edit-subject-active" className="ml-2 block text-sm font-medium text-slate-900">
                    Đang hoạt động (Kích hoạt giảng dạy)
                  </label>
                </div>
                <div className="mt-6 flex justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setEditingSubject(null)}
                    className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                  >
                    Hủy
                  </button>
                  <button
                    type="submit"
                    id="btn-save-edit-subject"
                    disabled={updateMutation.isPending}
                    className="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-indigo-500 disabled:opacity-50"
                  >
                    {updateMutation.isPending ? "Đang lưu..." : "Lưu thay đổi"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {detailSubject && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 flex items-center justify-center p-4">
            <div className="relative transform overflow-hidden rounded-2xl bg-white p-6 text-left shadow-xl transition-all sm:w-full sm:max-w-lg">
              <div className="flex items-start justify-between border-b border-slate-200 pb-3 mb-4">
                <h3 className="text-lg font-bold text-slate-900">Hồ sơ chi tiết môn học</h3>
                <button
                  type="button"
                  onClick={() => setDetailSubject(null)}
                  className="text-slate-400 hover:text-slate-600 text-2xl font-bold leading-none"
                >
                  ×
                </button>
              </div>

              {isDetailLoading ? (
                <div className="p-6 text-center text-sm text-slate-500">Đang tải chi tiết môn học...</div>
              ) : detailError ? (
                <div className="rounded-lg bg-red-50 p-3 text-sm text-red-700">{detailError}</div>
              ) : (
                <div className="space-y-4">
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block">
                      Mã môn học
                    </span>
                    <span className="text-sm font-mono font-bold text-indigo-600">
                      {detailSubject.subjectCode}
                    </span>
                  </div>
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block">
                      Tên môn học
                    </span>
                    <span className="text-base font-semibold text-slate-900">
                      {detailSubject.subjectName}
                    </span>
                  </div>
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block">
                      Mô tả môn học
                    </span>
                    <p className="text-sm text-slate-600 bg-slate-50 p-3 rounded-lg mt-1 border border-slate-100">
                      {detailSubject.description || "Chưa có mô tả cho môn học này."}
                    </p>
                  </div>
                  <div className="flex justify-between items-center bg-slate-50 p-3 rounded-lg border border-slate-100">
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-500">
                      Trạng thái hiện tại:
                    </span>
                    <span
                      className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                        detailSubject.isActive
                          ? "bg-emerald-100 text-emerald-800"
                          : "bg-slate-100 text-slate-700"
                      }`}
                    >
                      {detailSubject.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
                    </span>
                  </div>
                  <div>
                    <span className="text-xs font-bold uppercase tracking-wider text-slate-400 block">
                      Phiên bản dữ liệu (RowVersion)
                    </span>
                    <span className="text-xs font-mono text-slate-500 break-all">
                      {detailSubject.rowVersion}
                    </span>
                  </div>
                </div>
              )}

              <div className="mt-6 flex justify-end gap-2 border-t border-slate-200 pt-4">
                {canViewGraph && (
                  <Link
                    to={`/kien-thuc/do-thi?subjectId=${detailSubject.subjectId}`}
                    className="rounded-lg bg-indigo-50 px-3 py-2 text-xs font-semibold text-indigo-700 hover:bg-indigo-100"
                  >
                    Xem đồ thị tri thức →
                  </Link>
                )}
                <button
                  type="button"
                  onClick={() => setDetailSubject(null)}
                  className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                >
                  Đóng
                </button>
              </div>
            </div>
          </div>
        )}

        {deletingSubject && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-900/50 flex items-center justify-center p-4">
            <div className="relative transform overflow-hidden rounded-2xl bg-white p-6 text-left shadow-xl transition-all sm:w-full sm:max-w-md">
              <h3 className="text-lg font-bold text-slate-900 mb-2">Xác nhận xóa môn học</h3>
              <p className="text-sm text-slate-600 mb-4">
                Bạn có chắc chắn muốn xóa môn học{" "}
                <span className="font-bold text-slate-900">{deletingSubject.subjectName}</span> (Mã:{" "}
                <span className="font-mono text-indigo-600">{deletingSubject.subjectCode}</span>) khỏi hệ thống?
              </p>

              {deleteError && (
                <div id="delete-subject-error" className="mb-4 rounded-lg bg-red-50 p-3 text-sm text-red-700 border border-red-200">
                  {deleteError}
                </div>
              )}

              <div className="rounded-lg bg-amber-50 p-3 border border-amber-200 mb-4">
                <p className="text-xs text-amber-800 font-medium">
                  Lưu ý: Môn học chỉ có thể xóa nếu không có bất kỳ lớp học, giáo trình hoặc mục tiêu học tập nào liên kết.
                </p>
              </div>

              <div className="flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setDeletingSubject(null)}
                  className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  id="btn-confirm-delete-subject"
                  onClick={handleDeleteConfirm}
                  disabled={deleteMutation.isPending}
                  className="rounded-lg bg-red-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-red-500 disabled:opacity-50"
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
