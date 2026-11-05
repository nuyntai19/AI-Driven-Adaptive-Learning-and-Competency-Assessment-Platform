# Kế hoạch sửa phạm vi tri thức theo khối, giáo trình và lớp

Ngày kiểm tra: 22/10/2026. Trạng thái: **ĐÃ HOÀN THÀNH — KIỂM CHỨNG 23/10/2026**.

Người dùng đã duyệt triển khai kế hoạch trong chat ngày 22/10/2026 và tiếp tục duyệt [bảng phân loại ban đầu](../verification/KNOWLEDGE-GRADE-PROPOSALS-2026-10-22.md). Nội dung audit dưới đây ghi trạng thái trước triển khai; kết quả thực hiện và bằng chứng nghiệm thu ở [báo cáo kiểm chứng ngày 23/10/2026](../verification/KNOWLEDGE-GRADE-CLASS-SCOPE-REPORT-2026-10-23.md).

Yêu cầu: kiểm tra việc giáo trình/đồ thị tri thức chưa phân biệt khối và lớp, bao gồm database; lập kế hoạch sửa trước khi thực hiện. Đợt này chỉ đọc source, kiểm tra MySQL bằng SELECT trong transaction READ ONLY và chạy kiểm thử hiện có. Chưa sửa mã ứng dụng, migration, dữ liệu, seed hay triển khai dịch vụ.

## 1. Kết luận

**Có lỗi về phạm vi nghiệp vụ xuyên suốt database → API → giao diện.** Giáo trình và lớp đã có khối, nhưng node/edge tri thức chỉ thuộc trung tâm và môn. Không có quan hệ node–khối để API lọc hoặc database kiểm tra khi ghép giáo trình. Vì vậy đổi khối vẫn thấy cùng thư viện tri thức là hành vi xác nhận được từ mã, không phải suy đoán do cache.

Không đồng nhất việc dùng chung kiến thức với dữ liệu sai: một năng lực có thể dùng cho nhiều khối. Vấn đề là hiện chưa khai báo được phạm vi đó và chưa phân biệt đồ thị thư viện với đồ thị giáo trình/lớp. Chưa có bằng chứng mọi node đang dùng chung đều sai về chuyên môn.

Không phát hiện dữ liệu sai môn hoặc lớp áp dụng sai khối ngoài cơ chế ngoại lệ trong các truy vấn kiểm tra bên dưới. Đây chủ yếu là thiếu mô hình phân loại và guard; không phải kết luận database bị hỏng hoặc mất khóa ngoại.

## 2. Bằng chứng source

Đường dẫn trong bảng tính từ thư mục gốc repository; số dòng tại thời điểm kiểm tra.

| Mức độ | Phát hiện | Bằng chứng |
| --- | --- | --- |
| Cao | Node/edge không có khối hoặc mapping phạm vi; unique mã node hiện theo trung tâm + môn | `src/EduTwin.DAL/KnowledgeGraph/KnowledgeNode.cs:10`, `KnowledgeEdge.cs:10`; `src/EduTwin.DAL/Persistence/Configurations/KnowledgeGraph/KnowledgeNodeConfiguration.cs:90` |
| Cao | Graph GET chỉ nhận môn, lấy toàn bộ node/edge của môn; list node không nhận khối/lớp/giáo trình | `src/EduTwin.API/Controllers/KnowledgeGraphController.cs:38`; `src/EduTwin.BLL/KnowledgeGraph/GetKnowledgeGraphUseCase.cs:65`; `src/EduTwin.Contracts/KnowledgeGraph/KnowledgeNodeListQuery.cs:7` |
| Cao | Editor lấy node với key `[knowledgeNodes, subjectId]`; đổi khối chỉ cập nhật state, không tải lại phạm vi hay kiểm tra node đã chọn | `web/edutwin-web/src/pages/teacher/TeacherCurriculumEditorView.tsx:106`, `:277`, `:483` |
| Cao | Gán node chỉ kiểm trung tâm/môn/active; đổi khối bản nháp không kiểm lại node và lớp dự kiến; update còn nhận grade null | `src/EduTwin.BLL/CurriculumAndQuestions/AssignCurriculumNodesUseCase.cs:90`; `UpdateCurriculumUseCase.cs:49`, `:91` |
| Trung bình | Danh sách giáo trình Teacher thiếu bộ lọc khối/lớp/năm học; client/API đã có grade filter nhưng màn hình hiện tại chưa dùng | `web/edutwin-web/src/pages/teacher/TeacherCurriculumListView.tsx:57`, `:251`; `web/edutwin-web/src/api/curriculumApi.ts:24` |
| Trung bình | Graph chỉ cache theo môn; link mở graph từ editor không mang theo khối/giáo trình; đổi scope chưa reset canvas đúng phạm vi | `web/edutwin-web/src/pages/teacher/TeacherKnowledgeGraphView.tsx:105`, `:206`, `:826`; `TeacherCurriculumEditorView.tsx:582` |
| Trung bình | Response sau gán node thiếu GradeLevel mặc dù DTO có trường này | `src/EduTwin.BLL/CurriculumAndQuestions/AssignCurriculumNodesUseCase.cs:158` |
| Cao, downstream | Tính predicted score/risk dùng toàn bộ Topic môn; thêm chủ đề ngoài chương trình có thể thay đổi kết quả | `src/EduTwin.BLL/DigitalTwin/StudentGoalRiskUpdater.cs:41`, `:81` |
| Cao, downstream | Pool câu hỏi V4/adaptive chọn theo topic mà chưa lọc grade; cùng node dùng nhiều khối có thể lấy câu khác khối | `src/EduTwin.BLL/Recommendations/Planning/LearningPathV4Data.cs:39`; `src/EduTwin.BLL/Recommendations/AdaptiveQuestionSelector.cs:49` |
| Cần regression | V4 đọc prerequisite toàn môn và có thể block vì source ngoài tập node hiện tại không có status | `src/EduTwin.BLL/Recommendations/Planning/LearningPathV4Data.cs:68`, `:94`, `:124` |

Màn hình đang chạy là Teacher views qua `App.tsx:232` và các workspace trong `components/academic/`; sửa riêng các trang legacy `CurriculumListPage`/`KnowledgeGraphPage` không giải quyết các ảnh người dùng gửi.

Các phần đúng cần giữ:

- Database có trigger kiểm cùng trung tâm/môn cho curriculum-node và edge; có kiểm lớp, giáo viên, trạng thái, ngoại lệ khối khi áp dụng giáo trình. Không đề xuất bỏ các ràng buộc này.
- `StudentLearningScope` đã có phạm vi lớp → application → giáo trình Published → node. Không kết luận mọi màn hình học sinh đang lấy toàn môn. Nhánh tự luyện khi chưa từng có lớp vẫn cho toàn môn và cần điều chỉnh theo khối.
- Lớp dự kiến của Draft khác với lớp đang áp dụng Published. `class_curriculum_applications` giữ Primary/Supplemental và lịch sử; không lấy mọi dòng `curriculum_classes` làm phạm vi học hiện tại.
- Published/Archived có khóa sửa nội dung node và lịch sử. Knowledge Twin theo student + node giúp giữ năng lực khi chuyển lớp; không nhân bản Twin chỉ vì đổi lớp.

## 3. Kiểm tra MySQL đang chạy

Môi trường: container local `edutwin-mysql`, database `edutwin`, MySQL `8.0.35`. Mọi truy vấn audit thực hiện với `SET SESSION TRANSACTION READ ONLY; START TRANSACTION READ ONLY; ... COMMIT;`. Không in credential hay dữ liệu bài làm cá nhân.

### 3.1. Schema và migration

- 50 migration đã áp dụng; migration mới nhất `20261015180050_AddLearningPathV4Entities`, trùng tên và số lượng migration source hiện tại. Đây là đối chiếu lịch sử migration, không thay cho kiểm tra toàn bộ schema drift.
- `curriculums.grade_level` và `classes.grade_level` tồn tại; lớp có `academic_year`.
- `knowledge_nodes`/`knowledge_edges` không có grade/class/curriculum scope; không có mapping khối.
- Index node code thực tế là `(center_id, subject_id, node_code)`, đúng với cấu hình EF.
- Đã đối chiếu trigger đang tồn tại cho curriculum-node, application, vòng đời lớp/node/edge và tính bất biến lịch sử; không chỉ đọc migration chưa áp dụng.

### 3.2. Dữ liệu hiện tại

| Bảng | Số bản ghi |
| --- | ---: |
| knowledge_nodes | 13 |
| knowledge_edges | 8 |
| curriculums | 13 |
| curriculum_nodes | 27 |
| classes | 12 |
| curriculum_classes | 12 |
| class_curriculum_applications | 12 |

Trong cả `EDUTWIN_A` và `EDUTWIN_B`, ba giáo trình ENGLISH khối 10/11/12 đều chứa cùng ba node `ENG-TENSES`, `ENG-RELATIVE-CLAUSES`, `ENG-CONTEXT-VOCAB`. Điều này phù hợp seed hiện tại (`EduTwinSeedFactory.cs:171`), chưa chứng minh nội dung đó phù hợp tất cả khối.

Ở `EDUTWIN_A`:

- `MATH-FUNCTIONS` thuộc giáo trình khối 10 và bản nháp khối 12 V2.
- `MATH-EXP-LOG` thuộc giáo trình khối 11 và bản nháp khối 12 V2.
- `MATH-ANTIDERIVATIVE` thuộc giáo trình khối 12 và bản nháp khối 12 V2.
- Node `20006 / MATH10_PTBH` chưa thuộc giáo trình nào. Không thể xác nhận khối từ database; phần `10` trong mã chỉ là tên do người dùng đặt.
- Ba giáo trình Toán Published đang có 1 node/giáo trình; bản V2 Draft có 3 node, chưa áp dụng lớp.

Cả hai trung tâm đều có 15 câu ENGLISH chưa xóa và tất cả đang mang grade 10. Kết hợp với việc selector chỉ lọc topic, đây là nguy cơ rõ ràng khi học ở khối 11/12; chưa chạy phiên học thực tế để kết luận đã phát câu sai khối cho học sinh cụ thể.

### 3.3. Kiểm tra tính nhất quán

Các phép JOIN đối chiếu sau đều có **0 bản ghi lỗi**:

1. Curriculum-node khác môn.
2. Edge khác môn với một trong hai đầu.
3. Parent khác môn với node con.
4. Application khác môn với lớp hoặc giáo trình.
5. Application đang hiệu lực có lớp/giáo trình khác khối mà không đánh dấu ngoại lệ.
6. Giáo trình chưa xóa thiếu grade.
7. Application đang hiệu lực thiếu projection tương ứng trong `curriculum_classes`, hoặc projection không có application đang hiệu lực.

Không thể kiểm tra “node sai khối giáo trình” bằng ràng buộc hiện tại: node chưa có dữ liệu khối để so sánh. Không suy ra tính đúng chuyên môn chỉ vì các kiểm tra trên có kết quả 0.

## 4. Mô hình đề xuất để duyệt

### 4.1. Phân biệt các phạm vi

| Phạm vi | Nguồn xác định | Hành vi |
| --- | --- | --- |
| Thư viện môn/khối | Trung tâm + môn + mapping khối đã duyệt | Chọn nguồn tri thức để soạn chương trình |
| Giáo trình | Curriculum + grade mục tiêu + CurriculumNodes theo thứ tự | Chỉ hiển thị cấu trúc thuộc giáo trình đó |
| Lớp hiện tại | Class + application Primary/Supplemental đang hiệu lực + giáo trình Published | Hợp cấu trúc thực sự được áp dụng, giữ nguồn từng giáo trình/khối; thống kê năng lực khử trùng theo NodeId |
| Lịch sử lớp | Class + các application tại mốc/lịch sử được yêu cầu | Xem lại riêng, không đưa vào phạm vi học hiện tại |

Năm học lấy từ lớp. Giáo trình được tái sử dụng qua nhiều lớp/năm, nên không bắt buộc gắn một `class_id` hoặc một `academic_year` vào bản giáo trình dùng chung.

Cùng môn và khối nhưng hai lớp dùng giáo trình khác nhau được phép có đồ thị khác nhau. Hai lớp dùng cùng giáo trình có thể có đồ thị giống nhau. Lớp chưa áp dụng giáo trình trả trạng thái trống có lý do, không tự chuyển sang toàn môn.

Ngoại lệ học bổ sung khác khối vẫn được giữ: phạm vi lớp lấy grade của từng giáo trình được áp dụng hợp lệ, kèm nhãn ngoại lệ và lý do; không lọc cứng tất cả node bằng grade của lớp rồi làm mất phần bổ trợ.

### 4.2. Bổ sung dữ liệu, giữ định danh tri thức

Đề xuất giữ node canonical theo môn, thêm phiên bản cấu trúc `knowledge_graph_versions` theo môn/khối và mapping nhiều–nhiều `knowledge_node_grades` thuộc phiên bản:

- Mỗi phiên bản có center/subject/grade và trạng thái Draft/Published/Archived; grade chỉ 10/11/12. Thư viện khối dùng phiên bản hiện hành đã duyệt; giáo trình tham chiếu phiên bản cụ thể.
- Mapping có khóa duy nhất `(center_id, graph_version_id, node_id)`; grade/subject được xác định và kiểm nhất quán qua phiên bản. Bổ sung index truy vấn phiên bản hiện hành theo `(center_id, subject_id, grade_level)` và mapping theo NodeId.
- Một node có thể khai báo nhiều khối khi thực sự cùng một năng lực. Nếu nội dung/mức độ năng lực khác nhau phải tạo node mới có định danh riêng; không dùng chung NodeId chỉ vì tên giống nhau.
- Node chưa phân loại là “Chưa xác định khối”, không có nghĩa “áp dụng mọi khối”. Không cho chọn vào giáo trình mới trước khi phân loại hợp lệ.
- Parent dùng cho cây theo khối lưu trong mapping, có khóa ngoại đến parent cùng phiên bản và kiểm chu trình. `KnowledgeNode.ParentNodeId` hiện tại được giữ để tương thích trong chuyển đổi; view mới resolve parent từ phiên bản đã đối chiếu, không dùng parent canonical để tái dựng lịch sử nhiều khối.

Thêm `knowledge_edge_grades` thuộc phiên bản để khai báo quan hệ áp dụng ở khối nào, vẫn giữ `EdgeId` hiện có. `Weight` và `RelationType` được chốt trong mapping phiên bản, không sửa edge canonical đã chia sẻ. Đây là lựa chọn triển khai để cô lập cả loại quan hệ và trọng số giữa các khối. Chỉ lọc hai đầu theo node-grade mà giữ weight/relation chung sẽ chưa đủ. Các relation thông thường yêu cầu hai đầu thuộc cùng phiên bản; prerequisite xuyên khối khai báo nguồn ngoài phiên bản, hiển thị riêng theo grade đích. Giữ kiểm DAG prerequisite và cycle cây.

Khi publish, curriculum chốt `graph_version_id` bất biến và cấu trúc CurriculumNodes tương ứng. Parent/edge/mapping của phiên bản đã được Published/Archived hoặc application history tham chiếu không được UPDATE/DELETE; thay cấu trúc tạo phiên bản mới, vẫn giữ NodeId để giữ mastery. Application ledger chỉ ghi thời gian áp dụng, không tự snapshot topology. Current/History phải đọc phiên bản được curriculum tham chiếu. Không tuyên bố dựng được topology quá khứ trước ngày migration nếu trước đây không có snapshot.

Khi Primary/Supplemental cùng chứa một NodeId ở các khối hoặc phiên bản khác nhau, resolver giữ `(node_id, graph_version_id)` và nguồn curriculum/grade. Canvas hiển thị các vị trí riêng khi parent/quan hệ khác nhau, có nhãn giáo trình/khối; không ép về một ParentNodeId. Thống kê năng lực và mastery vẫn khử trùng theo NodeId.

Các mapping mới cần audit actor/time, concurrency khi sửa và guard không cho xóa phạm vi đang được giáo trình Published sử dụng. Cần có kiểm tra tại BLL và MySQL cho cả gán node, đổi grade giáo trình và sửa/xóa mapping. Foreign key bảo vệ tenant/subject; trigger hoặc transaction guard bảo vệ quan hệ grade và dependency nhiều bảng.

Không nhân bản toàn bộ node theo lớp; không đổi NodeId/EdgeId hiện có. Nội dung node dùng chung phải hiển thị rõ các khối bị ảnh hưởng; sửa nội dung Published tiếp tục tuân theo cơ chế khóa/clone. Giao diện lớp là phép chiếu để xem, thao tác cấu trúc thực hiện tại thư viện/giáo trình có quyền phù hợp.

### 4.3. Ranh giới prerequisite

Cạnh nội bộ chỉ hiển thị khi hai đầu nằm trong phép chiếu đang xem và relation thuộc grade tương ứng. Prerequisite nằm ngoài giáo trình/khối được trả riêng thành ngữ cảnh, có nhãn nguồn và khối; không đếm thành chủ đề của giáo trình đích.

Không xóa prerequisite ngoài phạm vi một cách máy móc: legacy có kiểm thử cố ý dùng mastery cũ của prerequisite ngoài curriculum. Thống nhất V4 với chính sách sử dụng bằng chứng hợp lệ hoặc đánh giá bổ sung, tránh coi thiếu node trong projection là chưa đạt và block vĩnh viễn.

## 5. Các bước triển khai sau khi duyệt

### Bước 1 — Khóa contract và chuẩn bị phân loại

- Cập nhật đồng bộ `PROJECT_REQUIREMENTS.md`, `CONSTITUTION.md`, `DATABASE_SCHEMA.md`, `API_CONTRACTS.md`, `UI_UX_SPEC.md` và tài liệu triển khai liên quan. Yêu cầu cũ chỉ nêu graph theo subject; đây là bổ sung nghiệp vụ chính thức.
- Xuất manifest đề xuất node → khối, parent → khối và edge → khối từ giáo trình/câu hỏi hiện tại. Ghi nguồn bằng chứng, node đa khối, node chưa gán và xung đột.
- Liên kết hiện tại là bằng chứng sử dụng, không tự thành xác nhận học thuật. Không tự suy grade từ tên/mã, hoặc tự gán toàn bộ 3 khối để vượt kiểm tra.
- Chốt manifest khi triển khai: nhóm nội dung đa khối ENGLISH, các node Toán dùng trong V2 và `MATH10_PTBH` phải được rà soát. Nếu phân loại mới làm thay đổi chương trình Published thì lập phiên bản mới và application tương ứng, giữ lịch sử cũ.

Đầu ra: contract, manifest có thể review và danh sách dữ liệu cần quyết định trước khi chuyển sang chế độ lọc mới.

### Bước 2 — Migration bổ sung và guard dữ liệu

- Thêm bảng phiên bản và mapping, FK phiên bản của curriculum, EF configuration/snapshot, index, FK/check/trigger cần thiết; không reset database hoặc chạy lại seed để thay dữ liệu cũ.
- Backfill chỉ theo manifest được duyệt; script có dry-run, chạy lại không nhân đôi và báo số lượng trước/sau.
- Ràng buộc grade khi tạo/gán/publish giáo trình; khi đổi grade Draft, kiểm lại node và lớp dự kiến trong transaction. Trả lỗi có danh sách xung đột để xử lý, không tự xóa lựa chọn.
- Khóa grade mục tiêu hợp lệ cho thao tác create/update mới; vẫn đọc được dữ liệu legacy chưa phân loại. Sửa response AssignNodes để trả GradeLevel nhất quán.
- Sửa/tháo mapping hoặc parent/edge phải kiểm mọi dependency liên quan; bảo vệ Published và lịch sử. Giữ cơ chế application khác khối có lý do/actor/time đã có.
- Tạo/sửa/import câu hỏi kiểm grade của Primary/Secondary mapping; prerequisite khác khối phải tường minh, không cấm nhầm nghiệp vụ ôn nền.

Đầu ra: schema và guard có integration test MySQL thật, manifest được áp dụng trên bản sao kiểm thử.

### Bước 3 — API resolve phạm vi tại server

- Graph/list node hỗ trợ phạm vi môn–khối/phiên bản, giáo trình hoặc lớp. Server resolve lại subject/grade/academicYear/version từ curriculum/class, từ chối tham số mâu thuẫn và ID giả.
- Response trả `resolvedScope`, grade applicability, nguồn giáo trình áp dụng, các node/cạnh nội bộ và prerequisite ngoài phạm vi riêng.
- List giáo trình bổ sung ClassId/AcademicYear/current-history; dùng GradeLevel filter đã có. Trả tên lớp/năm học cho lớp người gọi được quyền xem, phân biệt dự kiến/đang áp dụng.
- Shared material không làm lộ lớp, lịch sử hoặc học sinh của chủ sở hữu; lớp người gọi áp dụng giáo trình Shared được kiểm quyền theo application của chính họ.
- Dùng chung một bộ resolve scope cho graph, node picker và downstream; không để mỗi màn hình suy phạm vi khác nhau. Nhánh legacy application fallback phải có điều kiện chuyển đổi rõ, không hồi sinh lớp đã kết thúc.

Đầu ra: contract/API có kiểm quyền tenant, teacher ownership và các trường hợp scope rỗng/mâu thuẫn.

### Bước 4 — Giao diện đúng phạm vi

- Danh sách giáo trình: thêm lọc khối, lớp, năm học và trạng thái áp dụng; card hiển thị lớp + năm học theo quyền, thay vì chỉ số lượng.
- Editor: chọn môn/khối trước khi tải thư viện. Đổi grade tải thư viện mới, đánh dấu node đã chọn không tương thích và yêu cầu xử lý trước lưu; bảo vệ phản hồi request cũ đến trễ.
- Mở graph từ editor mang theo môn/khối/curriculum. Bản Draft chưa lưu mở thư viện môn–khối; muốn xem cấu trúc bản nháp chính xác thì lưu Draft trước, không tạo nhầm curriculum Published.
- Graph: hiển thị phạm vi ở tiêu đề/bộ lọc, nhãn khối và nguồn giáo trình; tách thư viện, giáo trình và lớp. Scope lớp cho biết Primary/Supplemental, ngoại lệ và năm học.
- Canvas selection/viewport reset theo toàn bộ scope. Query key thống nhất center/user/subject/grade/version/curriculum/class/history; mutation invalidate cả graph và thư viện node liên quan.
- Giữ scope khi chuyển sang ngân hàng câu hỏi/bài tập; empty state không hiển thị toàn môn thay thế.

Đầu ra: các Teacher views đang được route sử dụng được sửa và kiểm tra luồng ảnh người dùng gửi.

### Bước 5 — Đồng bộ học sinh, câu hỏi và chỉ số

- `StudentLearningScope`: lớp dùng application hiện hành; tự luyện độc lập dùng khối học sinh đã xác thực. Thiếu phạm vi thì yêu cầu chọn/hoàn thiện thông tin, không mặc định toàn môn.
- Adaptive/V4 lọc câu theo grade mục tiêu của ngữ cảnh học. Câu khác grade chỉ được đưa vào khi đang học giáo trình bổ trợ/ngoại lệ hoặc prerequisite được khai báo rõ; không trộn do dùng chung NodeId.
- Sửa predicted score/risk để lấy tập topic thuộc phạm vi hiện tại. StudentSubjectGoal vẫn theo môn: khi học nhiều lớp thì union node trong các lớp hiện hành và khử trùng; không ghi một chỉ số lớp vào goal toàn môn. Phạm vi trả ra phải được giải thích rõ.
- Rà graph dashboard/radar/recommendation dùng resolver chung, xử lý prerequisite ngoài scope nhất quán. Đổi policy/version khi kết quả dẫn xuất thay đổi.
- Không tự chạy lại toàn bộ điểm, mastery, lịch sử hay risk trong đợt migration. Việc tái tính dữ liệu dẫn xuất cần kế hoạch riêng có snapshot trước/sau, không sửa evidence gốc.

Đầu ra: thêm node ở khối khác không làm biến động chỉ số hiện tại; câu hỏi và lộ trình đúng ngữ cảnh được chọn.

### Bước 6 — Kiểm thử và chuyển đổi

- Test BLL/contract/frontend theo ma trận nghiệm thu dưới đây; MySQL thật kiểm được trigger, FK, transaction/concurrency mà InMemory không bảo đảm.
- Chạy migration trên bản sao database và fresh database; kiểm dry-run/rerun, fingerprint dữ liệu được bảo vệ và restore.
- Trước áp dụng local thật: backup riêng, thống kê IDs/links, fingerprint questions/attempts/analyses/evidence/Twins/history. Bật lọc mới chỉ khi phân loại đủ toàn bộ phạm vi Published đang hoạt động và xử lý xong xung đột.
- Deploy API/Web tương thích schema mới cùng đợt. Kiểm cache và scope links từ trang cũ; endpoint subject-only nếu còn hỗ trợ phải ghi rõ đây là tổng quan thư viện, không được picker dùng làm fallback.
- Rollback ưu tiên quay về build cũ với schema bổ sung giữ nguyên, sau khi đã test tương thích. Không dùng Down migration xóa mapping hoặc phục hồi DB cũ đè lên evidence mới phát sinh; nếu phải restore cần chặn ghi và kế hoạch bảo toàn phần phát sinh.

## 6. Ma trận nghiệm thu bắt buộc

| Tình huống | Kết quả cần có |
| --- | --- |
| Toán khối 10 so với 12 | Thư viện/graph chỉ có node được khai báo cho khối tương ứng; dùng chung chỉ khi mapping tường minh |
| Đổi grade trong editor sau khi đã chọn node/lớp | Nạp scope mới, chỉ ra xung đột; không lưu được cấu trúc sai grade |
| Gửi request trực tiếp, bỏ qua UI | Create/Assign/Update/Publish/import sai grade bị từ chối, DB guard cũng hoạt động |
| Hai lớp cùng khối nhưng khác giáo trình | Graph theo từng lớp khác đúng tập CurriculumNodes |
| Primary + Supplemental | Hợp đúng node, không đếm trùng, giữ grade ngoại lệ có lý do |
| Cùng tên lớp ở hai năm học | Chọn đúng ClassId và hiển thị năm học; không trộn Current/History |
| Lớp chưa có giáo trình, application đã kết thúc, thành viên đã rời lớp | Empty/read-only đúng ngữ cảnh, không fallback toàn môn |
| Shared curriculum của giáo viên khác | Đọc/áp dụng theo quyền, không lộ lớp của chủ sở hữu |
| Node/edge/parent nhiều khối | Parent/weight/relation không đổi ngầm giữa các khối; lịch sử đọc phiên bản đã chốt; cùng NodeId nhiều vị trí giữ được nguồn |
| Prerequisite ở khối thấp hơn | Có nhãn ngữ cảnh; dùng evidence hợp lệ, không block vĩnh viễn vì thiếu status trong projection |
| Cùng topic có câu grade 10/11/12 | Selector chỉ lấy grade hợp lệ với ngữ cảnh, trừ bổ trợ được khai báo rõ |
| Thêm node ở khối khác | Không làm giảm predicted score/tăng risk của học sinh ngoài phạm vi |
| Đổi scope nhanh, request trả sai thứ tự, quay lại từ graph | Không dùng lại node list/canvas cache sai scope |
| Migration và chạy lại seed | IDs và evidence/history không đổi; mapping không nhân đôi/không bị reset; seed mới khai báo scope đúng |

## 7. Kiểm chứng đã thực hiện trong đợt lập kế hoạch

- Frontend: `node --test tests/curriculumHardening.test.ts tests/curriculumApplicationUi.test.ts tests/knowledgeGraphQuestionFilter.test.ts tests/knowledgeGraphViewport.test.ts` — **36 pass, 0 fail**.
- Backend: `dotnet test tests/EduTwin.BLL.Tests/EduTwin.BLL.Tests.csproj --no-restore --filter "FullyQualifiedName~CurriculumAndQuestions" --logger "console;verbosity=minimal"` — **522 pass, 0 fail, 5 skip**. Các test MySQL trong nhóm không chạy; truy vấn audit database thật ở mục 3 là hoạt động khác.
- Source hiện thiếu regression test cho đổi grade → đổi thư viện, graph grade/class/year và guard node-grade. Các test xanh hiện tại không chứng nhận tính năng mới đúng.
- Chưa chạy toàn bộ test suite hoặc browser E2E; chưa tạo dữ liệu để tái hiện bằng thao tác ghi trên môi trường đang dùng.

## 8. Phạm vi duyệt

Đề nghị duyệt triển khai mô hình: **node được phân loại khối tường minh; quan hệ/cây có phạm vi khối; lớp lấy graph từ giáo trình đang áp dụng; năm học là ngữ cảnh của lớp; giữ NodeId và lịch sử**. Phạm vi sửa bao gồm database, API, Teacher UI và các luồng câu hỏi/chỉ số bị ảnh hưởng.

Phân loại chuyên môn cụ thể được chốt trong manifest đã được người dùng duyệt riêng. Không coi việc duyệt kiến trúc là cho phép tự đoán khối của nội dung ngoài manifest. Migration/triển khai được phép trong phạm vi kế hoạch đã duyệt, sau khi hoàn tất kiểm thử và backup.
