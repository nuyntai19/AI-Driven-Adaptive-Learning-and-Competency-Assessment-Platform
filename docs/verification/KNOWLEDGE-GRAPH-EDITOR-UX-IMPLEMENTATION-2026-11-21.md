# Giao diện soạn khung đồ thị tri thức — triển khai và kiểm chứng

Ngày 21/11/2026, Asia/Bangkok. Triển khai theo kế hoạch đã được người dùng duyệt: [KNOWLEDGE-GRAPH-EDITOR-UX-REDESIGN-PLAN-2026-11-21.md](../plans/KNOWLEDGE-GRAPH-EDITOR-UX-REDESIGN-PLAN-2026-11-21.md).

## Kết quả

- Khung soạn có một canvas chính, thư viện chủ đề và bảng chi tiết theo lựa chọn. Khi màn hình hẹp, thư viện/chi tiết mở trong drawer thay vì ép ba cột nhỏ.
- Chọn khung bằng tab Khối 10/11/12, trạng thái Draft/Published/Archived và thẻ phiên bản. Giữ nhãn Hiện hành/Chuyển tiếp, luồng tạo khung trống và sao chép thành bản nháp.
- Thư viện lọc độc lập theo khối nguồn, trạng thái khung nguồn, khung/giáo trình cụ thể, tình trạng trong khung đích và tên/mã. Không suy khối từ mã chủ đề; membership bản nháp được lấy từ node mappings.
- Thêm chủ đề bằng hành động “Thêm vào khung”; tạo liên kết bằng cách chọn nguồn rồi đích trên canvas hoặc danh sách thay thế; chỉnh quan hệ bằng inspector. Không còn checkbox áp dụng song song với nút gỡ.
- “Gỡ quan hệ khỏi khung” chỉ gỡ mapping. “Xóa khỏi thư viện” là thao tác khác, có xác nhận và giữ các guard cũ.
- Nhóm cha có thao tác trên canvas và phương án tìm bằng danh sách. Lớp hướng dẫn cha–con tách biệt, không tạo cạnh PartOf hoặc tính vào số quan hệ.
- Nút `? Giải thích quan hệ` mở bảng bốn loại quan hệ, có ví dụ, chiều mũi tên và tác động thực tế. Inspector dùng đúng nhãn loại/hướng, bao gồm màn hình xem class/curriculum hiện có.
- Lỗi cấu trúc có danh sách bền vững, tên/mã đối tượng, đường vòng, cách sửa, highlight và “Đến vị trí lỗi”. Chu trình dài có tóm tắt và phần mở xem đầy đủ. Không coi lỗi tải dữ liệu là đối tượng đã bị xóa.
- Lỗi ghi server, xung đột phiên bản và lỗi xóa/sửa được giữ ở workspace/dialog phù hợp; không chỉ dựa vào toast. Form sửa chủ đề liên kết lỗi với trường tương ứng.
- Bản xem trước phản ánh ngay node/edge mappings đang soạn. Canvas, inspector, danh sách, chẩn đoán và payload lưu dùng một nguồn state.
- Tình trạng ghi thư viện và cấu trúc chưa lưu được phân biệt. Hủy cấu trúc không tự DELETE đối tượng vừa tạo trong thư viện; hủy cũng tải lại snapshot để lấy phiên bản mới sau xung đột.
- Giữ rà soát trước khi duyệt, không tự lưu rồi duyệt; không thêm yêu cầu đồ thị phải có cha, liên thông hoặc có tiên quyết.

## Điều chỉnh trình bày sau khi kiểm tra ảnh

Thanh lưu đặt ở cuối workspace và có nút **“Đến lưu / duyệt”** ở đầu. Không phủ một thanh sticky lên canvas: ở 360 px thanh này từng che các node, nên đã đổi cách bố trí để không che nội dung. Đây chỉ là thay đổi vị trí/điều hướng, không đổi điều kiện lưu/duyệt.

Trên mobile, rút gọn toolbar canvas, đưa thiết lập trọng số/đường nối/ẩn nút đã tắt vào “Tùy chọn hiển thị”. Giữ tìm kiếm, pan/zoom, chú giải và phương án danh sách. Hộp trợ giúp mở từ form tạo cạnh sẽ tạm ẩn form nhưng giữ giá trị đang nhập, tránh hai focus trap đồng thời.

Kéo-thả thư viện và kéo dây không được bổ sung, đúng phần tùy chọn trong kế hoạch. Luồng click và bàn phím là phương thức đầy đủ.

## Phạm vi code

Frontend hiện có:

- `web/edutwin-web/src/pages/teacher/TeacherKnowledgeGraphView.tsx`: gắn workspace mới vào route đang dùng, tránh hai canvas; tái sử dụng form tạo chủ đề và các điều kiện quick action hiện tại; sửa nhãn quan hệ trong inspector chỉ xem.
- `web/edutwin-web/src/components/teacher/KnowledgeVersionPanel.tsx`: bố cục và điều phối tương tác soạn.
- `web/edutwin-web/src/components/teacher/KnowledgeGraphCanvas.tsx`: highlight lỗi, marker nguồn, định vị đối tượng, lớp cha–con và toolbar gọn. Giữ renderer/routing/worker/LOD.
- `web/edutwin-web/src/components/teacher/KnowledgeNodeEditDialog.tsx`: đánh dấu trường lỗi và focus, giữ payload/luồng mã tự sinh.

Các thành phần mới trong `web/edutwin-web/src/components/teacher/`:

- `useKnowledgeDraftEditor.ts`: queries, snapshot, dirty/reviewed, các action mapping và mutation cũ.
- `FrameworkPicker.tsx`, `TopicLibraryPanel.tsx`, `DraftInspector.tsx`, `DraftRelationDialog.tsx`, `RelationHelpDialog.tsx`.
- `knowledgeDraftEditor.css`: bố cục responsive và trạng thái trực quan.

Helper mới:

- `web/edutwin-web/src/utils/knowledgeDraftEditor.ts`: lọc nguồn, overlay mapping thành preview và chẩn đoán. Chẩn đoán không được dùng làm guard lưu/duyệt mới.
- `web/edutwin-web/src/utils/knowledgeRelations.ts`: một nguồn nhãn/ý nghĩa/màu của bốn loại quan hệ.

Không sửa backend, API contract, database, seed hay thuật toán lộ trình. Ba tệp migration đã có thay đổi trong worktree trước lượt triển khai không thuộc công việc này và không bị sửa trong lượt này. Không commit/push/deploy.

## Kiểm chứng đã chạy

Chạy trong `web/edutwin-web`. Môi trường shell không có npm/npx trên PATH, nên chạy trực tiếp các CLI đã cài trong `node_modules`, tương đương các bước của script frontend:

| Kiểm tra | Lệnh thực tế | Kết quả |
| --- | --- | --- |
| Frontend unit/regression | `node --test tests/*.test.ts` | **957 passed, 0 failed** |
| TypeScript project build | `node node_modules/typescript/bin/tsc -b --pretty false` | Đạt |
| Vite production build | `node node_modules/vite/bin/vite.js build` | Đạt; vẫn có cảnh báo chunk lớn, không nới budget |
| Bundle budget/import cycles | `node --test tests/bundle/bundleBudget.test.ts` | **4 passed, 0 failed** |
| ESLint các tệp frontend thay đổi | `node node_modules/eslint/bin/eslint.js` với danh sách tệp thay đổi | Đạt |
| Browser regression | `node tests/smoke/chrome_knowledge_draft_smoke.cjs` | Đạt các nhóm bên dưới |
| Whitespace | `git diff --check` | Đạt |

Có thể chạy lại bài browser bằng script mới `npm run test:knowledge-draft-browser` trong môi trường có npm. Không thêm dependency.

### Những gì bài browser đã kiểm tra

Edge headless dùng profile tạm, Vite cục bộ và API mocks trong `tests/smoke/knowledge-draft-fixture.tsx`. Không đăng nhập hoặc ghi dữ liệu học thuật thật.

1. Khối nguồn/trạng thái khung lọc đúng chủ đề chỉ có trong Draft; không đổi khung đích, không gọi ghi API.
2. Đổi loại edge mapping và giữ trọng số 0; payload save đúng, không gọi PATCH/DELETE cạnh thư viện.
3. Vòng cha–con hai nút giống ảnh người dùng: cả hai node được đánh dấu, thông báo nêu đúng vòng; “Đến vị trí lỗi” focus trường sửa; sửa cha làm lỗi biến mất.
4. Click nguồn/đích để tạo Gây lỗi; request giữ `graphVersionId`, `subjectId`, endpoints, enum và weight hiện có.
5. Gỡ quan hệ chỉ bỏ mapping, không xóa cạnh thư viện.
6. Đảo chiều tiên quyết tạo/tìm cạnh ngược rồi chọn đúng cạnh thay thế; payload không còn mapping cũ, không xóa cạnh gốc.
7. Gỡ node vẫn giữ các quan hệ tham chiếu và chỉ rõ lỗi thiếu endpoint; save gate cũ vẫn chặn trường hợp này.
8. Tạo chủ đề rồi hủy cấu trúc không gọi DELETE thư viện; sau hủy có thể mở bộ chọn khung.
9. Save conflict giữ thay đổi cục bộ và thông báo bền vững.
10. Hộp trợ giúp đủ bốn dòng, kiểm tra cả theme sáng và tối.
11. Drawer thư viện/chi tiết ở 360 px, không trùng ID form; không tràn ngang ở 360/768/1024/1440 px.
12. Published/read-only vẫn xem chi tiết nhưng không có thao tác tạo quan hệ của bản nháp.
13. Tích hợp **TeacherKnowledgeGraphView thật**: chỉ một canvas, mở form tạo chủ đề hiện có, tự sinh mã, đưa node mới vào preview và lưu đầy đủ mappings.
14. Publish chỉ được thực hiện sau xác nhận rà soát; mở dialog không tự gọi publish/save.

Unit tests bổ sung kiểm tra string IDs lớn, loại/trọng số hiệu lực khác thư viện, endpoint thiếu nhưng mapping không bị mất, source versions chưa tải là unknown, tiên quyết ngoài khối chỉ là ngữ cảnh hiển thị, vòng hỗn hợp PartOf/PrerequisiteOf, loại RelatedTo/CausesErrorIn không bị coi là tiên quyết, node rời rạc và tóm tắt chu trình dài.

Những assertions JSX cũ gắn với checkbox/combobox đã được cập nhật theo cấu trúc mới, giữ các bảo đảm guard/payload. Các bài scope/occurrence/layout/worker/mã chủ đề hiện có vẫn chạy trong 957 bài trên.

## Ảnh minh chứng

- [Workspace desktop](evidence/kg-editor-2026-11-21/desktop.png)
- [Lỗi vòng cha–con và vị trí sửa](evidence/kg-editor-2026-11-21/parent-cycle.png)
- [Bộ chọn khung theo khối/trạng thái](evidence/kg-editor-2026-11-21/framework-picker.png)
- [Bảng giải thích — theme tối](evidence/kg-editor-2026-11-21/relation-help.png)
- [Bảng giải thích — theme sáng](evidence/kg-editor-2026-11-21/relation-help-light.png)
- [Mobile](evidence/kg-editor-2026-11-21/mobile.png)
- [Chi tiết trên mobile](evidence/kg-editor-2026-11-21/mobile-inspector.png)
- [Trang Teacher thực tế trong fixture sau tạo và lưu chủ đề](evidence/kg-editor-2026-11-21/full-workspace.png)

Các ảnh dùng dữ liệu kiểm thử, không phải bằng chứng đã thay đổi dữ liệu trên server thật.

## Giới hạn của bằng chứng

Đã kiểm tra UI, state và các request qua API mocks, chưa chạy ghi/duyệt/xóa với backend thật hoặc tài khoản thật. Backend không đổi nên không chạy lại bộ test .NET. Các chi tiết lỗi phụ thuộc dữ liệu ngoài phạm vi tải vẫn hiển thị lý do server an toàn/mã hỗ trợ, không bịa tên hoặc đường vòng mà client không có.

Không hỗ trợ rollback API thư viện, autosave, tự thêm tiên quyết hoặc đổi điều kiện duyệt; các khả năng này nằm ngoài phạm vi đã duyệt.

## Sửa lỗi tiếp theo: mở “Đổi khung” với tên null

- Nguyên nhân: backend `KnowledgeGraphVersionDto.Title` là `string?`, nhưng frontend khai báo `title: string` và gọi trực tiếp `v.title.toLocaleLowerCase()` trong FrameworkPicker. Một khung không đặt tên làm render thất bại kể cả khi ô tìm kiếm đang rỗng. RouteChunkBoundary chỉ hiển thị thông báo dự phòng, không phải nguyên nhân lỗi kết nối/chunk.
- Sửa kiểu frontend thành `string | null`; dùng helper `frameworkTitle` cho tìm kiếm và hiển thị, bao gồm thẻ khung, tiêu đề đang mở, nguồn tiên quyết, dropdown và tooltip thư viện. Tên null/rỗng/chỉ khoảng trắng hiển thị `Khung Khối 10/11/12`; không ghi tên này ngược vào dữ liệu.
- Fixture browser có thêm ba phiên bản tên null/rỗng/khoảng trắng: trước sửa hộp Đổi khung không mở được, sau sửa mở được và tìm `Khung Khối 12` ra cả ba; tìm tên tiếng Việt không phân biệt hoa thường vẫn đúng. Không có mutation khi mở/tìm kiếm.
- Thêm hai unit tests cho tên dự phòng, giữ nguyên dữ liệu API và tìm kiếm tiếng Việt. Tổng hiện tại **959 tests passed**, TypeScript/Vite build, lint các tệp sửa và **4 bundle tests** đạt. Browser regression toàn luồng cũng đạt.
- Cập nhật frontend Docker cục bộ riêng service `web` để phục vụ bản sửa ở localhost:3000; không thay API hoặc database.

## Điều chỉnh tiếp theo: mở trình chỉnh sửa ngay từ Đồ thị tri thức

- Bổ sung nút **Chỉnh sửa khung tri thức** tại header trang **Đồ thị tri thức**, đồng thời giữ nút có sẵn tại trang chỉnh sửa giáo trình theo yêu cầu điều chỉnh của người dùng. Giáo viên chọn môn ngay trên trang đồ thị; nếu chưa chọn khối, nút mở hộp chọn Khối 10/11/12. Nếu đã chọn khối, mở thẳng khối đó.
- Giữ cơ chế hiện có: ưu tiên bản nháp không legacy do chính người dùng tạo trong môn/khối đã chọn. Nếu chưa có, tạo bản nháp từ bản Published hiện hành (hoặc bản Published có sẵn); không có nguồn thì tạo khung trống. Không yêu cầu giáo trình, lớp hoặc năm học, không tự lưu/duyệt bản nháp.
- Nút chỉ hiện với giáo viên có quyền đọc và sửa chủ đề/quan hệ; khi cần tạo bản nháp mới vẫn kiểm tra quyền tạo. Trong lúc mở có trạng thái chờ và khóa thao tác lặp, lỗi hiển thị tại nút/hộp chọn để thử lại. Phản hồi đến sau khi đổi môn/phạm vi không điều hướng sang khung cũ.
- Khôi phục nguyên nút và mutation tại trang giáo trình: hiện trong giáo trình Draft đã lưu, có môn/khối; giữ nguồn khung đang chọn, ngữ cảnh lớp và liên kết quay lại giáo trình như trước. Nút tại trang Đồ thị tri thức hoạt động độc lập. Không sửa API, backend, payload lưu cấu trúc hoặc điều kiện duyệt.
- Browser regression trên **TeacherKnowledgeGraphView thật** xác nhận hai nhánh: chọn Khối 12 mở bản nháp có sẵn **không gọi mutation**; Khối 11 chưa có bản nháp thì chỉ gọi `createVersion` một lần với đúng môn, khối và `sourceGraphVersionId`. URL vào editor không cần `curriculumId`, `classId`, `classContextId` hay `returnCurriculumId`. Các kiểm tra tạo chủ đề, lưu và duyệt sau đó vẫn đạt.
- Kiểm tra: **959 unit/regression tests**, TypeScript, ESLint các tệp ứng dụng sửa, production build, **4 bundle tests**, `git diff --check` và toàn bộ browser regression đạt. Build vẫn có cảnh báo chunk lớn đã có trước.
- [Ảnh nút mở trực tiếp trên trang đồ thị](evidence/kg-editor-2026-11-21/direct-edit-entry.png). Dữ liệu ảnh là fixture, không ghi dữ liệu học thuật thật.
- Sau khi khôi phục nút tại giáo trình, TypeScript, lint, **959 tests** và Docker production build đều đạt. Đã cập nhật riêng container `web`; localhost:3000 trả HTTP 200 với entry `/assets/index-BznpLxKj.js`.
