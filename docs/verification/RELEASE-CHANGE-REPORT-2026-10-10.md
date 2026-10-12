# Báo cáo tổng hợp thay đổi và rà soát trước push — 10/10/2026

## 1. Mốc bàn giao và phạm vi

Báo cáo này tổng hợp phần thay đổi **sau commit `a9918f4`**, trên nhánh `student/answer`. Đây là một mốc checkpoint đã kiểm chứng, không phải tuyên bố hệ thống không còn bất kỳ lỗi nào.

Người dùng yêu cầu rà soát, viết báo cáo và push; sau khi phát hiện lỗi phạm vi/nhóm yếu trên dashboard giáo viên, người dùng xác nhận **sửa dashboard rồi push**. Không duyệt điểm, đổi tài khoản, xóa bài làm hoặc reset database trong ca rà soát này.

- Ban đầu Git có **374 tệp** chưa commit: nhiều ca sửa tích lũy, không phải chỉ chức năng ảnh đề.
- **46 tệp bằng chứng local** được giữ riêng, gồm 43 ảnh/binary và 3 tệp bằng chứng văn bản; không đưa tên/điểm/bài làm học sinh từ ảnh runtime lên GitHub.
- Sau bổ sung audit, sửa dashboard và báo cáo này: **336 tệp văn bản/code** trong checkpoint. Danh sách đầy đủ ở phụ lục; kiểm tra staged diff là nguồn cuối.
- Ba EF Designer mới chứa **17.285 dòng không trống** (5.670 + 5.807 + 5.808), là model sinh tự động. Không hiểu số dòng đó là hàng chục nghìn dòng nghiệp vụ viết tay.
- Không commit `.env`, key Gemini/Groq, token, database dump, thư mục storage, build output, node_modules hoặc ảnh nháp cá nhân. Không xóa chúng khỏi máy.
- Push code **không** sao chép database localhost, các câu/bài Test 4–9 đã tạo trên UI hay ảnh của chúng sang máy khác.

## 2. Những gì đã thay đổi

### 2.1. Giao diện học sinh và thông tin workspace

Tăng kích thước chữ/control và không gian đọc, thay nền kem bằng nền sáng trung tính, giữ giao diện tối và responsive. Bộ đếm “Bài tập của tôi” và daily streak không còn số 3/7 ghi cứng: lấy dữ liệu API theo học sinh và phạm vi môn/lớp, loại bản nháp/đối tượng không được giao. Chuỗi ngày dựa trên hoạt động nộp bài/luyện tập theo múi giờ học tập.

Chi tiết: [workspace và vai trò](STUDENT-WORKSPACE-AND-ACCOUNT-ROLES-2026-10-08.md), [UI dễ đọc và ảnh đề](STUDENT-READABLE-UI-AND-QUESTION-IMAGES-2026-10-08.md).

### 2.2. Tài khoản và phân quyền bốn tác nhân

Tài khoản Student/Teacher mới được gắn vai trò hệ thống tương ứng trong cùng luồng tạo tài khoản, có nhật ký; thiếu vai trò chuẩn thì fail-closed, không tạo một tài khoản đăng nhập rồi vô dụng. Backfill quyền mặc định thiếu chỉ áp dụng vai trò hệ thống hợp lệ, không sửa vai trò tùy chỉnh hoặc tự kích hoạt lại phân công đã thu hồi.

Teacher phụ trách học thuật/câu hỏi/giáo trình/đồ thị/giao và chấm bài. Center Manager giữ quản trị tổ chức, phân quyền và báo cáo chỉ xem; không mở workspace Teacher để giải quyết lỗi thiếu quyền. Platform Admin vẫn cách ly quản trị nền tảng; Student chỉ đọc/làm bài trong phạm vi được cấp. API kiểm tra actor, tenant và ownership độc lập với việc ẩn nút.

Chi tiết: [quyền và nút điều hướng chủ đề](ACTOR-PERMISSIONS-AND-KNOWLEDGE-TOPIC-NAVIGATION-2026-10-08.md).

### 2.3. Đề bài bằng hình ảnh

Giáo viên có thể nhập đề kèm ảnh hoặc dùng ảnh chứa toàn bộ nội dung đề. Bản hiện tại nhận **PNG, tối đa 2 MiB**, kiểm tra cấu trúc PNG/CRC/dữ liệu, lưu ảnh có hash và kiểm tra tenant/ownership khi đọc. Học sinh, giáo viên và worker AI đọc cùng ảnh đề. Không gửi một đề thiếu ảnh/hỏng ảnh rồi coi như dữ kiện đầy đủ.

Rubric/đáp án/lời giải mẫu vẫn cần được soạn; ảnh không thay thế tiêu chí chấm. Ảnh đề giáo viên và ảnh nháp học sinh được phân vai rõ trong request. Giao diện đã sửa các vùng kết quả/rubric để render công thức toán thay vì hiện dấu dollar thô.

Chi tiết: [Test 4](GEOMETRY-IMAGE-TEST4-2026-10-09.md), [đề hoàn toàn trong ảnh và Test 5](STUDENT-SCOPE-MATH-AND-ADVANCED-GEOMETRY-2026-10-09.md), [Test 5](GEOMETRY-IMAGE-TEST5-2026-10-09.md).

### 2.4. Chấm bằng chứng hình vẽ và audit điểm

Giáo viên khai báo `visualRequirements` cho từng tiêu chí cần chấm nét vẽ/ký hiệu. Với nhóm này, Flash đọc ảnh nháp độc lập, không nhận đáp án/lời giải/lời tự khai; server kiểm tra rồi khóa `Present/Missing/Unclear`. Flash-Lite chấm rubric sau đó nhưng không được thay quan sát đã khóa. Thiếu ký hiệu ảnh không được dùng để trừ ké điểm tính toán.

Tiêu chí bị trừ điểm trong profile này phải có phần chưa đạt và bằng chứng cụ thể, được hiển thị cùng nhận xét. Tổng rubric do server tính, UI quy đổi thang 10; AI chấm đề xuất, giáo viên chốt. Không tự biến Manual thành “bỏ AI, giáo viên tự chấm”.

Microbatch câu thường vẫn 3 câu, cửa sổ gom 500 ms, giới hạn chung 2 Gemini calls theo adapter/quota hiện có. Nhóm chấm hình thêm bước đọc ảnh, cùng deadline 30 giây cho hai bước trong một lượt; không ảnh nháp thì tạo Missing tại server và không gọi model ảnh. Lỗi ảnh của một câu không bỏ câu thường trong cùng batch. Checkpoint/profile phân biệt model ảnh; không tự chạy lại kết quả đã thành công ở bài cũ.

**Test 9 đối chứng:** ảnh PNG giống hệt Test 5, thiếu M/ô góc vuông/dấu BM=MC; AI đề xuất **8/10 = 4/4 hình + 0/2 ký hiệu + 4/4 tính toán**, **22,97 giây**, **2 request**, **0 fallback**. Hash/bytes ở hai bước khớp ảnh gốc. Chưa duyệt thay giáo viên. Một ảnh đúng chưa chứng minh mọi hình đều đúng.

Chi tiết code, profile, latency và giới hạn: [báo cáo Test 8/9](GEOMETRY-INDEPENDENT-VISION-TEST8-9-2026-10-10.md).

### 2.5. Khối lớp và dữ liệu mẫu

Dữ liệu mẫu lớp Toán/Tiếng Anh tách khối 10/11/12; kiểm tra tương thích khối khi thêm học sinh, áp dụng giáo trình và giao bài. Ngoại lệ khác khối phải có lý do/người duyệt/thời điểm, không chỉ một boolean tùy ý. Giữ thành viên và bài làm cũ để đối chiếu, không ép chuyển bài làm sang lớp mới.

Chưa phân khối không đồng nghĩa đã lưu trữ. Trạng thái lớp do quản lý quyết định; dữ liệu mẫu là một phạm vi riêng, không phải lệnh sửa mọi trung tâm đang có dữ liệu.

Chi tiết: [phân khối và bảo toàn bài kiểm thử](GRADE-CLASS-SPLIT-AND-TEST-HISTORY-2026-10-08.md).

### 2.6. Phạm vi học, giáo trình áp dụng và lịch sử

Student chọn môn/lớp được học, **không** chọn hay đổi giáo trình. Giáo viên phụ trách áp dụng giáo trình Published; ledger là nguồn chuẩn cho lịch sử áp dụng. Mỗi lớp có một giáo trình chính hiện hành, có thể nhiều bổ trợ; thay/ngừng phải ghi lý do và giữ lượt áp dụng cũ. Kiểm tra cùng môn/tenant/khối và lớp thuộc giáo viên thao tác.

Dashboard/lộ trình/luyện tập/bài tập học sinh lọc theo lớp và chủ đề giáo trình, không lấy toàn bộ đồ thị làm thay thế khi chưa gán. Lịch sử chỉ có lớp đã kết thúc hoặc thành viên đã rời lớp; liên kết điều hướng giữa danh sách/chi tiết/kết quả giữ scope, không còn Test 4 từ Đang học bị mang sang lịch sử khác. Khung “Lớp áp dụng” trùng lặp đã được thống nhất: Published dùng panel ledger đang áp dụng; Draft giữ kế hoạch riêng, không hiện checkbox khóa gây hiểu nhầm.

Chi tiết: [phạm vi học](STUDENT-ACADEMIC-SCOPE-AND-CURRICULUM-HISTORY-2026-10-08.md), [nguồn chuẩn UI áp dụng](CURRICULUM-APPLICATION-UI-SOURCE-OF-TRUTH-2026-10-09.md), [lọc lịch sử](STUDENT-CLASS-HISTORY-FILTER-2026-10-09.md).

### 2.7. Vòng đời lớp và báo cáo

Active đi cùng Current, Archived đi cùng History ở server/database. Manager lưu trữ/mở lại phải có lý do, RowVersion, actor và nhật ký trước/sau. Migration sửa phân loại sai cũ theo Status, ghi nguồn System/Migration, không giả tác nhân người và không tự coi tên “kiểm thử cũ” là căn cứ lưu trữ.

Lớp lưu trữ không thêm thành viên/giao hoặc phát hành bài/nhận bài mới/tạo lộ trình hay luyện tập mới. Vẫn xem lịch sử, kết quả và chấm/duyệt bài đã nộp. Mở lại không tự phục hồi ứng dụng giáo trình đã kết thúc.

Báo cáo lớp Manager có route/quyền chỉ xem riêng, không redirect vào trang Teacher. Báo cáo Teacher/Manager dùng điểm server thang 10 và điểm đã chốt, không suy ra nộp đủ = 10. Nạp snapshot đầy đủ thay vì giả dữ liệu trang đầu là toàn lớp; UI phân trang, xuất CSV/Excel/in dùng toàn snapshot. Sửa header responsive và khoảng cách với sidebar.

Chi tiết: [vòng đời và báo cáo chuẩn](CLASS-LIFECYCLE-AND-AUTHORITATIVE-REPORTS-2026-10-09.md), [khóa học lớp lưu trữ](ARCHIVED-CLASS-LEARNING-LOCK-2026-10-09.md).

### 2.8. Điều kiện giáo trình và đồ thị

- Draft mới sửa cấu trúc giáo trình; Published/Archived bảo vệ cấu trúc/nội dung đã dùng. Clone tạo bản nháp nhưng không tự version từng nút tri thức.
- Không lưu trữ giáo trình còn lớp Active/Current áp dụng, kể cả lớp giáo viên khác dùng Shared. Khi hợp lệ: lý do + RowVersion + lịch sử, không tự ngắt lớp đang học.
- Không sửa nội dung/nút cha của nút trong giáo trình Published/Archived.
- Không tắt nút còn lớp đang học, con hoạt động, câu hỏi Active hoặc lộ trình đang học phụ thuộc.
- Xóa mềm nút chỉ khi không còn phụ thuộc giáo trình/câu hỏi/Twin/lịch sử/lộ trình/liên kết/root causes.
- Liên kết phải cùng môn/tenant, hai đầu hoạt động, không trùng/chu trình; chặn sửa khi phạm vi lớp đang học còn phụ thuộc. Khóa transaction theo môn bảo vệ concurrent graph edits; nhật ký commit cùng thay đổi.
- Trigger database bổ sung cho các vòng đời cụ thể, không thay thế kiểm tra quyền/DAG ở server.
- Không có API xóa cứng giáo trình để vượt ràng buộc lịch sử.

Chi tiết: [ràng buộc phụ thuộc](ACADEMIC-LIFECYCLE-DEPENDENCIES-2026-10-09.md), [điều kiện UI và đồ thị](ACADEMIC-UI-GRAPH-AND-LIFECYCLE-RULES-2026-10-09.md).

### 2.9. Đồ thị Toán 10–12 và cách xem nhiều chủ đề

Đã xây khung chủ đề cốt lõi/lựa chọn Toán 10–12 trên cùng đồ thị môn, giáo trình chọn các chủ đề phù hợp từng khối. Không coi đó là bộ bài giảng/SGK/câu hỏi hoàn chỉnh. Những nút tạo bằng UI trên localhost là dữ liệu runtime, không tự có trên một database mới chỉ bằng git clone.

Atlas/Radar học sinh được chia nhóm/phân trang thay vì đặt 81 nhãn quanh một vòng tròn. Đồ thị giáo viên có tổng quan/focus, fit view/zoom và phân nhóm giúp xem nhiều nút. Bộ chọn chủ đề trong ngân hàng/soạn câu hỏi có tìm kiếm và lọc context; nút xem câu hỏi/tạo bài bổ trợ truyền chủ đề và trang đích đọc để lọc.

Chi tiết: [khung Toán 10–12](MATH-10-12-KNOWLEDGE-GRAPH-AND-CURRICULA-2026-10-08.md), [bộ chọn chủ đề](QUESTION-TOPIC-PICKER-AND-GEOMETRY10-2026-10-09.md).

### 2.10. Lỗi dashboard giáo viên sửa thêm trước push

`GetClassDashboardUseCase` trước đó đọc `curriculum_classes` (kế hoạch cũ); không có link thì lấy cả đồ thị, và missing Twin bị coi là 0. Đây là nguồn tạo 81 nhóm yếu dù chưa đánh giá.

Bản sửa dùng `class_curriculum_applications` hiện hành/Published/cùng môn, không hồi sinh ứng dụng đã kết thúc hay fallback cả đồ thị. Chỉ Twin có `EvidenceCount > 0` tham gia average/nhóm yếu. API trả số chủ đề áp dụng, số chủ đề có bằng chứng và số cặp học sinh–chủ đề chưa đánh giá. Giao diện giải thích rõ “chưa đánh giá” khác điểm 0.

Tính trung bình theo dữ liệu đã đánh giá và trọng số chủ đề; nhóm yếu chứa học sinh thực sự dưới ngưỡng, kể cả khi trung bình lớp cao. Không thay điểm, năng lực hay lịch sử trong database. Bổ sung 7 case backend (kể cả các case theory) và 3 frontend guards.

## 3. Rà soát và kiểm chứng cuối

| Kiểm tra | Kết quả hiện có |
| --- | --- |
| .NET build cuối | 0 warning, 0 error |
| Toàn backend không bật môi trường SQL | 4.039 pass, 79 integration skip, 0 fail; tổng 4.118 |
| Frontend tests cuối | 650 pass, 0 fail |
| Bundle budget | 3 pass |
| TypeScript/Vite và Docker API/web | Build thành công |
| ESLint | Toàn frontend trước sửa dashboard: 0 error, 26 warning. Phạm vi dashboard sửa cuối: 0 error, 1 warning Fast Refresh có sẵn |
| MySQL tích hợp, schema tạm riêng | 12 ca dashboard đã được xác nhận đạt qua hai lượt: 10 pass ở lượt đầy đủ, 2 pass sau sửa fixture; không còn failure trong 12 ca này |
| UI Teacher Math trên bản cuối | Chưa kiểm chứng trực tiếp: tab đang ở màn hình đăng nhập. Không coi source guards/SQL là UI end-to-end đã đạt |
| Runtime orphan checks | 0 analysis/evidence/history/path/knowledge orphan; FK checks bật, append-only trigger có mặt |
| Quét credential trong tệp chuẩn bị commit | Không phát hiện pattern key/token/private key, .env ignored; kiểm tra lại staged diff trước commit |

Các test SQL tạo schema `edutwin_dash_GUID`, migrate/seed/test rồi chỉ dọn schema tự tạo. Không dùng schema `edutwin` làm fixture. Hai lần cấu hình kết nối đầu dừng trước nghiệp vụ (tên enum SSL và xác thực RSA); không tính là test ứng dụng đã chạy, cũng không coi là lỗi đã được bỏ qua. Lượt cuối yêu cầu TLS.

Hai failure ở lượt SQL đầy đủ là fixture cũ thiếu ledger áp dụng và thiếu khối hợp lệ. Đã bổ sung giáo trình Published/ledger/khối đúng trong schema test, build lại 0 warning/0 error và chạy lại cả hai ca: 2 pass, 0 fail. Không nới trigger, bỏ guard hay sửa dữ liệu ứng dụng để làm test qua. Trong 79 integration skip của lượt backend thông thường, 12 ca trên đã chạy riêng; 67 ca khác chưa được chạy trong lượt rà soát này.

Đối chiếu SQL chỉ đọc trên dữ liệu localhost: lớp Toán 10/11/12 có lần lượt 25/28/19 chủ đề thuộc giáo trình hiện hành, không phải 81 chủ đề toàn môn; số chủ đề có bằng chứng lần lượt 2/0/0. Đây là đối chiếu dữ liệu, không thay thế việc nhìn dashboard qua một phiên Teacher đã đăng nhập. Docker API/web đã build và chạy bản sửa cuối; ca rà soát này không gọi thêm Gemini hay nộp thêm bài để tiêu quota.

Không dùng số test pass để khẳng định không thể có bug. Kiểm thử source guards khác với UI end-to-end, và mock khác với request Gemini thật.

## 4. Migration và cách triển khai trên môi trường khác

Bốn migration mới so với HEAD:

1. `20261008095834_AddQuestionImages`.
2. `20261008162602_AddAcademicClassScopeAndCurriculumApplications`.
3. `20261008184057_AlignClassLifecycleScope`.
4. `20261009103000_HardenAcademicLifecycleDependencies`.

Cần sao lưu database + storage trước triển khai, kiểm tra migrations history, áp dụng theo thứ tự bằng luồng triển khai hiện có. Có DDL/trigger MySQL và phần data correction có transaction riêng; không reset/reseed một database đang có dữ liệu. Down không phải lệnh khôi phục toàn bộ lịch sử hay undo trạng thái lớp do quản lý đã thay.

Image storage và dữ liệu câu hỏi/bài tập runtime không nằm trong Git. Cấu hình bước vision là opt-in trong .env.example (trống/false); môi trường khác cần khai model/quota phù hợp và bật có chủ đích. Không sao chép key từ báo cáo.

Các script `scripts/ops`/`scripts/e2e` là công cụ vận hành/test riêng; không chạy tất cả khi deploy. Script tạo/xóa fixture hoặc sửa dữ liệu phải được xem phạm vi/backup/ủy quyền trước. Ca pre-push này chỉ chạy inventory/read-only và test schema cô lập, không chạy các script sửa/duyệt điểm/xóa bài demo.

## 5. Hạn chế còn tồn tại và việc làm sau

1. Chấm hình chỉ được đối chứng trên một PNG thiếu ký hiệu. Cần bộ ảnh nhiều hình đủ/sai/thiếu/khó đọc/xoay/lật; không tuyên bố vision tuyệt đối chính xác.
2. Chỉ số `reasoningQuality` 90/100 ở Test 9 vẫn cần hiệu chuẩn để tách khỏi thiếu sót trình bày hình. Không cộng chỉ số này vào tổng 8/10 và không tự nâng/duyệt điểm.
3. Retry thành công nhưng sai chưa có workflow riêng: sản phẩm hiện chỉ retry fallback/thất bại. Đó là lý do tạo Test 9 có xác nhận, không reset job SQL.
4. Hai bước vision chưa có checkpoint trung gian độc lập; bounded retry có thể đọc ảnh lại. Chưa benchmark lại 50/100 câu có rubric vẽ bắt buộc.
5. Tổng quan Center Manager vẫn có công thức coverage/zero-fill cũ, khác mean trên học sinh đã đánh giá ở dashboard Teacher. Không so hai chỉ số như cùng một định nghĩa; thống nhất công thức/nhãn coverage là phần riêng cần làm sau. Không giả rằng bản sửa Teacher đã sửa tất cả aggregate quản lý.
6. Đồ thị chưa có snapshot/version hoàn chỉnh cho mọi tham số/liên kết. Có audit và khóa nội dung lịch sử, nhưng không phải ảnh chụp bất biến của toàn đồ thị theo từng thời điểm.
7. Chưa làm local AI, hệ thống thông báo/nhật ký đa tác nhân tổng quát hoặc bộ giáo trình đầy đủ nội dung; các phần này đã được để sau.
8. Lint có warning hook/fast-refresh và MathLive chunk lớn; bundle budget hiện pass. Không gọi warning là lỗi build, cũng không giấu chúng.

Sau ca sửa dashboard, chưa phát hiện thêm regression chặn commit từ các kiểm thử đã hoàn tất. Kiểm chứng UI trực tiếp và 67 ca integration khác chưa thực hiện đã được ghi rõ ở mục 3. Đây là checkpoint có giới hạn đã ghi rõ, không phải “không còn lỗi vặt”.

## 6. Hướng dẫn đọc trong commit

Commit phải chứa ghi chú: **Đọc `docs/verification/RELEASE-CHANGE-REPORT-2026-10-10.md` trước khi review/deploy.** Dùng chính báo cáo này làm điểm vào; các báo cáo ngày trước là lịch sử triển khai tại thời điểm tương ứng, có thể còn câu “chưa push” đã đúng trong lượt đó.

Không force-push. Kiểm tra nhánh `student/answer` và remote trước/sau push; không tự merge nhánh khác hoặc đưa dữ liệu local vào Git để làm sạch số pending.

## 7. Phụ lục — danh mục tệp trong checkpoint

Danh sách lấy từ Git so với HEAD, sau khi loại bằng chứng runtime. Bao gồm cả tệp này; không coi toàn bộ dòng thay đổi của mỗi tệp là phát sinh riêng trong ca cuối. Tệp test/Designer có thể chiếm phần lớn số dòng, không phải UI đã “phình” tương ứng.


### Cấu hình gốc

- `.env.example`
- `.gitignore`
- `docker-compose.yml`

### Báo cáo/kế hoạch

- `docs/plans/ACADEMIC-SCOPE-WORKING-2026-10-08.md`
- `docs/plans/AI-GRADING-OPTIMIZATION-PAUSED-2026-10-07.md`
- `docs/plans/CLASS-LIFECYCLE-AND-REPORTS-WORKING-2026-10-09.md`
- `docs/verification/ACADEMIC-LIFECYCLE-DEPENDENCIES-2026-10-09.md`
- `docs/verification/ACADEMIC-UI-GRAPH-AND-LIFECYCLE-RULES-2026-10-09.md`
- `docs/verification/ACTOR-PERMISSIONS-AND-KNOWLEDGE-TOPIC-NAVIGATION-2026-10-08.md`
- `docs/verification/ARCHIVED-CLASS-LEARNING-LOCK-2026-10-09.md`
- `docs/verification/CHANGE-INVENTORY-2026-10-09.md`
- `docs/verification/CLASS-LIFECYCLE-AND-AUTHORITATIVE-REPORTS-2026-10-09.md`
- `docs/verification/CLASS-LIFECYCLE-AUDIT-2026-10-09.md`
- `docs/verification/CURRICULUM-APPLICATION-UI-SOURCE-OF-TRUTH-2026-10-09.md`
- `docs/verification/GEOMETRY-IMAGE-TEST4-2026-10-09.md`
- `docs/verification/GEOMETRY-IMAGE-TEST5-2026-10-09.md`
- `docs/verification/GEOMETRY-INDEPENDENT-VISION-TEST8-9-2026-10-10.md`
- `docs/verification/GEOMETRY-VISUAL-EVIDENCE-FIX-2026-10-09.md`
- `docs/verification/GRADE-CLASS-SPLIT-AND-TEST-HISTORY-2026-10-08.md`
- `docs/verification/MATH-10-12-KNOWLEDGE-GRAPH-AND-CURRICULA-2026-10-08.md`
- `docs/verification/QUESTION-TOPIC-PICKER-AND-GEOMETRY10-2026-10-09.md`
- `docs/verification/RELEASE-CHANGE-REPORT-2026-10-10.md`
- `docs/verification/STUDENT-ACADEMIC-SCOPE-AND-CURRICULUM-HISTORY-2026-10-08.md`
- `docs/verification/STUDENT-CLASS-HISTORY-FILTER-2026-10-09.md`
- `docs/verification/STUDENT-READABLE-UI-AND-QUESTION-IMAGES-2026-10-08.md`
- `docs/verification/STUDENT-SCOPE-MATH-AND-ADVANCED-GEOMETRY-2026-10-09.md`
- `docs/verification/STUDENT-WORKSPACE-AND-ACCOUNT-ROLES-2026-10-08.md`

### Script vận hành/kiểm chứng

- `scripts/e2e/README.md`
- `scripts/e2e/acceptance.nginx.conf`
- `scripts/e2e/acceptance_stack.cjs`
- `scripts/e2e/assignment_api_acceptance.cjs`
- `scripts/e2e/chrome_client.cjs`
- `scripts/e2e/debug_player.cjs`
- `scripts/e2e/fixture_manager.cjs`
- `scripts/e2e/smoke_test.cjs`
- `scripts/e2e/test_browser_flow.cjs`
- `scripts/e2e/test_page_render.cjs`
- `scripts/e2e/test_student_experience.cjs`
- `scripts/ops/academic_scope_integrity.cjs`
- `scripts/ops/apply_ai_grading_proposals_local.cjs`
- `scripts/ops/remove_demo_assignments.cjs`
- `scripts/ops/repair_known_e2e_orphans.cjs`
- `scripts/ops/run_lifecycle_checks.cjs`
- `scripts/ops/runtime_inventory.cjs`
- `scripts/ops/split_seed_grade_classes_local.cjs`
- `scripts/ops/verify_lifecycle_login_delta.cjs`
- `scripts/seed_student_assignments.sql`
- `scripts/start_api.ps1`
- `scripts/verification/create_geometry_image_only_fixture.py`
- `scripts/verification/pre_push_audit.cjs`

### EduTwin.API

- `src/EduTwin.API/AssessmentAndReasoning/AI/AIGradingOptions.cs`
- `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiAIService.cs`
- `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiOptions.cs`
- `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiPromptBuilder.cs`
- `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiResponseJsonSchema.cs`
- `src/EduTwin.API/AssessmentAndReasoning/AI/GeminiVisualEvidenceInspector.cs`
- `src/EduTwin.API/AssessmentAndReasoning/AI/GoogleGenAIGenerateContentClient.cs`
- `src/EduTwin.API/AssessmentAndReasoning/AI/IGeminiGenerateContentClient.cs`
- `src/EduTwin.API/AssessmentAndReasoning/AI/ReasoningBatchExecutor.cs`
- `src/EduTwin.API/AssessmentAndReasoning/AI/ReasoningMicroBatcher.cs`
- `src/EduTwin.API/Controllers/ClassesController.cs`
- `src/EduTwin.API/Controllers/CurriculumsController.cs`
- `src/EduTwin.API/Controllers/KnowledgeEdgesController.cs`
- `src/EduTwin.API/Controllers/KnowledgeNodesController.cs`
- `src/EduTwin.API/Controllers/LearningController.cs`
- `src/EduTwin.API/Controllers/QuestionImagesController.cs`
- `src/EduTwin.API/Controllers/RecommendationsController.cs`
- `src/EduTwin.API/Controllers/StudentsController.cs`
- `src/EduTwin.API/Security/CompositePermissionPolicies.cs`

### EduTwin.BLL

- `src/EduTwin.BLL/AssessmentAndReasoning/AI/AIAnalysisValidationException.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/AnalyzeReasoningRequest.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/AnalyzeReasoningResponse.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/AnalyzeReasoningResponseValidator.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/AI/StrictAIAnalysisResponseParser.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/AttemptSubmissionValidator.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIAnalysisCheckpointStore.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIAnalysisJobProcessor.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/Processing/AIAnalysisRequestFactory.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/Processing/IAIAnalysisRequestFactory.cs`
- `src/EduTwin.BLL/AssessmentAndReasoning/ReviewQueue/ListTeacherReviewQueueUseCase.cs`
- `src/EduTwin.BLL/Assignments/AssignmentResultCalculator.cs`
- `src/EduTwin.BLL/Assignments/CreateAssignmentUseCase.cs`
- `src/EduTwin.BLL/Assignments/GetStudentAssignmentUseCase.cs`
- `src/EduTwin.BLL/Assignments/ListStudentAssignmentsUseCase.cs`
- `src/EduTwin.BLL/Assignments/PublishAssignmentUseCase.cs`
- `src/EduTwin.BLL/Assignments/SaveAssignmentDraftUseCase.cs`
- `src/EduTwin.BLL/Assignments/StartStudentAssignmentUseCase.cs`
- `src/EduTwin.BLL/Assignments/StudentAssignmentScope.cs`
- `src/EduTwin.BLL/Assignments/SubmitStudentAssignmentUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/AcademicDependencyGuards.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/ArchiveCurriculumResult.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/ArchiveCurriculumUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/AssignCurriculumClassesUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/CreateQuestionUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/CurriculumApplicationUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/CurriculumClassScopeQuery.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/DependencyInjection.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/GetCurriculumUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/GetQuestionImageUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/GradingCriteriaValidator.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/ListCurriculumsUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/PublishCurriculumUseCase.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/QuestionImageContent.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/QuestionProjection.cs`
- `src/EduTwin.BLL/CurriculumAndQuestions/UpdateQuestionUseCase.cs`
- `src/EduTwin.BLL/Dashboards/DependencyInjection.cs`
- `src/EduTwin.BLL/Dashboards/GetClassDashboardUseCase.cs`
- `src/EduTwin.BLL/Dashboards/GetStudentDashboardUseCase.cs`
- `src/EduTwin.BLL/Dashboards/GetStudentWorkspaceSummaryUseCase.cs`
- `src/EduTwin.BLL/Dashboards/IGetStudentDashboardUseCase.cs`
- `src/EduTwin.BLL/Dashboards/StudentAcademicScopeReader.cs`
- `src/EduTwin.BLL/IdentityAndTenancy/NewAccountRoleProvisioning.cs`
- `src/EduTwin.BLL/KnowledgeGraph/CreateKnowledgeEdgeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/CreateKnowledgeEdgeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/CreateKnowledgeNodeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/CreateKnowledgeNodeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/DeleteKnowledgeEdgeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/DeleteKnowledgeEdgeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/DeleteKnowledgeNodeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/DeleteKnowledgeNodeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/GraphMutationTransaction.cs`
- `src/EduTwin.BLL/KnowledgeGraph/UpdateKnowledgeEdgeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/UpdateKnowledgeEdgeUseCase.cs`
- `src/EduTwin.BLL/KnowledgeGraph/UpdateKnowledgeNodeResult.cs`
- `src/EduTwin.BLL/KnowledgeGraph/UpdateKnowledgeNodeUseCase.cs`
- `src/EduTwin.BLL/Organization/AddStudentsToClassUseCase.cs`
- `src/EduTwin.BLL/Organization/ClassReportsUseCase.cs`
- `src/EduTwin.BLL/Organization/CreateStudentResult.cs`
- `src/EduTwin.BLL/Organization/CreateStudentUseCase.cs`
- `src/EduTwin.BLL/Organization/CreateTeacherUseCase.cs`
- `src/EduTwin.BLL/Organization/DeleteClassUseCase.cs`
- `src/EduTwin.BLL/Organization/DependencyInjection.cs`
- `src/EduTwin.BLL/Organization/GetClassUseCase.cs`
- `src/EduTwin.BLL/Organization/ListClassesUseCase.cs`
- `src/EduTwin.BLL/Organization/StudentClassScope.cs`
- `src/EduTwin.BLL/Organization/StudentLearningScope.cs`
- `src/EduTwin.BLL/Organization/UpdateClassUseCase.cs`
- `src/EduTwin.BLL/Organization/UpdateStudentResult.cs`
- `src/EduTwin.BLL/Organization/UpdateStudentUseCase.cs`
- `src/EduTwin.BLL/Recommendations/IRecommendationEngine.cs`
- `src/EduTwin.BLL/Recommendations/OpportunityCandidateBuilder.cs`
- `src/EduTwin.BLL/Recommendations/RecommendationEngine.cs`
- `src/EduTwin.BLL/Recommendations/UseCases/GenerateLearningPathUseCase.cs`
- `src/EduTwin.BLL/Recommendations/UseCases/GetLearningPathTopicsUseCase.cs`
- `src/EduTwin.BLL/Recommendations/UseCases/GetNextQuestionUseCase.cs`
- `src/EduTwin.BLL/Recommendations/UseCases/UpdateLearningPathSessionUseCase.cs`
- `src/EduTwin.BLL/Seeding/AuthorizationBootstrapper.cs`
- `src/EduTwin.BLL/Seeding/DefaultSystemRolePermissionBackfill.cs`
- `src/EduTwin.BLL/Seeding/EduTwinRuntimeSeeder.cs`
- `src/EduTwin.BLL/Seeding/ManifestEvaluator.cs`
- `src/EduTwin.BLL/Seeding/SeedExtensions.cs`

### EduTwin.Contracts

- `src/EduTwin.Contracts/AssessmentAndReasoning/RubricGrade.cs`
- `src/EduTwin.Contracts/AssessmentAndReasoning/SubmitAttemptRequest.cs`
- `src/EduTwin.Contracts/AssessmentAndReasoning/TeacherReviewQueueItemDto.cs`
- `src/EduTwin.Contracts/Assignments/ListStudentAssignmentsQuery.cs`
- `src/EduTwin.Contracts/Assignments/StudentAssignmentDetailDto.cs`
- `src/EduTwin.Contracts/Assignments/StudentQuestionDto.cs`
- `src/EduTwin.Contracts/CurriculumAndQuestions/ApplyCurriculumRequest.cs`
- `src/EduTwin.Contracts/CurriculumAndQuestions/ArchiveCurriculumRequest.cs`
- `src/EduTwin.Contracts/CurriculumAndQuestions/CreateQuestionRequest.cs`
- `src/EduTwin.Contracts/CurriculumAndQuestions/GradingCriteria.cs`
- `src/EduTwin.Contracts/CurriculumAndQuestions/QuestionDto.cs`
- `src/EduTwin.Contracts/CurriculumAndQuestions/StudentQuestionDto.cs`
- `src/EduTwin.Contracts/CurriculumAndQuestions/UpdateQuestionRequest.cs`
- `src/EduTwin.Contracts/Dashboards/ClassDashboardDto.cs`
- `src/EduTwin.Contracts/Dashboards/StudentAcademicContextDto.cs`
- `src/EduTwin.Contracts/Dashboards/StudentDashboardDto.cs`
- `src/EduTwin.Contracts/Dashboards/StudentWorkspaceSummaryDto.cs`
- `src/EduTwin.Contracts/Organization/ClassAcademicReportDto.cs`
- `src/EduTwin.Contracts/Organization/ClassDto.cs`
- `src/EduTwin.Contracts/Organization/ClassLearningScope.cs`
- `src/EduTwin.Contracts/Organization/UpdateClassRequest.cs`
- `src/EduTwin.Contracts/Recommendations/LearningPathQuestionnaireDto.cs`
- `src/EduTwin.Contracts/Recommendations/NextQuestionResponse.cs`

### EduTwin.DAL

- `src/EduTwin.DAL/CurriculumAndQuestions/ClassCurriculumApplication.cs`
- `src/EduTwin.DAL/CurriculumAndQuestions/Question.cs`
- `src/EduTwin.DAL/CurriculumAndQuestions/QuestionImage.cs`
- `src/EduTwin.DAL/Organization/Class.cs`
- `src/EduTwin.DAL/Persistence/Configurations/ClassConfiguration.cs`
- `src/EduTwin.DAL/Persistence/Configurations/ClassStudentConfiguration.cs`
- `src/EduTwin.DAL/Persistence/Configurations/CurriculumAndQuestions/ClassCurriculumApplicationConfiguration.cs`
- `src/EduTwin.DAL/Persistence/Configurations/CurriculumAndQuestions/QuestionConfiguration.cs`
- `src/EduTwin.DAL/Persistence/Configurations/CurriculumAndQuestions/QuestionImageConfiguration.cs`
- `src/EduTwin.DAL/Persistence/EduTwinDbContext.cs`
- `src/EduTwin.DAL/Persistence/Migrations/20261008095834_AddQuestionImages.Designer.cs`
- `src/EduTwin.DAL/Persistence/Migrations/20261008095834_AddQuestionImages.cs`
- `src/EduTwin.DAL/Persistence/Migrations/20261008162602_AddAcademicClassScopeAndCurriculumApplications.Designer.cs`
- `src/EduTwin.DAL/Persistence/Migrations/20261008162602_AddAcademicClassScopeAndCurriculumApplications.cs`
- `src/EduTwin.DAL/Persistence/Migrations/20261008184057_AlignClassLifecycleScope.Designer.cs`
- `src/EduTwin.DAL/Persistence/Migrations/20261008184057_AlignClassLifecycleScope.cs`
- `src/EduTwin.DAL/Persistence/Migrations/20261009103000_HardenAcademicLifecycleDependencies.cs`
- `src/EduTwin.DAL/Persistence/Migrations/EduTwinDbContextModelSnapshot.cs`
- `src/EduTwin.DAL/Seeding/AuthorizationPermissionCatalog.cs`
- `src/EduTwin.DAL/Seeding/DeterministicSeedIds.cs`
- `src/EduTwin.DAL/Seeding/EduTwinSeedFactory.cs`
- `src/EduTwin.DAL/Seeding/SeedDataContainer.cs`

### Backend tests

- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/AIAnalysisContractTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiAIServiceTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiImageTransportTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiPromptBuilderTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiResponseJsonSchemaTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/GeminiVisualEvidenceInspectionTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/IAIServiceContractTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/ReasoningMicroBatchTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/AI/VisualEvidenceGradingTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/Processing/AIAnalysisJobProcessorQuestionImageTests.cs`
- `tests/EduTwin.BLL.Tests/AssessmentAndReasoning/Processing/AIAnalysisRequestFactoryTests.cs`
- `tests/EduTwin.BLL.Tests/Assignments/CreateAssignmentUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/AcademicLifecycleDependencyTests.cs`
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/ArchiveCurriculumUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/AssignCurriculumClassesUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/CreateQuestionUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/CurriculumApplicationProjectionTests.cs`
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/GradingCriteriaValidatorTests.cs`
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/ListCurriculumsUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/PublishCurriculumUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/QuestionImageContentTests.cs`
- `tests/EduTwin.BLL.Tests/Dashboards/AcademicScopeTests.cs`
- `tests/EduTwin.BLL.Tests/Dashboards/ClassDashboardAcademicCoverageTests.cs`
- `tests/EduTwin.BLL.Tests/Dashboards/DashboardBoundaryUnitTests.cs`
- `tests/EduTwin.BLL.Tests/Dashboards/DashboardMySqlIntegrationTests.cs`
- `tests/EduTwin.BLL.Tests/Dashboards/StudentWorkspaceSummaryTests.cs`
- `tests/EduTwin.BLL.Tests/IdentityAndTenancy/AuthorizationBootstrapperTests.cs`
- `tests/EduTwin.BLL.Tests/IdentityAndTenancy/GlobalQueryFilterTests.cs`
- `tests/EduTwin.BLL.Tests/IdentityAndTenancy/NewAccountRoleProvisioningTests.cs`
- `tests/EduTwin.BLL.Tests/KnowledgeGraph/CreateKnowledgeEdgeUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/Organization/AccountRoleTestSeed.cs`
- `tests/EduTwin.BLL.Tests/Organization/AddStudentsToClassUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/Organization/ClassReportsTests.cs`
- `tests/EduTwin.BLL.Tests/Organization/CreateStudentUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/Organization/CreateTeacherUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/Organization/UpdateClassUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/Organization/UpdateStudentUseCaseTests.cs`
- `tests/EduTwin.BLL.Tests/Recommendations/ArchivedLearningScopeTests.cs`
- `tests/EduTwin.BLL.Tests/Recommendations/RecommendationCurriculumScopeTests.cs`
- `tests/EduTwin.BLL.Tests/Seeding/EduTwinRuntimeSeederTests.cs`
- `tests/EduTwin.BLL.Tests/Seeding/EduTwinSeedFactoryTests.cs`

### Web frontend

- `web/edutwin-web/nginx.conf`
- `web/edutwin-web/src/App.tsx`
- `web/edutwin-web/src/api/assignmentsApi.ts`
- `web/edutwin-web/src/api/curriculumApi.ts`
- `web/edutwin-web/src/api/dashboardsApi.ts`
- `web/edutwin-web/src/api/learningFeedbackApi.ts`
- `web/edutwin-web/src/api/learningPathApi.ts`
- `web/edutwin-web/src/api/organizationApi.ts`
- `web/edutwin-web/src/components/ClassHistoryPanel.tsx`
- `web/edutwin-web/src/components/QuestionImage.tsx`
- `web/edutwin-web/src/components/math/ScratchpadInlinePanel.tsx`
- `web/edutwin-web/src/components/math/mathPreviewUtils.ts`
- `web/edutwin-web/src/components/reviews/AssignmentGradingWorkspace.tsx`
- `web/edutwin-web/src/components/reviews/RubricGradeView.tsx`
- `web/edutwin-web/src/components/student/StudentAcademicContext.tsx`
- `web/edutwin-web/src/components/student/StudentKnowledgeMap.tsx`
- `web/edutwin-web/src/components/student/StudentLearningReadOnlyNotice.tsx`
- `web/edutwin-web/src/components/student/StudentRadarChart.tsx`
- `web/edutwin-web/src/components/teacher/CurriculumApplicationPanel.tsx`
- `web/edutwin-web/src/components/teacher/CurriculumArchiveDialog.tsx`
- `web/edutwin-web/src/components/teacher/GradingCriteriaEditor.tsx`
- `web/edutwin-web/src/components/teacher/KnowledgeGraphCanvas.tsx`
- `web/edutwin-web/src/components/teacher/KnowledgeTopicPicker.tsx`
- `web/edutwin-web/src/components/teacher/QuestionImageEditor.tsx`
- `web/edutwin-web/src/components/teacher/TeacherPrimitives.tsx`
- `web/edutwin-web/src/features/questions/useQuestions.ts`
- `web/edutwin-web/src/hooks/useStudentLearningAccess.ts`
- `web/edutwin-web/src/layouts/CenterManagerLayout.tsx`
- `web/edutwin-web/src/layouts/StudentLayout.tsx`
- `web/edutwin-web/src/pages/AuthorizationManagementPage.tsx`
- `web/edutwin-web/src/pages/CenterClassReportPage.tsx`
- `web/edutwin-web/src/pages/CenterDashboardPage.tsx`
- `web/edutwin-web/src/pages/ClassListPage.tsx`
- `web/edutwin-web/src/pages/LearningPlayerPage.tsx`
- `web/edutwin-web/src/pages/StudentAssignmentDetailPage.tsx`
- `web/edutwin-web/src/pages/StudentAssignmentsPage.tsx`
- `web/edutwin-web/src/pages/StudentDashboardPage.tsx`
- `web/edutwin-web/src/pages/StudentLearningPathPage.tsx`
- `web/edutwin-web/src/pages/StudentListPage.tsx`
- `web/edutwin-web/src/pages/StudentTwinPage.tsx`
- `web/edutwin-web/src/pages/classListHelpers.ts`
- `web/edutwin-web/src/pages/teacher/TeacherAssignmentEditorView.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherClassDashboardView.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherCurriculumEditorView.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherCurriculumListView.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherKnowledgeGraphView.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherQuestionBankView.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherQuestionEditorView.tsx`
- `web/edutwin-web/src/pages/teacher/TeacherStudentManagementView.tsx`
- `web/edutwin-web/src/pages/teacher/teacherExcelReports.ts`
- `web/edutwin-web/src/pages/teacher/teacherReportsHelpers.ts`
- `web/edutwin-web/src/styles/student-theme.css`
- `web/edutwin-web/src/types/assignments.ts`
- `web/edutwin-web/src/types/curriculum.ts`
- `web/edutwin-web/src/types/dashboards.ts`
- `web/edutwin-web/src/types/learning.ts`
- `web/edutwin-web/src/types/learningPath.ts`
- `web/edutwin-web/src/types/organization.ts`
- `web/edutwin-web/src/types/questions.ts`
- `web/edutwin-web/src/types/reviews.ts`
- `web/edutwin-web/src/utils/academicDisplay.ts`
- `web/edutwin-web/src/utils/academicLifecycleError.ts`
- `web/edutwin-web/src/utils/assignmentReviewTiming.ts`
- `web/edutwin-web/src/utils/competencyGroups.ts`
- `web/edutwin-web/src/utils/curriculumApplicationClasses.ts`
- `web/edutwin-web/src/utils/knowledgeGraphViewport.ts`
- `web/edutwin-web/src/utils/knowledgeTopicContext.ts`
- `web/edutwin-web/src/utils/knowledgeTopicOptions.ts`
- `web/edutwin-web/src/utils/questionImage.ts`
- `web/edutwin-web/src/utils/rubric.ts`
- `web/edutwin-web/src/utils/scratchpadPngImport.ts`
- `web/edutwin-web/src/utils/studentAcademicNavigation.ts`
- `web/edutwin-web/src/utils/studentAcademicScope.ts`
- `web/edutwin-web/src/utils/studentClassGrades.ts`
- `web/edutwin-web/src/utils/studentWorkspaceSummary.ts`
- `web/edutwin-web/tests/academicLifecycleDependencies.test.ts`
- `web/edutwin-web/tests/academicScopeCharts.test.ts`
- `web/edutwin-web/tests/archivedLearningLock.test.ts`
- `web/edutwin-web/tests/centerManagerDesignSystem.test.ts`
- `web/edutwin-web/tests/classLifecycleReports.test.ts`
- `web/edutwin-web/tests/curriculumApplicationUi.test.ts`
- `web/edutwin-web/tests/governanceRoleBoundary.test.ts`
- `web/edutwin-web/tests/gradingWorkflow.test.ts`
- `web/edutwin-web/tests/knowledgeGraphViewport.test.ts`
- `web/edutwin-web/tests/knowledgeTopicPicker.test.ts`
- `web/edutwin-web/tests/knowledgeTopicQuickActions.test.ts`
- `web/edutwin-web/tests/mathPreviewDelimiters.test.ts`
- `web/edutwin-web/tests/questionImage.test.ts`
- `web/edutwin-web/tests/rubric.test.ts`
- `web/edutwin-web/tests/scratchpadPngImport.test.ts`
- `web/edutwin-web/tests/studentClassGrades.test.ts`
- `web/edutwin-web/tests/studentClassHistory.test.ts`
- `web/edutwin-web/tests/studentDashboardResponsiveAndFilter.test.ts`
- `web/edutwin-web/tests/studentDetailScopeNavigation.test.ts`
- `web/edutwin-web/tests/studentReadableTheme.test.ts`
- `web/edutwin-web/tests/studentWorkspaceSummary.test.ts`
- `web/edutwin-web/tests/submittedMathReview.test.ts`
- `web/edutwin-web/tests/teacherDashboardAcademicCoverage.test.ts`
- `web/edutwin-web/tests/teacherHeaderResponsive.test.ts`
