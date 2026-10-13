# Kiểm thử trình duyệt: Excel V1 và bản nháp sau tải lại — 13/10/2026

## Phạm vi

Theo yêu cầu của người dùng, kiểm tra hai luồng thực tế trên bản hiện tại của nhánh `shin/check-code` (HEAD `93ca621` cùng các sửa bổ sung chưa commit). Không sửa code sản phẩm trong lượt này, không commit/push.

- Web nghiệm thu `http://127.0.0.1:3002`, API 5001, schema `edutwin_e2e_917b9430831b4f2d87b4b172fcc39595`.
- Dùng image API/web hiện tại; database mới migrate/seed, không dùng dữ liệu của stack chính 3000/5000/3307.
- Người dùng đăng nhập Teacher Math, sau đó student02 trong tab nghiệm thu. Không tự nhập thông tin đăng nhập của người dùng hoặc thay tài khoản ở tab chính.
- Chỉ nhập câu Draft và sửa draft của bài fixture. Không kích hoạt câu import, không nộp bài, không gọi AI hoặc tạo điểm.

## 1. Excel V1 — ĐẠT trong các ca đã thử

### File hợp lệ

1. Giáo viên mở Ngân hàng câu hỏi → Nhập từ Excel / CSV, chọn Toán.
2. Tải **mẫu Excel V1 theo môn** do ứng dụng xuất ra. File gốc 282.332 byte, 3 sheet Questions/Topics/Guide; 3 dòng ví dụ giữ nguyên.
3. Chọn file bằng file chooser thật của Chrome, gửi qua nút Kiểm tra & xem trước.
4. Kết quả: tổng 3 câu, đủ điều kiện 3, câu lỗi 0, lỗi/cảnh báo 0.
5. Mở chi tiết câu phân số: đề, lời giải và đáp án hiển thị đúng công thức; nhận NumericRational, khối 12, topic riêng MATH-EXP-LOG.
6. Xác nhận lưu Draft: UI tạo đúng các câu #20030, #20031, #20032.
7. MySQL: số câu toàn schema tăng 60 → 63. Đủ MultipleChoice/TextExact, ShortAnswer/NumericRational, Essay/Manual; topic lần lượt 10000/10001/10002; grade 12, score 10, vi/Private, đúng center và giáo viên.
8. Mở #20031 rồi lưu lại qua editor không đổi nội dung. Câu vẫn Draft, đáp án `5/6`, đề/lời giải giữ đúng một backslash LaTeX, row_version 2, không double-escape.

### File lỗi

Tạo bản sao bằng công cụ bảng tính, chỉ sửa `Questions!U4` (MaxScore) thành `1000` và `Questions!W4` (ReasoningRequired) thành `tru`; giữ nguyên các giá trị khác, sheet Topics/Guide và mẫu gốc. Đã render/đối chiếu giá trị và đọc lại XLSX xuất ra.

- Trình duyệt đọc đúng 3 câu: **2 hợp lệ, 1 câu lỗi, 2 lỗi trường**.
- UI nêu đúng Questions/dòng 4/Q003: MaxScore vượt 999.99; ReasoningRequired không thuộc true/false/1/0.
- Không tự coi `tru` là false, không đếm một câu có hai lỗi thành hai câu lỗi.
- Nút Xác nhận lưu Draft (2) bị vô hiệu khi chưa chủ động chọn nhập tập hợp hợp lệ.
- Chỉ xem trước, không xác nhận file lỗi; SQL vẫn 63 câu. Không có ghi âm thầm.
- Chưa thử nhánh SelectedValid qua browser trong lượt này; không suy rộng các ca trên thành kiểm chứng mọi định dạng/giới hạn tài nguyên.

Thao tác chờ download của công cụ kiểm tra đã timeout một lần, nhưng file tải thực tế thành công và được kiểm tra từ thư mục Downloads. Đây không phải lỗi import của ứng dụng. File chooser/preview/confirm đều được thực hiện qua UI, không thay bằng gọi API import.

## 2. Giữ bản nháp sau tải lại — DỮ LIỆU ĐẠT, CÒN LỖI HIỂN THỊ

### Chuẩn bị và thao tác

- Fixture bài 2 câu riêng, ID `e7e8841a-f503-4250-b92c-e21ff79b50e5`, cho student02. Tạo assignment fixture ở schema tạm bằng harness hiện có; start/lưu draft ban đầu bằng API.
- Trước lần mở UI: server có 2 draftAnswers, draftVersion 1; câu 10000 chọn option ID 10000 (A/7), câu 10001 trả lời `R \ {2}`; hai lập luận tổng hợp và confidence 80.
- Mở bài trên Chrome: cả hai bản nháp ban đầu được hydrate vào UI.
- Câu 1: thay lập luận bằng `RELOAD_Q1: Thay $x=3$ vào $y=2x+1$ ta được $y=7$.`, confidence 65.
- Câu 2: thay lập luận bằng `RELOAD_Q2: Mẫu số $x-2$ khác 0 nên $x \ne 2$.`, confidence 75.
- Đổi qua lại câu, tải lại trang thật, mở lại cả hai câu. Chờ hydrate/render hoàn tất trước khi đối chiếu.

### Kết quả đối chiếu

- Hai đáp án nguồn giữ nguyên, không bị đáp án câu khác ghi đè.
- Hai lập luận giữ nguyên source `$...$`/LaTeX trên server và render đúng trên UI sau tải lại.
- Confidence riêng 65/75 được giữ, không lẫn giữa câu.
- Trạng thái chọn phương án A được khôi phục trên câu trắc nghiệm.
- Đối chiếu API sau reload và lần chuyển câu cuối: đủ 2 draftAnswers, draftVersion cuối được ghi nhận **14**, các giá trị khớp kỳ vọng.
- SQL: **0 attempt** của assignment fixture. Không nộp bài, không tạo điểm hoặc kết quả AI.

### Lỗi được phát hiện, chưa sửa

**Đáp án MathEquivalent dạng chữ `R \ {2}` được vẽ thành `R 2` trong ô nhập sau hydrate/reload.**

Nguồn server vẫn là `R \ {2}`; không phải mất dữ liệu nháp. Luồng hydrate của LearningPlayerPage khi `answerDisplayLatex` rỗng/null đang fallback bằng `finalAnswer`. `inlineMathEditingText` coi displayLatex là công thức KaTeX và bọc `$...$`. Cú pháp tập hợp dạng chữ được bộ so khớp hỗ trợ không đồng nghĩa với cú pháp LaTeX trình bày, nên dấu ngoặc/cách viết hiệu tập hợp bị diễn giải sai.

- Tái hiện trên UI thật trước và sau tải lại, có ảnh.
- Không chấm bài trong ca này, không kết luận chất lượng chấm AI hoặc điểm thực tế từ ảnh hiển thị.
- Hướng sửa cần tách source so khớp và LaTeX hiển thị: chỉ coi displayLatex thật là LaTeX, hoặc chuyển ký hiệu tập hợp được hỗ trợ sang ký hiệu hiển thị an toàn. Không sửa raw answer hoặc chấm lại bài cũ.
- Đây là phát hiện để người dùng duyệt sửa tiếp; lượt yêu cầu kiểm thử không tự mở rộng sang sửa code.

## Bằng chứng và dọn môi trường

Các bằng chứng runtime được Git ignore tại `storage/verification/import-draft-20261013/`:

- `import-success.png`, `import-invalid.png`.
- `draft-after-reload-q1.png`, `draft-set-rendering-issue.png`.
- `import-db.json`, `draft-before.json`, `draft-after.json`; không chứa token/password.

Tab nghiệm thu đã đóng. `acceptance_stack.cjs stop` xác minh dọn đúng 2 container, schema và user của runId test. Bốn container chính vẫn chạy; MySQL healthy. Không rollback/restore database chính, không đổi dữ liệu Test 4–11. Các file fixture/builder trong scratch không nằm trong diff sản phẩm.

Kết luận: **Import Excel V1 đạt các ca UI/persistence đã thử. Bản nháp giữ đúng dữ liệu sau đổi câu và reload; riêng hiển thị ký hiệu tập hợp MathEquivalent còn lỗi cần xử lý.**

## Bổ sung: đã sửa và kiểm chứng ký hiệu tập hợp — 13/10/2026

Theo yêu cầu sửa tiếp của người dùng, lỗi hiển thị nêu trên đã được xử lý; các mục trước giữ nguyên để ghi lại kết quả lần kiểm thử đầu.

### Thay đổi

- LearningPlayerPage không còn lấy đáp án nguồn làm LaTeX hiển thị khi khôi phục draft thiếu `answerDisplayLatex`.
- Adapter giữ nguyên đáp án tập hợp dạng chữ, kể cả nháp cũ có `answerDisplayLatex` bằng đáp án nguồn. Không thay raw answer, chế độ so khớp, điểm hoặc dữ liệu bài đã nộp.
- Popup Chèn công thức dùng chung có nhóm **Tập hợp**: ℝ, {…}, ∖, ∈, ∉, ∪, ∩, ∅. Ngoặc nhọn có ô trống để điền; nút chèn qua ref công khai của VisualMathField.
- Popup giới hạn theo viewport và cuộn khi thiếu chiều cao; không khôi phục trình gõ MathLive thường trực ở ô đáp án.

### Kiểm chứng

- 78 kiểm thử trọng tâm đạt; toàn bộ frontend **753 đạt, 0 lỗi, 0 bỏ qua**. Lint các file sửa và TypeScript/Vite build đạt. Build còn cảnh báo kích thước bundle MathLive đã có, không phải lỗi build.
- Giáo viên: thử trên trang tạo mới, không lưu câu. Bấm ℝ → ∖ → {…}, điền 2, Hoàn tất; nguồn editor đúng `$\mathbb{R}\setminus\left\{2\right\}$`.
- Học sinh: database nghiệm thu riêng `edutwin_e2e_df600a034fcc4c0a87c7489f8aa797a0`, web 3002. Fixture `2e4e4f07-d0a5-469e-a1fb-9baa3f0b8587`, student02 đăng nhập thủ công.
- Đáp án gõ thường `R \ {2}`: đổi câu/tải lại thật, vẫn hiển thị đầy đủ. Server draftVersion 7 giữ raw đúng, displayLatex null và lập luận đúng source.
- Đáp án tạo bằng nhóm Tập hợp: đổi câu/tải lại thật, hiển thị đúng ℝ ∖ {2}. Server draftVersion 12 giữ cả raw/displayLatex là `\mathbb{R}\setminus\left\{2\right\}`; lập luận không đổi.
- Nháp legacy `524e2ae3-2e53-491a-9ec2-e2b95b668a80` có displayLatex bằng raw `R \ {2}`: sau tải lại vẫn giữ đủ dấu và ngoặc.
- Viewport 390×480: popup nằm trong khung màn hình. 390×320: popup từ y=16 đến y=304, nội dung 429 px cuộn trong khung 286 px; bấm Hủy được, đáp án nguồn giữ nguyên. Đã reset viewport.
- SQL toàn schema test: **0 attempt**. Không gọi AI, không nộp bài, không tạo/chốt điểm. Không suy rộng thành kiểm chứng chấm AI.

Bằng chứng được Git ignore tại `storage/verification/set-draft-fix-20261013/`: `plain-set-after-reload.jpg`, `set-buttons-popup.jpg`, `formula-set-after-reload.jpg`, `legacy-set-after-reload.jpg`, `plain-draft.json`, `formula-draft.json`.

Bản web mới đã cập nhật riêng container web ở cổng 3000; API/MySQL chính không thay đổi. Không commit/push.

Đã đóng tab nghiệm thu, reset viewport, trả trang tạo câu hỏi giáo viên về form trống ban đầu. Harness xác minh dọn đúng 2 container/schema/user của lượt test. Bốn container chính vẫn chạy, MySQL healthy. Giữ bằng chứng trên đĩa; không để lại bài fixture trong database chính.

### Điều chỉnh vị trí theo yêu cầu tiếp theo: bàn phím MathLive

Người dùng yêu cầu bỏ thanh ký hiệu ngoài popup. Nhóm Tập hợp được chuyển vào layout bàn phím ảo MathLive dùng chung qua API `mathVirtualKeyboard.layouts`. Giữ các layout số/ký hiệu/chữ sẵn có, thêm layout có ID cố định `edutwin-sets` đúng một lần khi khởi tạo/focus math-field. Nhóm mới có tám ký hiệu, hàng chữ số, dấu phân cách và phím di chuyển/xóa/ẩn bàn phím. Không đổi cách serialize/chấm hoặc bản sửa nháp phía trên.

Đã bỏ ambient `declare module "mathlive"` không có kiểu để sử dụng types chính thức đi kèm bản MathLive đã cài. Kiểm thử cấu hình xác nhận bảo toàn layout khác, không nhân đôi khi focus/mở lại, LaTeX hợp lệ và không còn thanh ngoài popup. Toàn bộ frontend: 754 đạt, 0 lỗi, 0 bỏ qua; TypeScript và lint file sửa đạt.

Build TypeScript/Vite cuối đạt, đã cập nhật riêng web cổng 3000. Kiểm thử UI giáo viên trên form tạo mới không lưu: mở Chèn công thức → biểu tượng bàn phím → Tập hợp, bấm ℝ → ∖ → {…} → phím 2 trong bàn phím; hoàn tất và xem mã cho đúng `$\mathbb{R}\setminus\left\{2\right\}$`. Mở lại công thức/bàn phím, có đúng một tab Tập hợp đang hiển thị cùng bốn nhóm mặc định; không còn group ký hiệu ngoài popup. Bằng chứng: `storage/verification/set-draft-fix-20261013/set-virtual-keyboard.jpg`. Lượt điều chỉnh này không nộp bài/gọi AI, không cần database test mới và không push.
