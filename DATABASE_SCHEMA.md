# EduTwin — Database Schema

> Phiên bản: 2.4 (Post-R08 Scope Amendments)
> Trạng thái: ACTIVE — 39 bảng vật lý trong EF migration model hiện hành (Bảng thứ 40: attempt_attachments là bảng mục tiêu sau Gate 5)
> Database: MySQL 8.x / InnoDB / utf8mb4
> ORM: Entity Framework Core 10
> Chủ sở hữu: Data/Architecture owners; thay đổi cần nhóm phê duyệt

## 1. Mục tiêu thiết kế

Schema phục vụ đồng thời bảy mục tiêu:

1. Cách ly dữ liệu Multi-tenant theo Center.
2. Lưu bằng chứng học tập thay vì chỉ lưu điểm tổng.
3. Cập nhật Learning Digital Twin có lịch sử và khả năng giải thích.
4. Hỗ trợ AI thất bại mà luồng học vẫn hoàn thành bằng Rule-based fallback.
5. Cho phép Teacher Override và tái tính toán deterministic.
6. Cấp quyền động theo từng Center mà không phá tenant isolation.
7. Ghi lại provenance và mức tin cậy của evidence trước khi cập nhật Digital Twin.

Schema gồm sáu module logic:

1. System Users & Organization.
2. Knowledge Graph.
3. Curriculum, Question Bank & Assignments.
4. Digital Twin & Personalization.
5. Assessment & AI Reasoning.
6. Dynamic Authorization & Evidence Governance.

Hệ thống có 39 bảng vật lý trong EF migration model hiện hành, bao gồm 7 bảng ở Module 6 và bảng watermark recommendation generation (bảng thứ 39: recommendation_generation_states). Bảng lưu trữ minh chứng đính kèm (bảng thứ 40: attempt_attachments) là bảng mục tiêu được triển khai tại Gate 5. Toàn bộ mô hình tuân thủ kiểm tra live-MySQL và Global Query Filter nghiêm ngặt.

## 2. Quy ước vật lý

### 2.1. Naming

- Table và column: snake_case, số nhiều cho table.
- Primary key: tên thực thể số ít + _id.
- Foreign key: cùng tên với primary key được tham chiếu.
- Index: ix_{table}_{columns}.
- Unique index: ux_{table}_{columns}.
- Foreign key: fk_{child}_{parent}_{purpose}.
- Check constraint: ck_{table}_{rule}.

- MySQL physical identifier không được dài quá 64 ký tự.
- Tên canonical theo ix_/ux_/fk_/ck_/pk_ vẫn là mặc định.
- Khi canonical name vượt 64 ký tự, chỉ được dùng alias đã được ghi rõ trong specification hoặc Change Proposal được Codex phê duyệt.
- Alias phải deterministic, dễ hiểu và không thay đổi columns/semantics.
- Không dùng hash hoặc tên viết tắt mơ hồ.

Các mapping được duyệt (CP-P04-001):
1. Canonical: ux_knowledge_edges_center_id_source_node_id_target_node_id_relation_type
   Physical alias: ux_knowledge_edges_center_id_source_id_target_id_relation_type
2. Canonical: ix_questions_center_id_subject_id_primary_topic_node_id_status_difficulty
   Physical alias: ix_questions_center_id_subject_id_topic_id_status_difficulty
3. Canonical: ux_student_assignment_progress_center_id_assignment_id_student_id
   Physical alias: ux_student_assignment_progress_center_assignment_id_student_id

Các mapping được duyệt (CP-P05-001):
1. Canonical: ix_twin_update_history_center_id_student_id_subject_id_created_at
   Physical alias: ix_twin_update_history_center_student_subject_created_at
2. Canonical: ix_recommendations_center_id_student_id_subject_id_status_generated_at
   Physical alias: ix_recommendations_center_student_subject_status_generated_at

### 2.2. Kiểu dữ liệu chuẩn

| Khái niệm | MySQL type | Ghi chú |
|---|---|---|
| Guid | VARCHAR(36) | Canonical lower-case; EF property là Guid |
| Transaction ID | BIGINT UNSIGNED AUTO_INCREMENT | Attempt, Analysis, Job, History |
| Thời gian UTC | DATETIME(6) | Không lưu local time |
| Phần trăm/điểm | DECIMAL(5,2) | 0.00–100.00 hoặc 0.00–10.00 tùy cột |
| Tiền/giá trị chính xác | DECIMAL | Không dùng FLOAT cho business value |
| Boolean | TINYINT(1) | 0/1 |
| Enum | VARCHAR(32) | Có CHECK constraint và enum tương ứng trong Contracts |
| Nội dung dài | TEXT hoặc LONGTEXT | Question/reasoning/feedback |
| Payload linh hoạt | JSON | Chỉ dùng khi không cần relational join |

Database dùng:

- ENGINE=InnoDB.
- CHARACTER SET=utf8mb4.
- COLLATION=utf8mb4_0900_ai_ci, hoặc collation utf8mb4 tương thích được chốt khi tạo database.
- Strict SQL mode.

### 2.3. Bộ cột chuẩn

Mọi table có marker MTA — Mutable Tenant Aggregate — phải có:

| Column | Type | Null | Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator |
| created_at | DATETIME(6) | No | UTC |
| created_by | VARCHAR(36) | Yes | User tạo; null cho system seed |
| updated_at | DATETIME(6) | No | UTC |
| updated_by | VARCHAR(36) | Yes | User sửa gần nhất |
| is_deleted | TINYINT(1) | No | Default 0 |
| deleted_at | DATETIME(6) | Yes | UTC |
| deleted_by | VARCHAR(36) | Yes | User soft delete |
| row_version | BIGINT UNSIGNED | No | Default 1; concurrency token |

Mọi table có marker TA — Tenant Append-only — phải có:

| Column | Type | Null | Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator |
| created_at | DATETIME(6) | No | UTC |
| created_by | VARCHAR(36) | Yes | Actor hoặc null cho system |

Join table chỉ cần center_id và created_at khi được ghi nhận như một quan hệ nghiệp vụ.

### 2.4. Tenant-safe foreign key

Mọi tenant-owned parent có unique alternate key:

~~~text
UNIQUE (center_id, <parent_id>)
~~~

Quan hệ giữa hai bảng tenant-owned dùng composite foreign key:

~~~text
(center_id, parent_id)
→ parent(center_id, parent_id)
~~~

Mục đích là ngăn một child của Center A tham chiếu parent của Center B ngay tại database. Global Query Filter và BLL ownership guard vẫn bắt buộc; composite FK không thay thế hai lớp đó.

### 2.5. Delete behavior

- Center: RESTRICT; không xóa trong MVP.
- User/Profile/Class/Subject/Curriculum/Question/Assignment: soft delete.
- Attempt, Reasoning Analysis, AI Job terminal, Twin History: không hard delete.
- Join chưa có lịch sử: CASCADE chỉ khi parent bị xóa vật lý trong môi trường test; business flow không hard delete parent.
- Quan hệ có bằng chứng lịch sử: RESTRICT.
- SET NULL chỉ dùng cho reference không còn cần thiết để bảo toàn audit, ví dụ current recommendation pointer.

## 3. Sơ đồ quan hệ cấp cao

~~~mermaid
erDiagram
    CENTERS ||--o{ USERS : owns
    USERS ||--o| STUDENTS : profile
    USERS ||--o| TEACHERS : profile
    CENTERS ||--o{ SUBJECTS : owns
    TEACHERS ||--o{ CLASSES : teaches
    CLASSES ||--o{ CLASS_STUDENTS : contains
    STUDENTS ||--o{ CLASS_STUDENTS : joins
    SUBJECTS ||--o{ KNOWLEDGE_NODES : structures
    KNOWLEDGE_NODES ||--o{ KNOWLEDGE_EDGES : source
    KNOWLEDGE_NODES ||--o{ KNOWLEDGE_EDGES : target
    SUBJECTS ||--o{ QUESTIONS : has
    QUESTIONS ||--o{ QUESTION_OPTIONS : has
    CLASSES ||--o{ ASSIGNMENTS : receives
    ASSIGNMENTS ||--o{ ASSIGNMENT_QUESTIONS : contains
    ASSIGNMENTS ||--o{ ASSIGNMENT_TARGETS : targets
    STUDENTS ||--o{ ATTEMPTS : submits
    QUESTIONS ||--o{ ATTEMPTS : answered
    ATTEMPTS ||--o| AI_ANALYSIS_JOBS : queues
    ATTEMPTS ||--o| REASONING_ANALYSES : produces
    STUDENTS ||--o{ KNOWLEDGE_TWINS : owns
    STUDENTS ||--o{ BEHAVIOR_TWINS : owns
    STUDENTS ||--o{ STUDENT_SUBJECT_GOALS : sets
    STUDENTS ||--o{ TWIN_UPDATE_HISTORY : explains
    STUDENTS ||--o{ LEARNING_PATHS : follows
    STUDENTS ||--o{ RECOMMENDATIONS : receives
    STUDENTS ||--o{ RECOMMENDATION_GENERATION_STATES : serializes
    SUBJECTS ||--o{ RECOMMENDATION_GENERATION_STATES : scopes
    CENTERS ||--o{ ROLES : defines
    USERS ||--o{ USER_ROLES : receives
    ROLES ||--o{ USER_ROLES : assigned
    PERMISSIONS ||--o{ PERMISSION_ACCOUNT_TYPES : permits_for
    ROLES ||--o{ ROLE_PERMISSIONS : grants
    PERMISSION_ACCOUNT_TYPES ||--o{ ROLE_PERMISSIONS : validates
    ATTEMPTS ||--o{ EVIDENCE_ASSESSMENTS : evaluated
    REASONING_ANALYSES ||--o{ EVIDENCE_ASSESSMENTS : informs
    CENTERS ||--o{ AUTHORIZATION_AUDIT_LOGS : audits
~~~

## 3.1. Danh mục 39 bảng hiện hành (và Bảng 40 mục tiêu sau Gate 5), mục đích và quan hệ chính

Đây là data dictionary cấp bảng. Các mục 4–42 bên dưới là data dictionary cấp cột của 39 bảng hiện hành và mục 43 là bảng mục tiêu thứ 40 sau Gate 5; không tạo thêm file schema song song.

| # | Table | Trạng thái | Chức năng | Quan hệ chính |
|---:|---|---|---|---|
| 1 | centers | Current | Tenant root và hồ sơ trung tâm | Parent của mọi dữ liệu tenant; provision qua deployment/seed |
| 2 | users | Current | Đăng nhập, account type, trạng thái và auth version | Thuộc centers; parent profiles, tokens, roles/audit actors |
| 3 | refresh_tokens | Current | Phiên refresh đã hash, rotation/revocation | Thuộc users trong cùng center |
| 4 | teachers | Current | Hồ sơ nghiệp vụ giáo viên | PK/FK users; parent classes/curriculums/content ownership |
| 5 | students | Current | Hồ sơ nghiệp vụ học sinh | PK/FK users; member class, owner attempts/twins/goals |
| 6 | subjects | Current | Môn học tenant-owned | Parent classes, graph, curriculum, question và twins |
| 7 | classes | Current | Lớp theo giáo viên, môn, năm học | FK teachers/subjects; parent membership/assignment |
| 8 | class_students | Current | Trạng thái học sinh trong lớp | Join classes–students cùng center |
| 9 | knowledge_nodes | Current | Chapter/topic/skill/concept của graph | FK subjects; parent edge/map/twin/recommendation |
| 10 | knowledge_edges | Current | Quan hệ prerequisite/hierarchy giữa node | Hai FK source/target knowledge_nodes cùng subject/center |
| 11 | curriculums | Current | Chương trình do giáo viên quản lý | FK teachers/subjects; join class/node |
| 12 | curriculum_classes | Current | Gán curriculum cho class | Join curriculums–classes |
| 13 | curriculum_nodes | Current | Thứ tự node trong curriculum | Join curriculums–knowledge_nodes |
| 14 | questions | Current | Ngân hàng câu hỏi và grading criteria | FK subject/teacher/topic; parent option/map/attempt |
| 15 | question_options | Current | Lựa chọn của câu MultipleChoice | FK questions cùng center |
| 16 | question_knowledge_nodes | Current | Node được câu hỏi đánh giá | Join questions–knowledge_nodes |
| 17 | assignments | Current | Bài tập của class và lifecycle publish/close | FK classes/teachers; parent question/target/progress |
| 18 | assignment_questions | Current | Snapshot thứ tự câu trong assignment | Join assignments–questions |
| 19 | assignment_targets | Current | Học sinh được giao bài | Join assignments–students |
| 20 | student_assignment_progress | Current | Tiến độ/điểm tổng theo assignment–student | FK assignment/student |
| 21 | student_subject_goals | Current | Mục tiêu điểm và thời gian theo môn | FK students/subjects |
| 22 | student_twins | Current | Root aggregate Learning Digital Twin | Unique theo student; tổng hợp trạng thái |
| 23 | knowledge_twins | Current | Mastery/risk theo student–subject–topic | FK student/subject/node/attempt |
| 24 | behavior_twins | Current | Aggregate telemetry học tập theo môn | FK student/subject |
| 25 | twin_update_history | Current | Lịch sử append-only của mọi lần tính Twin | FK student/subject/topic/attempt/analysis |
| 26 | learning_paths | Current | Phiên bản lộ trình active/superseded | FK student/subject/source attempt |
| 27 | learning_path_items | Current | Các bước topic/question trong lộ trình | FK learning_paths/node/question |
| 28 | recommendations | Current | Hành động học tiếp theo có breakdown | FK student/subject/node/question/source attempt |
| 29 | recommendation_generation_states | Current | Watermark bền vững chống trigger cũ/duplicate kể cả khi kết quả Blocked/NoCandidate | PK/FK center–student–subject |
| 30 | attempts | Current | Bài nộp, telemetry và chấm sơ bộ append-oriented | FK student/question/assignment; parent job/analysis/evidence |
| 31 | reasoning_analyses | Current | Observation AI/fallback và teacher override provenance | One-to-one logical với attempt; referenced by evidence/history |
| 32 | ai_analysis_jobs | Current | Queue bền vững, lease, retry và terminal state | Unique theo attempt |
| 33 | permissions | Current | Catalog capability toàn hệ thống, chỉ đọc ở runtime | Parent applicability/role grant |
| 34 | permission_account_types | Current | Khóa permission được dùng bởi account type nào | Join permissions–account type; principal cho role grant |
| 35 | roles | Current | Vai trò động do từng Center quản lý | Parent role_permissions/user_roles |
| 36 | role_permissions | Current | Permission set hiện hành của role | Join roles–permissions có account-type FK |
| 37 | user_roles | Current | Role active/revoked của user | Join users–roles có account-type FK |
| 38 | authorization_audit_logs | Current | Audit append-only của thay đổi quyền | FK actor/target users khi có |
| 39 | evidence_assessments | Current | Quyết định policy append-only, không nhân bản analysis/mastery | FK attempts/analyses/self-supersession |
| 40 | attempt_attachments | Target post-Gate 5 | Minh chứng ảnh nháp đính kèm Attempt | FK attempts; quan hệ 1:1, unique nonce giải quyết race condition |

# Module 1 — System Users & Organization

## 4. centers [Mutable root, không có center_id]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | PK |
| center_code | VARCHAR(32) | No | Unique, uppercase business code |
| center_name | VARCHAR(200) | No | Tên hiển thị chính thức của Center |
| status | VARCHAR(32) | No | Active, Suspended |
| timezone | VARCHAR(64) | No | Default Asia/Bangkok |
| created_at | DATETIME(6) | No | Thời điểm UTC tạo Center |
| updated_at | DATETIME(6) | No | Thời điểm UTC cập nhật Center gần nhất |
| is_deleted | TINYINT(1) | No | Default 0 |
| deleted_at | DATETIME(6) | Yes | Chỉ phục vụ vận hành ngoài course MVP; business API không xóa Center |
| row_version | BIGINT UNSIGNED | No | Default 1 |

Indexes/constraints:

- PK(center_id).
- UX(center_code).
- CHECK status IN (Active, Suspended).

Invariant:

- Center bị Suspended không được login/refresh hoặc tạo job mới.
- Center chỉ được provision/khóa bằng migration, seed hoặc deployment operation có kiểm soát; course MVP không có endpoint tạo/xóa Center hoặc quản lý Center khác.
- MVP chỉ cho CenterManager cập nhật profile Center hiện hành khi có permission.

## 5. users [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| user_id | VARCHAR(36) | No | PK |
| username | VARCHAR(100) | No | Unique trong Center |
| password_hash | VARCHAR(500) | No | Không lưu password |
| role_name | VARCHAR(32) | No | Legacy physical name; v2 semantics là account type Student, Teacher, CenterManager, PlatformAdmin |
| display_name | VARCHAR(200) | No | Tên hiển thị của tài khoản |
| status | VARCHAR(32) | No | Active, Locked, Disabled |
| last_login_at | DATETIME(6) | Yes | Lần đăng nhập thành công gần nhất theo UTC |
| auth_version | INT UNSIGNED | No | Default 1; nguồn duy nhất cho JSON authorizationVersion và JWT auth_version |
| ...MTA | | | Theo mục 2.3 |

Indexes/constraints:

- PK(user_id).
- UX(center_id, username).
- UX(center_id, user_id).
- UX(center_id, user_id, role_name) để làm principal key cho ràng buộc account type của user_roles.
- IX(center_id, role_name, status).
- CHECK role_name IN (Student, Teacher, CenterManager, PlatformAdmin).
- CHECK status IN (Active, Locked, Disabled).

Invariant:

- Một User chỉ thuộc một Center.
- Không đổi role_name/account type sau khi đã có profile; nếu cần phải qua use case riêng và migration data được duyệt.
- Sau RBAC cutover, role_name chỉ phân biệt loại hồ sơ/domain context; effective permission phải lấy từ user_roles và role_permissions.
- Password reset, user status, user-role và role-permission mutation ảnh hưởng user phải tăng auth_version atomically; server từ chối access token có claim cũ.

## 6. refresh_tokens [TA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| refresh_token_id | BIGINT UNSIGNED | No | PK, auto increment |
| user_id | VARCHAR(36) | No | Tenant-safe FK users |
| token_hash | CHAR(64) | No | SHA-256 hoặc hash tương đương; unique |
| expires_at | DATETIME(6) | No | Thời điểm token hết hiệu lực |
| revoked_at | DATETIME(6) | Yes | Thời điểm revoke; null khi còn hiệu lực |
| replaced_by_token_id | BIGINT UNSIGNED | Yes | Self FK |
| revoke_reason | VARCHAR(200) | Yes | Lý do logout, rotation, stale authorization hoặc khóa user |
| created_by_ip | VARCHAR(64) | Yes | IP tạo token đã được chuẩn hóa/redaction phù hợp |
| revoked_by_ip | VARCHAR(64) | Yes | IP thực hiện revoke nếu có |
| ...TA | | | Theo mục 2.3 |

Indexes/constraints:

- PK(refresh_token_id).
- UX(token_hash).
- IX(center_id, user_id, expires_at).
- FK(center_id, user_id) → users.

Invariant:

- Rotation là atomic transaction.
- Token đã revoke/replaced không dùng lại.

## 7. teachers [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| teacher_id | VARCHAR(36) | No | PK và tenant-safe FK users.user_id |
| department | VARCHAR(150) | Yes | Bộ môn/đơn vị chuyên môn |
| bio | VARCHAR(500) | Yes | Giới thiệu chuyên môn ngắn |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Constraints:

- PK(teacher_id).
- UX(center_id, teacher_id).
- FK(center_id, teacher_id) → users(center_id, user_id).
- User tương ứng phải có role Teacher; kiểm tra ở BLL.

## 8. students [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| student_id | VARCHAR(36) | No | PK và tenant-safe FK users.user_id |
| full_name | VARCHAR(200) | No | Họ tên nghiệp vụ của học sinh |
| grade_level | TINYINT UNSIGNED | No | 10, 11 hoặc 12 |
| date_of_birth | DATE | Yes | Mock Data |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Constraints:

- PK(student_id).
- UX(center_id, student_id).
- IX(center_id, grade_level).
- CHECK grade_level BETWEEN 10 AND 12.
- User tương ứng phải có role Student; kiểm tra ở BLL.

target_score và remaining_days không nằm ở students vì mục tiêu được quản lý theo Subject.

## 9. subjects [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| subject_id | VARCHAR(36) | No | PK |
| subject_code | VARCHAR(32) | No | Ví dụ MATH, ENGLISH |
| subject_name | VARCHAR(100) | No | Tên hiển thị tiếng Việt |
| description | VARCHAR(500) | Yes | Mô tả phạm vi/nội dung môn học |
| is_active | TINYINT(1) | No | Default 1 |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Indexes:

- PK(subject_id).
- UX(center_id, subject_code).
- UX(center_id, subject_id).

Subject là tenant-owned để Teacher của Center này không làm thay đổi catalog của Center khác.

## 10. classes [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| class_id | VARCHAR(36) | No | PK |
| teacher_id | VARCHAR(36) | No | Tenant-safe FK teachers |
| subject_id | VARCHAR(36) | No | Tenant-safe FK subjects |
| class_name | VARCHAR(150) | No | Tên lớp hiển thị trong Center |
| academic_year | VARCHAR(20) | No | Ví dụ 2026-2027 |
| status | VARCHAR(32) | No | Active, Archived |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Indexes:

- PK(class_id).
- UX(center_id, class_id).
- UX(center_id, class_name, academic_year).
- IX(center_id, teacher_id, status).
- IX(center_id, subject_id, status).

Invariant:

- Teacher và Subject phải cùng Center.
- Class trong MVP gắn với đúng một Subject.

## 11. class_students [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator và thành phần PK/FK |
| class_id | VARCHAR(36) | No | Tenant-safe FK classes |
| student_id | VARCHAR(36) | No | Tenant-safe FK students |
| joined_at | DATETIME(6) | No | Thời điểm UTC học sinh gia nhập lớp |
| status | VARCHAR(32) | No | Active, Removed |
| removed_at | DATETIME(6) | Yes | Thời điểm UTC rời/bị loại khỏi lớp |
| created_by | VARCHAR(36) | Yes | User cùng Center tạo membership |

Constraints:

- PK(center_id, class_id, student_id).
- IX(center_id, student_id, status).
- CHECK status IN (Active, Removed).

# Module 2 — Knowledge Graph

## 12. knowledge_nodes [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| node_id | BIGINT UNSIGNED | No | PK, auto increment |
| subject_id | VARCHAR(36) | No | Tenant-safe FK subjects |
| parent_node_id | BIGINT UNSIGNED | Yes | Hierarchy parent cùng Center/Subject |
| node_type | VARCHAR(32) | No | Subject, Chapter, Topic, Skill, Concept |
| node_code | VARCHAR(64) | No | Stable code |
| node_name | VARCHAR(200) | No | Tên hiển thị của đơn vị kiến thức |
| description | TEXT | Yes | Mô tả phạm vi/nội dung kiến thức |
| order_index | INT UNSIGNED | No | Default 0 |
| exam_importance | DECIMAL(5,2) | No | 0–100; chủ yếu dùng Topic |
| estimated_learning_minutes | INT UNSIGNED | No | Minimum 1 |
| is_active | TINYINT(1) | No | Default 1 |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Indexes/constraints:

- PK(node_id).
- UX(center_id, node_id).
- UX(center_id, subject_id, node_code).
- IX(center_id, subject_id, node_type, order_index).
- FK(center_id, parent_node_id) → knowledge_nodes; subject consistency kiểm tra BLL.
- CHECK node_type IN (Subject, Chapter, Topic, Skill, Concept).
- CHECK exam_importance BETWEEN 0 AND 100.
- CHECK estimated_learning_minutes > 0.

Invariant:

- Topic là đơn vị có Knowledge Twin.
- parent_node_id không được tạo hierarchy cycle.

## 13. knowledge_edges [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| edge_id | BIGINT UNSIGNED | No | PK, auto increment |
| subject_id | VARCHAR(36) | No | Tenant-safe FK subjects |
| source_node_id | BIGINT UNSIGNED | No | Tenant-safe FK node |
| target_node_id | BIGINT UNSIGNED | No | Tenant-safe FK node |
| relation_type | VARCHAR(32) | No | PrerequisiteOf, RelatedTo, PartOf, CausesErrorIn |
| weight | DECIMAL(5,2) | No | Default 1.00; 0–1 |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Indexes/constraints:

- PK(edge_id).
- UX(center_id, edge_id).
- UX(center_id, source_node_id, target_node_id, relation_type).
- IX(center_id, target_node_id, relation_type).
- CHECK source_node_id <> target_node_id.
- CHECK weight BETWEEN 0 AND 1.

Invariant:

- source/target cùng Center và Subject.
- PrerequisiteOf phải acyclic.
- PartOf hierarchy cycle cũng bị cấm.
- Cycle detection ở BLL trước transaction commit; không dùng trigger/procedure.

# Module 3 — Curriculum, Question Bank & Assignments

## 14. curriculums [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| curriculum_id | VARCHAR(36) | No | PK |
| teacher_id | VARCHAR(36) | No | Tenant-safe FK teachers |
| subject_id | VARCHAR(36) | No | Tenant-safe FK subjects |
| title | VARCHAR(250) | No | Tiêu đề curriculum hiển thị |
| description | TEXT | Yes | Mô tả mục tiêu/phạm vi curriculum |
| source_file | VARCHAR(500) | Yes | Reserved; MVP không upload |
| review_status | VARCHAR(32) | No | Draft, Published, Archived |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Indexes:

- UX(center_id, curriculum_id).
- IX(center_id, teacher_id, review_status).
- IX(center_id, subject_id, review_status).

## 15. curriculum_classes [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator và thành phần PK/FK |
| curriculum_id | VARCHAR(36) | No | Tenant-safe FK |
| class_id | VARCHAR(36) | No | Tenant-safe FK |
| assigned_at | DATETIME(6) | No | Thời điểm UTC curriculum được gán cho class |
| assigned_by | VARCHAR(36) | No | User cùng Center thực hiện assignment |

- PK(center_id, curriculum_id, class_id).
- Curriculum, Class và Subject phải tương thích.

## 16. curriculum_nodes [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator và thành phần PK/FK |
| curriculum_id | VARCHAR(36) | No | Tenant-safe FK curriculum |
| node_id | BIGINT UNSIGNED | No | Tenant-safe FK knowledge node |
| order_index | INT UNSIGNED | No | Thứ tự node trong curriculum |
| created_at | DATETIME(6) | No | Thời điểm UTC tạo mapping |

- PK(center_id, curriculum_id, node_id).
- UX(center_id, curriculum_id, order_index).
- Chỉ cho phép node cùng Subject.

## 17. questions [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| question_id | BIGINT UNSIGNED | No | PK, auto increment |
| subject_id | VARCHAR(36) | No | Tenant-safe FK |
| primary_topic_node_id | BIGINT UNSIGNED | No | Phải là Topic |
| created_by_teacher_id | VARCHAR(36) | No | Tenant-safe FK |
| question_type | VARCHAR(32) | No | MultipleChoice, ShortAnswer, Essay |
| answer_evaluation_mode | VARCHAR(32) | No | TextExact, NumericRational, Manual (Default TextExact) |
| difficulty | TINYINT UNSIGNED | No | 1–5 |
| question_text | LONGTEXT | No | Việt hoặc Anh |
| correct_answer | TEXT | No | Canonical final answer/model answer |
| solution | LONGTEXT | No | Teacher-authored explanation |
| expected_reasoning | LONGTEXT | Yes | AI context |
| grading_criteria | JSON | No | Versioned criteria object |
| max_score | DECIMAL(5,2) | No | Default 1.00 |
| estimated_time_seconds | INT UNSIGNED | No | > 0 |
| reasoning_required | TINYINT(1) | No | Default 1 |
| language_code | VARCHAR(8) | No | vi hoặc en |
| status | VARCHAR(32) | No | Draft, Active, Archived |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Indexes/constraints:

- UX(center_id, question_id).
- IX(center_id, subject_id, primary_topic_node_id, status, difficulty).
- IX(center_id, created_by_teacher_id, status).
- CHECK question_type IN (MultipleChoice, ShortAnswer, Essay).
- CHECK answer_evaluation_mode IN (TextExact, NumericRational, Manual).
- CHECK difficulty BETWEEN 1 AND 5.
- CHECK max_score > 0.
- CHECK estimated_time_seconds > 0.
- CHECK language_code IN (vi, en).
- CHECK status IN (Draft, Active, Archived).

grading_criteria JSON tối thiểu:

~~~json
{
  "schemaVersion": "1.0",
  "requiredIdeas": ["string"],
  "commonErrors": ["string"],
  "scoringNotes": "string"
}
~~~

## 18. question_options [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| option_id | BIGINT UNSIGNED | No | PK |
| question_id | BIGINT UNSIGNED | No | Tenant-safe FK |
| option_label | VARCHAR(8) | No | A, B, C, D... |
| option_text | TEXT | No | Nội dung lựa chọn |
| is_correct | TINYINT(1) | No | Đánh dấu đáp án đúng; Student projection không được lộ trước submit |
| order_index | INT UNSIGNED | No | Thứ tự hiển thị ổn định |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Indexes:

- UX(center_id, question_id, option_label).
- UX(center_id, question_id, order_index).

BLL invariant:

- Chỉ MultipleChoice có options.
- Active MultipleChoice có ít nhất 2 options và đúng 1 option correct trong MVP.

## 19. question_knowledge_nodes [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator và thành phần PK/FK |
| question_id | BIGINT UNSIGNED | No | Tenant-safe FK question |
| node_id | BIGINT UNSIGNED | No | Tenant-safe FK knowledge node được đánh giá |
| mapping_role | VARCHAR(32) | No | Primary, Secondary, Prerequisite |
| created_at | DATETIME(6) | No | Thời điểm UTC tạo mapping |

- PK(center_id, question_id, node_id, mapping_role).
- CHECK mapping_role IN (Primary, Secondary, Prerequisite).
- Có đúng một Primary mapping trùng primary_topic_node_id.

## 20. assignments [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| assignment_id | VARCHAR(36) | No | PK |
| class_id | VARCHAR(36) | No | Tenant-safe FK |
| created_by_teacher_id | VARCHAR(36) | No | Phải là Teacher của Class |
| title | VARCHAR(250) | No | Tiêu đề bài tập |
| instructions | TEXT | Yes | Hướng dẫn do giáo viên soạn |
| due_at | DATETIME(6) | Yes | UTC |
| status | VARCHAR(32) | No | Draft, Published, Closed, Archived |
| published_at | DATETIME(6) | Yes | Thời điểm UTC publish; null khi chưa publish |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

Indexes:

- UX(center_id, assignment_id).
- IX(center_id, class_id, status, due_at).
- CHECK status IN (Draft, Published, Closed, Archived).

## 21. assignment_questions [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator và thành phần PK/FK |
| assignment_id | VARCHAR(36) | No | Tenant-safe FK assignment |
| question_id | BIGINT UNSIGNED | No | Tenant-safe FK question |
| order_index | INT UNSIGNED | No | Thứ tự câu trong assignment |
| points | DECIMAL(5,2) | No | > 0 |
| created_at | DATETIME(6) | No | Thời điểm UTC materialize câu vào assignment |

- PK(center_id, assignment_id, question_id).
- UX(center_id, assignment_id, order_index).
- Question Subject phải trùng Class Subject.

## 22. assignment_targets [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator và thành phần PK/FK |
| assignment_id | VARCHAR(36) | No | Tenant-safe FK assignment |
| student_id | VARCHAR(36) | No | Tenant-safe FK student được giao |
| target_source | VARCHAR(32) | No | WholeClass, SelectedStudents, GapGroup |
| created_at | DATETIME(6) | No | Thời điểm UTC materialize target |
| created_by | VARCHAR(36) | No | User cùng Center publish/giao bài |

- PK(center_id, assignment_id, student_id).
- CHECK target_source IN (WholeClass, SelectedStudents, GapGroup).
- Student phải là active member của Class khi publish.
- Target được materialize để membership thay đổi sau này không làm mất lịch sử giao bài.

## 23. student_assignment_progress [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| progress_id | BIGINT UNSIGNED | No | PK |
| assignment_id | VARCHAR(36) | No | Tenant-safe FK assignment |
| student_id | VARCHAR(36) | No | Tenant-safe FK student |
| status | VARCHAR(32) | No | NotStarted, InProgress, Completed, Overdue |
| completed_question_count | INT UNSIGNED | No | Default 0 |
| total_question_count | INT UNSIGNED | No | Snapshot |
| started_at | DATETIME(6) | Yes | Thời điểm UTC bắt đầu |
| completed_at | DATETIME(6) | Yes | Thời điểm UTC hoàn tất |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

- UX(center_id, assignment_id, student_id).
- IX(center_id, student_id, status).
- CHECK completed_question_count <= total_question_count.

# Module 4 — Digital Twin & Personalization

## 24. student_subject_goals [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| goal_id | BIGINT UNSIGNED | No | PK |
| student_id | VARCHAR(36) | No | Tenant-safe FK student sở hữu mục tiêu |
| subject_id | VARCHAR(36) | No | Tenant-safe FK môn học của mục tiêu |
| target_score | DECIMAL(4,2) | No | 0–10 |
| remaining_days | INT UNSIGNED | No | 0–3650 |
| current_predicted_score | DECIMAL(4,2) | No | Default 0 |
| risk_score | DECIMAL(5,2) | No | Default 0; 0–100 |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

- UX(center_id, student_id, subject_id).
- IX(center_id, subject_id, risk_score).
- CHECK target_score BETWEEN 0 AND 10.
- CHECK current_predicted_score BETWEEN 0 AND 10.
- CHECK risk_score BETWEEN 0 AND 100.

## 25. student_twins [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| twin_id | VARCHAR(36) | No | PK |
| student_id | VARCHAR(36) | No | Unique một Twin root/Student |
| overall_mastery | DECIMAL(5,2) | No | Aggregate across active subjects |
| last_evidence_at | DATETIME(6) | Yes | Thời điểm UTC evidence hiệu lực gần nhất |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

- UX(center_id, student_id).
- CHECK overall_mastery BETWEEN 0 AND 100.

Student Twin là aggregate header; score/risk chi tiết nằm ở Subject Goal.

## 26. knowledge_twins [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| knowledge_twin_id | BIGINT UNSIGNED | No | PK |
| student_id | VARCHAR(36) | No | Tenant-safe FK student |
| subject_id | VARCHAR(36) | No | Tenant-safe FK subject |
| topic_node_id | BIGINT UNSIGNED | No | Phải là Topic |
| mastery_percentage | DECIMAL(5,2) | No | Default 0 |
| evidence_count | INT UNSIGNED | No | Default 0 |
| last_reasoning_quality | DECIMAL(5,2) | Yes | null nếu fallback |
| last_attempt_id | BIGINT UNSIGNED | Yes | Attempt gần nhất đã tạo effective knowledge evidence |
| last_evidence_at | DATETIME(6) | Yes | Thời điểm UTC effective knowledge evidence gần nhất |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

- UX(center_id, student_id, topic_node_id).
- IX(center_id, subject_id, mastery_percentage).
- CHECK mastery_percentage BETWEEN 0 AND 100.
- CHECK last_reasoning_quality IS NULL OR BETWEEN 0 AND 100.

## 27. behavior_twins [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| behavior_twin_id | BIGINT UNSIGNED | No | PK |
| student_id | VARCHAR(36) | No | Tenant-safe FK student |
| subject_id | VARCHAR(36) | No | Tenant-safe FK subject |
| avg_time_spent_seconds | DECIMAL(10,2) | No | Default 0 |
| skip_rate | DECIMAL(5,2) | No | 0–100 |
| change_answer_rate | DECIMAL(5,2) | No | 0–100 |
| avg_confidence | DECIMAL(5,2) | No | 0–100 |
| confidence_calibration | DECIMAL(5,2) | No | 0–100 |
| attempt_count | INT UNSIGNED | No | Default 0 |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

- UX(center_id, student_id, subject_id).
- CHECK các rate BETWEEN 0 AND 100.

## 28. twin_update_history [TA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| history_id | BIGINT UNSIGNED | No | PK, auto increment |
| student_id | VARCHAR(36) | No | Tenant-safe FK student |
| subject_id | VARCHAR(36) | No | Tenant-safe FK subject |
| topic_node_id | BIGINT UNSIGNED | No | Tenant-safe FK Topic được cập nhật/giữ nguyên |
| attempt_id | BIGINT UNSIGNED | Yes | Attempt tạo event; null cho migration/system recompute có provenance |
| analysis_id | BIGINT UNSIGNED | Yes | Analysis được dùng; null khi event chỉ từ observed data |
| event_source | VARCHAR(32) | No | AIAnalysis, RuleFallback, TeacherOverride, Replay |
| previous_mastery | DECIMAL(5,2) | No | Mastery trước event |
| new_mastery | DECIMAL(5,2) | No | Mastery sau event; bằng previous khi ReviewOnly |
| mastery_delta | DECIMAL(6,2) | No | Có thể âm |
| effective_reasoning_quality | DECIMAL(5,2) | Yes | Quality sau override/gate; null cho deterministic fallback |
| calculation_version | VARCHAR(20) | No | Ví dụ mastery-v1 |
| calculation_breakdown | JSON | No | Input/weight/output |
| explanation | VARCHAR(1000) | No | Human-readable |
| ...TA | | | Kế thừa center_id, created_at và created_by tại mục 2.3 |

Indexes:

- IX(center_id, student_id, subject_id, created_at).
- IX(center_id, topic_node_id, created_at).
- IX(center_id, attempt_id).

Table append-only; replay tạo event mới, không sửa event cũ.

## 29. learning_paths [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| learning_path_id | VARCHAR(36) | No | PK |
| student_id | VARCHAR(36) | No | Tenant-safe FK student nhận lộ trình |
| subject_id | VARCHAR(36) | No | Tenant-safe FK subject |
| strategy | VARCHAR(32) | No | LinearFallback, OpportunityGap |
| version | INT UNSIGNED | No | Tăng khi regenerate |
| status | VARCHAR(32) | No | Active, Superseded, Completed |
| generated_from_attempt_id | BIGINT UNSIGNED | Yes | Attempt làm thay đổi input; null cho bootstrap/manual regenerate |
| generated_at | DATETIME(6) | No | Thời điểm UTC sinh version |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

- Chỉ một Active path cho Student + Subject; bảo đảm bằng transaction/service và filtered strategy phù hợp MySQL.
- IX(center_id, student_id, subject_id, status).

## 30. learning_path_items [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| learning_path_item_id | BIGINT UNSIGNED | No | PK |
| learning_path_id | VARCHAR(36) | No | Tenant-safe FK learning path |
| topic_node_id | BIGINT UNSIGNED | No | Tenant-safe FK Topic được đề xuất |
| recommended_question_id | BIGINT UNSIGNED | Yes | Câu hỏi active phù hợp; null khi chưa có câu |
| rank_order | INT UNSIGNED | No | Vị trí deterministic trong path |
| opportunity_score | DECIMAL(5,2) | Yes | null cho linear |
| reason | VARCHAR(1000) | No | Giải thích từ template deterministic, không phải ranking do AI |
| status | VARCHAR(32) | No | Pending, Current, Completed, Skipped |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

- UX(center_id, learning_path_id, rank_order).
- UX(center_id, learning_path_id, topic_node_id).

## 31. recommendations [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| recommendation_id | BIGINT UNSIGNED | No | PK |
| student_id | VARCHAR(36) | No | Tenant-safe FK student nhận recommendation |
| subject_id | VARCHAR(36) | No | Tenant-safe FK subject |
| topic_node_id | BIGINT UNSIGNED | No | Topic do heuristic deterministic chọn |
| question_id | BIGINT UNSIGNED | Yes | Question được chọn; null nếu chỉ đề xuất Topic |
| recommendation_type | VARCHAR(32) | No | TopicAndQuestion, LinearFallback |
| opportunity_score | DECIMAL(5,2) | Yes | Điểm xếp hạng 0–100; null cho LinearFallback không chấm score |
| calculation_version | VARCHAR(20) | No | opportunity-v1 |
| calculation_breakdown | JSON | No | Input/factor/tie-break đủ để tái lập |
| explanation | VARCHAR(1000) | No | Giải thích deterministic/template; AI chỉ được diễn đạt bổ sung |
| source_attempt_id | BIGINT UNSIGNED | Yes | Attempt kích hoạt recompute nếu có |
| status | VARCHAR(32) | No | Active, Accepted, Dismissed, Superseded |
| generated_at | DATETIME(6) | No | Thời điểm UTC sinh recommendation |
| expires_at | DATETIME(6) | Yes | Hạn dùng nếu policy quy định |
| dismiss_reason | VARCHAR(1000) | Yes | Lý do học sinh bỏ qua; trim trước khi lưu, giữ nguyên khi retry idempotent |
| ...MTA | | | Kế thừa audit, soft-delete, tenant và row_version tại mục 2.3 |

- IX(center_id, student_id, subject_id, status, generated_at).
- BLL supersede recommendation cũ trong cùng transaction tạo recommendation mới.

## 32. recommendation_generation_states [Tenant state + row version]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | PK part; tenant discriminator |
| student_id | VARCHAR(36) | No | PK part; tenant-safe FK student |
| subject_id | VARCHAR(36) | No | PK part; tenant-safe FK subject |
| last_trigger_at | DATETIME(6) | No | UTC ordering watermark của trigger đã xử lý gần nhất |
| last_source_attempt_id | BIGINT UNSIGNED | Yes | Tie-break/idempotency identity khi trigger đến từ Attempt |
| last_outcome | VARCHAR(32) | No | Generated, NoCandidate hoặc Blocked |
| diagnostic_reason | VARCHAR(500) | Yes | Lý do terminal khi không tạo recommendation |
| created_at | DATETIME(6) | No | UTC |
| updated_at | DATETIME(6) | No | UTC |
| row_version | BIGINT UNSIGNED | No | Concurrency token |

- Chính xác một row cho mỗi center/student/subject.
- Check và update dưới cùng Student row lock trong Recommendation Transaction B.
- Không xóa row khi recommendation được Accepted/Dismissed; đây là nguồn ordering độc lập với current Active row.
- Trigger cũ hoặc retry cùng trigger-time/source-attempt trả StaleIgnored, không resurrect artifact cũ.

# Module 5 — Assessment & AI Reasoning

## 33. attempts [TA + trạng thái nghiệp vụ]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| attempt_id | BIGINT UNSIGNED | No | PK, auto increment |
| student_id | VARCHAR(36) | No | Tenant-safe FK student nộp bài |
| question_id | BIGINT UNSIGNED | No | Tenant-safe FK question được trả lời |
| assignment_id | VARCHAR(36) | Yes | Null nếu luyện tự do |
| final_answer | LONGTEXT | No | Câu trả lời cuối cùng dùng chấm sơ bộ deterministic |
| reasoning_text | LONGTEXT | Yes | Bắt buộc nếu question.reasoning_required |
| answer_display_latex | VARCHAR(2048) | Yes | Công thức LaTeX hiển thị của câu trả lời |
| is_correct | TINYINT(1) | Yes | Preliminary deterministic grade |
| awarded_score | DECIMAL(5,2) | Yes | Điểm sơ bộ theo grader/criteria; teacher có thể review theo use case |
| time_spent_seconds | INT UNSIGNED | No | Telemetry thời gian quan sát được |
| confidence | DECIMAL(5,2) | No | 0–100 |
| answer_changes | INT UNSIGNED | No | Default 0 |
| skipped | TINYINT(1) | No | Default 0 |
| reasoning_language | VARCHAR(8) | No | vi hoặc en |
| status | VARCHAR(32) | No | PendingAnalysis, Processing, Completed, NeedsTeacherReview, AnalysisFailed |
| client_submission_id | VARCHAR(36) | No | Idempotency key từ client |
| updated_at | DATETIME(6) | No | Thời điểm trạng thái thay đổi gần nhất |
| row_version | BIGINT UNSIGNED | No | Concurrency token |
| ...TA | | | Kế thừa center_id, created_at và created_by tại mục 2.3 |

Indexes/constraints:

- UX(center_id, student_id, client_submission_id).
- IX(center_id, student_id, question_id, created_at).
- IX(center_id, assignment_id, student_id).
- IX(center_id, status, created_at).
- CHECK status IN ('PendingAnalysis', 'Processing', 'Completed', 'NeedsTeacherReview', 'AnalysisFailed').
- CHECK confidence BETWEEN 0 AND 100.
- CHECK time_spent_seconds >= 0.
- CHECK reasoning_language IN (vi, en).

Attempts không soft delete; nếu cần loại khỏi replay phải có use case invalidate được phê duyệt trong tương lai.

## 34. reasoning_analyses [TA + override fields]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| analysis_id | BIGINT UNSIGNED | No | PK, auto increment |
| attempt_id | BIGINT UNSIGNED | No | Unique 1:1 |
| schema_version | VARCHAR(20) | No | ai-analysis-v1 |
| method_detected | VARCHAR(500) | Yes | Phương pháp suy luận được observation nhận diện |
| reasoning_quality | DECIMAL(5,2) | Yes | null cho fallback |
| error_type | VARCHAR(32) | No | None, Knowledge, Skill, Reasoning, Behavior, Presentation, Unknown |
| misconception | VARCHAR(1000) | Yes | Hiểu sai được phát hiện; null khi không đủ evidence |
| missing_steps | JSON | No | Array string |
| root_cause_node_ids | JSON | No | Array ID string, mỗi ID map về BIGINT và validated cùng Center |
| analysis_confidence | DECIMAL(5,2) | Yes | 0–100 |
| feedback | LONGTEXT | No | Cùng ngôn ngữ reasoning |
| is_fallback | TINYINT(1) | No | 1 khi nội dung do RuleBased fallback tạo, không phải Gemini |
| needs_teacher_review | TINYINT(1) | No | Cờ vận hành đưa analysis vào review queue |
| provider | VARCHAR(32) | No | Gemini hoặc RuleBased |
| model_name | VARCHAR(100) | Yes | Model provider trả về; null cho fallback |
| override_reasoning_quality | DECIMAL(5,2) | Yes | Quality giáo viên xác nhận/sửa |
| override_error_type | VARCHAR(32) | Yes | Error type giáo viên xác nhận/sửa |
| override_feedback | LONGTEXT | Yes | Feedback hiệu lực do giáo viên sửa |
| override_is_correct | TINYINT(1) | Yes | Correctness hiệu lực do giáo viên xác nhận |
| override_awarded_score | DECIMAL(5,2) | Yes | Điểm hiệu lực do giáo viên xác nhận/sửa; null cho phép reset về điểm sơ bộ |
| override_reason | VARCHAR(1000) | Yes | Lý do bắt buộc của override |
| overridden_by_user_id | VARCHAR(36) | Yes | Tenant-safe FK tới User thực hiện override; hỗ trợ Teacher hoặc CenterManager |
| overridden_at | DATETIME(6) | Yes | Thời điểm UTC override |
| override_version | INT UNSIGNED | No | Default 0 |
| updated_at | DATETIME(6) | No | Thay đổi khi override |
| row_version | BIGINT UNSIGNED | No | Concurrency token của record |
| ...TA | | | Kế thừa center_id, created_at và created_by tại mục 2.3 |

Indexes/constraints:

- UX(center_id, attempt_id).
- UX(center_id, analysis_id, attempt_id), alternate key cho Evidence FK khóa analysis cùng Attempt.
- IX(center_id, needs_teacher_review, created_at).
- CHECK quality/confidence IS NULL OR BETWEEN 0 AND 100.
- CHECK override_awarded_score IS NULL OR BETWEEN 0 AND 100.
- Override fields phải all-null hoặc có override_reason + teacher + time; BLL invariant.

Effective values:

- effective_reasoning_quality = override_reasoning_quality ?? reasoning_quality.
- effective_error_type = override_error_type ?? error_type.
- effective_feedback = override_feedback ?? feedback.
- effective_is_correct = override_is_correct ?? attempt.is_correct.
- effective_awarded_score = override_awarded_score ?? attempt.awarded_score.

Provenance semantics:

- Raw AI observation fields are immutable after analysis creation; Teacher Override only writes the dedicated override columns.
- Override columns represent the current effective human override and may be replaced through optimistic `override_version` concurrency.
- `evidence_assessments` and `twin_update_history` are append-only policy/replay lineage, but they do not snapshot the complete payload of every historical override version.
- Full payload-level override history requires a future approved append-only relation such as `reasoning_analysis_overrides`; that capability is not claimed by the current schema.

Không lưu raw Gemini request/response trong table này.

## 35. ai_analysis_jobs [TA + mutable state]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| analysis_job_id | BIGINT UNSIGNED | No | PK, auto increment |
| attempt_id | BIGINT UNSIGNED | No | Unique |
| status | VARCHAR(32) | No | Pending, Processing, Completed, FallbackCompleted, FailedTerminal |
| retry_count | TINYINT UNSIGNED | No | Default 0, tối đa 3 retries có exponential backoff |
| available_at | DATETIME(6) | No | Thời điểm UTC job đủ điều kiện claim/retry |
| started_at | DATETIME(6) | Yes | Thời điểm UTC bắt đầu processing gần nhất |
| completed_at | DATETIME(6) | Yes | Thời điểm UTC đạt terminal state |
| lease_owner | VARCHAR(100) | Yes | Worker instance |
| lease_until | DATETIME(6) | Yes | Hạn lease UTC để worker khác có thể reclaim |
| last_error_code | VARCHAR(100) | Yes | Sanitized |
| last_error_message | VARCHAR(1000) | Yes | Không chứa secret/raw payload |
| correlation_id | VARCHAR(64) | No | Correlation với request/log, không chứa secret |
| updated_at | DATETIME(6) | No | Dùng cho polling/audit state |
| row_version | BIGINT UNSIGNED | No | Concurrency |
| ...TA | | | Kế thừa center_id, created_at và created_by tại mục 2.3 |

Indexes/constraints:

- UX(center_id, attempt_id).
- IX(status, available_at, lease_until).
- IX(center_id, status, created_at).
- CHECK retry_count BETWEEN 0 AND 3.
- CHECK status IN (Pending, Processing, Completed, FallbackCompleted, FailedTerminal).

Recovery:

- Processing với lease_until < UTC now được reclaim.
- Job terminal không được xử lý lại.
- Unique attempt_id bảo đảm idempotency.

# Module 6 — Dynamic Authorization & Evidence Governance [Active — đã migration và kiểm tra trên MySQL]

## 36. permissions [System catalog, không có center_id]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| permission_id | VARCHAR(36) | No | PK; deterministic Guid do source/migration quản lý |
| permission_code | VARCHAR(100) | No | Stable code dạng module.resource.action |
| module_name | VARCHAR(64) | No | Nhóm capability |
| resource_name | VARCHAR(64) | No | Resource được bảo vệ |
| action_name | VARCHAR(32) | No | read, create, update, delete, assign, review, manage |
| description | VARCHAR(500) | No | Mô tả tiếng Việt cho màn hình quản trị |
| is_sensitive | TINYINT(1) | No | Cảnh báo quyền có khả năng nâng đặc quyền |
| is_delegable | TINYINT(1) | No | CenterManager có được gán quyền này hay không |
| status | VARCHAR(32) | No | Active, Deprecated |
| created_at | DATETIME(6) | No | UTC |
| updated_at | DATETIME(6) | No | UTC |

Indexes/constraints:

- PK(permission_id).
- UX(permission_code).
- IX(module_name, resource_name, action_name, status).
- CHECK status IN (Active, Deprecated).

Invariant:

- Permission catalog do source và migration định nghĩa; không có Platform Admin hoặc UI tạo permission tùy ý.
- permission_code không được đổi sau khi phát hành; dùng Deprecated và tạo code mới khi semantics thay đổi.
- Mỗi permission phải có ít nhất một row trong permission_account_types; không lưu danh sách loại tài khoản trong JSON vì quan hệ này cần join và foreign key.
- CenterManager chỉ được gán permission Active, is_delegable = 1 và tương thích target role. Nếu target role có account_type = CenterManager, permission mới còn phải nằm trong effective permission của actor; role Student/Teacher không áp dụng điều kiện actor-own vì CenterManager không thể sở hữu permission khác account type.

## 37. permission_account_types [System catalog join, không có center_id]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| permission_id | VARCHAR(36) | No | FK permissions global catalog |
| account_type | VARCHAR(32) | No | Student, Teacher, CenterManager hoặc PlatformAdmin |
| created_at | DATETIME(6) | No | UTC; do migration/seed quản lý |

Indexes/constraints:

- PK(permission_id, account_type).
- FK(permission_id) → permissions(permission_id) ON DELETE RESTRICT.
- IX(account_type, permission_id).
- CHECK account_type IN (Student, Teacher, CenterManager, PlatformAdmin).

Invariant:

- Catalog source định nghĩa loại tài khoản nào có thể nhận từng permission; UI không sửa trực tiếp bảng này.
- Mapping bootstrap phải khớp mục 66 API_CONTRACTS.md; thay đổi mapping là contract + migration change.
- API tổng hợp các row thành allowedAccountTypes; array API không phải nguồn dữ liệu JSON trong database.
- Xóa mapping đang được role_permissions tham chiếu phải bị RESTRICT.

## 38. roles [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| role_id | VARCHAR(36) | No | PK |
| role_code | VARCHAR(64) | No | Stable trong Center |
| role_name | VARCHAR(150) | No | Tên hiển thị |
| account_type | VARCHAR(32) | No | Student, Teacher, CenterManager hoặc PlatformAdmin |
| description | VARCHAR(500) | Yes | Phạm vi trách nhiệm |
| is_system_role | TINYINT(1) | No | Role bootstrap được bảo vệ |
| status | VARCHAR(32) | No | Active, Archived |
| ...MTA | | | Theo mục 2.3 |

Indexes/constraints:

- PK(role_id).
- UX(center_id, role_id).
- UX(center_id, role_id, account_type) để làm principal key cho role_permissions và user_roles.
- UX(center_id, role_code).
- IX(center_id, account_type, status, role_name).
- CHECK status IN (Active, Archived).
- CHECK account_type IN (Student, Teacher, CenterManager, PlatformAdmin).

Invariant:

- Role chỉ có hiệu lực trong đúng một Center.
- account_type của role immutable; role chỉ nhận permission có row tương ứng trong permission_account_types.
- Role tenant administrator bootstrap không được archive nếu sẽ làm Center không còn administrator hợp lệ.
- is_system_role không đồng nghĩa quyền global; role vẫn tenant-scoped.
- TenantAdminCorePermissionsV1 là chín permission authorization.permissions.read, authorization.roles.read/create/update/archive/manage_permissions, authorization.user_roles.read/assign và authorization.audit.read.

## 39. role_permissions [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator |
| role_id | VARCHAR(36) | No | Tenant-safe FK roles |
| permission_id | VARCHAR(36) | No | FK permissions global catalog |
| account_type | VARCHAR(32) | No | Bản sao có kiểm soát từ role; client không được gửi |
| granted_at | DATETIME(6) | No | UTC |
| granted_by_user_id | VARCHAR(36) | No | Tenant-safe FK users |

Indexes/constraints:

- PK(center_id, role_id, permission_id).
- FK(center_id, role_id, account_type) → roles(center_id, role_id, account_type).
- FK(permission_id, account_type) → permission_account_types(permission_id, account_type).
- FK(center_id, granted_by_user_id) → users(center_id, user_id).
- IX(center_id, permission_id, role_id).
- CHECK account_type IN (Student, Teacher, CenterManager, PlatformAdmin).

Invariant:

- Không gán permission Deprecated hoặc is_delegable = 0 qua UI.
- account_type được BLL lấy từ role, không nhận từ request; hai composite FK buộc role và permission tương thích ngay tại database.
- Grant/revoke phải cập nhật users.auth_version theo phạm vi ảnh hưởng và ghi authorization_audit_logs trong cùng transaction.

## 40. user_roles [Tenant current-state join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | Tenant discriminator |
| user_id | VARCHAR(36) | No | Tenant-safe FK users |
| role_id | VARCHAR(36) | No | Tenant-safe FK roles |
| account_type | VARCHAR(32) | No | Bản sao có kiểm soát từ user/role; client không được gửi |
| status | VARCHAR(32) | No | Active, Revoked |
| assigned_at | DATETIME(6) | No | UTC |
| assigned_by_user_id | VARCHAR(36) | No | Tenant-safe FK users |
| revoked_at | DATETIME(6) | Yes | UTC |
| revoked_by_user_id | VARCHAR(36) | Yes | Tenant-safe FK users |
| revoke_reason | VARCHAR(500) | Yes | Bắt buộc khi revoke |
| row_version | BIGINT UNSIGNED | No | Optimistic concurrency |

Indexes/constraints:

- PK(center_id, user_id, role_id).
- FK(center_id, user_id, account_type) → users(center_id, user_id, role_name).
- FK(center_id, role_id, account_type) → roles(center_id, role_id, account_type).
- FK actor columns → users(center_id, user_id).
- IX(center_id, role_id, status, user_id).
- IX(center_id, user_id, status).
- CHECK status IN (Active, Revoked).
- CHECK account_type IN (Student, Teacher, CenterManager, PlatformAdmin).

Invariant:

- User và role phải cùng Center.
- account_type được BLL lấy từ user/role, không nhận từ request; hai composite FK buộc users.role_name và roles.account_type trùng nhau ngay tại database.
- Mỗi lần assign/revoke phải tăng users.auth_version và ghi audit trong cùng transaction.
- Cấm tự gán quyền, cấp role vượt quá quyền của actor hoặc làm Center không còn ít nhất một CenterManager Active có đủ TenantAdminCorePermissionsV1.

## 41. authorization_audit_logs [TA append-only]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| authorization_audit_id | BIGINT UNSIGNED | No | PK, auto increment |
| actor_user_id | VARCHAR(36) | Yes | Null chỉ cho bootstrap/migration có source rõ ràng |
| action_type | VARCHAR(64) | No | RoleCreated, PermissionGranted, UserRoleAssigned, ... |
| target_type | VARCHAR(64) | No | Role, RolePermission, UserRole |
| target_id | VARCHAR(128) | No | Canonical target identifier |
| target_user_id | VARCHAR(36) | Yes | User chịu ảnh hưởng nếu có |
| permission_code | VARCHAR(100) | Yes | Capability liên quan nếu có |
| before_data | JSON | Yes | Snapshot trước thay đổi, đã redaction |
| after_data | JSON | Yes | Snapshot sau thay đổi, đã redaction |
| reason | VARCHAR(1000) | No | Lý do nghiệp vụ |
| trace_id | VARCHAR(64) | No | Correlation với request/log |
| ...TA | | | Theo mục 2.3 |

Indexes/constraints:

- PK(authorization_audit_id).
- UX(center_id, authorization_audit_id).
- IX(center_id, created_at, action_type).
- IX(center_id, actor_user_id, created_at).
- IX(center_id, target_user_id, created_at).
- Tenant-safe FK actor/target user khi khác null.

Invariant:

- Append-only; không update, soft delete hoặc hard delete trong business flow.
- Không lưu password, token, secret hoặc raw authorization header trong JSON.
- Audit failure làm rollback thay đổi authorization tương ứng.

## 42. evidence_assessments [TA append-only]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| evidence_assessment_id | BIGINT UNSIGNED | No | PK, auto increment |
| attempt_id | BIGINT UNSIGNED | No | Tenant-safe FK attempts |
| analysis_id | BIGINT UNSIGNED | Yes | Tenant-safe FK reasoning_analyses; null khi chưa có AI output |
| supersedes_assessment_id | BIGINT UNSIGNED | Yes | Bản đánh giá trước bị thay thế khi replay |
| source_type | VARCHAR(32) | No | AI, RuleFallback, TeacherOverride |
| trust_level | VARCHAR(32) | No | Trusted, Reduced, ReviewOnly |
| decision_mode | VARCHAR(32) | No | AIWeighted, DeterministicOnly, HumanConfirmed |
| reasoning_weight | DECIMAL(4,3) | No | 0.000–1.000 |
| reason_codes | JSON | No | Array code deterministic, không chỉ là free text |
| requires_teacher_review | TINYINT(1) | No | Default 0 |
| policy_version | VARCHAR(32) | No | Ví dụ evidence-gate-v1 |
| analysis_override_version | INT UNSIGNED | No | Version của reasoning_analysis được Gate dùng; 0 cho bản gốc |
| evaluated_at | DATETIME(6) | No | UTC |
| ...TA | | | Theo mục 2.3 |

Indexes/constraints:

- PK(evidence_assessment_id).
- UX(center_id, evidence_assessment_id).
- UX(center_id, evidence_assessment_id, attempt_id), alternate key cho self-FK supersession cùng Attempt.
- IX(center_id, attempt_id, evaluated_at).
- IX(center_id, requires_teacher_review, evaluated_at).
- FK(center_id, attempt_id) → attempts(center_id, attempt_id).
- FK(center_id, analysis_id, attempt_id) → reasoning_analyses(center_id, analysis_id, attempt_id) khi analysis_id khác null.
- FK(center_id, supersedes_assessment_id, attempt_id) → evidence_assessments(center_id, evidence_assessment_id, attempt_id) khi supersedes_assessment_id khác null.
- CHECK reasoning_weight BETWEEN 0 AND 1.
- CHECK trust_level IN (Trusted, Reduced, ReviewOnly).
- CHECK source_type IN (AI, RuleFallback, TeacherOverride).
- CHECK decision_mode IN (AIWeighted, DeterministicOnly, HumanConfirmed).
- Trigger `tr_evidence_no_self_supersede` từ chối insert tự supersede. MySQL không cho CHECK tham chiếu cột AUTO_INCREMENT, nên invariant này dùng trigger thay vì CHECK.
- Trigger `tr_evidence_append_only_update` và `tr_evidence_append_only_delete` chặn UPDATE/DELETE để database tự bảo vệ lịch sử append-only.

Invariant:

- Mỗi lần đánh giá/replay tạo row mới; không sửa lịch sử cũ.
- analysis_id nếu có phải thuộc đúng attempt_id của EvidenceAssessment; supersedes_assessment_id nếu có phải thuộc cùng attempt_id. BLL kiểm tra invariant trước insert và MySQL integration test phải chứng minh cả hai composite FK từ chối mismatch dù cùng Center.
- Fallback hoặc ReviewOnly có reasoning_weight = 0 và không làm đổi Knowledge Mastery.
- Preliminary is_correct null bắt buộc ReviewOnly, requires_teacher_review = 1 và không được coerce thành false/neutral; TeacherOverride/HumanConfirmed mới tạo effective correctness cho replay.
- Mapping v1: AI → AIWeighted; RuleFallback → ReviewOnly + DeterministicOnly; TeacherOverride → Trusted + HumanConfirmed.
- Replay là event_source ở twin_update_history, không phải source_type/trust_level/decision_mode ở evidence_assessments.
- TeacherOverride bắt buộc có analysis_override_version > 0, audit nguồn, actor và lý do ở reasoning_analyses/twin history liên quan.
- policy_version và reason_codes phải đủ để tái lập quyết định Gate.
- Bảng chỉ lưu policy decision/provenance; không copy feedback, mastery delta hoặc calculation breakdown từ analysis/history.

## 43. attempt_attachments [TA - Bảng vật lý mục tiêu thứ 40 sau Gate 5]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| id | BIGINT UNSIGNED | No | PK, auto increment |
| center_id | VARCHAR(36) | No | Tenant discriminator |
| attempt_id | BIGINT UNSIGNED | No | Tenant-safe FK attempts; quan hệ 1:1 |
| upload_nonce | VARCHAR(64) | No | Nonce định danh luồng upload đính kèm |
| storage_key | VARCHAR(256) | No | Đường dẫn tương đối lưu trữ blob |
| file_size_bytes | INT UNSIGNED | No | Dung lượng file nhị phân (1..5,242,880 bytes) |
| content_type | VARCHAR(64) | No | MIME type bắt buộc image/png |
| sha256_hash | VARCHAR(64) | No | Mã băm SHA-256 xác thực tính toàn vẹn của blob |
| created_at | DATETIME(6) | No | UTC; thời điểm nộp bài và promote blob |
| created_by | VARCHAR(36) | Yes | Actor user ID (học sinh nộp bài) |

Indexes/constraints:

- PK(id).
- UX(center_id, attempt_id) — quan hệ 1:1 giữa Attempt và minh chứng đính kèm.
- UX(center_id, upload_nonce) — unique key theo tenant, thẩm quyền tối cao giải quyết race condition khi submit.
- UX(center_id, storage_key) — ngăn ngừa trùng lặp đường dẫn lưu trữ trong tenant.
- FK(center_id, attempt_id) → attempts(center_id, attempt_id) ON DELETE RESTRICT.
- CONSTRAINT ck_attempt_attachments_file_size_bytes CHECK (file_size_bytes >= 1 AND file_size_bytes <= 5242880).
- CONSTRAINT ck_attempt_attachments_content_type CHECK (content_type = 'image/png').

Invariant:

- Mỗi Attempt chỉ có tối đa một bản vẽ đính kèm; lưu trữ và liên kết được thiết lập nguyên tử trong transaction nộp bài.
- upload_nonce được trích xuất từ signed Data Protection token và là nguồn phân xử duy nhất cho các request nộp bài đồng thời: request nào ghi CSDL trước sẽ thắng, request sau vi phạm unique constraint (MySQL Error 1062) sẽ nhận HTTP 409 UPLOAD_TOKEN_ALREADY_USED.
- storage_key có cấu trúc xác định: `tenants/{centerId}/attempt-attachments/{uploadNonce}.png`.
- Tải ảnh minh chứng qua API yêu cầu quyền sở hữu của học sinh hoặc giáo viên phụ trách bài tập (xác thực qua `IAttemptTeacherReviewScopeGuard`); bài tự do fail closed trả về HTTP 404.

## 44. Structured AI output contract lưu vào reasoning_analyses

Payload hợp lệ trước khi persistence:

~~~json
{
  "schemaVersion": "ai-analysis-v1",
  "language": "vi",
  "methodDetected": "Mô tả ngắn",
  "reasoningQuality": 72,
  "errorType": "Reasoning",
  "misconception": "Mô tả hiểu sai hoặc null",
  "missingSteps": ["Bước còn thiếu"],
  "rootCauseNodeIds": ["101"],
  "confidence": 85,
  "feedback": "Phản hồi cùng ngôn ngữ với học sinh"
}
~~~

Semantic validation:

- schemaVersion phải đúng.
- language là vi hoặc en.
- reasoningQuality và confidence là integer 0–100.
- errorType thuộc allow-list.
- rootCauseNodeIds tồn tại, cùng Center/Subject và active.
- feedback không rỗng.
- Không chấp nhận field thừa nếu parser được cấu hình strict.

## 45. Invariant liên module

### 43.1. Submit Attempt

- Student thuộc Center hiện tại.
- Question active, cùng Center.
- Nếu có Assignment: Student là target, Question thuộc Assignment, Assignment Published/không Closed.
- reasoning_text bắt buộc nếu Question yêu cầu.
- client_submission_id bảo đảm retry HTTP không tạo Attempt trùng.
- Transaction đầu chỉ lưu Attempt + Job + Progress.

### 43.2. Hoàn tất AI Job

Sau khi AI output hợp lệ hoặc fallback đã được dựng, Evidence Gate phải chạy structural/semantic/contradiction checks trước confidence và phân loại ba chiều trước khi transaction mutation bắt đầu. Transaction phải:

1. Insert reasoning_analyses.
2. Insert evidence_assessments với policy version và reason code.
3. Update attempts.status.
4. Upsert knowledge_twins chỉ khi reasoning_weight > 0.
5. Upsert behavior_twins từ dữ liệu quan sát được.
6. Update student_subject_goals predicted/risk nếu effective mastery thay đổi.
7. Insert twin_update_history, kể cả sự kiện ReviewOnly không đổi mastery.
8. Supersede Recommendation cũ và insert Recommendation mới nếu đầu vào hiệu lực thay đổi.
9. Regenerate/replace Learning Path active nếu cần.
10. Update student_assignment_progress.
11. Mark ai_analysis_jobs terminal.

Nếu transaction rollback, job không được đánh Completed.

### 43.3. Teacher Override

- Teacher có permission review phù hợp và resource scope tới Student; account type CenterManager không tự động thay thế permission sau cutover.
- Update override fields dùng row_version/override_version.
- Replay Attempts của Student trong Topic theo created_at, attempt_id.
- Rebuild Knowledge Twin từ baseline 0.
- Recompute Behavior, predicted score, risk và recommendation.
- Insert History event TeacherOverride/Replay.
- Toàn bộ nằm trong một transaction.

## 45. Index chiến lược

Ngoài index từng table, bắt buộc review EXPLAIN cho các query:

- Student dashboard theo student_id + subject_id.
- Teacher dashboard theo class_id.
- Center dashboard group theo center_id + subject_id/class_id.
- Pending AI job theo status + available_at.
- Twin history theo student + subject + created_at.
- Recommendation active theo student + subject + status.
- Assignment progress theo assignment + status.
- High-risk list theo subject/class và risk_score.

Không index mọi cột. Mỗi index phải gắn với query cụ thể trong API_CONTRACTS.md.

## 46. Global Query Filter

Áp dụng cho:

- Mọi table có center_id.
- MTA: center_id hiện tại AND is_deleted = 0.
- TA: center_id hiện tại.
- roles, role_permissions, user_roles, authorization_audit_logs và evidence_assessments luôn scope theo center_id.

Ngoại lệ:

- centers được load trong login bằng center_code trước khi JWT tồn tại.
- Login lookup users phải được scope bằng center_id lấy từ center_code.
- BackgroundService tạo explicit tenant scope từ job.center_id.

Không dùng request-provided center_id để khởi tạo filter.

## 47. Seed Data

Seed phải deterministic và idempotent.

### 46.1. Tenant

- Center A: dữ liệu demo chính.
- Center B: dữ liệu cách ly để chứng minh không cross-tenant.
- Mỗi Center có một CenterManager seed.
- Mỗi Center có role tenant administrator bootstrap và role mẫu Teacher/Student phù hợp account type.
- Permission catalog và permission_account_types dùng deterministic IDs/codes và seed idempotent từ source.
- Credentials chỉ dùng Development và lấy password từ environment/config seed, không ghi password thật vào repository.

### 46.2. Academic data

Hai Subject logic:

- Toán: Hàm số, Mũ–Logarit, Nguyên hàm.
- Tiếng Anh: Thì, Mệnh đề quan hệ, Từ vựng theo ngữ cảnh.

Mỗi Topic có:

- exam_importance.
- estimated_learning_minutes.
- order_index.
- prerequisite edge hợp lệ.
- 5 Question đa dạng difficulty/type.

Có 30 logical question definitions. Để dữ liệu vẫn tenant-owned, cùng bộ template có thể được clone vào cả hai Center; số row vật lý khi clone hai tenant là 60 nhưng nội dung logic vẫn là 30 câu.

Mỗi Center seed:

- 1 CenterManager.
- Tối thiểu 2 Teacher.
- Tối thiểu 2 Class.
- Tối thiểu 5 Student bằng Bogus với fixed random seed.
- Class membership và Subject Goal.

Không seed Attempt/Twin ở baseline chính nếu demo cần thể hiện thay đổi từ 0%; có thể có profile demo phụ chứa lịch sử mẫu, nhưng phải được gắn nhãn rõ.

## 48. Migration policy

- Migration 001: Tenant + Identity + Organization.
- Migration 002: Knowledge Graph.
- Migration 003: Curriculum + Questions + Assignments.
- Migration 004: Digital Twin + Personalization.
- Migration 005: Assessment + AI Jobs.
- Migration 006: Seed reference/demo data nếu tách khỏi runtime seeder.
- Migration 007: Dynamic Authorization (permissions, permission_account_types, roles, role_permissions, user_roles, authorization_audit_logs) và backfill role từ role_name.
- Migration 008: Evidence Governance (evidence_assessments) và backfill policy theo dữ liệu lịch sử đã được duyệt.

Tên migration thực tế phải diễn đạt nội dung, không dùng tên ngẫu nhiên.

Quy tắc:

- Không sửa migration đã merge vào main.
- Mọi schema change sau baseline cần Change Proposal.
- Migration phải chạy được từ database trống.
- Development reset chỉ được thực hiện có chủ ý; không tự drop database khi API start.
- Production-like startup không auto-apply destructive migration.
- Migration 007 phải tạo role/assignment tương đương trước cutover; không được tạo khoảng thời gian user mất quyền hoặc được quyền rộng hơn.
- Validation Migration 007 phải chứng minh không có permission thiếu permission_account_types và không có role_permissions/user_roles lệch account_type.
- Migration 008 không được tự suy diễn trust cho lịch sử thiếu dữ liệu; mặc định ReviewOnly và đánh dấu provenance backfill.
- Mọi migration v2 phải có validation query, backup/rollback procedure và chạy thử trên MySQL thật.

## 49. Data validation matrix

| Rule | DB | BLL | API |
|---|:---:|:---:|:---:|
| Percentage 0–100 | Có | Có | Có |
| Role/status enum | Có | Có | Có |
| Tenant ownership | Composite FK | Bắt buộc | Không nhận center_id |
| Knowledge cycle | Không | Bắt buộc | Trả 409 |
| Topic node type | Khó enforce | Bắt buộc | Validation error |
| One correct MC option | Không đầy đủ | Bắt buộc | Validation error |
| Reasoning required | Không | Bắt buộc | Validation error |
| Teacher owns Class | FK một phần | Bắt buộc | 404/403 theo policy |
| AI JSON schema | JSON validity | Bắt buộc | Không expose raw |
| Job state transition | CHECK | Bắt buộc | Read-only status |
| Override completeness | Một phần | Bắt buộc | Validation error |
| Role và user cùng Center | Composite FK | Bắt buộc | Không nhận center_id |
| Role và user cùng account type | Composite FK qua account_type | Bắt buộc | Không nhận account_type |
| Role và permission tương thích account type | Composite FK qua permission_account_types | Bắt buộc | 400 mismatch |
| Actor chỉ cấp quyền đang sở hữu | Không đầy đủ | Bắt buộc | 403/409 theo contract |
| Không mất tenant administrator cuối | Không đầy đủ | Transaction bắt buộc | 409 |
| Authorization audit append-only | FK/CHECK | Transaction bắt buộc | Không expose raw JSON |
| Evidence weight 0–1 | CHECK | Bắt buộc | Read-only result |
| Fallback không đổi Knowledge Mastery | Không đầy đủ | Bắt buộc | Không cho client override |

## 50. Không được thêm trong MVP

Không tạo table cho:

- AI raw logs/token ledger.
- Payment/subscription.
- Video/resource recommendation.
- OCR document/page.
- Vector embedding.
- Exam session/exam behavior.
- Teacher Twin/Center Twin.
- Notification/email.
- Chat history.

Nếu AI Developer cho rằng cần table mới, phải tạo Change Proposal; không tự ý tạo migration.

## 51. Checklist nghiệm thu schema

- [x] Baseline 31 bảng và 7 bảng Module 6 được đối chiếu với migration-generated SQL và MySQL information_schema.
- [x] Target v2 đủ 6 module và 38 bảng đã được migration và nghiệm thu trên MySQL thật.
- [ ] Mọi tenant-owned table có center_id.
- [ ] Composite tenant FK được cấu hình tại quan hệ nhạy cảm.
- [ ] Global Query Filter gồm tenant + soft delete.
- [ ] GUID/BIGINT đúng Hybrid PK Strategy.
- [ ] CHECK constraint và unique index đúng bảng.
- [ ] Delete behavior không làm mất Attempt/Analysis/History.
- [ ] DAG cycle validator tồn tại và được test.
- [ ] AI job unique theo Attempt và recover được lease hết hạn.
- [ ] Teacher Override giữ nguyên AI output gốc.
- [ ] Role/permission/user-role không thể cross-tenant, không thể lệch account type và không tạo privilege escalation.
- [ ] Mọi permission có ít nhất một permission_account_types row và không có mapping ngoài enum.
- [ ] Tenant administrator cuối cùng được bảo vệ bằng transaction/concurrency test.
- [ ] Authorization audit là append-only và cùng transaction với thay đổi quyền.
- [ ] Evidence Gate lưu policy version/reason code và fallback không đổi Knowledge Mastery.
- [ ] Seed hai Center không rò dữ liệu chéo.
- [ ] 30 logical questions bao phủ hai Subject và ba loại câu hỏi.
- [ ] Migration chạy được từ database trống.
- [ ] Migration v2 và query/index trọng yếu được kiểm tra trên MySQL thật, không chỉ EF InMemory/SQLite.
