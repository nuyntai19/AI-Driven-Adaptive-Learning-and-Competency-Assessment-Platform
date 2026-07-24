# EduTwin — API Contracts

> Phiên bản: 1.0  
> Trạng thái: FROZEN  
> Base URL: /api/v1  
> Media type: application/json; charset=utf-8  

## 1. Nguyên tắc contract

- API dùng REST-oriented contract, không trả EF Entity.
- Mọi ID được serialize thành JSON string, kể cả BIGINT, để tránh mất độ chính xác trong JavaScript.
- Client không gửi centerId. Tenant lấy từ JWT/ITenantContext.
- Timestamp dùng ISO 8601 UTC, ví dụ 2026-07-15T08:30:45.123456Z.
- Decimal được trả dưới dạng JSON number.
- Field không có giá trị dùng null; không thay null bằng chuỗi rỗng.
- Request unknown field phải bị từ chối tại contract nhạy cảm như AI output/override.
- API version chỉ thay khi có breaking change được phê duyệt.
- rowVersion được serialize thành string và bắt buộc khi update aggregate có concurrency.

## 2. Authentication transport

- Access Token trả trong response body và gửi qua Authorization: Bearer {token}.
- Refresh Token chỉ gửi bằng cookie HttpOnly tên edutwin_refresh.
- Cookie Production-like: HttpOnly, SameSite=Lax, Secure=true.
- Local HTTP Development có thể Secure=false bằng configuration.
- Frontend không lưu Refresh Token trong localStorage/sessionStorage.
- Access Token chỉ giữ trong memory/Zustand; khi reload dùng refresh endpoint.
- Axios interceptor chỉ refresh một lần cho một chuỗi request 401.

## 3. Response envelope

### 3.1. Thành công — single resource

~~~json
{
  "data": {},
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

### 3.2. Thành công — collection

~~~json
{
  "data": [],
  "meta": {
    "page": 1,
    "pageSize": 20,
    "totalItems": 0,
    "totalPages": 0,
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

### 3.3. Lỗi — Problem Details

~~~json
{
  "type": "https://edutwin.local/problems/validation",
  "title": "Dữ liệu không hợp lệ",
  "status": 400,
  "detail": "Một hoặc nhiều trường không hợp lệ.",
  "instance": "/api/v1/questions",
  "traceId": "00-abcd-1234-01",
  "errorCode": "VALIDATION_FAILED",
  "errors": {
    "questionText": ["Nội dung câu hỏi là bắt buộc."]
  }
}
~~~

## 4. HTTP status chuẩn

| Status | Dùng khi |
|---:|---|
| 200 | Query/update thành công |
| 201 | Tạo resource thành công |
| 202 | Attempt + AI Job đã được nhận |
| 204 | Soft delete/logout/remove membership thành công |
| 400 | Validation hoặc request malformed |
| 401 | Chưa xác thực/token hết hạn |
| 403 | Đã xác thực nhưng role không được phép |
| 404 | Không tồn tại hoặc resource thuộc tenant khác |
| 409 | Concurrency, duplicate, DAG cycle, invalid state transition |
| 422 | Request hợp lệ về cú pháp nhưng không thể xử lý nghiệp vụ |
| 500 | Lỗi server không dự kiến |
| 503 | Dependency tạm thời unavailable; không dùng cho AI fallback đã xử lý |

## 5. Error code chuẩn

| errorCode | HTTP | Ý nghĩa |
|---|---:|---|
| VALIDATION_FAILED | 400 | Field validation |
| AUTH_INVALID_CREDENTIALS | 401 | Sai Center/username/password |
| AUTH_TOKEN_EXPIRED | 401 | Access Token hết hạn |
| AUTH_REFRESH_INVALID | 401 | Refresh Token invalid/revoked |
| AUTH_USER_DISABLED | 403 | User/Center bị khóa |
| FORBIDDEN_RESOURCE | 403 | Role đúng nhưng không có ownership |
| RESOURCE_NOT_FOUND | 404 | Không tồn tại hoặc cross-tenant |
| CONCURRENCY_CONFLICT | 409 | rowVersion cũ |
| DUPLICATE_RESOURCE | 409 | Vi phạm unique business key |
| DUPLICATE_SUBMISSION | 409 | clientSubmissionId trùng nhưng payload khác |
| DAG_CYCLE_DETECTED | 409 | Edge/hierarchy tạo cycle |
| INVALID_STATE_TRANSITION | 409 | Publish/close/process sai trạng thái |
| ASSIGNMENT_NOT_AVAILABLE | 422 | Student không phải target hoặc bài đã đóng |
| QUESTION_REASONING_REQUIRED | 422 | Thiếu reasoningText |
| AI_JOB_NOT_TERMINAL | 422 | Feedback chưa sẵn sàng |
| OVERRIDE_REPLAY_FAILED | 500 | Transaction replay rollback |

## 6. Pagination/filter/sort

Query chung:

- page: mặc định 1.
- pageSize: mặc định 20, tối đa 100.
- search: tối đa 200 ký tự.
- sortBy: allow-list theo endpoint.
- sortDirection: asc hoặc desc.

Collection rỗng trả data: [], không trả 404.

## 7. Enum contract

| Enum | Giá trị |
|---|---|
| RoleName | Student, Teacher, CenterManager |
| UserStatus | Active, Locked, Disabled |
| ClassStatus | Active, Archived |
| NodeType | Subject, Chapter, Topic, Skill, Concept |
| RelationType | PrerequisiteOf, RelatedTo, PartOf, CausesErrorIn |
| QuestionType | MultipleChoice, ShortAnswer, Essay |
| QuestionStatus | Draft, Active, Archived |
| AssignmentStatus | Draft, Published, Closed, Archived |
| ProgressStatus | NotStarted, InProgress, Completed, Overdue |
| AttemptStatus | PendingAnalysis, Processing, Completed, NeedsTeacherReview |
| AIJobStatus | Pending, Processing, Completed, FallbackCompleted, FailedTerminal |
| ErrorType | None, Knowledge, Skill, Reasoning, Behavior, Presentation, Unknown |
| RecommendationStatus | Active, Accepted, Dismissed, Superseded |
| LearningPathStrategy | LinearFallback, OpportunityGap |

# Authentication

## 8. POST /auth/login

Quyền: Anonymous.

Request:

~~~json
{
  "centerCode": "EDU-A",
  "username": "manager.a",
  "password": "development-password"
}
~~~

Response 200:

~~~json
{
  "data": {
    "accessToken": "jwt",
    "tokenType": "Bearer",
    "expiresInSeconds": 900,
    "user": {
      "userId": "4bd79f57-55bb-4f08-a69b-47ee532343f1",
      "centerId": "06182f25-c428-4865-833f-80f8ca297c11",
      "centerName": "EduTwin Center A",
      "username": "manager.a",
      "displayName": "Quản lý Trung tâm A",
      "role": "CenterManager"
    }
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

Side effect: Set-Cookie edutwin_refresh.

## 9. POST /auth/refresh

Quyền: có refresh cookie hợp lệ.

Request body: không có.

Response 200: cùng data accessToken/user như login và rotate refresh cookie.

## 10. POST /auth/logout

Quyền: Authenticated hoặc refresh cookie hợp lệ.

Request body: không có.  
Response: 204, revoke refresh token hiện tại và clear cookie.

## 11. GET /auth/me

Quyền: Authenticated.

Response 200 dùng object user như login, bổ sung:

~~~json
{
  "data": {
    "userId": "4bd79f57-55bb-4f08-a69b-47ee532343f1",
    "centerId": "06182f25-c428-4865-833f-80f8ca297c11",
    "centerName": "EduTwin Center A",
    "username": "manager.a",
    "displayName": "Quản lý Trung tâm A",
    "role": "CenterManager",
    "status": "Active"
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

# Center và Organization

## 12. GET /centers/me

Quyền: CenterManager.

Response 200:

~~~json
{
  "data": {
    "centerId": "06182f25-c428-4865-833f-80f8ca297c11",
    "centerCode": "EDU-A",
    "centerName": "EduTwin Center A",
    "status": "Active",
    "timezone": "Asia/Bangkok",
    "rowVersion": "3"
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

## 13. PATCH /centers/me

Quyền: CenterManager.

Request:

~~~json
{
  "centerName": "EduTwin Learning Center A",
  "timezone": "Asia/Bangkok",
  "rowVersion": "3"
}
~~~

Response 200: Center DTO với rowVersion mới.

## 14. GET /centers/me/dashboard

Quyền: CenterManager.

Query:

- subjectId: optional.
- riskThreshold: default 70.

Response 200:

~~~json
{
  "data": {
    "summary": {
      "teacherCount": 8,
      "studentCount": 120,
      "classCount": 10
    },
    "masteryBySubject": [
      {
        "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
        "subjectName": "Toán",
        "averageMastery": 61.25
      }
    ],
    "highRiskByClass": [
      {
        "classId": "de17cff8-a6ce-4e42-9781-a1dc07e2e625",
        "className": "Toán 12A",
        "highRiskStudentCount": 7,
        "totalStudentCount": 30
      }
    ],
    "classRanking": [
      {
        "rank": 1,
        "classId": "de17cff8-a6ce-4e42-9781-a1dc07e2e625",
        "className": "Toán 12A",
        "subjectName": "Toán",
        "averageMastery": 71.4,
        "assignmentCompletionRate": 86.7
      }
    ],
    "generatedAt": "2026-07-15T08:30:45.123456Z"
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

# Teacher management

## 15. Teacher DTO

~~~json
{
  "teacherId": "2a584ad0-6ea5-4ff7-a3a9-9baf8cbc2036",
  "username": "teacher.math",
  "displayName": "Nguyễn Văn Giáo",
  "department": "Toán",
  "status": "Active",
  "classCount": 2,
  "rowVersion": "1"
}
~~~

## 16. POST /teachers

Quyền: CenterManager.

Request:

~~~json
{
  "username": "teacher.math",
  "temporaryPassword": "change-me-123",
  "displayName": "Nguyễn Văn Giáo",
  "department": "Toán"
}
~~~

Response 201: Teacher DTO.

Transaction: User + Teacher profile.

## 17. GET /teachers

Quyền: CenterManager.  
Query: page, pageSize, search, status.  
Response 200: collection Teacher DTO.

## 18. GET /teachers/{teacherId}

Quyền: CenterManager hoặc chính Teacher.  
Response 200: Teacher DTO.

## 19. PATCH /teachers/{teacherId}

Quyền: CenterManager.

Request:

~~~json
{
  "displayName": "Nguyễn Văn Giáo",
  "department": "Khoa Toán",
  "status": "Active",
  "rowVersion": "1"
}
~~~

Response 200: Teacher DTO với rowVersion mới.

## 20. DELETE /teachers/{teacherId}

Quyền: CenterManager.  
Response 204.  
Rule: từ chối 409 nếu Teacher còn Class Active; không hard delete.

# Student management và goal

## 21. Student DTO

~~~json
{
  "studentId": "baf68743-a272-4983-a9e2-41663734a7c2",
  "username": "student.001",
  "fullName": "Trần Minh An",
  "gradeLevel": 12,
  "status": "Active",
  "activeClassCount": 2,
  "rowVersion": "1"
}
~~~

## 22. POST /students

Quyền: Teacher hoặc CenterManager.

Request:

~~~json
{
  "username": "student.001",
  "temporaryPassword": "change-me-123",
  "fullName": "Trần Minh An",
  "gradeLevel": 12,
  "classIds": [
    "de17cff8-a6ce-4e42-9781-a1dc07e2e625"
  ]
}
~~~

Response 201: Student DTO.

Rule:

- Teacher chỉ được thêm Student vào Class mình sở hữu.
- Transaction tạo User + Student + memberships + Student Twin root.

## 23. GET /students

Quyền: Teacher hoặc CenterManager.

Query:

- classId optional.
- search, status, gradeLevel.
- page, pageSize.

Teacher chỉ nhận Student thuộc Class mình quản lý.

## 24. GET /students/{studentId}

Quyền: CenterManager, Teacher có ownership hoặc chính Student.  
Response 200: Student DTO + classes + subject goals.

## 25. PATCH /students/{studentId}

Quyền: CenterManager hoặc Teacher có ownership.

Request:

~~~json
{
  "fullName": "Trần Minh An",
  "gradeLevel": 12,
  "status": "Active",
  "rowVersion": "1"
}
~~~

Response 200: Student DTO.

## 26. PUT /students/{studentId}/goals/{subjectId}

Quyền: chính Student, Teacher có ownership hoặc CenterManager.

Request:

~~~json
{
  "targetScore": 8.5,
  "remainingDays": 120,
  "rowVersion": null
}
~~~

rowVersion null khi tạo mới, bắt buộc khi update.

Response 200:

~~~json
{
  "data": {
    "goalId": "1001",
    "studentId": "baf68743-a272-4983-a9e2-41663734a7c2",
    "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
    "targetScore": 8.5,
    "remainingDays": 120,
    "currentPredictedScore": 0,
    "riskScore": 68,
    "rowVersion": "1"
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

## 27. GET /students/{studentId}/goals

Quyền: chính Student, Teacher có ownership hoặc CenterManager.  
Response 200: collection Goal DTO.

# Classes

## 28. Class DTO

~~~json
{
  "classId": "de17cff8-a6ce-4e42-9781-a1dc07e2e625",
  "className": "Toán 12A",
  "academicYear": "2026-2027",
  "subject": {
    "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
    "subjectName": "Toán"
  },
  "teacher": {
    "teacherId": "2a584ad0-6ea5-4ff7-a3a9-9baf8cbc2036",
    "displayName": "Nguyễn Văn Giáo"
  },
  "studentCount": 30,
  "status": "Active",
  "rowVersion": "1"
}
~~~

## 29. POST /classes

Quyền: CenterManager.

Request:

~~~json
{
  "className": "Toán 12A",
  "academicYear": "2026-2027",
  "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
  "teacherId": "2a584ad0-6ea5-4ff7-a3a9-9baf8cbc2036"
}
~~~

Response 201: Class DTO.

## 30. GET /classes

Quyền: Teacher hoặc CenterManager.

Query: teacherId optional cho CenterManager, subjectId, status, page, pageSize.  
Teacher chỉ nhận Class của mình.

## 31. GET /classes/{classId}

Quyền: Teacher owner hoặc CenterManager.  
Response 200: Class DTO.

## 32. PATCH /classes/{classId}

Quyền: CenterManager.

Request:

~~~json
{
  "className": "Toán 12A nâng cao",
  "teacherId": "2a584ad0-6ea5-4ff7-a3a9-9baf8cbc2036",
  "status": "Active",
  "rowVersion": "1"
}
~~~

Response 200: Class DTO.

Subject không đổi sau khi Class đã có Assignment.

## 33. POST /classes/{classId}/students

Quyền: Teacher owner hoặc CenterManager.

Request:

~~~json
{
  "studentIds": [
    "baf68743-a272-4983-a9e2-41663734a7c2"
  ]
}
~~~

Response 200:

~~~json
{
  "data": {
    "classId": "de17cff8-a6ce-4e42-9781-a1dc07e2e625",
    "addedCount": 1,
    "alreadyMemberCount": 0
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

## 34. DELETE /classes/{classId}/students/{studentId}

Quyền: Teacher owner hoặc CenterManager.  
Response 204.  
Rule: đổi membership sang Removed; không xóa Assignment Target lịch sử.

## 35. GET /classes/{classId}/students

Quyền: Teacher owner hoặc CenterManager.  
Query: status, search, page, pageSize.  
Response 200: collection Student DTO.

# Subjects và Knowledge Graph

## 36. Subject DTO

~~~json
{
  "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
  "subjectCode": "MATH",
  "subjectName": "Toán",
  "description": "Môn Toán THPT",
  "isActive": true,
  "rowVersion": "1"
}
~~~

## 37. Subject endpoints

| Method | Path | Role | Request | Response |
|---|---|---|---|---|
| POST | /subjects | Teacher, CenterManager | CreateSubjectRequest | 201 Subject DTO |
| GET | /subjects | Authenticated | query isActive | 200 collection |
| GET | /subjects/{subjectId} | Authenticated | — | 200 Subject DTO |
| PATCH | /subjects/{subjectId} | Teacher, CenterManager | UpdateSubjectRequest + rowVersion | 200 Subject DTO |
| DELETE | /subjects/{subjectId} | CenterManager | — | 204 hoặc 409 nếu đã có evidence |

CreateSubjectRequest:

~~~json
{
  "subjectCode": "PHYSICS",
  "subjectName": "Vật lý",
  "description": "Môn Vật lý THPT"
}
~~~

## 38. Knowledge Node DTO

~~~json
{
  "nodeId": "101",
  "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
  "parentNodeId": "90",
  "nodeType": "Topic",
  "nodeCode": "MATH.LOG",
  "nodeName": "Mũ và Logarit",
  "description": "Các kiến thức về hàm mũ và logarit",
  "orderIndex": 2,
  "examImportance": 20,
  "estimatedLearningMinutes": 180,
  "isActive": true,
  "rowVersion": "1"
}
~~~

## 39. POST /knowledge/nodes

Quyền: Teacher hoặc CenterManager.

Request giống Node DTO, bỏ nodeId/rowVersion.
Response 201: Node DTO.

## 40. GET /knowledge/nodes

Quyền: Authenticated.

Query: subjectId bắt buộc; nodeType, parentNodeId, isActive.  
Response 200: collection Node DTO, mặc định sort orderIndex/nodeId.

## 41. PATCH /knowledge/nodes/{nodeId}

Quyền: Teacher hoặc CenterManager.

Request:

~~~json
{
  "parentNodeId": "90",
  "nodeName": "Mũ và Logarit",
  "description": "Nội dung cập nhật",
  "orderIndex": 2,
  "examImportance": 20,
  "estimatedLearningMinutes": 180,
  "isActive": true,
  "rowVersion": "1"
}
~~~

Response 200.  
Conflict DAG_CYCLE_DETECTED nếu đổi parent tạo cycle.

## 42. POST /knowledge/edges

Quyền: Teacher hoặc CenterManager.

Request:

~~~json
{
  "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
  "sourceNodeId": "100",
  "targetNodeId": "101",
  "relationType": "PrerequisiteOf",
  "weight": 1
}
~~~

Response 201:

~~~json
{
  "data": {
    "edgeId": "5001",
    "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
    "sourceNodeId": "100",
    "targetNodeId": "101",
    "relationType": "PrerequisiteOf",
    "weight": 1,
    "rowVersion": "1"
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

## 43. Knowledge graph endpoints

| Method | Path | Role | Ý nghĩa |
|---|---|---|---|
| GET | /knowledge/graph?subjectId={id} | Authenticated | Nodes + edges cho visual/query |
| PATCH | /knowledge/edges/{edgeId} | Teacher, CenterManager | Update weight + rowVersion |
| DELETE | /knowledge/edges/{edgeId} | Teacher, CenterManager | Soft delete |
| DELETE | /knowledge/nodes/{nodeId} | CenterManager | Soft delete nếu chưa có evidence |

Graph response:

~~~json
{
  "data": {
    "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
    "nodes": [
      {
        "nodeId": "101",
        "nodeType": "Topic",
        "nodeCode": "MATH.LOG",
        "nodeName": "Mũ và Logarit",
        "orderIndex": 2,
        "examImportance": 20
      }
    ],
    "edges": [
      {
        "edgeId": "5001",
        "sourceNodeId": "100",
        "targetNodeId": "101",
        "relationType": "PrerequisiteOf",
        "weight": 1
      }
    ]
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

# Curriculum

## 44. Curriculum DTO

~~~json
{
  "curriculumId": "078a0cf5-10f7-4700-8064-380f59206f86",
  "teacherId": "2a584ad0-6ea5-4ff7-a3a9-9baf8cbc2036",
  "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
  "title": "Lộ trình Toán 12",
  "description": "Giáo trình nhập thủ công",
  "sourceFile": null,
  "reviewStatus": "Draft",
  "classIds": [
    "078a0cf5-10f7-4700-8064-380f59206f86"
  ],
  "nodeIds": ["100", "101"],
  "rowVersion": "1"
}
~~~

Ghi chú DTO:

- `sourceFile`: luôn `null` trong MVP.
- `nodeIds`: mảng string ID trả về theo thứ tự `CurriculumNode.OrderIndex` tăng dần.
- `classIds`: mảng string GUID trả về theo thứ tự deterministic (`ClassId` tăng dần).

## 45. Curriculum endpoints

### 45.1. Bảng Endpoint & Phân quyền

| Method | Path | Role | Ownership & Rules | Request / Response |
|---|---|---|---|---|
| POST | /curriculums | Teacher, CenterManager | Teacher: tự sở hữu (`teacherId` bắt buộc null). CenterManager: chỉ định `teacherId` hợp lệ. Student: 403 Forbidden. | CreateCurriculumRequest $\rightarrow$ 201 DTO |
| GET | /curriculums | Teacher, CenterManager | Teacher: chỉ xem bài do mình sở hữu. CenterManager: xem toàn Center. Student: 403 Forbidden. | Query `subjectId`, `status` $\rightarrow$ 200 Collection |
| GET | /curriculums/{id} | Teacher owner, CenterManager | Teacher: chỉ bài mình sở hữu. CenterManager: toàn Center. Non-owner/student: 404/403. | — $\rightarrow$ 200 DTO |
| PATCH | /curriculums/{id} | Teacher owner, CenterManager | Chỉ áp dụng khi `reviewStatus` là `Draft`. Bắt buộc `title`, `rowVersion`. | UpdateCurriculumRequest $\rightarrow$ 200 DTO |
| POST | /curriculums/{id}/publish | Teacher owner, CenterManager | Chỉ áp dụng khi `reviewStatus` là `Draft`. Bắt buộc `rowVersion`. | PublishCurriculumRequest $\rightarrow$ 200 DTO |
| PUT | /curriculums/{id}/classes | Teacher owner, CenterManager | Chỉ áp dụng khi `reviewStatus` là `Draft`. Atomic replace. Bắt buộc `rowVersion`. | UpdateCurriculumClassesRequest $\rightarrow$ 200 DTO |
| PUT | /curriculums/{id}/nodes | Teacher owner, CenterManager | Chỉ áp dụng khi `reviewStatus` là `Draft`. Atomic replace (1..N). Bắt buộc `rowVersion`. | UpdateCurriculumNodesRequest $\rightarrow$ 200 DTO |

### 45.2. Quy tắc Chi tiết Endpoints

#### 1. POST /curriculums (Tạo Curriculum)

Request payload:

~~~json
{
  "teacherId": null,
  "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
  "title": "Lộ trình Toán 12",
  "description": "Giáo trình nhập thủ công",
  "nodeIds": ["100", "101"]
}
~~~

Quy tắc xử lý:

- `teacherId`: nullable string GUID trong JSON.
  - **Caller là Teacher**:
    - `teacherId` bắt buộc phải là `null` hoặc omitted.
    - Server tự động gán `TeacherId = authenticated UserId`.
    - Nếu Teacher gửi `teacherId` non-null: từ chối 400 `VALIDATION_FAILED`.
  - **Caller là CenterManager**:
    - `teacherId` bắt buộc phải khác null / khác chuỗi rỗng. Nếu missing, null, hoặc rỗng: trả 400 `VALIDATION_FAILED`.
    - Teacher được chỉ định chỉ hợp lệ khi đồng thời thỏa mãn các điều kiện sau:
      1. Teacher tồn tại trong cùng Center (`Teacher.CenterId == CenterManager.CenterId`).
      2. `Teacher.IsDeleted == false`.
      3. Linked User tồn tại trong cùng Center (`User.CenterId == CenterManager.CenterId`).
      4. `User.IsDeleted == false`.
      5. `User.RoleName == Teacher` (hoặc Role `Teacher`).
      6. `User.Status == Active`.
    - Nếu bất kỳ điều kiện nào không đạt (không tồn tại, cross-tenant, inactive, deleted, hoặc role không phải Teacher): trả 404 `RESOURCE_NOT_FOUND`.
  - Tuyệt đối không tự chọn teacher đầu tiên; không gán CenterManager UserId vào TeacherId; không làm schema database nullable.
- Khởi tạo mặc định:
  - `reviewStatus = Draft`.
  - `sourceFile = null`.
  - `centerId` lấy từ authenticated tenant context.
  - `rowVersion = 1`.
- `subjectId`: required GUID, thuộc cùng Center, active, không deleted.
- `title`: required, độ dài thô (raw length) từ 1 đến 250 ký tự. Không trim trước khi kiểm tra độ dài.
- `description`: optional text.
- `nodeIds`: mảng string bắt buộc (có thể rỗng `[]`). Các `nodeId` không duplicate, mỗi ID là ASCII unsigned integer > 0. Mỗi Node phải active, không deleted, thuộc cùng Center và trùng `SubjectId` với Curriculum. `OrderIndex` được lưu chính xác theo vị trí mảng (1..N).

#### 2. GET /curriculums (Danh sách Curriculum)

Query parameters:

- `subjectId`: optional GUID string.
- `status`: optional string enum (`Draft`, `Published`, `Archived`).

Quy tắc non-pagination, visibility & filter:

- Endpoint P09-T01 **không hỗ trợ** query parameters `page` hoặc `pageSize`.
- Endpoint trả về toàn bộ collection đã lọc trong phạm vi ownership.
- Response dùng envelope thành công chuẩn với mảng `data: [...]`. Phần `meta` chỉ gồm `traceId` và `timestamp` (không có pagination meta `page`, `pageSize`, `totalItems`, `totalPages`).
- `Teacher`: chỉ nhận danh sách Curriculum có `TeacherId == authenticated UserId`.
- `CenterManager`: nhận tất cả Curriculum chưa bị xóa (`IsDeleted == false`) trong Center.
- `Student`: không thuộc allowed roles của endpoint, Authorization policy trả HTTP 403 Forbidden.
- Nếu không có kết quả: trả 200 OK với mảng `data: []`.
- Filter `status`: parse strict, case-sensitive theo enum `ReviewStatus`. Nếu không hợp lệ trả 400 `VALIDATION_FAILED`.
- Thứ tự kết quả (Deterministic ordering): Sắp xếp theo `UpdatedAt` giảm dần (`DESC`), sau đó `CurriculumId` tăng dần (`ASC`).

#### 3. GET /curriculums/{id} (Chi tiết Curriculum)

- Teacher chỉ truy cập được Curriculum do mình sở hữu. CenterManager truy cập được toàn bộ trong Center. Student nhận HTTP 403 Forbidden.
- Nếu không tồn tại, thuộc Center khác, đã bị soft-delete, hoặc Teacher không sở hữu: trả 404 `RESOURCE_NOT_FOUND` (fail-closed).

#### 4. PATCH /curriculums/{id} (Cập nhật thông tin cơ bản)

Request payload:

~~~json
{
  "title": "Lộ trình Toán 12 cập nhật",
  "description": "Nội dung cập nhật",
  "rowVersion": "1"
}
~~~

Quy tắc:

- Chỉ cho phép khi `reviewStatus == Draft`. Nếu trạng thái là `Published` hoặc `Archived`: trả 409 `INVALID_STATE_TRANSITION`.
- `title`: field **bắt buộc** trong `UpdateCurriculumRequest`. Nếu missing, `null`, chuỗi rỗng (`""`), hoặc chỉ chứa khoảng trắng (`"   "`): trả 400 `VALIDATION_FAILED`. Validate độ dài thô (raw length 1–250 ký tự) trên raw input trước mọi thao tác trim. Không trim để biến input vượt giới hạn thành hợp lệ.
- `description`: optional text update. Không tự đặt giới hạn 2000 ký tự.
- `rowVersion`: bắt buộc (áp dụng Strict rowVersion contract tại mục 45.3).

#### 5. POST /curriculums/{id}/publish (Xuất bản Curriculum)

Request payload:

~~~json
{
  "rowVersion": "1"
}
~~~

Quy tắc:

- Chỉ cho phép khi `reviewStatus == Draft`.
- Chuyển `reviewStatus` từ `Draft` $\rightarrow$ `Published`.
- Nếu `reviewStatus` hiện tại đã là `Published` hoặc `Archived`: trả 409 `INVALID_STATE_TRANSITION`.
- `rowVersion`: bắt buộc (áp dụng Strict rowVersion contract tại mục 45.3).

#### 6. PUT /curriculums/{id}/classes (Gán danh sách Lớp học)

Request payload:

~~~json
{
  "classIds": [
    "078a0cf5-10f7-4700-8064-380f59206f86"
  ],
  "rowVersion": "1"
}
~~~

Quy tắc:

- Chỉ cho phép khi `reviewStatus == Draft`. Nếu `Published` hoặc `Archived`: trả 409 `INVALID_STATE_TRANSITION`.
- `classIds`: mảng string GUID bắt buộc, được phép rỗng `[]` để gỡ bỏ toàn bộ lớp. Không chứa duplicate.
- Mỗi `classId` phải thuộc cùng Center, active (`Status == Active`), không deleted, và có `Class.SubjectId == Curriculum.SubjectId`. Nếu vi phạm: trả 404 `RESOURCE_NOT_FOUND`.
- Thực hiện thay thế toàn bộ (atomic replacement).
- `AssignedAt` lưu thời gian UTC hiện tại (`TimeProvider`).
- `AssignedBy` lưu authenticated `UserId`.
- `rowVersion`: bắt buộc (áp dụng Strict rowVersion contract tại mục 45.3).

#### 7. PUT /curriculums/{id}/nodes (Gán danh sách Bài học/Nút kiến thức theo thứ tự)

Request payload:

~~~json
{
  "nodeIds": ["100", "101"],
  "rowVersion": "1"
}
~~~

Quy tắc:

- Chỉ cho phép khi `reviewStatus == Draft`. Nếu `Published` hoặc `Archived`: trả 409 `INVALID_STATE_TRANSITION`.
- `nodeIds`: mảng string bắt buộc (có thể rỗng `[]`), không duplicate. Mỗi `nodeId` là ASCII unsigned integer > 0.
- Mỗi Node phải active, không deleted, thuộc cùng Center và trùng `SubjectId` với Curriculum. Nếu vi phạm: trả 404 `RESOURCE_NOT_FOUND`.
- Thực hiện thay thế toàn bộ (atomic replacement). Persist `OrderIndex` chính xác theo thứ tự mảng (1..N).
- `rowVersion`: bắt buộc (áp dụng Strict rowVersion contract tại mục 45.3).

### 45.3. Quy tắc Concurrency Control & State Machine

1. **State Machine**:
   - State lifecycle: `Draft` $\rightarrow$ `Published`.
   - Trạng thái `Published` và `Archived` là immutable trong Phase P09-T01. Mọi yêu cầu mutation (`PATCH`, `PUT /classes`, `PUT /nodes`, `POST /publish`) trên resource không ở trạng thái `Draft` phải bị từ chối với 409 `INVALID_STATE_TRANSITION`.
   - Không có endpoint chuyển trạng thái sang `Archived` trong MVP hiện tại.
2. **Strict Concurrency Control**:
   - Áp dụng thống nhất cho cả bốn mutation endpoints: `PATCH /curriculums/{id}`, `POST /curriculums/{id}/publish`, `PUT /curriculums/{id}/classes`, `PUT /curriculums/{id}/nodes`.
   - Trường `rowVersion` trong JSON payload bắt buộc phải:
     - Là JSON string bắt buộc.
     - Khớp chính xác định dạng ASCII digits `[0-9]+`.
     - Parse thành unsigned integer > 0 (`ulong > 0`).
     - Kiểm tra validation trên raw input; không trim leading/trailing whitespace.
     - Nếu missing, `null`, empty, whitespace-only, hoặc chứa định dạng không hợp lệ như `"+1"`, `"1.0"`, `" 1"`, `"1 "` $\rightarrow$ trả 400 `VALIDATION_FAILED`.
   - Token hợp lệ về định dạng nhưng không khớp với bản ghi CSDL hiện tại (stale rowVersion) $\rightarrow$ trả 409 `CONCURRENCY_CONFLICT`.
   - Thao tác mutation thành công sẽ tăng `Curriculum.RowVersion` thêm chính xác 1 đơn vị.

Không có upload/analyze/map AI endpoint trong MVP.

# Question Bank

## 46. Question DTO

~~~json
{
  "questionId": "9001",
  "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
  "primaryTopicNodeId": "101",
  "questionType": "MultipleChoice",
  "difficulty": 3,
  "questionText": "Giải phương trình ...",
  "correctAnswer": "B",
  "solution": "Các bước giải do giáo viên cung cấp.",
  "expectedReasoning": "Xác định điều kiện, đổi về cùng cơ số, kết luận.",
  "gradingCriteria": {
    "schemaVersion": "1.0",
    "requiredIdeas": [
      "Xác định điều kiện",
      "Biến đổi đúng"
    ],
    "commonErrors": [
      "Quên điều kiện"
    ],
    "scoringNotes": "Ưu tiên reasoning."
  },
  "maxScore": 1,
  "estimatedTimeSeconds": 180,
  "reasoningRequired": true,
  "languageCode": "vi",
  "status": "Active",
  "options": [
    {
      "optionId": "9101",
      "label": "A",
      "text": "x = 1",
      "isCorrect": false,
      "orderIndex": 1
    },
    {
      "optionId": "9102",
      "label": "B",
      "text": "x = 2",
      "isCorrect": true,
      "orderIndex": 2
    }
  ],
  "knowledgeMappings": [
    {
      "nodeId": "101",
      "mappingRole": "Primary"
    }
  ],
  "rowVersion": "2"
}
~~~

Student-facing Question DTO không bao gồm correctAnswer, solution, expectedReasoning, gradingCriteria hoặc isCorrect của options.

## 47. POST /questions

Quyền: Teacher hoặc CenterManager.

Request: Question DTO bỏ IDs, status mặc định Draft, rowVersion.  
Response 201: Teacher-facing Question DTO.

## 48. Question endpoints

| Method | Path | Role | Ghi chú |
|---|---|---|---|
| GET | /questions | Teacher, CenterManager | Filter subjectId/topicId/type/difficulty/status |
| GET | /questions/{id} | Teacher, CenterManager | Teacher-facing DTO |
| PATCH | /questions/{id} | Creator Teacher, CenterManager | Full editable payload + rowVersion |
| POST | /questions/{id}/activate | Creator Teacher, CenterManager | Validate options/criteria |
| POST | /questions/{id}/archive | Creator Teacher, CenterManager | Soft archive |
| DELETE | /questions/{id} | CenterManager | Chỉ Draft chưa có Attempt |

Activate request:

~~~json
{
  "rowVersion": "2"
}
~~~

# Assignments

## 49. Assignment DTO

~~~json
{
  "assignmentId": "12ae0f80-d90e-4627-964f-4404e692e3d6",
  "classId": "de17cff8-a6ce-4e42-9781-a1dc07e2e625",
  "title": "Bài luyện Mũ và Logarit",
  "instructions": "Trình bày rõ cách làm.",
  "dueAt": "2026-07-20T16:59:59Z",
  "status": "Draft",
  "questionCount": 3,
  "targetStudentCount": 5,
  "questions": [
    {
      "questionId": "9001",
      "orderIndex": 1,
      "points": 1
    }
  ],
  "targets": [
    {
      "studentId": "baf68743-a272-4983-a9e2-41663734a7c2",
      "targetSource": "GapGroup"
    }
  ],
  "rowVersion": "1"
}
~~~

## 50. POST /assignments

Quyền: Teacher owner của Class hoặc CenterManager.

Request:

~~~json
{
  "classId": "de17cff8-a6ce-4e42-9781-a1dc07e2e625",
  "title": "Bài luyện Mũ và Logarit",
  "instructions": "Trình bày rõ cách làm.",
  "dueAt": "2026-07-20T16:59:59Z",
  "questionIds": ["9001", "9002", "9003"],
  "targetMode": "SelectedStudents",
  "studentIds": [
    "baf68743-a272-4983-a9e2-41663734a7c2"
  ]
}
~~~

targetMode: WholeClass hoặc SelectedStudents. GapGroup UI gửi SelectedStudents và server ghi targetSource GapGroup khi source được chỉ định.

Response 201: Assignment DTO Draft.

## 51. Assignment endpoints

| Method | Path | Role | Ý nghĩa |
|---|---|---|---|
| GET | /assignments | Teacher, CenterManager | Filter classId/status/due range |
| GET | /assignments/{id} | Teacher owner, CenterManager | Full DTO |
| PATCH | /assignments/{id} | Teacher owner, CenterManager | Chỉ Draft; payload như create + rowVersion |
| POST | /assignments/{id}/publish | Teacher owner, CenterManager | Materialize targets/progress |
| POST | /assignments/{id}/close | Teacher owner, CenterManager | Close |
| GET | /assignments/{id}/progress | Teacher owner, CenterManager | Student progress collection |
| GET | /students/me/assignments | Student | Filter status |
| GET | /students/me/assignments/{id} | Target Student | Student-facing questions |

Publish request:

~~~json
{
  "rowVersion": "1"
}
~~~

Publish response 200: Assignment DTO status Published.

Student assignment response không lộ đáp án:

~~~json
{
  "data": {
    "assignmentId": "12ae0f80-d90e-4627-964f-4404e692e3d6",
    "title": "Bài luyện Mũ và Logarit",
    "instructions": "Trình bày rõ cách làm.",
    "dueAt": "2026-07-20T16:59:59Z",
    "progress": {
      "status": "InProgress",
      "completedQuestionCount": 1,
      "totalQuestionCount": 3
    },
    "questions": [
      {
        "questionId": "9001",
        "questionType": "MultipleChoice",
        "difficulty": 3,
        "questionText": "Giải phương trình ...",
        "estimatedTimeSeconds": 180,
        "reasoningRequired": true,
        "languageCode": "vi",
        "options": [
          {
            "optionId": "9101",
            "label": "A",
            "text": "x = 1"
          }
        ],
        "attemptStatus": null
      }
    ]
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

# Learning Mode và AI Job

## 52. POST /learning/attempts

Quyền: Student.

Request:

~~~json
{
  "clientSubmissionId": "60fc1a2f-c2d9-433a-bcee-f4fb3a520b7b",
  "questionId": "9001",
  "assignmentId": "12ae0f80-d90e-4627-964f-4404e692e3d6",
  "finalAnswer": "B",
  "reasoningText": "Em đặt điều kiện rồi đưa hai vế về cùng cơ số...",
  "timeSpentSeconds": 165,
  "confidence": 80,
  "answerChanges": 1,
  "skipped": false
}
~~~

Response 202:

~~~json
{
  "data": {
    "attemptId": "12001",
    "analysisJobId": "13001",
    "attemptStatus": "PendingAnalysis",
    "jobStatus": "Pending",
    "pollUrl": "/api/v1/learning/analysis-jobs/13001",
    "pollAfterMilliseconds": 3000
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:30:45.123456Z"
  }
}
~~~

Idempotency:

- Cùng Student + clientSubmissionId + cùng payload luôn trả lại 202 với Attempt/Job hiện có và trạng thái hiện tại.
- Cùng key nhưng payload khác trả 409 DUPLICATE_SUBMISSION.

## 53. GET /learning/analysis-jobs/{analysisJobId}

Quyền: Student sở hữu Attempt; Teacher owner; CenterManager.

Pending/Processing response:

~~~json
{
  "data": {
    "analysisJobId": "13001",
    "attemptId": "12001",
    "status": "Processing",
    "retryCount": 0,
    "terminal": false,
    "feedbackUrl": null,
    "updatedAt": "2026-07-15T08:31:00Z"
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:31:00Z"
  }
}
~~~

Terminal response:

~~~json
{
  "data": {
    "analysisJobId": "13001",
    "attemptId": "12001",
    "status": "Completed",
    "retryCount": 0,
    "terminal": true,
    "feedbackUrl": "/api/v1/learning/attempts/12001/feedback",
    "updatedAt": "2026-07-15T08:31:06Z"
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:31:06Z"
  }
}
~~~

Frontend poll 3 giây, dừng khi terminal=true.

## 54. GET /learning/attempts/{attemptId}/feedback

Quyền: Student owner; Teacher owner; CenterManager.

Response 200:

~~~json
{
  "data": {
    "attemptId": "12001",
    "questionId": "9001",
    "status": "Completed",
    "grading": {
      "isCorrect": true,
      "awardedScore": 1,
      "maxScore": 1
    },
    "analysis": {
      "analysisId": "14001",
      "schemaVersion": "ai-analysis-v1",
      "methodDetected": "Đưa hai vế về cùng cơ số",
      "reasoningQuality": 72,
      "qualityBand": "Acceptable",
      "errorType": "Reasoning",
      "misconception": null,
      "missingSteps": ["Chưa kết luận đối chiếu điều kiện"],
      "rootCauseNodes": [
        {
          "nodeId": "101",
          "nodeName": "Mũ và Logarit"
        }
      ],
      "confidence": 85,
      "feedback": "Em đã chọn đúng phương pháp, cần đối chiếu điều kiện ở bước cuối.",
      "isFallback": false,
      "needsTeacherReview": false,
      "hasTeacherOverride": false
    },
    "twinChange": {
      "topicNodeId": "101",
      "topicName": "Mũ và Logarit",
      "previousMastery": 45,
      "newMastery": 51.75,
      "delta": 6.75,
      "explanation": "Mastery tăng do reasoning ở mức Acceptable và đáp án đúng."
    },
    "recommendation": {
      "recommendationId": "15001",
      "type": "TopicAndQuestion",
      "topicNodeId": "101",
      "topicName": "Mũ và Logarit",
      "questionId": "9004",
      "opportunityScore": 82.4,
      "explanation": "Topic có cơ hội tăng điểm cao và đã thỏa prerequisite."
    }
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:31:06Z"
  }
}
~~~

Fallback trả reasoningQuality/analysis confidence null, isFallback=true, needsTeacherReview=true.

## 55. GET /learning/attempts

Quyền:

- Student: chỉ của mình.
- Teacher: cần studentId và ownership.
- CenterManager: trong Center.

Query: studentId, subjectId, questionId, assignmentId, status, from, to, page, pageSize.  
Response: collection Attempt Summary DTO.

## 56. GET /learning/next-question?subjectId={id}

Quyền: Student.

Response:

~~~json
{
  "data": {
    "strategy": "OpportunityGap",
    "recommendationId": "15001",
    "topic": {
      "nodeId": "101",
      "nodeName": "Mũ và Logarit",
      "mastery": 51.75
    },
    "question": {
      "questionId": "9004",
      "questionType": "ShortAnswer",
      "difficulty": 3,
      "questionText": "Tính ...",
      "estimatedTimeSeconds": 240,
      "reasoningRequired": true,
      "languageCode": "vi"
    },
    "explanation": "Đề xuất dựa trên Opportunity Gap cao nhất."
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:31:06Z"
  }
}
~~~

Dưới 3 Attempt trả strategy LinearFallback.

# Student Dashboard và Twin

## 57. GET /students/me/dashboard?subjectId={subjectId}

Quyền: Student.

Response 200:

~~~json
{
  "data": {
    "student": {
      "studentId": "baf68743-a272-4983-a9e2-41663734a7c2",
      "fullName": "Trần Minh An"
    },
    "subject": {
      "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
      "subjectName": "Toán"
    },
    "goal": {
      "targetScore": 8.5,
      "remainingDays": 120,
      "currentPredictedScore": 5.2,
      "riskScore": 26.4
    },
    "masteryRadar": [
      {
        "topicNodeId": "100",
        "topicName": "Hàm số",
        "mastery": 62.5
      },
      {
        "topicNodeId": "101",
        "topicName": "Mũ và Logarit",
        "mastery": 51.75
      }
    ],
    "progressLine": [
      {
        "recordedAt": "2026-07-14T08:00:00Z",
        "overallSubjectMastery": 45.2
      },
      {
        "recordedAt": "2026-07-15T08:31:06Z",
        "overallSubjectMastery": 49.7
      }
    ],
    "action": {
      "strategy": "OpportunityGap",
      "topicNodeId": "101",
      "topicName": "Mũ và Logarit",
      "questionId": "9004",
      "opportunityScore": 82.4,
      "explanation": "Topic có cơ hội tăng điểm cao nhất."
    },
    "generatedAt": "2026-07-15T08:31:06Z"
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:31:06Z"
  }
}
~~~

## 58. Student Twin endpoints

| Method | Path | Query | Response |
|---|---|---|---|
| GET | /students/me/twin | subjectId required | Knowledge + Behavior summary |
| GET | /students/me/twin/history | subjectId, topicId optional, from/to | History collection |
| GET | /students/me/recommendation | subjectId required | Active Recommendation |
| POST | /students/me/recommendation/{id}/accept | — | 200 updated status |
| POST | /students/me/recommendation/{id}/dismiss | reason optional | 200 updated status |
| GET | /students/me/learning-path | subjectId required | Active Learning Path |

Twin history item:

~~~json
{
  "historyId": "16001",
  "topicNodeId": "101",
  "topicName": "Mũ và Logarit",
  "eventSource": "AIAnalysis",
  "previousMastery": 45,
  "newMastery": 51.75,
  "delta": 6.75,
  "reasoningQuality": 72,
  "explanation": "Mastery tăng do reasoning ở mức Acceptable và đáp án đúng.",
  "recordedAt": "2026-07-15T08:31:06Z"
}
~~~

# Teacher Dashboard, Gap Groups và Override

## 59. GET /classes/{classId}/dashboard

Quyền: Teacher owner. CenterManager dùng cùng endpoint nếu cần.

Query: riskThreshold default 70.

Response:

~~~json
{
  "data": {
    "class": {
      "classId": "de17cff8-a6ce-4e42-9781-a1dc07e2e625",
      "className": "Toán 12A",
      "subjectId": "2ed34b81-0b0d-457c-888d-6a78f50a33d2",
      "subjectName": "Toán"
    },
    "overview": {
      "studentCount": 30,
      "averagePredictedScore": 6.15,
      "averageMastery": 61.5,
      "assignmentCompletionRate": 78.3
    },
    "highRiskStudents": [
      {
        "studentId": "baf68743-a272-4983-a9e2-41663734a7c2",
        "fullName": "Trần Minh An",
        "targetScore": 8.5,
        "predictedScore": 1,
        "remainingDays": 15,
        "riskScore": 73.13
      }
    ],
    "weakTopics": [
      {
        "topicNodeId": "101",
        "topicName": "Mũ và Logarit",
        "averageMastery": 42.3,
        "affectedStudentCount": 18
      }
    ],
    "gapGroups": [
      {
        "groupKey": "topic-101-below-60",
        "topicNodeId": "101",
        "topicName": "Mũ và Logarit",
        "threshold": 60,
        "studentCount": 5,
        "studentIds": [
          "baf68743-a272-4983-a9e2-41663734a7c2"
        ],
        "suggestedAction": "Giao bài luyện Mũ và Logarit."
      }
    ],
    "generatedAt": "2026-07-15T08:31:06Z"
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T08:31:06Z"
  }
}
~~~

Gap Group là projection động, không tạo table riêng.

## 60. GET /teachers/me/review-queue

Quyền: Teacher.

Query: classId optional, page, pageSize.  
Chỉ trả Analysis needsTeacherReview=true của Student thuộc Class Teacher.

Response item:

~~~json
{
  "analysisId": "14001",
  "attemptId": "12001",
  "studentId": "baf68743-a272-4983-a9e2-41663734a7c2",
  "studentName": "Trần Minh An",
  "questionId": "9001",
  "questionText": "Giải phương trình ...",
  "finalAnswer": "B",
  "reasoningText": "Em đặt điều kiện...",
  "isFallback": true,
  "reasoningQuality": null,
  "feedback": "AI không khả dụng; kết quả tạm dựa trên đáp án.",
  "createdAt": "2026-07-15T08:31:06Z"
}
~~~

## 61. POST /teachers/me/reasoning-analyses/{analysisId}/override

Quyền: Teacher có ownership hoặc CenterManager.

Request:

~~~json
{
  "reasoningQuality": 78,
  "errorType": "Presentation",
  "feedback": "Cách làm đúng nhưng thiếu kết luận.",
  "isCorrect": true,
  "reason": "Giáo viên đã kiểm tra trực tiếp bài làm.",
  "overrideVersion": 0
}
~~~

Response 200:

~~~json
{
  "data": {
    "analysisId": "14001",
    "hasTeacherOverride": true,
    "overrideVersion": 1,
    "overriddenAt": "2026-07-15T09:00:00Z",
    "replay": {
      "studentId": "baf68743-a272-4983-a9e2-41663734a7c2",
      "topicNodeId": "101",
      "attemptsReplayed": 4,
      "previousMastery": 51.75,
      "newMastery": 58.2,
      "newRiskScore": 35.1,
      "recommendationRecalculated": true
    }
  },
  "meta": {
    "traceId": "00-abcd-1234-01",
    "timestamp": "2026-07-15T09:00:00Z"
  }
}
~~~

Toàn bộ replay là transaction. Conflict nếu overrideVersion cũ.

## 62. GET /teachers/me/students/{studentId}/twin

Quyền: Teacher có ownership.  
Query: subjectId required.  
Response: Student dashboard data + chi tiết History/Reasoning gần nhất, không trả secret/raw Gemini.

# Health và operational endpoints

## 63. GET /health/live

Quyền: Anonymous.  
Response 200:

~~~json
{
  "status": "Healthy"
}
~~~

Không kiểm tra dependency.

## 64. GET /health/ready

Quyền: Anonymous trong local container network; có thể hạn chế ngoài môi trường local.

Response 200 khi API và MySQL sẵn sàng:

~~~json
{
  "status": "Healthy",
  "checks": {
    "mysql": "Healthy"
  }
}
~~~

Gemini không là readiness dependency vì có fallback.

# Authorization matrix

## 65. Ma trận quyền

| Resource/use case | Student | Teacher | CenterManager |
|---|:---:|:---:|:---:|
| Login/me/logout | Own | Own | Own |
| Manage Center | — | — | Center |
| Create Teacher | — | — | Center |
| Create Student | — | Own Classes | Center |
| Manage Class | — | Read own | CRUD Center |
| Manage Subject/KG | Read | CRUD Center content | CRUD |
| Manage Curriculum | — | Own | All Center |
| Manage Question | Student-safe read | Own/Create | All Center |
| Manage Assignment | Read targeted | Own Classes | All Center |
| Submit Attempt | Own | — | — |
| View Attempt/Feedback | Own | Own Classes | Center |
| Student Dashboard | Own | Own Classes | Center |
| Teacher Dashboard | — | Own Classes | Center |
| Center Dashboard | — | — | Center |
| Teacher Override | — | Own Classes | Center |

“Own Classes” luôn phải được kiểm tra bằng quan hệ Teacher–Class và Class–Student; không dựa vào ID client.

# Contract cho AI adapter

## 66. IAIService request contract logic

Đây là internal BLL contract, không phải public endpoint:

~~~json
{
  "schemaVersion": "ai-analysis-v1",
  "language": "vi",
  "question": {
    "questionType": "MultipleChoice",
    "questionText": "Giải phương trình ...",
    "correctAnswer": "B",
    "solution": "Lời giải chuẩn",
    "expectedReasoning": "Các ý mong đợi",
    "gradingCriteria": {
      "schemaVersion": "1.0",
      "requiredIdeas": ["Xác định điều kiện"],
      "commonErrors": ["Quên điều kiện"],
      "scoringNotes": "Ưu tiên reasoning"
    }
  },
  "studentSubmission": {
    "finalAnswer": "B",
    "reasoningText": "Em đặt điều kiện...",
    "timeSpentSeconds": 165,
    "confidence": 80,
    "answerChanges": 1
  },
  "allowedKnowledgeNodes": [
    {
      "nodeId": "101",
      "nodeName": "Mũ và Logarit"
    }
  ]
}
~~~

Không gửi username, password, token, center name hoặc dữ liệu Student không cần thiết.

## 67. Gemini response contract

~~~json
{
  "schemaVersion": "ai-analysis-v1",
  "language": "vi",
  "methodDetected": "Đưa hai vế về cùng cơ số",
  "reasoningQuality": 72,
  "errorType": "Reasoning",
  "misconception": null,
  "missingSteps": ["Chưa đối chiếu điều kiện"],
  "rootCauseNodeIds": ["101"],
  "confidence": 85,
  "feedback": "Em đã chọn đúng phương pháp nhưng cần đối chiếu điều kiện."
}
~~~

Lưu ý: rootCauseNodeIds là string trong JSON contract dù database là BIGINT.

Validation failure được tính là AI call failure và đi vào retry/fallback policy.

# Dashboard query semantics

## 68. Quy tắc tính aggregate

- averageMastery: trung bình weighted theo exam_importance của Topic active.
- currentPredictedScore: 10 × averageMastery / 100.
- highRisk: riskScore >= query riskThreshold, mặc định 70.
- weak Topic: class average mastery < 60.
- assignmentCompletionRate: Completed targets / total published targets × 100.
- Student chưa có Knowledge Twin được tính Mastery 0, không bị loại khỏi mẫu.
- Dashboard response phải kèm generatedAt.
- Query phải tenant-scoped trước khi group/aggregate.

# Versioning và change policy

## 69. Breaking change

Các thay đổi sau là breaking:

- Đổi tên/xóa field.
- Đổi type field.
- Đổi enum value.
- Đổi status code thành công/lỗi.
- Đổi authorization.
- Đổi semantic của công thức.
- Đưa field nhạy cảm vào Student-facing DTO.

Breaking change cần Change Proposal và cập nhật file này trước source code.

Thêm optional field có thể là non-breaking nhưng vẫn phải cập nhật contract và frontend owner xác nhận.

## 70. Contract acceptance checklist

- [ ] Tất cả endpoint dùng /api/v1.
- [ ] Không request nào nhận centerId.
- [ ] BIGINT ID trả dạng string.
- [ ] Student DTO không lộ đáp án/lời giải trước khi submit.
- [ ] 202 Attempt có pollUrl.
- [ ] Polling dừng bằng terminal=true.
- [ ] Fallback vẫn trả feedback contract hợp lệ.
- [ ] Teacher Override có overrideVersion và replay result.
- [ ] Dashboard aggregate tính cả Student chưa có evidence.
- [ ] Cross-tenant ID trả 404.
- [ ] Problem Details có traceId và errorCode.
- [ ] rowVersion được dùng ở update mutable aggregate.
- [ ] API implementation khớp schema và authorization matrix.
