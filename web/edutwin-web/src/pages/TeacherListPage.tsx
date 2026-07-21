import React, { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { isAxiosError } from "axios";
import { organizationApi } from "../api/organizationApi";
import type { ProblemDetails, UserStatus } from "../types/auth";
import type { TeacherListParams, CreateTeacherRequest } from "../types/organization";

class CreateTeacherMutationError extends Error {
  constructor(public readonly errorCode?: string) {
    super("CREATE_TEACHER_FAILED");
  }
}

export const TeacherListPage: React.FC = () => {
  const queryClient = useQueryClient();
  const [page, setPage] = useState<number>(1);
  const pageSize = 20;
  const [search, setSearch] = useState<string>("");
  const [status, setStatus] = useState<UserStatus | "">("");

  // Input states for the search form (before submitting)
  const [searchInput, setSearchInput] = useState<string>("");
  const [statusInput, setStatusInput] = useState<UserStatus | "">("");

  // Create form states
  const [isCreating, setIsCreating] = useState(false);
  const [username, setUsername] = useState("");
  const [temporaryPassword, setTemporaryPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [department, setDepartment] = useState("");

  const [successMessage, setSuccessMessage] = useState("");
  const [errorMessage, setErrorMessage] = useState("");

  const queryParams: TeacherListParams = {
    page,
    pageSize,
    search: search.trim() !== "" ? search.trim() : undefined,
    status: status !== "" ? status : undefined,
  };

  const { data, isLoading, isFetching, isError: isListError } = useQuery({
    queryKey: ["teachers", queryParams.page, queryParams.pageSize, queryParams.search, queryParams.status],
    queryFn: () => organizationApi.listTeachers(queryParams),
  });

  const createMutation = useMutation({
    mutationFn: async (request: CreateTeacherRequest) => {
      try {
        return await organizationApi.createTeacher(request);
      } catch (error) {
        const errorCode = isAxiosError<ProblemDetails>(error)
          ? error.response?.data?.errorCode
          : undefined;

        throw new CreateTeacherMutationError(errorCode);
      } finally {
        request.temporaryPassword = "";
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["teachers"] });
      resetCreateForm();
      setIsCreating(false);
      setSuccessMessage("Đã tạo giáo viên thành công.");
      setTimeout(() => setSuccessMessage(""), 5000);
    },
    onError: (error) => {
      if (error instanceof CreateTeacherMutationError && error.errorCode) {
        switch (error.errorCode) {
          case "DUPLICATE_RESOURCE":
            setErrorMessage("Tên đăng nhập đã tồn tại trong trung tâm.");
            break;
          case "VALIDATION_FAILED":
            setErrorMessage("Thông tin giáo viên không hợp lệ.");
            break;
          case "RESOURCE_NOT_FOUND":
            setErrorMessage("Trung tâm hiện không khả dụng.");
            break;
          default:
            setErrorMessage("Không thể tạo giáo viên. Vui lòng thử lại.");
        }
      } else {
        setErrorMessage("Không thể tạo giáo viên. Vui lòng thử lại.");
      }
    }
  });

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput);
    setStatus(statusInput);
    setPage(1);
  };

  const resetCreateForm = () => {
    setUsername("");
    setTemporaryPassword("");
    setDisplayName("");
    setDepartment("");
    setErrorMessage("");
  };

  const handleCancelCreate = () => {
    resetCreateForm();
    setIsCreating(false);
  };

  const handleCreateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage("");
    setSuccessMessage("");

    const normalizedUsername = username.trim();
    const normalizedDisplayName = displayName.trim();
    const normalizedDepartment = department.trim();

    if (
      normalizedUsername === "" || normalizedUsername.length > 100 ||
      temporaryPassword.length < 12 || temporaryPassword.length > 200 ||
      normalizedDisplayName === "" || normalizedDisplayName.length > 200 ||
      (normalizedDepartment && normalizedDepartment.length > 150)
    ) {
      setErrorMessage("Thông tin giáo viên không hợp lệ. Vui lòng kiểm tra lại các trường bắt buộc.");
      return;
    }

    createMutation.mutate({
      username: normalizedUsername,
      temporaryPassword,
      displayName: normalizedDisplayName,
      department: normalizedDepartment
    });
  };

  const statusLabels: Record<string, string> = {
    Active: "Hoạt động",
    Locked: "Bị khóa",
    Disabled: "Vô hiệu hóa",
  };

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold leading-7 text-gray-900 sm:truncate sm:text-3xl sm:tracking-tight">
              Quản lý Giáo viên
            </h1>
          </div>
          <div className="flex gap-4">
            <button
              type="button"
              onClick={() => setIsCreating(true)}
              className="inline-flex items-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
            >
              Thêm giáo viên
            </button>
            <Link
              to="/"
              className="inline-flex items-center rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50"
            >
              Về trang chủ
            </Link>
          </div>
        </div>

        {successMessage && (
          <div className="mb-6 rounded-md bg-green-50 p-4" role="status">
            <p className="text-sm font-medium text-green-800">{successMessage}</p>
          </div>
        )}

        {isCreating && (
          <div className="mb-8 rounded-lg bg-white shadow p-6">
            <h2 className="text-lg font-medium leading-6 text-gray-900 mb-4">Thêm giáo viên mới</h2>

            {errorMessage && (
              <div className="mb-4 rounded-md bg-red-50 p-4" role="alert">
                <p className="text-sm font-medium text-red-800">{errorMessage}</p>
              </div>
            )}

            <form onSubmit={handleCreateSubmit} className="space-y-4">
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label htmlFor="username" className="block text-sm font-medium leading-6 text-gray-900">Tên đăng nhập <span className="text-red-500">*</span></label>
                  <input
                    type="text"
                    id="username"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    required
                    maxLength={100}
                    disabled={createMutation.isPending}
                    className="mt-2 block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                  />
                </div>

                <div>
                  <label htmlFor="temporaryPassword" className="block text-sm font-medium leading-6 text-gray-900">Mật khẩu tạm thời <span className="text-red-500">*</span></label>
                  <input
                    type="password"
                    id="temporaryPassword"
                    autoComplete="new-password"
                    value={temporaryPassword}
                    onChange={(e) => setTemporaryPassword(e.target.value)}
                    required
                    minLength={12}
                    maxLength={200}
                    disabled={createMutation.isPending}
                    className="mt-2 block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                  />
                </div>

                <div>
                  <label htmlFor="displayName" className="block text-sm font-medium leading-6 text-gray-900">Họ tên <span className="text-red-500">*</span></label>
                  <input
                    type="text"
                    id="displayName"
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    required
                    maxLength={200}
                    disabled={createMutation.isPending}
                    className="mt-2 block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                  />
                </div>

                <div>
                  <label htmlFor="department" className="block text-sm font-medium leading-6 text-gray-900">Bộ phận</label>
                  <input
                    type="text"
                    id="department"
                    value={department}
                    onChange={(e) => setDepartment(e.target.value)}
                    maxLength={150}
                    disabled={createMutation.isPending}
                    className="mt-2 block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6 disabled:opacity-50"
                  />
                </div>
              </div>

              <div className="flex justify-end gap-3 pt-4 border-t border-gray-100">
                <button
                  type="button"
                  onClick={handleCancelCreate}
                  disabled={createMutation.isPending}
                  className="rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50 disabled:opacity-50"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:opacity-50"
                >
                  {createMutation.isPending ? "Đang xử lý..." : "Lưu"}
                </button>
              </div>
            </form>
          </div>
        )}

        <div className="mb-8 overflow-hidden rounded-lg bg-white shadow">
          <div className="p-6">
            <form onSubmit={handleSearch} className="flex flex-col gap-4 sm:flex-row sm:items-end">
              <div className="w-full sm:max-w-xs">
                <label htmlFor="search" className="block text-sm font-medium leading-6 text-gray-900">
                  Tìm kiếm
                </label>
                <div className="mt-2">
                  <input
                    type="text"
                    name="search"
                    id="search"
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    placeholder="Tên đăng nhập hoặc họ tên"
                    className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6"
                  />
                </div>
              </div>

              <div className="w-full sm:max-w-xs">
                <label htmlFor="status" className="block text-sm font-medium leading-6 text-gray-900">
                  Trạng thái
                </label>
                <div className="mt-2">
                  <select
                    id="status"
                    name="status"
                    value={statusInput}
                    onChange={(e) => setStatusInput(e.target.value as UserStatus | "")}
                    className="block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6"
                  >
                    <option value="">Tất cả</option>
                    <option value="Active">Hoạt động</option>
                    <option value="Locked">Bị khóa</option>
                    <option value="Disabled">Vô hiệu hóa</option>
                  </select>
                </div>
              </div>

              <div>
                <button
                  type="submit"
                  disabled={isFetching}
                  className="inline-flex w-full items-center justify-center rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 disabled:bg-indigo-400 sm:w-auto"
                >
                  Tìm kiếm
                </button>
              </div>
            </form>
          </div>
        </div>

        {isListError && (
          <div className="mb-6 rounded-md bg-red-50 p-4">
            <div className="flex">
              <div className="ml-3">
                <h3 className="text-sm font-medium text-red-800">Đã xảy ra lỗi</h3>
                <div className="mt-2 text-sm text-red-700">
                  <p>Không thể tải danh sách giáo viên. Vui lòng thử lại sau.</p>
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
                    Tên đăng nhập
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Họ tên
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Bộ phận
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Trạng thái
                  </th>
                  <th scope="col" className="px-3 py-3.5 text-left text-sm font-semibold text-gray-900">
                    Số lớp
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {isLoading ? (
                  <tr>
                    <td colSpan={5} className="py-10 text-center text-sm text-gray-500">
                      Đang tải dữ liệu...
                    </td>
                  </tr>
                ) : data?.data.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-10 text-center text-sm text-gray-500">
                      Không tìm thấy giáo viên nào.
                    </td>
                  </tr>
                ) : (
                  data?.data.map((teacher) => (
                    <tr key={teacher.teacherId}>
                      <td className="whitespace-nowrap py-4 pl-4 pr-3 text-sm font-medium text-gray-900 sm:pl-6">
                        {teacher.username}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        {teacher.displayName}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        {teacher.department || "-"}
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm">
                        <span className={`inline-flex items-center rounded-md px-2 py-1 text-xs font-medium ring-1 ring-inset ${
                          teacher.status === "Active" ? "bg-green-50 text-green-700 ring-green-600/20" :
                          teacher.status === "Locked" ? "bg-yellow-50 text-yellow-800 ring-yellow-600/20" :
                          "bg-red-50 text-red-700 ring-red-600/10"
                        }`}>
                          {statusLabels[teacher.status] || teacher.status}
                        </span>
                      </td>
                      <td className="whitespace-nowrap px-3 py-4 text-sm text-gray-500">
                        {teacher.classCount}
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
    </div>
  );
};
