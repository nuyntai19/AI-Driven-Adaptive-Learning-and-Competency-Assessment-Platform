import { isAxiosError } from "axios";
import type { ProblemDetails } from "../types/auth";
import type { AccountType } from "../types/auth";
import type {
  AuthorizationAuditQueryParams,
  AuthorizationRoleDto,
  AuthorizationRoleQueryParams,
  AuthorizationRoleStatus,
  AuthorizationUserQueryParams,
  AssignedAuthorizationRoleDto,
  PermissionDto,
} from "../types/authorization";

export interface ParsedSafeError {
  message: string;
  traceId?: string;
  code?: string;
}

export const parseSafeError = (error: unknown): ParsedSafeError => {
  if (!isAxiosError<ProblemDetails>(error)) {
    return { message: "Không thể hoàn tất thao tác. Vui lòng kiểm tra kết nối mạng và thử lại." };
  }

  const data = error.response?.data;
  const code = data?.errorCode;
  const traceId = data?.traceId;

  const messages: Record<string, string> = {
    AUTH_PRIVILEGE_ESCALATION:
      "Không thể cấp quyền cao hơn quyền hiện có của bạn hoặc gán quyền quản trị nền tảng.",
    ROLE_ACCOUNT_TYPE_MISMATCH:
      "Vai trò hoặc quyền hạn không tương thích với loại tài khoản của đối tượng.",
    LAST_TENANT_ADMIN:
      "Không thể thực hiện thao tác vì phải giữ lại ít nhất một quản lý trung tâm có đủ quyền quản trị hợp lệ.",
    CONCURRENCY_CONFLICT:
      "Dữ liệu đã được cập nhật bởi một phiên làm việc khác (OCC Concurrency Conflict). Vui lòng làm mới trang để nhận phiên bản mới nhất.",
    INVALID_STATE_TRANSITION:
      "Không thể thay đổi trạng thái vai trò hệ thống hoặc chuyển trạng thái không hợp lệ.",
    VALIDATION_FAILED:
      "Dữ liệu nhập vào chưa hợp lệ. Vui lòng kiểm tra lại các trường bắt buộc và định dạng.",
    RESOURCE_NOT_FOUND:
      "Không tìm thấy tài nguyên yêu cầu trong trung tâm hiện tại (Fail-closed).",
    DUPLICATE_RESOURCE:
      "Mã vai trò đã tồn tại trong trung tâm. Vui lòng chọn một mã vai trò khác.",
    AUTH_PERMISSION_REQUIRED:
      "Bạn không có đủ quyền hạn để thực hiện thao tác này.",
    CANNOT_DELEGATE_UNOWNED_PERMISSIONS:
      "Bạn chỉ có thể ủy quyền các quyền hạn mà chính bạn hiện đang sở hữu.",
    SYSTEM_ROLE_PROTECTED:
      "Vai trò hệ thống được bảo vệ và không được phép chỉnh sửa hoặc xóa trực tiếp.",
  };

  const safeMessage =
    (code && messages[code]) ||
    "Không thể hoàn tất thao tác do lỗi hệ thống. Vui lòng thử lại hoặc liên hệ quản trị viên.";

  return { message: safeMessage, traceId, code };
};

export const buildRoleQueryParams = (
  page: number,
  pageSize: number,
  search?: string,
  accountType?: AccountType | "all",
  status?: AuthorizationRoleStatus | "all",
): AuthorizationRoleQueryParams => {
  const query: AuthorizationRoleQueryParams = {
    page,
    pageSize,
  };
  const trimmedSearch = search?.trim();
  if (trimmedSearch) {
    query.search = trimmedSearch;
  }
  if (accountType && accountType !== "all") {
    query.accountType = accountType;
  }
  if (status && status !== "all") {
    query.status = status;
  }
  return query;
};

export const buildUserQueryParams = (
  page: number,
  pageSize: number,
  search?: string,
  accountType?: AccountType | "all",
  status?: string | "all",
): AuthorizationUserQueryParams => {
  const query: AuthorizationUserQueryParams = {
    page,
    pageSize,
  };
  const trimmedSearch = search?.trim();
  if (trimmedSearch) {
    query.search = trimmedSearch;
  }
  if (accountType && accountType !== "all") {
    query.accountType = accountType;
  }
  if (status && status !== "all") {
    query.status = status;
  }
  return query;
};

export interface AuditFilters {
  from?: string;
  to?: string;
  actorUserId?: string;
  targetUserId?: string;
  targetId?: string;
  permissionCode?: string;
  actionType?: string;
}

export const buildAuditQueryParams = (
  page: number,
  pageSize: number,
  filters: AuditFilters,
): AuthorizationAuditQueryParams => {
  const query: AuthorizationAuditQueryParams = {
    page,
    pageSize,
  };

  if (filters.from) {
    query.from = new Date(filters.from).toISOString();
  }
  if (filters.to) {
    const toDate = new Date(filters.to);
    if (!filters.to.includes("T")) {
      toDate.setHours(23, 59, 59, 999);
    }
    query.to = toDate.toISOString();
  }
  if (filters.actorUserId?.trim()) {
    query.actorUserId = filters.actorUserId.trim();
  }
  if (filters.targetUserId?.trim()) {
    query.targetUserId = filters.targetUserId.trim();
  }
  if (filters.targetId?.trim()) {
    query.targetId = filters.targetId.trim();
  }
  if (filters.permissionCode?.trim()) {
    query.permissionCode = filters.permissionCode.trim();
  }
  if (filters.actionType?.trim()) {
    query.actionType = filters.actionType.trim();
  }

  return query;
};

export const isSelfUser = (
  currentUserId: string | undefined,
  targetUserId: string | undefined,
): boolean => {
  if (!currentUserId || !targetUserId) return false;
  return currentUserId.toLowerCase() === targetUserId.toLowerCase();
};

export const filterCompatibleRoles = (
  roles: AuthorizationRoleDto[],
  accountType: AccountType | undefined,
): AuthorizationRoleDto[] => {
  if (!accountType) return [];
  return roles.filter(
    (role) => role.accountType === accountType && role.status === "Active",
  );
};

export const validateRoleCreation = (
  roleCode: string,
  roleName: string,
  accountType: string,
  description?: string,
): { valid: boolean; error?: string } => {
  const trimmedCode = roleCode.trim();
  const trimmedName = roleName.trim();

  if (!trimmedCode) {
    return { valid: false, error: "Mã vai trò không được để trống." };
  }
  if (trimmedCode.length > 64 || !/^[A-Z][A-Z0-9_]*$/.test(trimmedCode)) {
    return {
      valid: false,
      error: "Mã vai trò phải từ 1-64 ký tự, bắt đầu bằng chữ in hoa và chỉ chứa chữ in hoa, chữ số hoặc dấu gạch dưới.",
    };
  }
  if (!trimmedName) {
    return { valid: false, error: "Tên vai trò không được để trống." };
  }
  if (trimmedName.length > 150) {
    return { valid: false, error: "Tên vai trò không được vượt quá 150 ký tự." };
  }
  if (description && description.length > 500) {
    return { valid: false, error: "Mô tả vai trò không được vượt quá 500 ký tự." };
  }
  if (!["Teacher", "Student", "CenterManager"].includes(accountType)) {
    return { valid: false, error: "Loại tài khoản không hợp lệ." };
  }
  return { valid: true };
};

export interface EffectivePermissionItem {
  code: string;
  description: string;
  sourceRoles: string[];
}

export function hydrateKnownRolesFromUserAuth(
  prevKnownRoles: Map<string, AuthorizationRoleDto>,
  assignedRoles: AssignedAuthorizationRoleDto[],
): Map<string, AuthorizationRoleDto> {
  const next = new Map(prevKnownRoles);
  for (const r of assignedRoles) {
    const existing = next.get(r.roleId);
    if (!existing) {
      next.set(r.roleId, {
        roleId: r.roleId,
        roleCode: r.roleCode,
        roleName: r.roleName,
        accountType: r.accountType,
        description: null,
        isSystemRole: false,
        status: "Active",
        permissionCodes: r.permissionCodes ?? [],
        activeUserCount: 1,
        rowVersion: "1",
      });
    } else if (r.permissionCodes && r.permissionCodes.length > 0 && existing.permissionCodes.length === 0) {
      next.set(r.roleId, {
        ...existing,
        permissionCodes: r.permissionCodes,
      });
    }
  }
  return next;
}

export const PERMISSION_DESCRIPTIONS_VI: Record<string, string> = {
  // Assignments (Bài tập & Giao bài)
  "assignments.assignments.read": "Cho phép xem danh sách và chi tiết bài tập trong phạm vi được phân công.",
  "assignments.assignments.create": "Cho phép tạo bài tập mới cho lớp học.",
  "assignments.assignments.update": "Cho phép chỉnh sửa thông tin, cấu hình và thời hạn bài tập.",
  "assignments.assignments.publish": "Cho phép phát hành và giao bài tập cho học sinh.",
  "assignments.assignments.close": "Cho phép đóng hoặc kết thúc đợt làm bài tập.",

  // Authorization (Vai trò & Phân quyền)
  "authorization.permissions.read": "Cho phép xem danh mục quyền hạn của toàn hệ thống.",
  "authorization.roles.read": "Cho phép xem danh sách vai trò và phân quyền tương ứng.",
  "authorization.roles.create": "Cho phép tạo vai trò tùy chỉnh mới.",
  "authorization.roles.update": "Cho phép chỉnh sửa thông tin vai trò.",
  "authorization.roles.archive": "Cho phép lưu trữ hoặc vô hiệu hóa vai trò.",
  "authorization.roles.manage_permissions": "Cho phép cấu hình và thay đổi quyền hạn cho vai trò.",
  "authorization.user_roles.read": "Cho phép xem danh sách phân công vai trò của người dùng.",
  "authorization.user_roles.assign": "Cho phép gán hoặc thu hồi vai trò của người dùng.",
  "authorization.audit.read": "Cho phép xem nhật ký kiểm toán bảo mật và phân quyền.",

  // Curriculum (Giáo trình & Ngân hàng câu hỏi)
  "curriculum.curriculums.read": "Cho phép xem danh sách và nội dung chi tiết giáo trình.",
  "curriculum.curriculums.create": "Cho phép tạo mới giáo trình học tập.",
  "curriculum.curriculums.update": "Cho phép chỉnh sửa cấu trúc và nội dung giáo trình.",
  "curriculum.curriculums.publish": "Cho phép xuất bản giáo trình để đưa vào giảng dạy.",
  "curriculum.questions.read": "Cho phép xem ngân hàng câu hỏi trắc nghiệm và bài tập.",
  "curriculum.questions.create": "Cho phép tạo câu hỏi mới vào ngân hàng câu hỏi.",
  "curriculum.questions.update": "Cho phép chỉnh sửa nội dung và đáp án câu hỏi trong phạm vi được cấp.",
  "curriculum.questions.publish": "Cho phép phê duyệt và xuất bản câu hỏi vào ngân hàng đề.",
  "curriculum.questions.delete": "Cho phép xóa câu hỏi khỏi ngân hàng câu hỏi.",

  // Dashboards (Bảng điều khiển)
  "dashboards.center.read": "Cho phép xem bảng điều khiển phân tích tổng hợp của trung tâm.",
  "dashboards.student.read_own": "Cho phép học sinh xem bảng điều khiển tiến độ học tập cá nhân.",
  "dashboards.teacher.read_scoped": "Cho phép giáo viên xem bảng điều khiển các lớp được phân công.",

  // Knowledge (Knowledge Graph & Cây tri thức)
  "knowledge.subjects.read": "Cho phép xem danh sách và thông tin môn học.",
  "knowledge.subjects.create": "Cho phép tạo môn học mới.",
  "knowledge.subjects.update": "Cho phép cập nhật thông tin môn học.",
  "knowledge.subjects.delete": "Cho phép xóa môn học.",
  "knowledge.nodes.read": "Cho phép xem sơ đồ cây khái niệm kiến thức.",
  "knowledge.nodes.create": "Cho phép tạo mới khái niệm/đơn vị kiến thức.",
  "knowledge.nodes.update": "Cho phép chỉnh sửa khái niệm/đơn vị kiến thức.",
  "knowledge.nodes.delete": "Cho phép xóa khái niệm/đơn vị kiến thức.",
  "knowledge.edges.read": "Cho phép xem mạng lưới liên kết quan hệ kiến thức.",
  "knowledge.edges.create": "Cho phép tạo liên kết quan hệ giữa các khái niệm kiến thức.",
  "knowledge.edges.update": "Cho phép cập nhật liên kết quan hệ kiến thức.",
  "knowledge.edges.delete": "Cho phép xóa liên kết quan hệ kiến thức.",

  // Learning & Attempts (Quá trình làm bài)
  "learning.attempts.read_own": "Cho phép học sinh xem lại lịch sử và kết quả bài làm của chính mình.",
  "learning.attempts.read_scoped": "Cho phép giáo viên xem kết quả làm bài của học sinh trong lớp phụ trách.",
  "learning.attempts.submit": "Cho phép nộp bài làm đánh giá hoặc bài tập.",

  // Organization (Cơ cấu tổ chức & Tài khoản)
  "organization.center.read": "Cho phép xem thông tin hồ sơ trung tâm.",
  "organization.center.update": "Cho phép cập nhật thông tin định danh và cấu hình trung tâm.",
  "organization.center.manage": "Cho phép quản trị toàn diện thông tin và thiết lập của trung tâm.",
  "organization.classes.read": "Cho phép xem danh sách và thông tin lớp học.",
  "organization.classes.create": "Cho phép tạo lớp học mới.",
  "organization.classes.update": "Cho phép chỉnh sửa thông tin lớp học.",
  "organization.classes.manage_members": "Cho phép quản lý phân công giáo viên và học sinh vào lớp học.",
  "organization.students.read": "Cho phép xem danh sách và hồ sơ học sinh.",
  "organization.students.create": "Cho phép tạo tài khoản và hồ sơ học sinh mới.",
  "organization.students.update": "Cho phép cập nhật thông tin học sinh.",
  "organization.students.delete": "Cho phép xóa tài khoản học sinh.",
  "organization.students.reset_password": "Cho phép đặt lại mật khẩu cho tài khoản học sinh.",
  "organization.teachers.read": "Cho phép xem danh sách và hồ sơ giáo viên.",
  "organization.teachers.create": "Cho phép tạo tài khoản và hồ sơ giáo viên mới.",
  "organization.teachers.update": "Cho phép cập nhật thông tin giáo viên.",
  "organization.teachers.delete": "Cho phép xóa tài khoản giáo viên.",
  "organization.teachers.reset_password": "Cho phép đặt lại mật khẩu cho tài khoản giáo viên.",

  // Recommendations (Gợi ý học tập)
  "recommendations.student.read_own": "Cho phép học sinh xem gợi ý lộ trình học tập cá nhân hóa.",
  "recommendations.student.update_own": "Cho phép học sinh tương tác và phản hồi gợi ý lộ trình học tập.",

  // Digital Twin & AI Reasoning (Hồ sơ số & Đánh giá năng lực)
  "twin.reasoning.review": "Cho phép giáo viên xem xét và thẩm định kết quả đánh giá năng lực từ AI.",
  "twin.reasoning.override": "Cho phép giáo viên can thiệp và điều chỉnh kết quả đánh giá của AI.",
  "twin.student.read_own": "Cho phép học sinh xem hồ sơ năng lực số (Digital Twin) của chính mình.",
  "twin.student.read_scoped": "Cho phép giáo viên/quản lý xem hồ sơ năng lực số của học sinh trong phạm vi phụ trách.",
  "twin.student.update_own": "Cho phép học sinh cập nhật mục tiêu học tập cá nhân.",
  "twin.student.update_scoped": "Cho phép giáo viên/quản lý cập nhật mục tiêu và đánh giá năng lực học sinh.",

  // Platform (Quản trị nền tảng)
  "platform.account.manage_own": "Cho phép quản trị viên tự quản lý tài khoản nền tảng của mình.",
  "platform.audit.read": "Cho phép xem nhật ký kiểm toán hoạt động cấp toàn nền tảng.",
  "platform.centers.read": "Cho phép xem danh sách các trung tâm trên nền tảng.",
  "platform.centers.manage": "Cho phép quản lý thông tin và cấu hình các trung tâm trên nền tảng.",
  "platform.managers.manage": "Cho phép quản lý tài khoản quản trị viên trung tâm.",
};

const ACTION_LABELS_VI: Record<string, string> = {
  read: "xem",
  read_own: "xem của cá nhân",
  read_scoped: "xem trong phạm vi phân công",
  create: "tạo mới",
  update: "chỉnh sửa",
  update_own: "cập nhật của cá nhân",
  update_scoped: "cập nhật trong phạm vi phân công",
  delete: "xóa",
  publish: "xuất bản / phát hành",
  close: "đóng / kết thúc",
  assign: "gán vai trò",
  archive: "lưu trữ",
  submit: "nộp bài",
  review: "thẩm định / xem xét",
  override: "ghi đè / can thiệp",
  manage: "quản trị",
  manage_members: "quản lý thành viên",
  manage_own: "tự quản lý",
  reset_password: "đặt lại mật khẩu",
  manage_permissions: "quản lý phân quyền",
};

const RESOURCE_LABELS_VI: Record<string, string> = {
  questions: "câu hỏi",
  curriculums: "giáo trình",
  assignments: "bài tập",
  teachers: "giáo viên",
  students: "học sinh",
  classes: "lớp học",
  subjects: "môn học",
  nodes: "khái niệm kiến thức",
  edges: "liên kết kiến thức",
  roles: "vai trò",
  permissions: "danh mục quyền hạn",
  user_roles: "phân công vai trò",
  audit: "nhật ký kiểm toán",
  center: "trung tâm",
  centers: "các trung tâm",
  managers: "quản trị viên",
  account: "tài khoản",
  attempts: "lượt làm bài",
  reasoning: "đánh giá năng lực AI",
};

export function getLocalizedPermissionDescription(
  permissionCode: string,
  fallbackDescription?: string,
): string {
  if (PERMISSION_DESCRIPTIONS_VI[permissionCode]) {
    return PERMISSION_DESCRIPTIONS_VI[permissionCode];
  }

  // If fallback is already a clean custom text without mechanical English tokens
  if (
    fallbackDescription &&
    !fallbackDescription.includes("trong phạm vi được cấp") &&
    !fallbackDescription.match(/\b(create|update|read|delete|publish|close|questions|assignments|curriculums|nodes|edges|subjects|teachers|students|classes)\b/i)
  ) {
    return fallbackDescription;
  }

  // Dynamic fallback: parse code parts (e.g. curriculum.questions.update)
  const parts = permissionCode.split(".");
  if (parts.length >= 3) {
    const action = ACTION_LABELS_VI[parts[2]] ?? parts[2];
    const resource = RESOURCE_LABELS_VI[parts[1]] ?? parts[1];
    return `Cho phép ${action} ${resource} trong phạm vi được cấp.`;
  }

  return fallbackDescription || "Cho phép thao tác trong phạm vi được cấp.";
}

export function computeEffectivePermissionsBreakdown(
  selectedRoleIds: string[],
  knownRoles: Map<string, AuthorizationRoleDto>,
  catalog: PermissionDto[],
): EffectivePermissionItem[] {
  const selectedRoles = selectedRoleIds
    .map((id) => knownRoles.get(id))
    .filter((r): r is AuthorizationRoleDto => r !== undefined);
  const permissionSources: Record<string, string[]> = {};

  for (const role of selectedRoles) {
    for (const code of role.permissionCodes) {
      (permissionSources[code] ??= []).push(role.roleName);
    }
  }

  const permissionDescriptions = new Map(
    catalog.map((p) => [
      p.permissionCode,
      getLocalizedPermissionDescription(p.permissionCode, p.description),
    ]),
  );

  return Object.entries(permissionSources)
    .map(([code, sourceRoles]) => ({
      code,
      description:
        permissionDescriptions.get(code) ??
        getLocalizedPermissionDescription(code, "Quyền vận hành"),
      sourceRoles,
    }))
    .sort((a, b) => a.code.localeCompare(b.code));
}

export function getMissingCanonicalRoleIds(
  selectedRoleIds: string[],
  knownRoles: Map<string, AuthorizationRoleDto>,
  fetchedRoleIds: Set<string>,
): string[] {
  return selectedRoleIds.filter((id) => {
    const r = knownRoles.get(id);
    return (!r || r.permissionCodes.length === 0) && !fetchedRoleIds.has(id);
  });
}
