import React, { useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useCurriculums } from "../features/curriculum/useCurriculums";
import type { ReviewStatus } from "../types/curriculum";
import { useAuthStore } from "../stores/authStore";
import { permissions } from "../auth/permissions";
import { mapSafeOperationalError } from "../utils/problemDetails";
import { organizationApi } from "../api/organizationApi";
import {
  CenterManagerThemeScope,
  PageHeader,
  SafeErrorPanel,
  Skeleton,
  StatusBadge,
} from "../components/centerManager";

const statusLabels: Record<ReviewStatus, string> = {
  Draft: "Bản nháp",
  Published: "Đã xuất bản",
  Archived: "Đã lưu trữ",
};

/**
 * Modern CenterManager Dark Enterprise SaaS view for CurriculumListPage
 */
const CenterManagerCurriculumListView: React.FC = () => {
  const navigate = useNavigate();
  const hasPermission = useAuthStore((state) => state.hasPermission);

  const canCreate = hasPermission(permissions.curriculumsCreate);
  const canUpdate = hasPermission(permissions.curriculumsUpdate);
  const canReadSubjects = hasPermission(permissions.subjectsRead);

  const [selectedSubjectId, setSelectedSubjectId] = useState<string>("");
  const [selectedStatus, setSelectedStatus] = useState<ReviewStatus | "">("");

  // Query active subjects to resolve subjectId -> subjectName and populate filter dropdown
  const { data: subjectsData } = useQuery({
    queryKey: ["subjects", "active-for-curriculum-list"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: canReadSubjects,
  });

  const subjects = useMemo(() => subjectsData?.data ?? [], [subjectsData?.data]);

  const subjectsMap = useMemo(() => {
    const map = new Map<string, string>();
    for (const sub of subjects) {
      map.set(sub.subjectId, `${sub.subjectName} (${sub.subjectCode})`);
    }
    return map;
  }, [subjects]);

  const {
    data: response,
    isLoading,
    isError,
    error,
    refetch,
  } = useCurriculums(
    selectedSubjectId || undefined,
    (selectedStatus as ReviewStatus) || undefined
  );

  const curriculums = useMemo(() => response?.data ?? [], [response?.data]);

  return (
    <CenterManagerThemeScope data-actor="center-manager">
      <div className="space-y-6 p-6 lg:p-8">
        <PageHeader
          eyebrow="Nội dung học thuật"
          title="Giáo trình & Lộ trình học"
          description="Quản lý các chương trình học, cấu trúc bài giảng và phân bổ cho từng lớp học."
          breadcrumbs={[
            { label: "CenterManager", href: "/quan-ly/tong-quan-trung-tam" },
            { label: "Giáo trình" },
          ]}
          actions={
            canCreate && (
              <button
                type="button"
                id="btn-create-curriculum"
                onClick={() => navigate("/quan-ly/giao-trinh/tao-moi")}
                className="cm-primary-button text-sm"
              >
                + Tạo lộ trình mới
              </button>
            )
          }
        />

        {/* Filter Bar */}
        <div className="flex flex-col gap-4 rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex flex-1 flex-col gap-3 sm:flex-row sm:items-center">
            {canReadSubjects && (
              <div className="w-full sm:max-w-xs">
                <label
                  htmlFor="filter-curriculum-subject"
                  className="mb-1 block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]"
                >
                  Môn học
                </label>
                <select
                  id="filter-curriculum-subject"
                  value={selectedSubjectId}
                  onChange={(e) => setSelectedSubjectId(e.target.value)}
                  className="cm-field w-full px-3 py-2 text-sm"
                >
                  <option value="">-- Tất cả môn học --</option>
                  {subjects.map((sub) => (
                    <option key={sub.subjectId} value={sub.subjectId}>
                      {sub.subjectName} ({sub.subjectCode})
                    </option>
                  ))}
                </select>
              </div>
            )}

            <div className="w-full sm:max-w-xs">
              <label
                htmlFor="filter-curriculum-status"
                className="mb-1 block text-xs font-semibold uppercase tracking-wider text-[var(--cm-text-muted)]"
              >
                Trạng thái
              </label>
              <select
                id="filter-curriculum-status"
                value={selectedStatus}
                onChange={(e) => setSelectedStatus(e.target.value as ReviewStatus | "")}
                className="cm-field w-full px-3 py-2 text-sm"
              >
                <option value="">-- Tất cả trạng thái --</option>
                <option value="Draft">Bản nháp (Draft)</option>
                <option value="Published">Đã xuất bản (Published)</option>
                <option value="Archived">Đã lưu trữ (Archived)</option>
              </select>
            </div>
          </div>

          {(selectedSubjectId || selectedStatus) && (
            <button
              type="button"
              onClick={() => {
                setSelectedSubjectId("");
                setSelectedStatus("");
              }}
              className="cm-secondary-button text-xs py-1.5 px-3 self-end sm:self-auto"
            >
              Xóa bộ lọc
            </button>
          )}
        </div>

        {/* Loading State */}
        {isLoading && (
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3">
            {[1, 2, 3, 4, 5, 6].map((idx) => (
              <div
                key={idx}
                className="rounded-2xl border border-[var(--cm-border)] bg-[var(--cm-surface)] p-5 space-y-4"
              >
                <div className="flex justify-between">
                  <Skeleton className="h-5 w-2/3" />
                  <Skeleton className="h-5 w-16" />
                </div>
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-4/5" />
                <div className="border-t border-[var(--cm-border-subtle)] pt-3 flex justify-between">
                  <Skeleton className="h-4 w-24" />
                  <Skeleton className="h-6 w-16" />
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Error State */}
        {isError && (
          <SafeErrorPanel
            error={error}
            fallback="Không thể tải danh sách giáo trình. Vui lòng thử lại."
            onRetry={() => refetch()}
          />
        )}

        {/* Empty State */}
        {!isLoading && !isError && curriculums.length === 0 && (
          <div className="rounded-2xl border border-[var(--cm-border-subtle)] bg-[var(--cm-surface)] p-12 text-center">
            <div className="mx-auto mb-3 grid h-12 w-12 place-items-center rounded-2xl bg-indigo-500/10 text-indigo-300 text-xl font-bold">
              📚
            </div>
            <h3 className="text-base font-semibold text-[var(--cm-text)]">
              Không tìm thấy lộ trình học nào
            </h3>
            <p className="mt-1 text-sm text-[var(--cm-text-muted)]">
              {selectedSubjectId || selectedStatus
                ? "Thử thay đổi hoặc xóa bộ lọc để xem các lộ trình khác."
                : "Chưa có giáo trình nào được tạo trong trung tâm."}
            </p>
            {canCreate && !selectedSubjectId && !selectedStatus && (
              <button
                type="button"
                onClick={() => navigate("/quan-ly/giao-trinh/tao-moi")}
                className="cm-primary-button mt-4 text-xs"
              >
                + Tạo lộ trình đầu tiên
              </button>
            )}
          </div>
        )}

        {/* Cards Grid */}
        {!isLoading && !isError && curriculums.length > 0 && (
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2 lg:grid-cols-3">
            {curriculums.map((curriculum) => {
              const subjectName = subjectsMap.get(curriculum.subjectId) || `Môn (#${curriculum.subjectId.slice(0, 8)})`;
              const nodeCount = curriculum.nodeIds?.length || 0;
              const classCount = curriculum.classIds?.length || 0;

              return (
                <div
                  key={curriculum.curriculumId}
                  className="cm-surface flex flex-col justify-between rounded-2xl border border-[var(--cm-border)] p-5 transition hover:border-[var(--cm-border-hover)] shadow-sm"
                >
                  <div className="space-y-3">
                    <div className="flex items-start justify-between gap-2">
                      <h3
                        className="text-base font-semibold text-[var(--cm-text)] line-clamp-1"
                        title={curriculum.title}
                      >
                        {curriculum.title}
                      </h3>
                      <StatusBadge
                        status={curriculum.reviewStatus}
                        label={statusLabels[curriculum.reviewStatus] || curriculum.reviewStatus}
                      />
                    </div>

                    <div className="flex items-center gap-2">
                      <span className="inline-flex items-center rounded-full bg-cyan-500/10 px-2.5 py-0.5 text-xs font-medium text-[var(--cm-cyan)]">
                        {subjectName}
                      </span>
                    </div>

                    <p className="text-xs text-[var(--cm-text-secondary)] line-clamp-2 min-h-[2.25rem]">
                      {curriculum.description || "Chưa có mô tả chi tiết cho giáo trình này."}
                    </p>
                  </div>

                  <div className="mt-5 border-t border-[var(--cm-border-subtle)] pt-4">
                    <div className="flex items-center justify-between text-xs text-[var(--cm-text-muted)] mb-3">
                      <span>
                        <strong className="text-[var(--cm-text)]">{nodeCount}</strong> nút kiến thức
                      </span>
                      <span>
                        <strong className="text-[var(--cm-text)]">{classCount}</strong> lớp áp dụng
                      </span>
                    </div>

                    <div className="flex justify-end">
                      <button
                        type="button"
                        id={`btn-edit-curriculum-${curriculum.curriculumId}`}
                        onClick={() => navigate(`/quan-ly/giao-trinh/${curriculum.curriculumId}`)}
                        className="cm-secondary-button text-xs py-1.5 px-3"
                      >
                        {canUpdate ? "Chỉnh sửa & Phân bổ" : "Xem chi tiết"}
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </CenterManagerThemeScope>
  );
};

/**
 * Legacy CurriculumListPage preserved for non-CenterManager accounts (Teacher, etc.)
 */
const LegacyCurriculumListPage: React.FC = () => {
  const [subjectId, setSubjectId] = useState<string>("");
  const [status, setStatus] = useState<ReviewStatus | "">("");
  const canCreate = useAuthStore((state) => state.hasPermission(permissions.curriculumsCreate));
  const canUpdate = useAuthStore((state) => state.hasPermission(permissions.curriculumsUpdate));

  const { data: response, isLoading, isError, error } = useCurriculums(
    subjectId || undefined,
    (status as ReviewStatus) || undefined
  );

  return (
    <div className="p-6 max-w-7xl mx-auto">
      <div className="flex justify-between items-center mb-6">
        <h1 className="text-3xl font-bold text-slate-800">Quản lý Lộ trình học</h1>
        {canCreate && (
          <Link
            to="/quan-ly/giao-trinh/tao-moi"
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition shadow-sm font-medium"
          >
            Tạo Lộ trình mới
          </Link>
        )}
      </div>

      <div className="bg-white p-4 rounded-xl shadow-sm border border-slate-100 mb-6 flex gap-4">
        <div className="flex-1">
          <label className="block text-sm font-medium text-slate-600 mb-1">Môn học</label>
          <input
            type="text"
            placeholder="Nhập ID môn học..."
            value={subjectId}
            onChange={(e) => setSubjectId(e.target.value)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none"
          />
        </div>
        <div className="flex-1">
          <label className="block text-sm font-medium text-slate-600 mb-1">Trạng thái</label>
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as any)}
            className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-2 focus:ring-blue-500 outline-none bg-white"
          >
            <option value="">Tất cả</option>
            <option value="Draft">Bản nháp (Draft)</option>
            <option value="Published">Đã xuất bản (Published)</option>
            <option value="Archived">Đã lưu trữ (Archived)</option>
          </select>
        </div>
      </div>

      {isLoading && (
        <div className="flex justify-center items-center py-20">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600"></div>
        </div>
      )}

      {isError && (
        <div className="bg-red-50 text-red-600 p-4 rounded-lg border border-red-100">
          {mapSafeOperationalError(error, "Không thể tải danh sách giáo trình.")}
        </div>
      )}

      {!isLoading && !isError && (!response?.data || response.data.length === 0) && (
        <div className="text-center py-20 bg-slate-50 rounded-xl border border-slate-100">
          <p className="text-slate-500 mb-4">Không tìm thấy lộ trình nào phù hợp.</p>
        </div>
      )}

      {!isLoading && !isError && response?.data && response.data.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {response.data.map((curriculum) => (
            <div
              key={curriculum.curriculumId}
              className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden hover:shadow-md transition"
            >
              <div className="p-5">
                <div className="flex justify-between items-start mb-2">
                  <h3 className="font-bold text-lg text-slate-800 line-clamp-1">{curriculum.title}</h3>
                  <span
                    className={`px-2 py-1 text-xs font-semibold rounded-full ${
                      curriculum.reviewStatus === "Published"
                        ? "bg-green-100 text-green-700"
                        : curriculum.reviewStatus === "Draft"
                        ? "bg-amber-100 text-amber-700"
                        : "bg-slate-100 text-slate-700"
                    }`}
                  >
                    {curriculum.reviewStatus}
                  </span>
                </div>
                <p className="text-sm text-slate-500 mb-4 line-clamp-2">
                  {curriculum.description || "Chưa có mô tả"}
                </p>

                <div className="flex justify-between items-center text-sm text-slate-500 pt-4 border-t border-slate-100">
                  <span>Môn học: {curriculum.subjectId.substring(0, 8)}...</span>
                  {canUpdate && (
                    <Link
                      to={`/quan-ly/giao-trinh/${curriculum.curriculumId}`}
                      className="text-blue-600 font-medium hover:underline"
                    >
                      Chi tiết
                    </Link>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export const CurriculumListPage: React.FC = () => {
  const user = useAuthStore((state) => state.user);
  return user?.accountType === "CenterManager" ? (
    <CenterManagerCurriculumListView />
  ) : (
    <LegacyCurriculumListPage />
  );
};
