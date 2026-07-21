import React, { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { isAxiosError } from "axios";
import { organizationApi } from "../api/organizationApi";
import type { ClassListParams, ClassStatus } from "../types/organization";
import type { ProblemDetails } from "../types/auth";
import { useAuthStore } from "../stores/authStore";

export const ClassListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const { user } = useAuthStore();
  const isCenterManager = user?.role === "CenterManager";

  const [page, setPage] = useState<number>(1);
  const pageSize = 10;
  const [status, setStatus] = useState<ClassStatus | "">("");

  // Input state for status form
  const [statusInput, setStatusInput] = useState<ClassStatus | "">("");

  // Create form state
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [className, setClassName] = useState("");
  const [academicYear, setAcademicYear] = useState("");
  const [subjectId, setSubjectId] = useState("");
  const [teacherId, setTeacherId] = useState("");
  const [createError, setCreateError] = useState<string | null>(null);
  const [createSuccess, setCreateSuccess] = useState<string | null>(null);

  const queryParams: ClassListParams = {
    page,
    pageSize,
    status: status !== "" ? status : undefined,
  };

  const { data, isLoading, isFetching, isError } = useQuery({
    queryKey: ["classes", queryParams.page, queryParams.pageSize, queryParams.status],
    queryFn: () => organizationApi.listClasses(queryParams),
  });

  const shouldFetchOptions = isCenterManager && isCreateModalOpen;

  const {
    data: subjectsData,
    isLoading: isLoadingSubjects,
    isFetching: isFetchingSubjects,
    isError: isErrorSubjects,
  } = useQuery({
    queryKey: ["subjects", "active"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: shouldFetchOptions,
  });

  const {
    data: teachersData,
    isLoading: isLoadingTeachers,
    isFetching: isFetchingTeachers,
    isError: isErrorTeachers,
  } = useQuery({
    queryKey: ["teachers", "active"],
    queryFn: () => organizationApi.listTeachers({ page: 1, pageSize: 100, status: "Active" }),
    enabled: shouldFetchOptions,
  });

  const createClassMutation = useMutation({
    mutationFn: organizationApi.createClass,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["classes"] });
      setCreateSuccess("Tạo lớp học thành công!");
      resetForm();
      setIsCreateModalOpen(false);
      setTimeout(() => setCreateSuccess(null), 3000);
    },
    onError: (error: unknown) => {
      const errorCode = isAxiosError<ProblemDetails>(error)
        ? error.response?.data?.errorCode
        : undefined;

      if (errorCode === "DUPLICATE_RESOURCE") {
        setCreateError("Tên lớp và năm học này đã tồn tại trong trung tâm.");
      } else if (errorCode === "VALIDATION_FAILED") {
        setCreateError("Thông tin lớp học không hợp lệ.");
      } else if (errorCode === "RESOURCE_NOT_FOUND") {
        setCreateError("Giáo viên hoặc môn học đã chọn không tồn tại hoặc không còn khả dụng.");
      } else {
        setCreateError("Không thể tạo lớp học. Vui lòng thử lại.");
      }
    },
  });

  const resetForm = () => {
    setClassName("");
    setAcademicYear("");
    setSubjectId("");
    setTeacherId("");
    setCreateError(null);
  };

  const handleCancelCreate = () => {
    resetForm();
    setIsCreateModalOpen(false);
  };

  const normalizedClassName = className.trim();
  const normalizedAcademicYear = academicYear.trim();
  const normalizedSubjectId = subjectId.trim();
  const normalizedTeacherId = teacherId.trim();

  const isFormValid =
    isCenterManager &&
    isCreateModalOpen &&
    normalizedClassName.length > 0 &&
    normalizedClassName.length <= 150 &&
    normalizedAcademicYear.length > 0 &&
    normalizedAcademicYear.length <= 20 &&
    normalizedSubjectId.length > 0 &&
    normalizedTeacherId.length > 0 &&
    !isLoadingSubjects &&
    !isFetchingSubjects &&
    !isErrorSubjects &&
    !isLoadingTeachers &&
    !isFetchingTeachers &&
    !isErrorTeachers &&
    (subjectsData?.data?.length ?? 0) > 0 &&
    (teachersData?.data?.length ?? 0) > 0 &&
    !createClassMutation.isPending;

  const isSubmitDisabled = !isFormValid;

  const handleCreateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setCreateError(null);

    const normClassName = className.trim();
    const normAcademicYear = academicYear.trim();
    const normSubjectId = subjectId.trim();
    const normTeacherId = teacherId.trim();

    const isValid =
      isCenterManager &&
      isCreateModalOpen &&
      normClassName.length > 0 &&
      normClassName.length <= 150 &&
      normAcademicYear.length > 0 &&
      normAcademicYear.length <= 20 &&
      normSubjectId.length > 0 &&
      normTeacherId.length > 0 &&
      !isLoadingSubjects &&
      !isFetchingSubjects &&
      !isErrorSubjects &&
      !isLoadingTeachers &&
      !isFetchingTeachers &&
      !isErrorTeachers &&
      (subjectsData?.data?.length ?? 0) > 0 &&
      (teachersData?.data?.length ?? 0) > 0 &&
      !createClassMutation.isPending;

    if (!isValid) {
      if (
        isLoadingSubjects ||
        isFetchingSubjects ||
        isErrorSubjects ||
        isLoadingTeachers ||
        isFetchingTeachers ||
        isErrorTeachers ||
        (subjectsData?.data?.length ?? 0) === 0 ||
        (teachersData?.data?.length ?? 0) === 0
      ) {
        setCreateError("Danh sách giáo viên hoặc môn học chưa sẵn sàng. Vui lòng thử lại.");
      } else {
        setCreateError("Thông tin lớp học không hợp lệ.");
      }
      return;
    }

    createClassMutation.mutate({
      className: normClassName,
      academicYear: normAcademicYear,
      subjectId: normSubjectId,
      teacherId: normTeacherId,
    });
  };

  const handleFilter = (e: React.FormEvent) => {
    e.preventDefault();
    setStatus(statusInput);
    setPage(1);
  };

  const statusLabels: Record<string, string> = {
    Active: "Hoạt động",
    Archived: "Đã lưu trữ",
  };

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold leading-7 text-gray-900 sm:truncate sm:text-3xl sm:tracking-tight">
              Danh sách Lớp học
            </h1>
          </div>
          <div className="flex flex-wrap gap-2">
            <Link
              to="/"
              className="inline-flex items-center rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
            >
              Về trang chủ
            </Link>
            {isCenterManager && (
              <button
                type="button"
                onClick={() => setIsCreateModalOpen(true)}
                className="inline-flex items-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
              >
                Thêm lớp học
              </button>
            )}
          </div>
        </div>

        {createSuccess && (
          <div className="mb-6 rounded-md bg-green-50 p-4" role="status">
            <div className="flex">
              <div className="ml-3">
                <p className="text-sm font-medium text-green-800">{createSuccess}</p>
              </div>
            </div>
          </div>
        )}

        <div className="mb-8 overflow-hidden rounded-lg bg-white shadow">
          <div className="p-6">
            <form onSubmit={handleFilter} className="flex flex-col gap-4 sm:flex-row sm:items-end">
              <div className="w-full sm:max-w-xs">
                <label htmlFor="status" className="block text-sm font-medium leading-6 text-gray-900">
                  Trạng thái
                </label>
                <div className="mt-2">
                  <select
                    id="status"
                    name="status"
                    value={statusInput}
                    onChange={(e) => setStatusInput(e.target.value as ClassStatus | "")}
                    disabled={isFetching}
                    className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <option value="">Tất cả</option>
                    <option value="Active">Hoạt động</option>
                    <option value="Archived">Đã lưu trữ</option>
                  </select>
                </div>
              </div>

              <div>
                <button
                  type="submit"
                  disabled={isFetching}
                  className="inline-flex w-full items-center justify-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:bg-indigo-400 sm:w-auto"
                >
                  Lọc
                </button>
              </div>
            </form>
          </div>
        </div>

        {isError && (
          <div className="mb-6 rounded-md bg-red-50 p-4" role="alert">
            <div className="flex">
              <div className="ml-3">
                <h3 className="text-sm font-medium text-red-800">Đã xảy ra lỗi</h3>
                <div className="mt-2 text-sm text-red-700">
                  <p>Không thể tải danh sách lớp học. Vui lòng thử lại sau.</p>
                </div>
              </div>
            </div>
          </div>
        )}

        <div className="overflow-hidden bg-white shadow sm:rounded-lg">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-300">
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
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {isLoading ? (
                  <tr>
                    <td colSpan={6} className="py-10 text-center text-sm text-gray-500">
                      Đang tải dữ liệu...
                    </td>
                  </tr>
                ) : data?.data.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-10 text-center text-sm text-gray-500">
                      Không tìm thấy lớp học nào.
                    </td>
                  </tr>
                ) : (
                  data?.data.map((cls) => (
                    <tr key={cls.classId}>
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
                        {cls.studentCount}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm">
                        <span className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset ${
                          cls.status === "Active" ? "bg-green-50 text-green-700 ring-green-600/20" :
                          "bg-yellow-50 text-yellow-800 ring-yellow-600/20"
                        }`}>
                          {statusLabels[cls.status] || cls.status}
                        </span>
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
                    Hiển thị trang <span className="font-medium">{data.meta.page}</span> / <span className="font-medium">{data.meta.totalPages}</span> (Tổng số {data.meta.totalItems} kết quả)
                  </p>
                </div>
                <div>
                  <nav className="isolate inline-flex -space-x-px rounded-md shadow-sm" aria-label="Phân trang">
                    <button
                      type="button"
                      onClick={() => setPage((p) => Math.max(1, p - 1))}
                      disabled={page === 1 || isFetching}
                      className="relative inline-flex items-center rounded-l-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 focus:z-20 focus:outline-offset-0 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <span className="sr-only">Trang trước</span>
                      <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                        <path fillRule="evenodd" d="M12.79 5.23a.75.75 0 01-.02 1.06L8.832 10l3.938 3.71a.75.75 0 11-1.04 1.08l-4.5-4.25a.75.75 0 010-1.08l4.5-4.25a.75.75 0 011.06.02z" clipRule="evenodd" />
                      </svg>
                    </button>
                    <button
                      type="button"
                      onClick={() => setPage((p) => Math.min(data.meta.totalPages, p + 1))}
                      disabled={page === data.meta.totalPages || isFetching}
                      className="relative inline-flex items-center rounded-r-md px-2 py-2 text-gray-400 ring-1 ring-inset ring-gray-300 hover:bg-gray-50 focus:z-20 focus:outline-offset-0 disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <span className="sr-only">Trang sau</span>
                      <svg className="h-5 w-5" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                        <path fillRule="evenodd" d="M7.21 14.77a.75.75 0 01.02-1.06L11.168 10 7.23 6.29a.75.75 0 111.04-1.08l4.5 4.25a.75.75 0 010 1.08l-4.5 4.25a.75.75 0 01-1.06-.02z" clipRule="evenodd" />
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

      {isCreateModalOpen && isCenterManager && (
        <div className="fixed inset-0 z-10 overflow-y-auto">
          <div className="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
            <div className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" onClick={createClassMutation.isPending ? undefined : handleCancelCreate}></div>

            <div className="relative transform overflow-hidden rounded-lg bg-white px-4 pb-4 pt-5 text-left shadow-xl transition-all sm:my-8 sm:w-full sm:max-w-lg sm:p-6">
              <div>
                <h3 className="text-lg font-semibold leading-6 text-gray-900 mb-5">Thêm lớp học</h3>

                {createError && (
                  <div className="mb-4 rounded-md bg-red-50 p-4" role="alert">
                    <p className="text-sm font-medium text-red-800">{createError}</p>
                  </div>
                )}

                <form onSubmit={handleCreateSubmit} className="space-y-4">
                  <div>
                    <label htmlFor="className" className="block text-sm font-medium leading-6 text-gray-900">
                      Tên lớp <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <input
                        type="text"
                        id="className"
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
                    <label htmlFor="academicYear" className="block text-sm font-medium leading-6 text-gray-900">
                      Năm học <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <input
                        type="text"
                        id="academicYear"
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
                    <label htmlFor="subjectId" className="block text-sm font-medium leading-6 text-gray-900">
                      Môn học <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <select
                        id="subjectId"
                        required
                        value={subjectId}
                        onChange={(e) => setSubjectId(e.target.value)}
                        disabled={createClassMutation.isPending || isLoadingSubjects || isFetchingSubjects || isErrorSubjects}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50 disabled:bg-gray-100"
                      >
                        <option value="">Chọn môn học</option>
                        {(isLoadingSubjects || isFetchingSubjects) && <option value="" disabled>Đang tải môn học...</option>}
                        {isErrorSubjects && !isLoadingSubjects && !isFetchingSubjects && <option value="" disabled>Lỗi tải môn học</option>}
                        {(!isLoadingSubjects && !isFetchingSubjects && !isErrorSubjects && (subjectsData?.data?.length ?? 0) === 0) && <option value="" disabled>Không có môn học hoạt động</option>}
                        {subjectsData?.data?.map((sub) => (
                          <option key={sub.subjectId} value={sub.subjectId}>
                            {sub.subjectCode} - {sub.subjectName}
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>

                  <div>
                    <label htmlFor="teacherId" className="block text-sm font-medium leading-6 text-gray-900">
                      Giáo viên <span className="text-red-500">*</span>
                    </label>
                    <div className="mt-2">
                      <select
                        id="teacherId"
                        required
                        value={teacherId}
                        onChange={(e) => setTeacherId(e.target.value)}
                        disabled={createClassMutation.isPending || isLoadingTeachers || isFetchingTeachers || isErrorTeachers}
                        className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50 disabled:bg-gray-100"
                      >
                        <option value="">Chọn giáo viên</option>
                        {(isLoadingTeachers || isFetchingTeachers) && <option value="" disabled>Đang tải giáo viên...</option>}
                        {isErrorTeachers && !isLoadingTeachers && !isFetchingTeachers && <option value="" disabled>Lỗi tải giáo viên</option>}
                        {(!isLoadingTeachers && !isFetchingTeachers && !isErrorTeachers && (teachersData?.data?.length ?? 0) === 0) && <option value="" disabled>Không có giáo viên hoạt động</option>}
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
                      disabled={isSubmitDisabled}
                      className="inline-flex w-full justify-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 sm:col-start-2 disabled:bg-indigo-400 disabled:cursor-not-allowed"
                    >
                      {createClassMutation.isPending ? "Đang tạo..." : "Lưu lớp học"}
                    </button>
                    <button
                      type="button"
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
    </div>
  );
};
