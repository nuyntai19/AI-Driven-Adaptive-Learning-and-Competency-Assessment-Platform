# Danh mục thay đổi chờ commit — rà soát 09/10/2026

Snapshot so với HEAD `a9918f4`, trước khi thêm hai báo cáo rà soát ngày 09/10. Thư mục gốc: `D:/AI-Driven Adaptive Learning and Competency Assessment Platform`.

Đây là **toàn bộ working tree đang chờ**, không phải toàn bộ do ca lớp/giáo trình vừa rồi tạo ra. Các ca trước chưa commit riêng, nên một số tệp được sửa bởi nhiều ca; không thể quy toàn bộ số dòng của một tệp dùng chung cho ca mới nhất. Các tệp mới tính số dòng hiện tại, tệp tracked dùng git numstat. Không liệt kê payload ảnh/bản sao lưu/secret; không đọc hoặc in secret.

- 188 tệp văn bản/code trong snapshot, +20536/-320 dòng.
- 3 tệp schema sinh tự động chiếm +14138 dòng. Không phải toàn bộ là logic viết tay; vẫn phải rà soát schema/constraint tương ứng.
- 71 tệp được xác định có liên quan ca phạm vi lớp/áp dụng giáo trình; nhiều tệp dùng chung với sửa phân khối, quyền, huy hiệu hoặc ảnh đề trước đó.
- 3 tệp của sửa responsive trong lượt này; riêng hai tệp giao diện chỉ +5/-5, thêm 2 kiểm thử.
- 111 tệp còn lại thuộc các sửa trước đó/tài liệu/script khác hoặc chưa thể quy riêng cho ca mới.

## 1. Tệp liên quan phạm vi lớp/giáo trình

Tính năng: API context/applications; lọc lớp/giáo trình; ledger, actor/grade/tenant checks; biểu đồ phân nhóm; điều hướng; dữ liệu mẫu và kiểm thử. Số dòng dưới đây là delta toàn working tree của tệp, không khẳng định là delta riêng của lượt cuối.

| Tệp | Trạng thái | Delta |
| --- | --- | ---: |
| `docs/plans/ACADEMIC-SCOPE-WORKING-2026-10-08.md` | Mới | +95 / -0 |
| `docs/verification/STUDENT-ACADEMIC-SCOPE-AND-CURRICULUM-HISTORY-2026-10-08.md` | Mới | +106 / -0 |
| `scripts/ops/academic_scope_integrity.cjs` | Mới | +39 / -0 |
| `src/EduTwin.API/Controllers/CurriculumsController.cs` | Sửa | +19 / -0 |
| `src/EduTwin.API/Controllers/StudentsController.cs` | Sửa | +40 / -4 |
| `src/EduTwin.BLL/Assignments/ListStudentAssignmentsUseCase.cs` | Sửa | +6 / -0 |
| `src/EduTwin.BLL/CurriculumAndQuestions/ArchiveCurriculumUseCase.cs` | Sửa | +6 / -0 |
| `src/EduTwin.BLL/CurriculumAndQuestions/AssignCurriculumClassesUseCase.cs` | Sửa | +1 / -0 |
| `src/EduTwin.BLL/CurriculumAndQuestions/CurriculumApplicationUseCase.cs` | Mới | +112 / -0 |
| `src/EduTwin.BLL/CurriculumAndQuestions/DependencyInjection.cs` | Sửa | +2 / -0 |
| `src/EduTwin.BLL/CurriculumAndQuestions/PublishCurriculumUseCase.cs` | Sửa | +20 / -21 |
| `src/EduTwin.BLL/Dashboards/DependencyInjection.cs` | Sửa | +2 / -0 |
| `src/EduTwin.BLL/Dashboards/GetStudentDashboardUseCase.cs` | Sửa | +30 / -8 |
| `src/EduTwin.BLL/Dashboards/GetStudentWorkspaceSummaryUseCase.cs` | Mới | +111 / -0 |
| `src/EduTwin.BLL/Dashboards/IGetStudentDashboardUseCase.cs` | Sửa | +2 / -0 |
| `src/EduTwin.BLL/Dashboards/StudentAcademicScopeReader.cs` | Mới | +60 / -0 |
| `src/EduTwin.BLL/Organization/AddStudentsToClassUseCase.cs` | Sửa | +4 / -0 |
| `src/EduTwin.BLL/Organization/CreateStudentUseCase.cs` | Sửa | +23 / -1 |
| `src/EduTwin.BLL/Organization/DeleteClassUseCase.cs` | Sửa | +1 / -1 |
| `src/EduTwin.BLL/Organization/GetClassUseCase.cs` | Sửa | +2 / -0 |
| `src/EduTwin.BLL/Organization/ListClassesUseCase.cs` | Sửa | +1 / -0 |
| `src/EduTwin.BLL/Organization/UpdateClassUseCase.cs` | Sửa | +3 / -0 |
| `src/EduTwin.BLL/Recommendations/OpportunityCandidateBuilder.cs` | Sửa | +32 / -5 |
| `src/EduTwin.BLL/Seeding/EduTwinRuntimeSeeder.cs` | Sửa | +1 / -0 |
| `src/EduTwin.BLL/Seeding/ManifestEvaluator.cs` | Sửa | +18 / -3 |
| `src/EduTwin.Contracts/Assignments/ListStudentAssignmentsQuery.cs` | Sửa | +2 / -0 |
| `src/EduTwin.Contracts/CurriculumAndQuestions/ApplyCurriculumRequest.cs` | Mới | +25 / -0 |
| `src/EduTwin.Contracts/Dashboards/StudentAcademicContextDto.cs` | Mới | +26 / -0 |
| `src/EduTwin.Contracts/Dashboards/StudentDashboardDto.cs` | Sửa | +5 / -0 |
| `src/EduTwin.Contracts/Organization/ClassDto.cs` | Sửa | +1 / -0 |
| `src/EduTwin.Contracts/Organization/ClassLearningScope.cs` | Mới | +4 / -0 |
| `src/EduTwin.DAL/CurriculumAndQuestions/ClassCurriculumApplication.cs` | Mới | +32 / -0 |
| `src/EduTwin.DAL/Organization/Class.cs` | Sửa | +1 / -0 |
| `src/EduTwin.DAL/Persistence/Configurations/ClassConfiguration.cs` | Sửa | +3 / -0 |
| `src/EduTwin.DAL/Persistence/Configurations/ClassStudentConfiguration.cs` | Sửa | +2 / -0 |
| `src/EduTwin.DAL/Persistence/Configurations/CurriculumAndQuestions/ClassCurriculumApplicationConfiguration.cs` | Mới | +51 / -0 |
| `src/EduTwin.DAL/Persistence/EduTwinDbContext.cs` | Sửa | +2 / -0 |
| `src/EduTwin.DAL/Persistence/Migrations/20261008162602_AddAcademicClassScopeAndCurriculumApplications.cs` | Mới | +265 / -0 |
| `src/EduTwin.DAL/Seeding/EduTwinSeedFactory.cs` | Sửa | +56 / -21 |
| `src/EduTwin.DAL/Seeding/SeedDataContainer.cs` | Sửa | +1 / -0 |
| `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/AssignCurriculumClassesUseCaseTests.cs` | Sửa | +3 / -0 |
| `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/PublishCurriculumUseCaseTests.cs` | Sửa | +1 / -0 |
| `tests/EduTwin.BLL.Tests/Dashboards/AcademicScopeTests.cs` | Mới | +79 / -0 |
| `tests/EduTwin.BLL.Tests/Dashboards/DashboardBoundaryUnitTests.cs` | Sửa | +22 / -0 |
| `tests/EduTwin.BLL.Tests/Dashboards/DashboardMySqlIntegrationTests.cs` | Sửa | +244 / -0 |
| `tests/EduTwin.BLL.Tests/IdentityAndTenancy/GlobalQueryFilterTests.cs` | Sửa | +4 / -2 |
| `tests/EduTwin.BLL.Tests/Organization/AddStudentsToClassUseCaseTests.cs` | Sửa | +18 / -1 |
| `tests/EduTwin.BLL.Tests/Organization/CreateStudentUseCaseTests.cs` | Sửa | +24 / -0 |
| `tests/EduTwin.BLL.Tests/Recommendations/RecommendationCurriculumScopeTests.cs` | Sửa | +39 / -0 |
| `tests/EduTwin.BLL.Tests/Seeding/EduTwinRuntimeSeederTests.cs` | Sửa | +13 / -0 |
| `tests/EduTwin.BLL.Tests/Seeding/EduTwinSeedFactoryTests.cs` | Sửa | +27 / -0 |
| `web/edutwin-web/src/api/assignmentsApi.ts` | Sửa | +2 / -0 |
| `web/edutwin-web/src/api/dashboardsApi.ts` | Sửa | +19 / -1 |
| `web/edutwin-web/src/components/student/StudentAcademicContext.tsx` | Mới | +47 / -0 |
| `web/edutwin-web/src/components/student/StudentKnowledgeMap.tsx` | Sửa | +41 / -2 |
| `web/edutwin-web/src/components/student/StudentRadarChart.tsx` | Sửa | +13 / -2 |
| `web/edutwin-web/src/components/teacher/CurriculumApplicationPanel.tsx` | Mới | +72 / -0 |
| `web/edutwin-web/src/layouts/StudentLayout.tsx` | Sửa | +33 / -49 |
| `web/edutwin-web/src/pages/LearningPlayerPage.tsx` | Sửa | +15 / -9 |
| `web/edutwin-web/src/pages/StudentAssignmentDetailPage.tsx` | Sửa | +6 / -6 |
| `web/edutwin-web/src/pages/StudentAssignmentsPage.tsx` | Sửa | +5 / -2 |
| `web/edutwin-web/src/pages/StudentDashboardPage.tsx` | Sửa | +39 / -15 |
| `web/edutwin-web/src/pages/StudentTwinPage.tsx` | Sửa | +6 / -4 |
| `web/edutwin-web/src/pages/teacher/TeacherCurriculumEditorView.tsx` | Sửa | +3 / -1 |
| `web/edutwin-web/src/types/assignments.ts` | Sửa | +1 / -0 |
| `web/edutwin-web/src/types/dashboards.ts` | Sửa | +22 / -0 |
| `web/edutwin-web/src/types/organization.ts` | Sửa | +1 / -0 |
| `web/edutwin-web/src/utils/competencyGroups.ts` | Mới | +30 / -0 |
| `web/edutwin-web/src/utils/studentAcademicNavigation.ts` | Mới | +14 / -0 |
| `web/edutwin-web/tests/academicScopeCharts.test.ts` | Mới | +48 / -0 |
| `web/edutwin-web/tests/studentDashboardResponsiveAndFilter.test.ts` | Sửa | +17 / -19 |

## 2. Sửa responsive lần này

| Tệp | Trạng thái | Delta |
| --- | --- | ---: |
| `web/edutwin-web/src/components/teacher/TeacherPrimitives.tsx` | Sửa | +3 / -3 |
| `web/edutwin-web/src/pages/teacher/TeacherStudentManagementView.tsx` | Sửa | +2 / -2 |
| `web/edutwin-web/tests/teacherHeaderResponsive.test.ts` | Mới | +19 / -0 |

## 3. Schema sinh tự động

| Tệp | Trạng thái | Delta |
| --- | --- | ---: |
| `src/EduTwin.DAL/Persistence/Migrations/20261008095834_AddQuestionImages.Designer.cs` | Mới | +6861 / -0 |
| `src/EduTwin.DAL/Persistence/Migrations/20261008162602_AddAcademicClassScopeAndCurriculumApplications.Designer.cs` | Mới | +7041 / -0 |
| `src/EduTwin.DAL/Persistence/Migrations/EduTwinDbContextModelSnapshot.cs` | Sửa | +236 / -0 |

## 4. Các tệp khác đang chờ (không quy cho ca lớp/giáo trình)

Nhóm gồm quyền tài khoản, ảnh đề/đầu vào AI, liên kết chủ đề, phân khối lớp mẫu, UI dễ đọc, checkpoint/AI grading và các tài liệu/script cũ. Cần tách commit có mục đích khi được phép push; không gộp tất cả chỉ vì đang dirty.

| Tệp | Trạng thái | Delta |
| --- | --- | ---: |
| `docs/plans/AI-GRADING-OPTIMIZATION-PAUSED-2026-10-07.md` | Mới | +180 / -0 |
| `docs/verification/ACTOR-PERMISSIONS-AND-KNOWLEDGE-TOPIC-NAVIGATION-2026-10-08.md` | Mới | +53 / -0 |
| `docs/verification/GRADE-CLASS-SPLIT-AND-TEST-HISTORY-2026-10-08.md` | Mới | +117 / -0 |
| `docs/verification/MATH-10-12-KNOWLEDGE-GRAPH-AND-CURRICULA-2026-10-08.md` | Mới | +55 / -0 |
| `docs/verification/STUDENT-READABLE-UI-AND-QUESTION-IMAGES-2026-10-08.md` | Mới | +88 / -0 |
| `docs/verification/STUDENT-WORKSPACE-AND-ACCOUNT-ROLES-2026-10-08.md` | Mới | +68 / -0 |
| `scripts/e2e/acceptance_stack.cjs` | Mới | +75 / -0 |
| `scripts/e2e/acceptance.nginx.conf` | Mới | +7 / -0 |
| `scripts/e2e/assignment_api_acceptance.cjs` | Mới | +146 / -0 |
| `scripts/e2e/chrome_client.cjs` | Mới | +375 / -0 |
| `scripts/e2e/debug_player.cjs` | Mới | +35 / -0 |
| `scripts/e2e/fixture_manager.cjs` | Mới | +53 / -0 |
| `scripts/e2e/README.md` | Mới | +56 / -0 |
| `scripts/e2e/smoke_test.cjs` | Mới | +21 / -0 |
| `scripts/e2e/test_browser_flow.cjs` | Mới | +136 / -0 |
| `scripts/e2e/test_page_render.cjs` | Mới | +51 / -0 |
| `scripts/e2e/test_student_experience.cjs` | Mới | +171 / -0 |
| `scripts/ops/apply_ai_grading_proposals_local.cjs` | Mới | +61 / -0 |
| `scripts/ops/remove_demo_assignments.cjs` | Mới | +57 / -0 |
| `scripts/ops/repair_known_e2e_orphans.cjs` | Mới | +77 / -0 |
| `scripts/ops/runtime_inventory.cjs` | Mới | +34 / -0 |
| `scripts/ops/split_seed_grade_classes_local.cjs` | Mới | +75 / -0 |
| `scripts/seed_student_assignments.sql` | Mới | +74 / -0 |
| `scripts/start_api.ps1` | Mới | +15 / -0 |
| `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiAIService.cs` | Sửa | +2 / -2 |
| `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiPromptBuilder.cs` | Sửa | +6 / -3 |
| `src/EduTwin.API/AssessmentAndReasoning/AI/ReasoningBatchExecutor.cs` | Sửa | +1 / -1 |
| `src/EduTwin.API/AssessmentAndReasoning/AI/ReasoningMicroBatcher.cs` | Sửa | +1 / -1 |
| `src/EduTwin.API/Controllers/QuestionImagesController.cs` | Mới | +24 / -0 |
| `src/EduTwin.API/Security/CompositePermissionPolicies.cs` | Sửa | +2 / -0 |
| `src/EduTwin.BLL/AssessmentAndReasoning/AI/AnalyzeReasoningRequest.cs` | Sửa | +12 / -0 |
| `src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIAnalysisCheckpointStore.cs` | Sửa | +10 / -0 |
| `src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIAnalysisJobProcessor.cs` | Sửa | +12 / -1 |
| `src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIAnalysisRequestFactory.cs` | Sửa | +3 / -1 |
| `src/EduTwin.BLL/AssessmentAndReasoning/Processing/IAIAnalysisRequestFactory.cs` | Sửa | +2 / -1 |
| `src/EduTwin.BLL/AssessmentAndReasoning/ReviewQueue/ListTeacherReviewQueueUseCase.cs` | Sửa | +1 / -0 |
| `src/EduTwin.BLL/Assignments/GetStudentAssignmentUseCase.cs` | Sửa | +1 / -0 |
| `src/EduTwin.BLL/CurriculumAndQuestions/CreateQuestionUseCase.cs` | Sửa | +28 / -2 |
| `src/EduTwin.BLL/CurriculumAndQuestions/GetQuestionImageUseCase.cs` | Mới | +41 / -0 |
| `src/EduTwin.BLL/CurriculumAndQuestions/QuestionImageContent.cs` | Mới | +81 / -0 |
| `src/EduTwin.BLL/CurriculumAndQuestions/QuestionProjection.cs` | Sửa | +1 / -0 |
| `src/EduTwin.BLL/CurriculumAndQuestions/UpdateQuestionUseCase.cs` | Sửa | +33 / -4 |
| `src/EduTwin.BLL/IdentityAndTenancy/NewAccountRoleProvisioning.cs` | Mới | +40 / -0 |
| `src/EduTwin.BLL/Organization/CreateStudentResult.cs` | Sửa | +3 / -1 |
| `src/EduTwin.BLL/Organization/CreateTeacherUseCase.cs` | Sửa | +3 / -0 |
| `src/EduTwin.BLL/Organization/UpdateStudentResult.cs` | Sửa | +3 / -1 |
| `src/EduTwin.BLL/Organization/UpdateStudentUseCase.cs` | Sửa | +12 / -0 |
| `src/EduTwin.BLL/Recommendations/RecommendationEngine.cs` | Sửa | +2 / -1 |
| `src/EduTwin.BLL/Seeding/AuthorizationBootstrapper.cs` | Sửa | +64 / -19 |
| `src/EduTwin.BLL/Seeding/DefaultSystemRolePermissionBackfill.cs` | Mới | +97 / -0 |
| `src/EduTwin.BLL/Seeding/SeedExtensions.cs` | Sửa | +6 / -0 |
| `src/EduTwin.Contracts/AssessmentAndReasoning/TeacherReviewQueueItemDto.cs` | Sửa | +1 / -0 |
| `src/EduTwin.Contracts/Assignments/StudentQuestionDto.cs` | Sửa | +1 / -0 |
| `src/EduTwin.Contracts/CurriculumAndQuestions/CreateQuestionRequest.cs` | Sửa | +2 / -0 |
| `src/EduTwin.Contracts/CurriculumAndQuestions/QuestionDto.cs` | Sửa | +1 / -0 |
| `src/EduTwin.Contracts/CurriculumAndQuestions/StudentQuestionDto.cs` | Sửa | +1 / -0 |
| `src/EduTwin.Contracts/CurriculumAndQuestions/UpdateQuestionRequest.cs` | Sửa | +2 / -0 |
| `src/EduTwin.Contracts/Dashboards/StudentWorkspaceSummaryDto.cs` | Mới | +12 / -0 |
| `src/EduTwin.Contracts/Recommendations/NextQuestionResponse.cs` | Sửa | +2 / -1 |
| `src/EduTwin.DAL/CurriculumAndQuestions/Question.cs` | Sửa | +1 / -0 |
| `src/EduTwin.DAL/CurriculumAndQuestions/QuestionImage.cs` | Mới | +16 / -0 |
| `src/EduTwin.DAL/Persistence/Configurations/CurriculumAndQuestions/QuestionConfiguration.cs` | Sửa | +2 / -0 |
| `src/EduTwin.DAL/Persistence/Configurations/CurriculumAndQuestions/QuestionImageConfiguration.cs` | Mới | +25 / -0 |
| `src/EduTwin.DAL/Persistence/Migrations/20261008095834_AddQuestionImages.cs` | Mới | +58 / -0 |
| `src/EduTwin.DAL/Seeding/AuthorizationPermissionCatalog.cs` | Sửa | +3 / -1 |
| `src/EduTwin.DAL/Seeding/DeterministicSeedIds.cs` | Sửa | +16 / -0 |
| `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/AIAnalysisContractTests.cs` | Sửa | +3 / -1 |
| `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiPromptBuilderTests.cs` | Sửa | +25 / -0 |
| `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/IAIServiceContractTests.cs` | Sửa | +5 / -0 |
| `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/ReasoningMicroBatchTests.cs` | Sửa | +12 / -0 |
| `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/Processing/AIAnalysisJobProcessorQuestionImageTests.cs` | Mới | +37 / -0 |
| `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/Processing/AIAnalysisRequestFactoryTests.cs` | Sửa | +14 / -0 |
| `tests/EduTwin.BLL.Tests/Assignments/CreateAssignmentUseCaseTests.cs` | Sửa | +3 / -2 |
| `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/CreateQuestionUseCaseTests.cs` | Sửa | +82 / -0 |
| `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/QuestionImageContentTests.cs` | Mới | +48 / -0 |
| `tests/EduTwin.BLL.Tests/Dashboards/StudentWorkspaceSummaryTests.cs` | Mới | +133 / -0 |
| `tests/EduTwin.BLL.Tests/IdentityAndTenancy/AuthorizationBootstrapperTests.cs` | Sửa | +137 / -1 |
| `tests/EduTwin.BLL.Tests/IdentityAndTenancy/NewAccountRoleProvisioningTests.cs` | Mới | +116 / -0 |
| `tests/EduTwin.BLL.Tests/Organization/AccountRoleTestSeed.cs` | Mới | +21 / -0 |
| `tests/EduTwin.BLL.Tests/Organization/CreateTeacherUseCaseTests.cs` | Sửa | +12 / -0 |
| `tests/EduTwin.BLL.Tests/Organization/UpdateStudentUseCaseTests.cs` | Sửa | +23 / -0 |
| `web/edutwin-web/nginx.conf` | Sửa | +3 / -0 |
| `web/edutwin-web/src/api/learningFeedbackApi.ts` | Sửa | +2 / -0 |
| `web/edutwin-web/src/App.tsx` | Sửa | +2 / -1 |
| `web/edutwin-web/src/components/QuestionImage.tsx` | Mới | +32 / -0 |
| `web/edutwin-web/src/components/reviews/AssignmentGradingWorkspace.tsx` | Sửa | +2 / -0 |
| `web/edutwin-web/src/components/teacher/QuestionImageEditor.tsx` | Mới | +30 / -0 |
| `web/edutwin-web/src/features/questions/useQuestions.ts` | Sửa | +5 / -2 |
| `web/edutwin-web/src/layouts/CenterManagerLayout.tsx` | Sửa | +8 / -2 |
| `web/edutwin-web/src/pages/AuthorizationManagementPage.tsx` | Sửa | +2 / -0 |
| `web/edutwin-web/src/pages/StudentListPage.tsx` | Sửa | +19 / -5 |
| `web/edutwin-web/src/pages/teacher/TeacherAssignmentEditorView.tsx` | Sửa | +146 / -37 |
| `web/edutwin-web/src/pages/teacher/TeacherKnowledgeGraphView.tsx` | Sửa | +36 / -19 |
| `web/edutwin-web/src/pages/teacher/TeacherQuestionBankView.tsx` | Sửa | +75 / -18 |
| `web/edutwin-web/src/pages/teacher/TeacherQuestionEditorView.tsx` | Sửa | +23 / -4 |
| `web/edutwin-web/src/styles/student-theme.css` | Sửa | +37 / -5 |
| `web/edutwin-web/src/types/learning.ts` | Sửa | +1 / -0 |
| `web/edutwin-web/src/types/questions.ts` | Sửa | +5 / -0 |
| `web/edutwin-web/src/types/reviews.ts` | Sửa | +1 / -0 |
| `web/edutwin-web/src/utils/knowledgeTopicContext.ts` | Mới | +86 / -0 |
| `web/edutwin-web/src/utils/questionImage.ts` | Mới | +27 / -0 |
| `web/edutwin-web/src/utils/studentClassGrades.ts` | Mới | +8 / -0 |
| `web/edutwin-web/src/utils/studentWorkspaceSummary.ts` | Mới | +16 / -0 |
| `web/edutwin-web/tests/centerManagerDesignSystem.test.ts` | Sửa | +4 / -1 |
| `web/edutwin-web/tests/governanceRoleBoundary.test.ts` | Mới | +46 / -0 |
| `web/edutwin-web/tests/gradingWorkflow.test.ts` | Sửa | +1 / -0 |
| `web/edutwin-web/tests/knowledgeTopicQuickActions.test.ts` | Mới | +164 / -0 |
| `web/edutwin-web/tests/questionImage.test.ts` | Mới | +21 / -0 |
| `web/edutwin-web/tests/studentClassGrades.test.ts` | Mới | +21 / -0 |
| `web/edutwin-web/tests/studentReadableTheme.test.ts` | Mới | +18 / -0 |
| `web/edutwin-web/tests/studentWorkspaceSummary.test.ts` | Mới | +28 / -0 |

## Tệp phát sinh sau snapshot

- `docs/verification/CLASS-LIFECYCLE-AUDIT-2026-10-09.md`: nguyên nhân, các vấn đề nghiệp vụ cần duyệt hướng sửa, kết quả responsive và phạm vi kiểm chứng.
- `docs/verification/CHANGE-INVENTORY-2026-10-09.md`: bản danh mục này.
- Bằng chứng responsive trong `docs/verification/evidence/class-scope-review/`, không phải source logic.

Chưa push. Không tự chỉnh migration đã áp dụng; mọi sửa dữ liệu lớp cần phương án bổ sung được duyệt, bản sao lưu, dấu vết thực hiện và đối chiếu bài làm cũ.
