import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { organizationApi } from "../api/organizationApi";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import {
  useCurriculum,
  useCreateCurriculum,
  useUpdateCurriculum,
  useUpdateCurriculumClasses,
  useUpdateCurriculumNodes,
  usePublishCurriculum,
} from "../features/curriculum/useCurriculums";
import type { CreateCurriculumRequest, UpdateCurriculumRequest, ReviewStatus } from "../types/curriculum";
import {
  shouldDisplayTeacherSelector,
  validateCurriculumForm,
  buildCreateCurriculumPayload,
  buildUpdateCurriculumPayload,
  isConcurrencyConflictError,
} from "./curriculumEditorHelpers";
import { mapSafeOperationalError } from "../utils/problemDetails";
import {
  CenterManagerThemeScope,
  PageHeader,
  SafeErrorPanel,
  Skeleton,
  StatusBadge,
  Modal,
  ConcurrencyBanner,
} from "../components/centerManager";

/**
 * Modern CenterManager Dark Enterprise SaaS view for CurriculumEditorPage
 */
const CenterManagerCurriculumEditorView: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEditMode = Boolean(id);

  const user = useAuthStore((state) => state.user);
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const isCenterManager = user?.accountType === "CenterManager";
  const canReadTeachers = hasPermission(permissions.teachersRead);
  const canCreateCurriculums = hasPermission(permissions.curriculumsCreate);
  const canUpdateCurriculums = hasPermission(permissions.curriculumsUpdate);
  const canPublishCurriculums = hasPermission(permissions.curriculumsPublish);

  const canSaveInfo = isEditMode ? canUpdateCurriculums : canCreateCurriculums;

  const [formData, setFormData] = useState<{
    title: string;
    description: string;
    subjectId: string;
    teacherId: string;
    nodeIds: string[];
    classIds: string[];
    rowVersion: string;
    reviewStatus: ReviewStatus;
  }>({
    title: "",
    description: "",
    subjectId: "",
    teacherId: "",
    nodeIds: [],
    classIds: [],
    rowVersion: "0",
    reviewStatus: "Draft",
  });

  const [activeTab, setActiveTab] = useState<"info" | "nodes" | "classes">("info");

  // Input states for adding new node ID or class ID
  const [newNodeIdInput, setNewNodeIdInput] = useState("");
  const [newClassIdInput, setNewClassIdInput] = useState("");

  // Operational feedback states
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [concurrencyConflict, setConcurrencyConflict] = useState<boolean>(false);

  // Publish Modal State
  const [isPublishModalOpen, setIsPublishModalOpen] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);

  const {
    data: curriculumData,
    isLoading: isLoadingCurriculum,
    isError: isErrorCurriculum,
    error: curriculumError,
    refetch: refetchCurriculum,
  } = useCurriculum(id || "");

  const createMutation = useCreateCurriculum();
  const updateMutation = useUpdateCurriculum();
  const updateClassesMutation = useUpdateCurriculumClasses();
  const updateNodesMutation = useUpdateCurriculumNodes();
  const publishMutation = usePublishCurriculum();

  const { data: subjectsData, isLoading: isLoadingSubjects } = useQuery({
    queryKey: ["subjects", "active-for-curriculum-editor"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const { data: teachersData, isLoading: isLoadingTeachers } = useQuery({
    queryKey: ["teachers", "active-for-curriculum-editor"],
    queryFn: () => organizationApi.listTeachers({ page: 1, pageSize: 100, status: "Active" }),
    enabled: isCenterManager && canReadTeachers,
  });

  // Sync loaded curriculum data into form state
  useEffect(() => {
    if (isEditMode && curriculumData?.data) {
      const c = curriculumData.data;
      setFormData({
        title: c.title,
        description: c.description || "",
        subjectId: c.subjectId,
        teacherId: c.teacherId || "",
        nodeIds: c.nodeIds || [],
        classIds: c.classIds || [],
        rowVersion: c.rowVersion,
        reviewStatus: c.reviewStatus as ReviewStatus,
      });
      setConcurrencyConflict(false);
    }
  }, [isEditMode, curriculumData]);

  const handleInputChange = (field: keyof typeof formData, value: any) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
    setFormError(null);
    setSuccessMessage(null);
  };

  const handleRefreshAfterConflict = async () => {
    setConcurrencyConflict(false);
    setFormError(null);
    const refreshed = await refetchCurriculum();
    if (refreshed.data?.data) {
      const c = refreshed.data.data;
      setFormData({
        title: c.title,
        description: c.description || "",
        subjectId: c.subjectId,
        teacherId: c.teacherId || "",
        nodeIds: c.nodeIds || [],
        classIds: c.classIds || [],
        rowVersion: c.rowVersion,
        reviewStatus: c.reviewStatus as ReviewStatus,
      });
      setSuccessMessage("Đã nạp dữ liệu và RowVersion mới nhất thành công.");
    }
  };

  // 1. Save General Info
  const handleSaveInfo = () => {
    setFormError(null);
    setSuccessMessage(null);

    const validation = validateCurriculumForm(formData, { isEditMode, isCenterManager });
    if (!validation.isValid) {
      setFormError(validation.errorMessage || "Dữ liệu biểu mẫu không hợp lệ.");
      return;
    }

    if (!isEditMode) {
      createMutation.mutate(
        buildCreateCurriculumPayload(
          {
            title: formData.title,
            description: formData.description,
            subjectId: formData.subjectId,
            teacherId: formData.teacherId,
            nodeIds: formData.nodeIds,
          },
          isCenterManager
        ),
        {
          onSuccess: (res) => {
            navigate(`/quan-ly/giao-trinh/${res.data.curriculumId}`);
          },
          onError: (err: any) => {
            setFormError(mapSafeOperationalError(err, "Không thể tạo lộ trình học. Vui lòng thử lại."));
          },
        }
      );
    } else {
      updateMutation.mutate(
        {
          id: id!,
          data: buildUpdateCurriculumPayload({
            title: formData.title,
            description: formData.description,
            rowVersion: formData.rowVersion,
          }),
        },
        {
          onSuccess: (res) => {
            setFormData((prev) => ({ ...prev, rowVersion: res.data.rowVersion }));
            queryClient.setQueryData(["curriculums", id], res);
            setSuccessMessage("Cập nhật thông tin chung thành công!");
          },
          onError: (err: any) => {
            if (isConcurrencyConflictError(err)) {
              setConcurrencyConflict(true);
            } else {
              setFormError(mapSafeOperationalError(err, "Không thể cập nhật lộ trình học."));
            }
          },
        }
      );
    }
  };

  // 2. Save Nodes
  const handleSaveNodes = () => {
    if (!isEditMode) return;
    setFormError(null);
    setSuccessMessage(null);

    updateNodesMutation.mutate(
      {
        id: id!,
        data: {
          nodeIds: formData.nodeIds,
          rowVersion: formData.rowVersion,
        },
      },
      {
        onSuccess: (res) => {
          setFormData((prev) => ({ ...prev, rowVersion: res.data.rowVersion }));
          queryClient.setQueryData(["curriculums", id], res);
          setSuccessMessage("Cập nhật danh sách nút kiến thức thành công!");
        },
        onError: (err: any) => {
          if (isConcurrencyConflictError(err)) {
            setConcurrencyConflict(true);
          } else {
            setFormError(mapSafeOperationalError(err, "Không thể cập nhật danh sách nút kiến thức."));
          }
        },
      }
    );
  };

  // 3. Save Classes
  const handleSaveClasses = () => {
    if (!isEditMode) return;
    setFormError(null);
    setSuccessMessage(null);

    updateClassesMutation.mutate(
      {
        id: id!,
        data: {
          classIds: formData.classIds,
          rowVersion: formData.rowVersion,
        },
      },
      {
        onSuccess: (res) => {
          setFormData((prev) => ({ ...prev, rowVersion: res.data.rowVersion }));
          queryClient.setQueryData(["curriculums", id], res);
          setSuccessMessage("Cập nhật phân bổ lớp học thành công!");
        },
        onError: (err: any) => {
          if (isConcurrencyConflictError(err)) {
            setConcurrencyConflict(true);
          } else {
            setFormError(mapSafeOperationalError(err, "Không thể cập nhật phân bổ lớp học."));
          }
        },
      }
    );
  };

  // 4. Publish Curriculum
  const handleConfirmPublish = () => {
    if (!isEditMode) return;
    setPublishError(null);

    publishMutation.mutate(
      {
        id: id!,
        data: { rowVersion: formData.rowVersion },
      },
      {
        onSuccess: (res) => {
          setFormData((prev) => ({
            ...prev,
            rowVersion: res.data.rowVersion,
            reviewStatus: res.data.reviewStatus as ReviewStatus,
          }));
          queryClient.setQueryData(["curriculums", id], res);
          setIsPublishModalOpen(false);
          setSuccessMessage("Xuất bản lộ trình học thành công! Trạng thái hiện tại: Published.");
        },
        onError: (err: any) => {
          if (isConcurrencyConflictError(err)) {
            setIsPublishModalOpen(false);
            setConcurrencyConflict(true);
          } else {
            setPublishError(mapSafeOperationalError(err, "Không thể xuất bản lộ trình học."));
          }
        },
      }
    );
  };

  // Node array helpers
  const handleAddNodeId = () => {
    const trimmed = newNodeIdInput.trim();
    if (!trimmed) return;
    if (formData.nodeIds.includes(trimmed)) {
      setFormError("Nút kiến thức này đã tồn tại trong danh sách.");
      return;
    }
    setFormData((prev) => ({ ...prev, nodeIds: [...prev.nodeIds, trimmed] }));
    setNewNodeIdInput("");
    setFormError(null);
  };

  const handleRemoveNodeId = (index: number) => {
    setFormData((prev) => ({
      ...prev,
      nodeIds: prev.nodeIds.filter((_, i) => i !== index),
    }));
  };

  const handleMoveNode = (index: number, direction: "up" | "down") => {
    const targetIndex = direction === "up" ? index - 1 : index + 1;
    if (targetIndex < 0 || targetIndex >= formData.nodeIds.length) return;
    const newArr = [...formData.nodeIds];
    const temp = newArr[index];
    newArr[index] = newArr[targetIndex];
    newArr[targetIndex] = temp;
    setFormData((prev) => ({ ...prev, nodeIds: newArr }));
  };

  // Class array helpers
  const handleAddClassId = () => {
    const trimmed = newClassIdInput.trim();
    if (!trimmed) return;
    if (formData.classIds.includes(trimmed)) {
      setFormError("Mã lớp này đã tồn tại trong danh sách.");
      return;
    }
    setFormData((prev) => ({ ...prev, classIds: [...prev.classIds, trimmed] }));
    setNewClassIdInput("");
    setFormError(null);
  };

  const handleRemoveClassId = (index: number) => {
    setFormData((prev) => ({
      ...prev,
      classIds: prev.classIds.filter((_, i) => i !== index),
    }));
  };

  const isDraft = formData.reviewStatus === "Draft";
  const isReadOnly = !isDraft;

  if (isEditMode && isLoadingCurriculum) {
    return (
      <CenterManagerThemeScope data-actor="center-manager">
        <div className="space-y-6 p-6 lg:p-8">
          <Skeleton className="h-10 w-64 rounded-lg" />
          <Skeleton className="h-96 w-full rounded-2xl" />
        </div>
      </CenterManagerThemeScope>
    );
  }

  if (isEditMode && isErrorCurriculum) {
    return (
      <CenterManagerThemeScope data-actor="center-manager">
        <div className="p-6 lg:p-8">
          <SafeErrorPanel
            error={curriculumError}
            fallback="Đã xảy ra lỗi khi lấy thông tin lộ trình từ hệ thống."
            onRetry={() => refetchCurriculum()}
          />
        </div>
      </CenterManagerThemeScope>
    );
  }

  return (
    <CenterManagerThemeScope data-actor="center-manager">
      <div className="space-y-6 p-6 lg:p-8">
        <PageHeader
          eyebrow="QUẢN LÝ LỘ TRÌNH"
          title={isEditMode ? "Chỉnh sửa Lộ trình học" : "Tạo Lộ trình học mới"}
          description="Thiết kế khung chương trình kiến thức, cấu trúc bài giảng và phân bổ cho từng lớp học trực thuộc."
          breadcrumbs={[
            { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
            { label: "Giáo trình", href: "/quan-ly/giao-trinh" },
            { label: isEditMode ? formData.title || "Chi tiết" : "Tạo mới" },
          ]}
          actions={
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => navigate("/quan-ly/giao-trinh")}
                className="cm-secondary-button text-sm"
              >
                Quay lại
              </button>
              {isEditMode && (
                <StatusBadge
                  status={formData.reviewStatus}
                  label={
                    formData.reviewStatus === "Draft"
                      ? "Bản nháp"
                      : formData.reviewStatus === "Published"
                      ? "Đã xuất bản"
                      : "Đã lưu trữ"
                  }
                />
              )}
              {isEditMode && isDraft && canPublishCurriculums && (
                <button
                  type="button"
                  id="btn-open-publish-modal"
                  onClick={() => setIsPublishModalOpen(true)}
                  disabled={publishMutation.isPending}
                  className="cm-primary-button text-sm bg-emerald-600 hover:bg-emerald-500 border-emerald-500/30"
                >
                  Xuất bản (Publish)
                </button>
              )}
            </div>
          }
        />

        {/* Global Concurrency Conflict Banner */}
        {concurrencyConflict && (
          <ConcurrencyBanner
            onReload={handleRefreshAfterConflict}
            isReloading={isLoadingCurriculum}
          />
        )}

        {/* Success Alert */}
        {successMessage && (
          <div className="flex items-center justify-between rounded-xl bg-emerald-500/10 p-4 border border-emerald-500/30 text-sm font-medium text-emerald-300">
            <span>{successMessage}</span>
            <button
              type="button"
              onClick={() => setSuccessMessage(null)}
              className="text-xs font-semibold text-emerald-400 hover:text-emerald-200"
            >
              Đóng
            </button>
          </div>
        )}

        {/* Form Error Alert */}
        {formError && (
          <div className="flex items-center justify-between rounded-xl bg-rose-500/10 p-4 border border-rose-500/30 text-sm font-medium text-rose-300">
            <span>{formError}</span>
            <button
              type="button"
              onClick={() => setFormError(null)}
              className="text-xs font-semibold text-rose-400 hover:text-rose-200"
            >
              Đóng
            </button>
          </div>
        )}

        {/* Published / Read-only notice */}
        {isReadOnly && isEditMode && (
          <div className="rounded-xl bg-blue-500/10 p-4 border border-blue-500/30 text-xs font-medium text-blue-300">
            Lộ trình học đã ở trạng thái <strong>{formData.reviewStatus}</strong>. Chế độ chỉ đọc: không thể sửa đổi nội dung hoặc phân bổ lớp học.
          </div>
        )}

        {/* Workspace Card */}
        <div className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] overflow-hidden shadow-xl">
          {/* Navigation Tabs */}
          <div className="flex border-b border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] px-2">
            <button
              type="button"
              id="tab-curriculum-info"
              onClick={() => setActiveTab("info")}
              className={`py-3 px-5 text-sm font-medium transition border-b-2 ${
                activeTab === "info"
                  ? "border-[var(--cm-cyan)] text-[var(--cm-text)]"
                  : "border-transparent text-[var(--cm-text-secondary)] hover:text-[var(--cm-text)]"
              }`}
            >
              Thông tin chung
            </button>
            <button
              type="button"
              id="tab-curriculum-nodes"
              onClick={() => isEditMode && setActiveTab("nodes")}
              disabled={!isEditMode}
              className={`py-3 px-5 text-sm font-medium transition border-b-2 ${
                activeTab === "nodes"
                  ? "border-[var(--cm-cyan)] text-[var(--cm-text)]"
                  : "border-transparent text-[var(--cm-text-secondary)] hover:text-[var(--cm-text)]"
              } ${!isEditMode ? "opacity-40 cursor-not-allowed" : ""}`}
            >
              Nội dung kiến thức ({formData.nodeIds.length})
            </button>
            <button
              type="button"
              id="tab-curriculum-classes"
              onClick={() => isEditMode && setActiveTab("classes")}
              disabled={!isEditMode}
              className={`py-3 px-5 text-sm font-medium transition border-b-2 ${
                activeTab === "classes"
                  ? "border-[var(--cm-cyan)] text-[var(--cm-text)]"
                  : "border-transparent text-[var(--cm-text-secondary)] hover:text-[var(--cm-text)]"
              } ${!isEditMode ? "opacity-40 cursor-not-allowed" : ""}`}
            >
              Phân bổ Lớp học ({formData.classIds.length})
            </button>
          </div>

          <div className="p-6 lg:p-8">
            {/* TAB 1: General Info */}
            {activeTab === "info" && (
              <div className="max-w-3xl space-y-5">
                <div>
                  <label htmlFor="curriculum-title-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                    Tiêu đề lộ trình <span className="text-rose-400">*</span>
                  </label>
                  <input
                    id="curriculum-title-input"
                    type="text"
                    required
                    maxLength={200}
                    disabled={isReadOnly}
                    value={formData.title}
                    onChange={(e) => handleInputChange("title", e.target.value)}
                    placeholder="VD: Lộ trình Nền tảng Toán học Lớp 10"
                    className="cm-input w-full text-sm"
                  />
                </div>

                <div>
                  <label htmlFor="curriculum-subject-select" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                    Môn học <span className="text-rose-400">*</span>
                  </label>
                  {isEditMode ? (
                    <input
                      type="text"
                      disabled
                      value={
                        subjectsData?.data?.find((s) => s.subjectId === formData.subjectId)
                          ? `${subjectsData?.data?.find((s) => s.subjectId === formData.subjectId)?.subjectName} (${subjectsData?.data?.find((s) => s.subjectId === formData.subjectId)?.subjectCode})`
                          : formData.subjectId
                      }
                      className="cm-input w-full text-sm opacity-70 cursor-not-allowed bg-[var(--cm-surface-subtle)]"
                    />
                  ) : (
                    <select
                      id="curriculum-subject-select"
                      disabled={isLoadingSubjects}
                      value={formData.subjectId}
                      onChange={(e) => handleInputChange("subjectId", e.target.value)}
                      className="cm-select w-full text-sm"
                    >
                      <option value="">-- Chọn môn học --</option>
                      {subjectsData?.data?.map((sub) => (
                        <option key={sub.subjectId} value={sub.subjectId}>
                          {sub.subjectName} ({sub.subjectCode})
                        </option>
                      ))}
                    </select>
                  )}
                </div>

                {shouldDisplayTeacherSelector({ isEditMode, isCenterManager }) && (
                  <div>
                    <label htmlFor="curriculum-teacher-select" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                      Giáo viên phụ trách <span className="text-rose-400">*</span>
                    </label>
                    <select
                      id="curriculum-teacher-select"
                      data-testid="curriculum-teacher-select"
                      disabled={isLoadingTeachers || !canReadTeachers}
                      value={formData.teacherId}
                      onChange={(e) => handleInputChange("teacherId", e.target.value)}
                      className="cm-select w-full text-sm"
                    >
                      <option value="">-- Chọn giáo viên phụ trách --</option>
                      {teachersData?.data?.map((teacher) => (
                        <option key={teacher.teacherId} value={teacher.teacherId}>
                          {teacher.displayName} (@{teacher.username})
                        </option>
                      ))}
                    </select>
                    <p className="mt-1 text-xs text-[var(--cm-text-muted)]">
                      {canReadTeachers
                        ? "Lộ trình học bắt buộc phải do một giáo viên trong trung tâm phụ trách."
                        : "Bạn cần quyền xem giáo viên để chọn người phụ trách lộ trình."}
                    </p>
                  </div>
                )}

                {isEditMode && formData.teacherId && (
                  <div>
                    <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                      Giáo viên phụ trách
                    </label>
                    <input
                      type="text"
                      disabled
                      value={
                        teachersData?.data?.find((t) => t.teacherId === formData.teacherId)
                          ? `${teachersData?.data?.find((t) => t.teacherId === formData.teacherId)?.displayName} (@${teachersData?.data?.find((t) => t.teacherId === formData.teacherId)?.username})`
                          : formData.teacherId
                      }
                      className="cm-input w-full text-sm opacity-70 cursor-not-allowed bg-[var(--cm-surface-subtle)]"
                    />
                  </div>
                )}

                <div>
                  <label htmlFor="curriculum-desc-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                    Mô tả chi tiết
                  </label>
                  <textarea
                    id="curriculum-desc-input"
                    rows={4}
                    maxLength={1000}
                    disabled={isReadOnly}
                    value={formData.description}
                    onChange={(e) => handleInputChange("description", e.target.value)}
                    placeholder="Mô tả mục tiêu, đối tượng và kết quả đầu ra của lộ trình học..."
                    className="cm-input w-full text-sm"
                  />
                </div>

                {isDraft && canSaveInfo && (
                  <div className="pt-4 border-t border-[var(--cm-border-subtle)]">
                    <button
                      type="button"
                      id="btn-save-curriculum-info"
                      onClick={handleSaveInfo}
                      disabled={createMutation.isPending || updateMutation.isPending}
                      className="cm-primary-button text-sm py-2.5 px-6"
                    >
                      {createMutation.isPending || updateMutation.isPending
                        ? "Đang lưu..."
                        : isEditMode
                        ? "Cập nhật thông tin"
                        : "Tạo mới lộ trình"}
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* TAB 2: Knowledge Nodes */}
            {activeTab === "nodes" && (
              <div className="max-w-3xl space-y-6">
                <div>
                  <h3 className="text-sm font-bold text-[var(--cm-text)]">
                    Chuỗi nút kiến thức ({formData.nodeIds.length})
                  </h3>
                  <p className="text-xs text-[var(--cm-text-secondary)] mt-0.5">
                    Thứ tự trong danh sách tương ứng với tiến trình học tập được đề xuất cho học sinh.
                  </p>
                </div>

                {/* Interactive Add Node Bar */}
                {isDraft && canUpdateCurriculums && (
                  <div className="flex gap-2">
                    <input
                      type="text"
                      value={newNodeIdInput}
                      onChange={(e) => setNewNodeIdInput(e.target.value)}
                      onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), handleAddNodeId())}
                      placeholder="Nhập mã nút kiến thức (Node ID) để thêm vào chuỗi..."
                      className="cm-input flex-1 text-sm font-mono"
                    />
                    <button
                      type="button"
                      onClick={handleAddNodeId}
                      className="cm-secondary-button text-sm px-4 shrink-0"
                    >
                      + Thêm vào lộ trình
                    </button>
                  </div>
                )}

                {/* Node List View */}
                {formData.nodeIds.length === 0 ? (
                  <div className="rounded-xl border border-dashed border-[var(--cm-border)] p-8 text-center text-xs text-[var(--cm-text-muted)]">
                    Chưa có nút kiến thức nào trong lộ trình này.
                  </div>
                ) : (
                  <div className="space-y-2">
                    {formData.nodeIds.map((nodeId, index) => (
                      <div
                        key={`${nodeId}-${index}`}
                        className="flex items-center justify-between rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] p-3 text-xs"
                      >
                        <div className="flex items-center gap-3 min-w-0">
                          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-[var(--cm-indigo)]/20 font-bold text-[var(--cm-cyan)] shrink-0">
                            {index + 1}
                          </span>
                          <span className="font-mono font-medium text-[var(--cm-text)] truncate">{nodeId}</span>
                        </div>

                        {isDraft && canUpdateCurriculums && (
                          <div className="flex items-center gap-1.5 shrink-0">
                            <button
                              type="button"
                              disabled={index === 0}
                              onClick={() => handleMoveNode(index, "up")}
                              className="cm-icon-button h-7 w-7 text-xs border border-[var(--cm-border)] disabled:opacity-30"
                              title="Di chuyển lên"
                            >
                              ▲
                            </button>
                            <button
                              type="button"
                              disabled={index === formData.nodeIds.length - 1}
                              onClick={() => handleMoveNode(index, "down")}
                              className="cm-icon-button h-7 w-7 text-xs border border-[var(--cm-border)] disabled:opacity-30"
                              title="Di chuyển xuống"
                            >
                              ▼
                            </button>
                            <button
                              type="button"
                              onClick={() => handleRemoveNodeId(index)}
                              className="cm-icon-button h-7 w-7 text-xs border border-[var(--cm-border)] text-rose-400 hover:text-rose-300"
                              title="Xóa khỏi danh sách"
                            >
                              ✕
                            </button>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}

                {/* Raw ID Editor Fallback / Bulk Input */}
                <div className="pt-4 border-t border-[var(--cm-border-subtle)]">
                  <label htmlFor="curriculum-raw-nodes-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                    Nhập nhanh hàng loạt (cách nhau bởi dấu phẩy)
                  </label>
                  <textarea
                    id="curriculum-raw-nodes-input"
                    rows={3}
                    disabled={isReadOnly}
                    value={formData.nodeIds.join(", ")}
                    onChange={(e) =>
                      handleInputChange(
                        "nodeIds",
                        e.target.value
                          .split(",")
                          .map((s) => s.trim())
                          .filter((s) => s)
                      )
                    }
                    placeholder="VD: 101, 102, 103"
                    className="cm-input w-full text-xs font-mono"
                  />
                </div>

                {isDraft && canUpdateCurriculums && (
                  <div className="pt-4 border-t border-[var(--cm-border-subtle)]">
                    <button
                      type="button"
                      id="btn-save-curriculum-nodes"
                      onClick={handleSaveNodes}
                      disabled={updateNodesMutation.isPending}
                      className="cm-primary-button text-sm py-2.5 px-6"
                    >
                      {updateNodesMutation.isPending ? "Đang lưu..." : "Lưu danh sách kiến thức"}
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* TAB 3: Classes Assignment */}
            {activeTab === "classes" && (
              <div className="max-w-3xl space-y-6">
                <div>
                  <h3 className="text-sm font-bold text-[var(--cm-text)]">
                    Phân bổ Lớp học ({formData.classIds.length})
                  </h3>
                  <p className="text-xs text-[var(--cm-text-secondary)] mt-0.5">
                    Các lớp học được gán lộ trình này sẽ áp dụng nội dung bài giảng theo đúng tiến trình cấu hình.
                  </p>
                </div>

                {/* Interactive Add Class Bar */}
                {isDraft && canUpdateCurriculums && (
                  <div className="flex gap-2">
                    <input
                      type="text"
                      value={newClassIdInput}
                      onChange={(e) => setNewClassIdInput(e.target.value)}
                      onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), handleAddClassId())}
                      placeholder="Nhập mã lớp học (Class ID / UUID) để phân bổ..."
                      className="cm-input flex-1 text-sm font-mono"
                    />
                    <button
                      type="button"
                      onClick={handleAddClassId}
                      className="cm-secondary-button text-sm px-4 shrink-0"
                    >
                      + Phân bổ lớp
                    </button>
                  </div>
                )}

                {/* Class List View */}
                {formData.classIds.length === 0 ? (
                  <div className="rounded-xl border border-dashed border-[var(--cm-border)] p-8 text-center text-xs text-[var(--cm-text-muted)]">
                    Chưa có lớp học nào được phân bổ lộ trình này.
                  </div>
                ) : (
                  <div className="space-y-2">
                    {formData.classIds.map((classId, index) => (
                      <div
                        key={`${classId}-${index}`}
                        className="flex items-center justify-between rounded-xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface-subtle)] p-3 text-xs"
                      >
                        <div className="flex items-center gap-3 min-w-0">
                          <span className="flex h-6 w-6 items-center justify-center rounded-full bg-[var(--cm-indigo)]/20 font-bold text-[var(--cm-cyan)] shrink-0">
                            {index + 1}
                          </span>
                          <span className="font-mono font-medium text-[var(--cm-text)] truncate">{classId}</span>
                        </div>

                        {isDraft && canUpdateCurriculums && (
                          <button
                            type="button"
                            onClick={() => handleRemoveClassId(index)}
                            className="cm-icon-button h-7 w-7 text-xs border border-[var(--cm-border)] text-rose-400 hover:text-rose-300"
                            title="Xóa khỏi phân bổ"
                          >
                            ✕
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                )}

                {/* Raw Class ID Editor / Bulk Input */}
                <div className="pt-4 border-t border-[var(--cm-border-subtle)]">
                  <label htmlFor="curriculum-raw-classes-input" className="block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)] mb-1">
                    Nhập nhanh danh sách lớp (cách nhau bởi dấu phẩy)
                  </label>
                  <textarea
                    id="curriculum-raw-classes-input"
                    rows={3}
                    disabled={isReadOnly}
                    value={formData.classIds.join(", ")}
                    onChange={(e) =>
                      handleInputChange(
                        "classIds",
                        e.target.value
                          .split(",")
                          .map((s) => s.trim())
                          .filter((s) => s)
                      )
                    }
                    placeholder="VD: 440939eb-..., ed712b7a-..."
                    className="cm-input w-full text-xs font-mono"
                  />
                </div>

                {isDraft && canUpdateCurriculums && (
                  <div className="pt-4 border-t border-[var(--cm-border-subtle)]">
                    <button
                      type="button"
                      id="btn-save-curriculum-classes"
                      onClick={handleSaveClasses}
                      disabled={updateClassesMutation.isPending}
                      className="cm-primary-button text-sm py-2.5 px-6"
                    >
                      {updateClassesMutation.isPending ? "Đang lưu..." : "Lưu danh sách lớp học"}
                    </button>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Accessible Publish Confirmation Modal */}
        <Modal
          isOpen={isPublishModalOpen}
          title="Xác nhận xuất bản lộ trình học"
          description="Chuyển trạng thái lộ trình sang Published để kích hoạt cho các lớp học"
          onClose={() => setIsPublishModalOpen(false)}
        >
          {publishError && (
            <div className="mb-4 rounded-lg bg-rose-500/10 p-3 border border-rose-500/30 text-xs font-medium text-rose-300">
              {publishError}
            </div>
          )}

          <div className="space-y-4 text-xs text-[var(--cm-text-secondary)]">
            <p>
              Bạn có chắc chắn muốn xuất bản lộ trình học{" "}
              <strong className="text-[var(--cm-text)]">{formData.title}</strong>?
            </p>

            <div className="rounded-xl bg-[var(--cm-surface-subtle)] p-3 border border-[var(--cm-border-subtle)] space-y-1.5">
              <div className="flex justify-between">
                <span>Số lượng nút kiến thức:</span>
                <strong className="text-[var(--cm-text)]">{formData.nodeIds.length}</strong>
              </div>
              <div className="flex justify-between">
                <span>Số lượng lớp áp dụng:</span>
                <strong className="text-[var(--cm-cyan)]">{formData.classIds.length}</strong>
              </div>
            </div>

            <div className="rounded-xl bg-amber-500/10 p-3 border border-amber-500/30 text-amber-300">
              <strong>Lưu ý:</strong> Sau khi xuất bản, lộ trình sẽ chuyển sang trạng thái <strong>Published</strong> và hệ thống sẽ khóa chỉnh sửa thông tin chung, danh sách nút kiến thức và lớp học được phân bổ.
            </div>
          </div>

          <div className="flex items-center justify-end gap-3 pt-4 border-t border-[var(--cm-border-subtle)] mt-5">
            <button
              type="button"
              onClick={() => setIsPublishModalOpen(false)}
              disabled={publishMutation.isPending}
              className="cm-secondary-button text-xs py-2 px-4"
            >
              Hủy
            </button>
            <button
              type="button"
              id="btn-confirm-publish-curriculum"
              onClick={handleConfirmPublish}
              disabled={publishMutation.isPending}
              className="cm-primary-button text-xs py-2 px-5 bg-emerald-600 hover:bg-emerald-500 border-emerald-500/30"
            >
              {publishMutation.isPending ? "Đang xuất bản..." : "Xác nhận xuất bản"}
            </button>
          </div>
        </Modal>
      </div>
    </CenterManagerThemeScope>
  );
};

/**
 * Legacy view preserved 100% for Teacher and other non-CenterManager accounts
 */
const LegacyCurriculumEditorPage = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const isEditMode = !!id;

  const user = useAuthStore((state) => state.user);
  const isCenterManager = user?.accountType === "CenterManager";
  const canReadTeachers = useAuthStore((state) => state.hasPermission(permissions.teachersRead));

  const [formData, setFormData] = useState<
    Partial<
      CreateCurriculumRequest &
        UpdateCurriculumRequest & { classIds: string[]; rowVersion: string; reviewStatus: string }
    >
  >({
    title: "",
    description: "",
    subjectId: "",
    teacherId: "",
    nodeIds: [],
    classIds: [],
    rowVersion: "0",
    reviewStatus: "Draft",
  });

  const [activeTab, setActiveTab] = useState<"info" | "nodes" | "classes">("info");

  const { data: curriculumData, isLoading: isLoadingCurriculum, refetch: refetchCurriculum } = useCurriculum(
    id || ""
  );

  const createMutation = useCreateCurriculum();
  const updateMutation = useUpdateCurriculum();
  const updateClassesMutation = useUpdateCurriculumClasses();
  const updateNodesMutation = useUpdateCurriculumNodes();
  const publishMutation = usePublishCurriculum();

  const { data: subjectsData, isLoading: isLoadingSubjects } = useQuery({
    queryKey: ["subjects", "active"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const { data: teachersData, isLoading: isLoadingTeachers } = useQuery({
    queryKey: ["teachers", "active"],
    queryFn: () => organizationApi.listTeachers({ page: 1, pageSize: 100, status: "Active" }),
    enabled: isCenterManager && canReadTeachers,
  });

  useEffect(() => {
    if (isEditMode && curriculumData?.data) {
      const c = curriculumData.data;
      setFormData({
        title: c.title,
        description: c.description || "",
        subjectId: c.subjectId,
        teacherId: c.teacherId || "",
        nodeIds: c.nodeIds || [],
        classIds: c.classIds || [],
        rowVersion: c.rowVersion,
        reviewStatus: c.reviewStatus,
      });
    }
  }, [isEditMode, curriculumData]);

  const handleInputChange = (field: keyof typeof formData, value: any) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const handleSaveInfo = () => {
    const validation = validateCurriculumForm(formData, { isEditMode, isCenterManager });
    if (!validation.isValid) {
      alert(validation.errorMessage!);
      return;
    }

    if (!isEditMode) {
      createMutation.mutate(
        buildCreateCurriculumPayload(
          {
            title: formData.title!,
            description: formData.description,
            subjectId: formData.subjectId!,
            teacherId: formData.teacherId,
            nodeIds: formData.nodeIds || [],
          },
          isCenterManager
        ),
        {
          onSuccess: (res) => {
            alert("Tạo lộ trình thành công!");
            navigate(`/quan-ly/giao-trinh/${res.data.curriculumId}`);
          },
          onError: (err: any) => {
            alert(mapSafeOperationalError(err, "Không thể lưu giáo trình."));
          },
        }
      );
    } else {
      updateMutation.mutate(
        {
          id: id!,
          data: buildUpdateCurriculumPayload({
            title: formData.title!,
            description: formData.description,
            rowVersion: formData.rowVersion!,
          }),
        },
        {
          onSuccess: (res) => {
            alert("Cập nhật thông tin thành công!");
            setFormData((prev) => ({ ...prev, rowVersion: res.data.rowVersion }));
          },
          onError: (err: any) => {
            if (isConcurrencyConflictError(err)) {
              alert(
                "Xung đột phiên bản (409): Dữ liệu lộ trình đã được cập nhật bởi phiên khác. Đang tải lại dữ liệu mới nhất..."
              );
              refetchCurriculum();
            } else {
              alert(mapSafeOperationalError(err, "Không thể cập nhật lớp của giáo trình."));
            }
          },
        }
      );
    }
  };

  const handleSaveNodes = () => {
    if (!isEditMode) return;
    updateNodesMutation.mutate(
      {
        id: id!,
        data: {
          nodeIds: formData.nodeIds || [],
          rowVersion: formData.rowVersion!,
        },
      },
      {
        onSuccess: (res) => {
          alert("Cập nhật kiến thức thành công!");
          setFormData((prev) => ({ ...prev, rowVersion: res.data.rowVersion }));
        },
        onError: (err: any) => {
          if (err.response?.status === 409) {
            alert(
              "Xung đột phiên bản (409): Dữ liệu lộ trình đã được cập nhật bởi phiên khác. Đang tải lại dữ liệu mới nhất..."
            );
            refetchCurriculum();
          } else {
            alert(mapSafeOperationalError(err, "Không thể cập nhật nút kiến thức của giáo trình."));
          }
        },
      }
    );
  };

  const handleSaveClasses = () => {
    if (!isEditMode) return;
    updateClassesMutation.mutate(
      {
        id: id!,
        data: {
          classIds: formData.classIds || [],
          rowVersion: formData.rowVersion!,
        },
      },
      {
        onSuccess: (res) => {
          alert("Gán lớp học thành công!");
          setFormData((prev) => ({ ...prev, rowVersion: res.data.rowVersion }));
        },
        onError: (err: any) => {
          if (err.response?.status === 409) {
            alert(
              "Xung đột phiên bản (409): Dữ liệu lộ trình đã được cập nhật bởi phiên khác. Đang tải lại dữ liệu mới nhất..."
            );
            refetchCurriculum();
          } else {
            alert(mapSafeOperationalError(err, "Không thể xuất bản giáo trình."));
          }
        },
      }
    );
  };

  const handlePublish = () => {
    if (!isEditMode) return;
    if (confirm("Bạn có chắc chắn muốn xuất bản lộ trình này? Sau khi xuất bản sẽ không thể sửa đổi.")) {
      publishMutation.mutate(
        {
          id: id!,
          data: { rowVersion: formData.rowVersion! },
        },
        {
          onSuccess: (res) => {
            alert("Xuất bản thành công!");
            setFormData((prev) => ({
              ...prev,
              rowVersion: res.data.rowVersion,
              reviewStatus: res.data.reviewStatus,
            }));
          },
          onError: (err: any) => {
            if (err.response?.status === 409) {
              alert(
                "Xung đột phiên bản (409): Lộ trình đã được thay đổi trước khi xuất bản. Đang tải lại dữ liệu mới nhất..."
              );
              refetchCurriculum();
            } else {
              alert(mapSafeOperationalError(err, "Không thể cập nhật giáo trình."));
            }
          },
        }
      );
    }
  };

  if (isEditMode && isLoadingCurriculum) {
    return <div className="p-6 text-center">Đang tải dữ liệu...</div>;
  }

  const isDraft = formData.reviewStatus === "Draft";

  return (
    <div className="p-6 max-w-5xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold text-slate-800">
          {isEditMode ? "Chỉnh sửa Lộ trình học" : "Tạo Lộ trình học mới"}
        </h1>
        <div className="flex gap-3">
          <button
            onClick={() => navigate("/quan-ly/giao-trinh")}
            className="px-4 py-2 border border-slate-300 text-slate-700 rounded-lg hover:bg-slate-50 transition"
          >
            Quay lại
          </button>
          {isEditMode && isDraft && (
            <button
              onClick={handlePublish}
              disabled={publishMutation.isPending}
              className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition shadow-sm font-medium disabled:opacity-50"
            >
              Xuất bản (Publish)
            </button>
          )}
        </div>
      </div>

      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden mb-6">
        <div className="flex border-b border-slate-200">
          <button
            className={`px-6 py-3 font-medium text-sm ${
              activeTab === "info" ? "text-blue-600 border-b-2 border-blue-600" : "text-slate-600 hover:text-slate-800"
            }`}
            onClick={() => setActiveTab("info")}
          >
            Thông tin chung
          </button>
          <button
            className={`px-6 py-3 font-medium text-sm ${
              activeTab === "nodes" ? "text-blue-600 border-b-2 border-blue-600" : "text-slate-600 hover:text-slate-800"
            } ${!isEditMode && "opacity-50 cursor-not-allowed"}`}
            onClick={() => isEditMode && setActiveTab("nodes")}
            disabled={!isEditMode}
          >
            Nội dung kiến thức
          </button>
          <button
            className={`px-6 py-3 font-medium text-sm ${
              activeTab === "classes" ? "text-blue-600 border-b-2 border-blue-600" : "text-slate-600 hover:text-slate-800"
            } ${!isEditMode && "opacity-50 cursor-not-allowed"}`}
            onClick={() => isEditMode && setActiveTab("classes")}
            disabled={!isEditMode}
          >
            Phân bổ Lớp học
          </button>
        </div>

        <div className="p-6">
          {activeTab === "info" && (
            <div className="space-y-4 max-w-2xl">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">
                  Tiêu đề lộ trình <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={formData.title}
                  onChange={(e) => handleInputChange("title", e.target.value)}
                  disabled={!isDraft}
                  className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none disabled:bg-slate-50"
                  placeholder="Nhập tiêu đề..."
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">
                  Môn học <span className="text-red-500">*</span>
                </label>
                {isEditMode ? (
                  <input
                    type="text"
                    value={
                      subjectsData?.data?.find((s: any) => s.subjectId === formData.subjectId)
                        ? `${subjectsData?.data?.find((s: any) => s.subjectId === formData.subjectId)?.subjectName} — ${formData.subjectId}`
                        : formData.subjectId
                    }
                    disabled
                    className="w-full border border-slate-300 rounded-lg px-4 py-2 outline-none bg-slate-100 text-slate-500 text-sm"
                  />
                ) : (
                  <select
                    value={formData.subjectId}
                    onChange={(e) => handleInputChange("subjectId", e.target.value)}
                    disabled={isLoadingSubjects}
                    className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white disabled:bg-slate-100"
                  >
                    <option value="">-- Chọn môn học --</option>
                    {subjectsData?.data?.map((subject: any) => (
                      <option key={subject.subjectId} value={subject.subjectId}>
                        {subject.subjectName} — {subject.subjectId}
                      </option>
                    ))}
                  </select>
                )}
              </div>

              {shouldDisplayTeacherSelector({ isEditMode, isCenterManager }) && (
                <div>
                  <label htmlFor="curriculum-teacher-select" className="block text-sm font-medium text-slate-700 mb-1">
                    Giáo viên phụ trách <span className="text-red-500">*</span>
                  </label>
                  <select
                    id="curriculum-teacher-select"
                    data-testid="curriculum-teacher-select"
                    value={formData.teacherId || ""}
                    onChange={(e) => handleInputChange("teacherId", e.target.value)}
                    disabled={isLoadingTeachers || !canReadTeachers}
                    className="w-full border border-slate-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white disabled:bg-slate-100"
                  >
                    <option value="">-- Chọn giáo viên phụ trách --</option>
                    {teachersData?.data?.map((teacher: any) => (
                      <option key={teacher.teacherId} value={teacher.teacherId}>
                        {teacher.displayName} (@{teacher.username})
                      </option>
                    ))}
                  </select>
                  <p className={`text-xs mt-1 ${canReadTeachers ? "text-slate-500" : "text-amber-700"}`}>
                    {canReadTeachers
                      ? "Lộ trình học bắt buộc phải do một giáo viên trong trung tâm phụ trách."
                      : "Bạn cần quyền xem giáo viên để chọn người phụ trách lộ trình."}
                  </p>
                </div>
              )}

              {isEditMode && isCenterManager && formData.teacherId && (
                <div>
                  <label className="block text-sm font-medium text-slate-700 mb-1">Giáo viên phụ trách</label>
                  <input
                    type="text"
                    value={
                      teachersData?.data?.find((t: any) => t.teacherId === formData.teacherId)
                        ? `${teachersData?.data?.find((t: any) => t.teacherId === formData.teacherId)?.displayName} (@${teachersData?.data?.find((t: any) => t.teacherId === formData.teacherId)?.username})`
                        : formData.teacherId
                    }
                    disabled
                    className="w-full border border-slate-300 rounded-lg px-4 py-2 outline-none bg-slate-100 text-slate-500 text-sm"
                  />
                </div>
              )}

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">Mô tả chi tiết</label>
                <textarea
                  rows={4}
                  value={formData.description}
                  onChange={(e) => handleInputChange("description", e.target.value)}
                  disabled={!isDraft}
                  className="w-full border border-slate-300 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none resize-y disabled:bg-slate-50"
                  placeholder="Mô tả lộ trình học..."
                ></textarea>
              </div>

              {isDraft && (
                <div className="pt-4">
                  <button
                    onClick={handleSaveInfo}
                    disabled={createMutation.isPending || updateMutation.isPending}
                    className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium disabled:opacity-50"
                  >
                    {isEditMode ? "Cập nhật thông tin" : "Tạo mới lộ trình"}
                  </button>
                </div>
              )}
            </div>
          )}

          {activeTab === "nodes" && (
            <div className="space-y-4 max-w-2xl">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">
                  Danh sách Knowledge Node IDs (cách nhau bởi dấu phẩy)
                </label>
                <textarea
                  rows={4}
                  value={formData.nodeIds?.join(", ")}
                  onChange={(e) =>
                    handleInputChange(
                      "nodeIds",
                      e.target.value
                        .split(",")
                        .map((s) => s.trim())
                        .filter((s) => s)
                    )
                  }
                  disabled={!isDraft}
                  className="w-full border border-slate-300 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none resize-y disabled:bg-slate-50"
                  placeholder="VD: 101, 102, 103"
                ></textarea>
                <p className="text-xs text-slate-500 mt-1">Các node kiến thức sẽ được học theo thứ tự đã nhập.</p>
              </div>

              {isDraft && (
                <div className="pt-4">
                  <button
                    onClick={handleSaveNodes}
                    disabled={updateNodesMutation.isPending}
                    className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium disabled:opacity-50"
                  >
                    Lưu danh sách kiến thức
                  </button>
                </div>
              )}
            </div>
          )}

          {activeTab === "classes" && (
            <div className="space-y-4 max-w-2xl">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">
                  Danh sách Class IDs (cách nhau bởi dấu phẩy)
                </label>
                <textarea
                  rows={4}
                  value={formData.classIds?.join(", ")}
                  onChange={(e) =>
                    handleInputChange(
                      "classIds",
                      e.target.value
                        .split(",")
                        .map((s) => s.trim())
                        .filter((s) => s)
                    )
                  }
                  disabled={!isDraft}
                  className="w-full border border-slate-300 rounded-lg px-4 py-3 focus:ring-2 focus:ring-blue-500 outline-none resize-y disabled:bg-slate-50"
                  placeholder="VD: eed34b81-..., fed34b81-..."
                ></textarea>
                <p className="text-xs text-slate-500 mt-1">Các lớp học sẽ được áp dụng lộ trình này.</p>
              </div>

              {isDraft && (
                <div className="pt-4">
                  <button
                    onClick={handleSaveClasses}
                    disabled={updateClassesMutation.isPending}
                    className="px-6 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium disabled:opacity-50"
                  >
                    Lưu danh sách lớp học
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export const CurriculumEditorPage = () => {
  const user = useAuthStore((state) => state.user);
  return user?.accountType === "CenterManager" ? (
    <CenterManagerCurriculumEditorView />
  ) : (
    <LegacyCurriculumEditorPage />
  );
};
