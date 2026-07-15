# EduTwin — Database Schema

> Phiên bản: 1.0  
> Trạng thái: FROZEN — Data First Baseline  
> Database: MySQL 8.x / InnoDB / utf8mb4  
> ORM: Entity Framework Core 10  

## 1. Mục tiêu thiết kế

Schema phục vụ đồng thời năm mục tiêu:

1. Cách ly dữ liệu Multi-tenant theo Center.
2. Lưu bằng chứng học tập thay vì chỉ lưu điểm tổng.
3. Cập nhật Learning Digital Twin có lịch sử và khả năng giải thích.
4. Hỗ trợ AI thất bại mà luồng học vẫn hoàn thành bằng Rule-based fallback.
5. Cho phép Teacher Override và tái tính toán deterministic.

Schema được chia thành đúng năm module logic:

1. System Users & Organization.
2. Knowledge Graph.
3. Curriculum, Question Bank & Assignments.
4. Digital Twin & Personalization.
5. Assessment & AI Reasoning.

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
~~~

# Module 1 — System Users & Organization

## 4. centers [Mutable root, không có center_id]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | PK |
| center_code | VARCHAR(32) | No | Unique, uppercase business code |
| center_name | VARCHAR(200) | No | |
| status | VARCHAR(32) | No | Active, Suspended |
| timezone | VARCHAR(64) | No | Default Asia/Bangkok |
| created_at | DATETIME(6) | No | |
| updated_at | DATETIME(6) | No | |
| is_deleted | TINYINT(1) | No | Default 0 |
| deleted_at | DATETIME(6) | Yes | |
| row_version | BIGINT UNSIGNED | No | Default 1 |

Indexes/constraints:

- PK(center_id).
- UX(center_code).
- CHECK status IN (Active, Suspended).

Invariant:

- Center bị Suspended không được login/refresh hoặc tạo job mới.
- MVP không có endpoint xóa Center.

## 5. users [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| user_id | VARCHAR(36) | No | PK |
| username | VARCHAR(100) | No | Unique trong Center |
| password_hash | VARCHAR(500) | No | Không lưu password |
| role_name | VARCHAR(32) | No | Student, Teacher, CenterManager |
| display_name | VARCHAR(200) | No | |
| status | VARCHAR(32) | No | Active, Locked, Disabled |
| last_login_at | DATETIME(6) | Yes | |
| auth_version | INT UNSIGNED | No | Default 1; tăng để revoke token diện rộng |
| ...MTA | | | Theo mục 2.3 |

Indexes/constraints:

- PK(user_id).
- UX(center_id, username).
- UX(center_id, user_id).
- IX(center_id, role_name, status).
- CHECK role_name IN (Student, Teacher, CenterManager).
- CHECK status IN (Active, Locked, Disabled).

Invariant:

- Một User chỉ thuộc một Center.
- Không đổi role_name sau khi đã có profile; nếu cần phải qua use case riêng và migration data được duyệt.

## 6. refresh_tokens [TA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| refresh_token_id | BIGINT UNSIGNED | No | PK, auto increment |
| user_id | VARCHAR(36) | No | Tenant-safe FK users |
| token_hash | CHAR(64) | No | SHA-256 hoặc hash tương đương; unique |
| expires_at | DATETIME(6) | No | |
| revoked_at | DATETIME(6) | Yes | |
| replaced_by_token_id | BIGINT UNSIGNED | Yes | Self FK |
| revoke_reason | VARCHAR(200) | Yes | |
| created_by_ip | VARCHAR(64) | Yes | |
| revoked_by_ip | VARCHAR(64) | Yes | |
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
| department | VARCHAR(150) | Yes | |
| bio | VARCHAR(500) | Yes | |
| ...MTA | | | |

Constraints:

- PK(teacher_id).
- UX(center_id, teacher_id).
- FK(center_id, teacher_id) → users(center_id, user_id).
- User tương ứng phải có role Teacher; kiểm tra ở BLL.

## 8. students [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| student_id | VARCHAR(36) | No | PK và tenant-safe FK users.user_id |
| full_name | VARCHAR(200) | No | |
| grade_level | TINYINT UNSIGNED | No | 10, 11 hoặc 12 |
| date_of_birth | DATE | Yes | Mock Data |
| ...MTA | | | |

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
| description | VARCHAR(500) | Yes | |
| is_active | TINYINT(1) | No | Default 1 |
| ...MTA | | | |

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
| class_name | VARCHAR(150) | No | |
| academic_year | VARCHAR(20) | No | Ví dụ 2026-2027 |
| status | VARCHAR(32) | No | Active, Archived |
| ...MTA | | | |

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
| center_id | VARCHAR(36) | No | |
| class_id | VARCHAR(36) | No | Tenant-safe FK classes |
| student_id | VARCHAR(36) | No | Tenant-safe FK students |
| joined_at | DATETIME(6) | No | |
| status | VARCHAR(32) | No | Active, Removed |
| removed_at | DATETIME(6) | Yes | |
| created_by | VARCHAR(36) | Yes | |

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
| node_name | VARCHAR(200) | No | |
| description | TEXT | Yes | |
| order_index | INT UNSIGNED | No | Default 0 |
| exam_importance | DECIMAL(5,2) | No | 0–100; chủ yếu dùng Topic |
| estimated_learning_minutes | INT UNSIGNED | No | Minimum 1 |
| is_active | TINYINT(1) | No | Default 1 |
| ...MTA | | | |

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
| ...MTA | | | |

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
| title | VARCHAR(250) | No | |
| description | TEXT | Yes | |
| source_file | VARCHAR(500) | Yes | Reserved; MVP không upload |
| review_status | VARCHAR(32) | No | Draft, Published, Archived |
| ...MTA | | | |

Indexes:

- UX(center_id, curriculum_id).
- IX(center_id, teacher_id, review_status).
- IX(center_id, subject_id, review_status).

## 15. curriculum_classes [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | |
| curriculum_id | VARCHAR(36) | No | Tenant-safe FK |
| class_id | VARCHAR(36) | No | Tenant-safe FK |
| assigned_at | DATETIME(6) | No | |
| assigned_by | VARCHAR(36) | No | |

- PK(center_id, curriculum_id, class_id).
- Curriculum, Class và Subject phải tương thích.

## 16. curriculum_nodes [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | |
| curriculum_id | VARCHAR(36) | No | |
| node_id | BIGINT UNSIGNED | No | |
| order_index | INT UNSIGNED | No | |
| created_at | DATETIME(6) | No | |

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
| ...MTA | | | |

Indexes/constraints:

- UX(center_id, question_id).
- IX(center_id, subject_id, primary_topic_node_id, status, difficulty).
- IX(center_id, created_by_teacher_id, status).
- CHECK question_type IN (MultipleChoice, ShortAnswer, Essay).
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
| option_text | TEXT | No | |
| is_correct | TINYINT(1) | No | |
| order_index | INT UNSIGNED | No | |
| ...MTA | | | |

Indexes:

- UX(center_id, question_id, option_label).
- UX(center_id, question_id, order_index).

BLL invariant:

- Chỉ MultipleChoice có options.
- Active MultipleChoice có ít nhất 2 options và đúng 1 option correct trong MVP.

## 19. question_knowledge_nodes [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | |
| question_id | BIGINT UNSIGNED | No | |
| node_id | BIGINT UNSIGNED | No | |
| mapping_role | VARCHAR(32) | No | Primary, Secondary, Prerequisite |
| created_at | DATETIME(6) | No | |

- PK(center_id, question_id, node_id, mapping_role).
- CHECK mapping_role IN (Primary, Secondary, Prerequisite).
- Có đúng một Primary mapping trùng primary_topic_node_id.

## 20. assignments [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| assignment_id | VARCHAR(36) | No | PK |
| class_id | VARCHAR(36) | No | Tenant-safe FK |
| created_by_teacher_id | VARCHAR(36) | No | Phải là Teacher của Class |
| title | VARCHAR(250) | No | |
| instructions | TEXT | Yes | |
| due_at | DATETIME(6) | Yes | UTC |
| status | VARCHAR(32) | No | Draft, Published, Closed, Archived |
| published_at | DATETIME(6) | Yes | |
| ...MTA | | | |

Indexes:

- UX(center_id, assignment_id).
- IX(center_id, class_id, status, due_at).
- CHECK status IN (Draft, Published, Closed, Archived).

## 21. assignment_questions [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | |
| assignment_id | VARCHAR(36) | No | |
| question_id | BIGINT UNSIGNED | No | |
| order_index | INT UNSIGNED | No | |
| points | DECIMAL(5,2) | No | > 0 |
| created_at | DATETIME(6) | No | |

- PK(center_id, assignment_id, question_id).
- UX(center_id, assignment_id, order_index).
- Question Subject phải trùng Class Subject.

## 22. assignment_targets [Tenant join]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| center_id | VARCHAR(36) | No | |
| assignment_id | VARCHAR(36) | No | |
| student_id | VARCHAR(36) | No | |
| target_source | VARCHAR(32) | No | WholeClass, SelectedStudents, GapGroup |
| created_at | DATETIME(6) | No | |
| created_by | VARCHAR(36) | No | |

- PK(center_id, assignment_id, student_id).
- CHECK target_source IN (WholeClass, SelectedStudents, GapGroup).
- Student phải là active member của Class khi publish.
- Target được materialize để membership thay đổi sau này không làm mất lịch sử giao bài.

## 23. student_assignment_progress [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| progress_id | BIGINT UNSIGNED | No | PK |
| assignment_id | VARCHAR(36) | No | |
| student_id | VARCHAR(36) | No | |
| status | VARCHAR(32) | No | NotStarted, InProgress, Completed, Overdue |
| completed_question_count | INT UNSIGNED | No | Default 0 |
| total_question_count | INT UNSIGNED | No | Snapshot |
| started_at | DATETIME(6) | Yes | |
| completed_at | DATETIME(6) | Yes | |
| ...MTA | | | |

- UX(center_id, assignment_id, student_id).
- IX(center_id, student_id, status).
- CHECK completed_question_count <= total_question_count.

# Module 4 — Digital Twin & Personalization

## 24. student_subject_goals [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| goal_id | BIGINT UNSIGNED | No | PK |
| student_id | VARCHAR(36) | No | |
| subject_id | VARCHAR(36) | No | |
| target_score | DECIMAL(4,2) | No | 0–10 |
| remaining_days | INT UNSIGNED | No | 0–3650 |
| current_predicted_score | DECIMAL(4,2) | No | Default 0 |
| risk_score | DECIMAL(5,2) | No | Default 0; 0–100 |
| ...MTA | | | |

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
| last_evidence_at | DATETIME(6) | Yes | |
| ...MTA | | | |

- UX(center_id, student_id).
- CHECK overall_mastery BETWEEN 0 AND 100.

Student Twin là aggregate header; score/risk chi tiết nằm ở Subject Goal.

## 26. knowledge_twins [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| knowledge_twin_id | BIGINT UNSIGNED | No | PK |
| student_id | VARCHAR(36) | No | |
| subject_id | VARCHAR(36) | No | |
| topic_node_id | BIGINT UNSIGNED | No | Phải là Topic |
| mastery_percentage | DECIMAL(5,2) | No | Default 0 |
| evidence_count | INT UNSIGNED | No | Default 0 |
| last_reasoning_quality | DECIMAL(5,2) | Yes | null nếu fallback |
| last_attempt_id | BIGINT UNSIGNED | Yes | |
| last_evidence_at | DATETIME(6) | Yes | |
| ...MTA | | | |

- UX(center_id, student_id, topic_node_id).
- IX(center_id, subject_id, mastery_percentage).
- CHECK mastery_percentage BETWEEN 0 AND 100.
- CHECK last_reasoning_quality IS NULL OR BETWEEN 0 AND 100.

## 27. behavior_twins [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| behavior_twin_id | BIGINT UNSIGNED | No | PK |
| student_id | VARCHAR(36) | No | |
| subject_id | VARCHAR(36) | No | |
| avg_time_spent_seconds | DECIMAL(10,2) | No | Default 0 |
| skip_rate | DECIMAL(5,2) | No | 0–100 |
| change_answer_rate | DECIMAL(5,2) | No | 0–100 |
| avg_confidence | DECIMAL(5,2) | No | 0–100 |
| confidence_calibration | DECIMAL(5,2) | No | 0–100 |
| attempt_count | INT UNSIGNED | No | Default 0 |
| ...MTA | | | |

- UX(center_id, student_id, subject_id).
- CHECK các rate BETWEEN 0 AND 100.

## 28. twin_update_history [TA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| history_id | BIGINT UNSIGNED | No | PK, auto increment |
| student_id | VARCHAR(36) | No | |
| subject_id | VARCHAR(36) | No | |
| topic_node_id | BIGINT UNSIGNED | No | |
| attempt_id | BIGINT UNSIGNED | Yes | |
| analysis_id | BIGINT UNSIGNED | Yes | |
| event_source | VARCHAR(32) | No | AIAnalysis, RuleFallback, TeacherOverride, Replay |
| previous_mastery | DECIMAL(5,2) | No | |
| new_mastery | DECIMAL(5,2) | No | |
| mastery_delta | DECIMAL(6,2) | No | Có thể âm |
| effective_reasoning_quality | DECIMAL(5,2) | Yes | |
| calculation_version | VARCHAR(20) | No | Ví dụ mastery-v1 |
| calculation_breakdown | JSON | No | Input/weight/output |
| explanation | VARCHAR(1000) | No | Human-readable |
| ...TA | | | |

Indexes:

- IX(center_id, student_id, subject_id, created_at).
- IX(center_id, topic_node_id, created_at).
- IX(center_id, attempt_id).

Table append-only; replay tạo event mới, không sửa event cũ.

## 29. learning_paths [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| learning_path_id | VARCHAR(36) | No | PK |
| student_id | VARCHAR(36) | No | |
| subject_id | VARCHAR(36) | No | |
| strategy | VARCHAR(32) | No | LinearFallback, OpportunityGap |
| version | INT UNSIGNED | No | Tăng khi regenerate |
| status | VARCHAR(32) | No | Active, Superseded, Completed |
| generated_from_attempt_id | BIGINT UNSIGNED | Yes | |
| generated_at | DATETIME(6) | No | |
| ...MTA | | | |

- Chỉ một Active path cho Student + Subject; bảo đảm bằng transaction/service và filtered strategy phù hợp MySQL.
- IX(center_id, student_id, subject_id, status).

## 30. learning_path_items [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| learning_path_item_id | BIGINT UNSIGNED | No | PK |
| learning_path_id | VARCHAR(36) | No | |
| topic_node_id | BIGINT UNSIGNED | No | |
| recommended_question_id | BIGINT UNSIGNED | Yes | |
| rank_order | INT UNSIGNED | No | |
| opportunity_score | DECIMAL(5,2) | Yes | null cho linear |
| reason | VARCHAR(1000) | No | |
| status | VARCHAR(32) | No | Pending, Current, Completed, Skipped |
| ...MTA | | | |

- UX(center_id, learning_path_id, rank_order).
- UX(center_id, learning_path_id, topic_node_id).

## 31. recommendations [MTA]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| recommendation_id | BIGINT UNSIGNED | No | PK |
| student_id | VARCHAR(36) | No | |
| subject_id | VARCHAR(36) | No | |
| topic_node_id | BIGINT UNSIGNED | No | |
| question_id | BIGINT UNSIGNED | Yes | |
| recommendation_type | VARCHAR(32) | No | TopicAndQuestion, LinearFallback |
| opportunity_score | DECIMAL(5,2) | Yes | |
| calculation_version | VARCHAR(20) | No | opportunity-v1 |
| calculation_breakdown | JSON | No | |
| explanation | VARCHAR(1000) | No | |
| source_attempt_id | BIGINT UNSIGNED | Yes | |
| status | VARCHAR(32) | No | Active, Accepted, Dismissed, Superseded |
| generated_at | DATETIME(6) | No | |
| expires_at | DATETIME(6) | Yes | |
| ...MTA | | | |

- IX(center_id, student_id, subject_id, status, generated_at).
- BLL supersede recommendation cũ trong cùng transaction tạo recommendation mới.

# Module 5 — Assessment & AI Reasoning

## 32. attempts [TA + trạng thái nghiệp vụ]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| attempt_id | BIGINT UNSIGNED | No | PK, auto increment |
| student_id | VARCHAR(36) | No | |
| question_id | BIGINT UNSIGNED | No | |
| assignment_id | VARCHAR(36) | Yes | Null nếu luyện tự do |
| final_answer | LONGTEXT | No | |
| reasoning_text | LONGTEXT | Yes | Bắt buộc nếu question.reasoning_required |
| is_correct | TINYINT(1) | Yes | Preliminary deterministic grade |
| awarded_score | DECIMAL(5,2) | Yes | |
| time_spent_seconds | INT UNSIGNED | No | |
| confidence | DECIMAL(5,2) | No | 0–100 |
| answer_changes | INT UNSIGNED | No | Default 0 |
| skipped | TINYINT(1) | No | Default 0 |
| reasoning_language | VARCHAR(8) | No | vi hoặc en |
| status | VARCHAR(32) | No | PendingAnalysis, Processing, Completed, NeedsTeacherReview |
| client_submission_id | VARCHAR(36) | No | Idempotency key từ client |
| updated_at | DATETIME(6) | No | Thời điểm trạng thái thay đổi gần nhất |
| row_version | BIGINT UNSIGNED | No | Concurrency token |
| ...TA | | | |

Indexes/constraints:

- UX(center_id, student_id, client_submission_id).
- IX(center_id, student_id, question_id, created_at).
- IX(center_id, assignment_id, student_id).
- IX(center_id, status, created_at).
- CHECK confidence BETWEEN 0 AND 100.
- CHECK time_spent_seconds >= 0.
- CHECK reasoning_language IN (vi, en).

Attempts không soft delete; nếu cần loại khỏi replay phải có use case invalidate được phê duyệt trong tương lai.

## 33. reasoning_analyses [TA + override fields]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| analysis_id | BIGINT UNSIGNED | No | PK, auto increment |
| attempt_id | BIGINT UNSIGNED | No | Unique 1:1 |
| schema_version | VARCHAR(20) | No | ai-analysis-v1 |
| method_detected | VARCHAR(500) | Yes | |
| reasoning_quality | DECIMAL(5,2) | Yes | null cho fallback |
| error_type | VARCHAR(32) | No | None, Knowledge, Skill, Reasoning, Behavior, Presentation, Unknown |
| misconception | VARCHAR(1000) | Yes | |
| missing_steps | JSON | No | Array string |
| root_cause_node_ids | JSON | No | Array ID string, mỗi ID map về BIGINT và validated cùng Center |
| analysis_confidence | DECIMAL(5,2) | Yes | 0–100 |
| feedback | LONGTEXT | No | Cùng ngôn ngữ reasoning |
| is_fallback | TINYINT(1) | No | |
| needs_teacher_review | TINYINT(1) | No | |
| provider | VARCHAR(32) | No | Gemini hoặc RuleBased |
| model_name | VARCHAR(100) | Yes | |
| override_reasoning_quality | DECIMAL(5,2) | Yes | |
| override_error_type | VARCHAR(32) | Yes | |
| override_feedback | LONGTEXT | Yes | |
| override_is_correct | TINYINT(1) | Yes | |
| override_reason | VARCHAR(1000) | Yes | |
| overridden_by_teacher_id | VARCHAR(36) | Yes | |
| overridden_at | DATETIME(6) | Yes | |
| override_version | INT UNSIGNED | No | Default 0 |
| updated_at | DATETIME(6) | No | Thay đổi khi override |
| row_version | BIGINT UNSIGNED | No | Concurrency token của record |
| ...TA | | | |

Indexes/constraints:

- UX(center_id, attempt_id).
- IX(center_id, needs_teacher_review, created_at).
- CHECK quality/confidence IS NULL OR BETWEEN 0 AND 100.
- Override fields phải all-null hoặc có override_reason + teacher + time; BLL invariant.

Effective values:

- effective_reasoning_quality = override_reasoning_quality ?? reasoning_quality.
- effective_error_type = override_error_type ?? error_type.
- effective_feedback = override_feedback ?? feedback.
- effective_is_correct = override_is_correct ?? attempt.is_correct.

Không lưu raw Gemini request/response trong table này.

## 34. ai_analysis_jobs [TA + mutable state]

| Column | Type | Null | Constraint/Ý nghĩa |
|---|---|---:|---|
| analysis_job_id | BIGINT UNSIGNED | No | PK, auto increment |
| attempt_id | BIGINT UNSIGNED | No | Unique |
| status | VARCHAR(32) | No | Pending, Processing, Completed, FallbackCompleted, FailedTerminal |
| retry_count | TINYINT UNSIGNED | No | Default 0, tối đa 1 retry |
| available_at | DATETIME(6) | No | |
| started_at | DATETIME(6) | Yes | |
| completed_at | DATETIME(6) | Yes | |
| lease_owner | VARCHAR(100) | Yes | Worker instance |
| lease_until | DATETIME(6) | Yes | |
| last_error_code | VARCHAR(100) | Yes | Sanitized |
| last_error_message | VARCHAR(1000) | Yes | Không chứa secret/raw payload |
| correlation_id | VARCHAR(64) | No | |
| updated_at | DATETIME(6) | No | Dùng cho polling/audit state |
| row_version | BIGINT UNSIGNED | No | Concurrency |
| ...TA | | | |

Indexes/constraints:

- UX(center_id, attempt_id).
- IX(status, available_at, lease_until).
- IX(center_id, status, created_at).
- CHECK retry_count BETWEEN 0 AND 1.
- CHECK status IN (Pending, Processing, Completed, FallbackCompleted, FailedTerminal).

Recovery:

- Processing với lease_until < UTC now được reclaim.
- Job terminal không được xử lý lại.
- Unique attempt_id bảo đảm idempotency.

## 35. Structured AI output contract lưu vào reasoning_analyses

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

## 36. Invariant liên module

### 36.1. Submit Attempt

- Student thuộc Center hiện tại.
- Question active, cùng Center.
- Nếu có Assignment: Student là target, Question thuộc Assignment, Assignment Published/không Closed.
- reasoning_text bắt buộc nếu Question yêu cầu.
- client_submission_id bảo đảm retry HTTP không tạo Attempt trùng.
- Transaction đầu chỉ lưu Attempt + Job + Progress.

### 36.2. Hoàn tất AI Job

Sau khi AI output hợp lệ hoặc fallback đã được dựng, một transaction phải:

1. Insert reasoning_analyses.
2. Update attempts.status.
3. Upsert knowledge_twins.
4. Upsert behavior_twins.
5. Update student_subject_goals predicted/risk.
6. Insert twin_update_history.
7. Supersede Recommendation cũ và insert Recommendation mới.
8. Regenerate/replace Learning Path active nếu cần.
9. Update student_assignment_progress.
10. Mark ai_analysis_jobs terminal.

Nếu transaction rollback, job không được đánh Completed.

### 36.3. Teacher Override

- Teacher sở hữu Class chứa Student hoặc có quyền CenterManager.
- Update override fields dùng row_version/override_version.
- Replay Attempts của Student trong Topic theo created_at, attempt_id.
- Rebuild Knowledge Twin từ baseline 0.
- Recompute Behavior, predicted score, risk và recommendation.
- Insert History event TeacherOverride/Replay.
- Toàn bộ nằm trong một transaction.

## 37. Index chiến lược

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

## 38. Global Query Filter

Áp dụng cho:

- Mọi table có center_id.
- MTA: center_id hiện tại AND is_deleted = 0.
- TA: center_id hiện tại.

Ngoại lệ:

- centers được load trong login bằng center_code trước khi JWT tồn tại.
- Login lookup users phải được scope bằng center_id lấy từ center_code.
- BackgroundService tạo explicit tenant scope từ job.center_id.

Không dùng request-provided center_id để khởi tạo filter.

## 39. Seed Data

Seed phải deterministic và idempotent.

### 39.1. Tenant

- Center A: dữ liệu demo chính.
- Center B: dữ liệu cách ly để chứng minh không cross-tenant.
- Mỗi Center có một CenterManager seed.
- Credentials chỉ dùng Development và lấy password từ environment/config seed, không ghi password thật vào repository.

### 39.2. Academic data

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

## 40. Migration policy

- Migration 001: Tenant + Identity + Organization.
- Migration 002: Knowledge Graph.
- Migration 003: Curriculum + Questions + Assignments.
- Migration 004: Digital Twin + Personalization.
- Migration 005: Assessment + AI Jobs.
- Migration 006: Seed reference/demo data nếu tách khỏi runtime seeder.

Tên migration thực tế phải diễn đạt nội dung, không dùng tên ngẫu nhiên.

Quy tắc:

- Không sửa migration đã merge vào main.
- Mọi schema change sau baseline cần Change Proposal.
- Migration phải chạy được từ database trống.
- Development reset chỉ được thực hiện có chủ ý; không tự drop database khi API start.
- Production-like startup không auto-apply destructive migration.

## 41. Data validation matrix

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

## 42. Không được thêm trong MVP

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

## 43. Checklist nghiệm thu schema

- [ ] Đủ 5 module và toàn bộ table đã liệt kê.
- [ ] Mọi tenant-owned table có center_id.
- [ ] Composite tenant FK được cấu hình tại quan hệ nhạy cảm.
- [ ] Global Query Filter gồm tenant + soft delete.
- [ ] GUID/BIGINT đúng Hybrid PK Strategy.
- [ ] CHECK constraint và unique index đúng bảng.
- [ ] Delete behavior không làm mất Attempt/Analysis/History.
- [ ] DAG cycle validator tồn tại và được test.
- [ ] AI job unique theo Attempt và recover được lease hết hạn.
- [ ] Teacher Override giữ nguyên AI output gốc.
- [ ] Seed hai Center không rò dữ liệu chéo.
- [ ] 30 logical questions bao phủ hai Subject và ba loại câu hỏi.
- [ ] Migration chạy được từ database trống.
