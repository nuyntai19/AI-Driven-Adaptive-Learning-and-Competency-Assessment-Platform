import React, { useState, useMemo } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { curriculumApi } from "../../api/curriculumApi";
import { organizationApi } from "../../api/organizationApi";
import type { Curriculum, ReviewStatus } from "../../types/curriculum";
import { useAuthStore } from "../../stores/authStore";
import { permissions } from "../../auth/permissions";
import {
  TeacherPageHeader,
  TeacherMetricCard,
  TeacherStatusBadge,
  TeacherSkeleton,
  TeacherSafeErrorPanel,
} from "../../components/teacher/TeacherPrimitives";
import { TeacherFilterBar } from "../../components/teacher/TeacherFilterBar";

export const TeacherCurriculumListView: React.FC = () => {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();

  const selectedSubjectId = searchParams.get("subjectId") || "";
  const selectedStatus = searchParams.get("status") || "";
  const [searchTerm, setSearchTerm] = useState("");

  const hasPermission = useAuthStore((state) => state.hasPermission);
  const canCreate = hasPermission(permissions.curriculumsCreate);
  const canPublish = hasPermission(permissions.curriculumsPublish);

  // Fetch subjects
  const { data: subjectsData } = useQuery({
    queryKey: ["teacherSubjectsList"],
    queryFn: () => organizationApi.listSubjects(true),
  });

  const subjects = subjectsData?.data ?? [];

  // Fetch curriculums
  const {
    data: curriculumsData,
    isLoading,
    isError,
    error,
    refetch,
  } = useQuery({
    queryKey: ["teacherCurriculums", selectedSubjectId, selectedStatus],
    queryFn: () =>
      curriculumApi.getAll({
        subjectId: selectedSubjectId || undefined,
        status: (selectedStatus as ReviewStatus) || undefined,
      }),
  });

  const curriculums: Curriculum[] = curriculumsData?.data ?? [];

  // Filter curriculums locally by search term
  const filteredCurriculums = useMemo(() => {
    if (!searchTerm.trim()) return curriculums;
    const q = searchTerm.toLowerCase();
    return curriculums.filter(
      (c) =>
        c.title.toLowerCase().includes(q) ||
        c.description?.toLowerCase().includes(q)
    );
  }, [curriculums, searchTerm]);

  // Publish mutation
  const publishMutation = useMutation({
    mutationFn: async (curriculum: Curriculum) => {
      return await curriculumApi.publish(curriculum.curriculumId, {
        rowVersion: curriculum.rowVersion,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teacherCurriculums"] });
    },
  });

  // Calculate stats
  const totalCount = curriculums.length;
  const publishedCount = curriculums.filter((c) => c.reviewStatus === "Published").length;
  const draftCount = curriculums.filter((c) => c.reviewStatus === "Draft").length;
  const totalNodesCount = curriculums.reduce((acc, curr) => acc + (curr.nodeIds?.length ?? 0), 0);

  return (
    <div className="th-page-container">
      <TeacherPageHeader
        title="Soạn Thảo Giáo Trình & Khung Đào Tạo"
        subtitle="Quản lý cấu trúc bài giảng, phân bổ cây chủ đề tri thức và xuất bản giáo trình cho các lớp học"
        actions={
          canCreate ? (
            <Link to="/giao-vien/giao-trinh/tao-moi" className="th-primary-button" style={{ textDecoration: "none" }}>
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M12 5v14M5 12h14" />
              </svg>
              Soạn Giáo Trình Mới
            </Link>
          ) : undefined
        }
      />

      {/* Metrics Row */}
      <div className="th-stats-grid" style={{ marginBottom: "20px" }}>
        <TeacherMetricCard
          label="Tổng Giáo Trình"
          value={totalCount}
          unit="khung"
          color="emerald"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M4 19.5A2.5 2.5 0 016.5 17H20" />
              <path d="M6.5 2H20v20H6.5A2.5 2.5 0 014 19.5v-15A2.5 2.5 0 016.5 2z" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Đang Áp Dụng (Published)"
          value={publishedCount}
          unit="giáo trình"
          color="cyan"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M22 11.08V12a10 10 0 11-5.93-9.14" />
              <polyline points="22 4 12 14.01 9 11.01" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Bản Nháp (Draft)"
          value={draftCount}
          unit="bản"
          color="amber"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" />
              <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
            </svg>
          }
        />
        <TeacherMetricCard
          label="Tổng Chủ Đề Tích Hợp"
          value={totalNodesCount}
          unit="nodes"
          color="indigo"
          icon={
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="18" cy="5" r="3" />
              <circle cx="6" cy="12" r="3" />
              <circle cx="18" cy="19" r="3" />
              <line x1="8.59" y1="13.51" x2="15.42" y2="17.49" />
              <line x1="15.41" y1="6.51" x2="8.59" y2="10.49" />
            </svg>
          }
        />
      </div>

      {/* Filter Bar */}
      <TeacherFilterBar
        search={{
          value: searchTerm,
          onChange: setSearchTerm,
          placeholder: "Tìm kiếm theo tên giáo trình, mô tả...",
        }}
      >
        <select
          className="th-select"
          value={selectedSubjectId}
          onChange={(e) => {
            const p = new URLSearchParams(searchParams);
            if (e.target.value) p.set("subjectId", e.target.value);
            else p.delete("subjectId");
            setSearchParams(p);
          }}
        >
          <option value="">Tất cả môn học</option>
          {subjects.map((s) => (
            <option key={s.subjectId} value={s.subjectId}>{s.subjectName}</option>
          ))}
        </select>

        <select
          className="th-select"
          value={selectedStatus}
          onChange={(e) => {
            const p = new URLSearchParams(searchParams);
            if (e.target.value) p.set("status", e.target.value);
            else p.delete("status");
            setSearchParams(p);
          }}
        >
          <option value="">Tất cả trạng thái</option>
          <option value="Draft">Bản nháp (Draft)</option>
          <option value="Published">Đã xuất bản (Published)</option>
          <option value="Archived">Lưu trữ (Archived)</option>
        </select>
      </TeacherFilterBar>

      {isLoading && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))", gap: "20px", marginTop: "20px" }}>
          <TeacherSkeleton height={200} />
          <TeacherSkeleton height={200} />
          <TeacherSkeleton height={200} />
        </div>
      )}

      {isError && (
        <TeacherSafeErrorPanel
          error={error}
          title="Không thể tải danh sách giáo trình"
          onRetry={() => refetch()}
        />
      )}

      {!isLoading && !isError && filteredCurriculums.length === 0 && (
        <div className="th-surface" style={{ padding: "48px", textAlign: "center", borderRadius: "12px", marginTop: "20px" }}>
          <div style={{ width: "56px", height: "56px", borderRadius: "50%", background: "var(--th-primary-subtle)", color: "var(--th-primary)", display: "flex", alignItems: "center", justifyContent: "center", margin: "0 auto 16px" }}>
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M4 19.5A2.5 2.5 0 016.5 17H20" />
              <path d="M6.5 2H20v20H6.5A2.5 2.5 0 014 19.5v-15A2.5 2.5 0 016.5 2z" />
            </svg>
          </div>
          <h3 style={{ fontSize: "1.15rem", fontWeight: 600, color: "var(--th-text-primary)", marginBottom: "8px" }}>
            Chưa có giáo trình nào
          </h3>
          <p style={{ color: "var(--th-text-secondary)", maxWidth: "400px", margin: "0 auto 20px" }}>
            Bắt đầu thiết kế khung chương trình đào tạo và phân bổ các chủ đề tri thức cho môn học.
          </p>
          {canCreate && (
            <Link to="/giao-vien/giao-trinh/tao-moi" className="th-primary-button" style={{ textDecoration: "none", display: "inline-flex" }}>
              Soạn Giáo Trình Mới Ngay
            </Link>
          )}
        </div>
      )}

      {!isLoading && !isError && filteredCurriculums.length > 0 && (
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(340px, 1fr))", gap: "20px", marginTop: "20px" }}>
          {filteredCurriculums.map((curr) => {
            const subject = subjects.find((s) => s.subjectId === curr.subjectId);
            return (
              <div
                key={curr.curriculumId}
                className="th-surface"
                style={{
                  borderRadius: "12px",
                  padding: "20px",
                  display: "flex",
                  flexDirection: "column",
                  justifyContent: "space-between",
                  border: "1px solid var(--th-border-color)",
                }}
              >
                <div>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "10px" }}>
                    <span className="th-badge th-badge-neutral" style={{ fontSize: "0.75rem" }}>
                      {subject?.subjectName || "Môn học"}
                    </span>
                    <TeacherStatusBadge
                      status={
                        curr.reviewStatus === "Published"
                          ? "success"
                          : curr.reviewStatus === "Draft"
                          ? "warning"
                          : "neutral"
                      }
                      label={
                        curr.reviewStatus === "Published"
                          ? "Đã Xuất Bản"
                          : curr.reviewStatus === "Draft"
                          ? "Bản Nháp"
                          : "Lưu Trữ"
                      }
                    />
                  </div>

                  <h3 style={{ fontSize: "1.1rem", fontWeight: 700, color: "var(--th-text-primary)", margin: "0 0 8px 0" }}>
                    {curr.title}
                  </h3>

                  <p
                    style={{
                      fontSize: "0.85rem",
                      color: "var(--th-text-secondary)",
                      margin: "0 0 16px 0",
                      display: "-webkit-box",
                      WebkitLineClamp: 2,
                      WebkitBoxOrient: "vertical",
                      overflow: "hidden",
                    }}
                  >
                    {curr.description || "Chưa có mô tả chi tiết cho giáo trình này."}
                  </p>
                </div>

                <div>
                  <div style={{ display: "flex", gap: "16px", padding: "12px 0", borderTop: "1px solid var(--th-border-color)", borderBottom: "1px solid var(--th-border-color)", marginBottom: "14px", fontSize: "0.8rem", color: "var(--th-text-muted)" }}>
                    <div>
                      <strong style={{ color: "var(--th-text-primary)", display: "block" }}>{curr.nodeIds?.length ?? 0}</strong>
                      Chủ đề / Nodes
                    </div>
                    <div>
                      <strong style={{ color: "var(--th-text-primary)", display: "block" }}>{curr.classIds?.length ?? 0}</strong>
                      Lớp áp dụng
                    </div>
                  </div>

                  <div style={{ display: "flex", gap: "8px", justifyContent: "flex-end" }}>
                    {canPublish && curr.reviewStatus === "Draft" && (
                      <button
                        className="th-button-secondary"
                        onClick={() => publishMutation.mutate(curr)}
                        disabled={publishMutation.isPending}
                        style={{ fontSize: "0.8rem", padding: "6px 12px" }}
                      >
                        Xuất bản
                      </button>
                    )}

                    <Link
                      to={`/giao-vien/giao-trinh/${curr.curriculumId}`}
                      className="th-primary-button"
                      style={{ fontSize: "0.8rem", padding: "6px 14px", textDecoration: "none" }}
                    >
                      Chỉnh sửa & Cấu trúc
                    </Link>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
