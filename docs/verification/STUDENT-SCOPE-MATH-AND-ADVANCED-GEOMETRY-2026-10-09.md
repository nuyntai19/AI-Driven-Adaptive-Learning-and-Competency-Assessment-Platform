# Sửa phạm vi bài tập, dựng công thức và chuẩn bị câu hình học nâng cao

## Trạng thái

- Hai lỗi frontend đã sửa và triển khai trên web localhost; kiểm tra trực tiếp bằng skill computer-use trên Chrome/student01.
- 643 test frontend passed, 0 failed; production build và Docker web build thành công.
- Không sửa API, schema, pipeline AI, quota, điểm/bài làm cũ. Không commit, stage hoặc push.
- Cập nhật sau lượt tạo và thử: hai câu đã lưu vào ngân hàng (#20068: đề hoàn toàn trên ảnh; #20069: bắt buộc Vẽ nháp). Người dùng đã kích hoạt và đưa vào Test 5. Đã nộp thử bằng student01; xem `GEOMETRY-IMAGE-TEST5-2026-10-09.md` để đọc kết quả, thời gian và lỗi chấm ký hiệu hình còn lại.

## Lỗi 1: chuyển phạm vi khi mở chi tiết bài tập

Nguyên nhân: bộ chọn môn/lớp/lịch sử thay query nhưng giữ nguyên `/hoc-tap/bai-tap/{assignmentId}`. Query của chi tiết bài lấy theo ID, nên nội dung cũ vẫn hiện dưới phạm vi mới.

Sửa: khi người dùng đổi môn, lớp hoặc Đang học/Xem lịch sử trên trang chi tiết, chuyển về danh sách bài tập của phạm vi mới, bỏ ID bài cũ. Các trang tổng quan/danh sách giữ chức năng điều chỉnh query như trước. Không đổi trạng thái lớp hoặc chuyển bài tập giữa lớp.

Kiểm tra Chrome:

- Từ chi tiết Test 4 của Lớp Toán 10 bấm Xem lịch sử: chuyển sang danh sách lớp Toán cũ `50000000-0000-0000-0000-000000000005`, chỉ có Test 1/2/3, không Test 4.
- Bấm Đang học: danh sách Lớp Toán 10 có Test 4; mở lại bài được bình thường.
- Từ chi tiết Test 4 chọn Tiếng Anh: chuyển sang danh sách lớp Tiếng Anh 10, không còn nội dung Test 4.

## Lỗi 2: công thức hiện nguyên dấu dollar

- Rubric trước đây in `title/comment` như chuỗi thường: đổi sang RichMathText dùng chung cho học sinh/giáo viên, giữ nguyên điểm đã lưu.
- API công thức của KaTeX không nhận dấu bao `$...$`: chỉ gỡ cặp delimiter bao toàn bộ công thức trước khi dựng (`$...$`, `$$...$$`, `\(...\)`, `\[...\]`). Không sửa chuỗi hỗn hợp hoặc dữ liệu bài làm trong DB.
- Giữ `trust: false`, không cho công thức sinh liên kết JavaScript hoặc HTML thực thi.
- Chrome xác nhận rubric AI và rubric đã duyệt mỗi phần có hai công thức dựng hợp lệ, zero `.katex-error`. Đáp án câu 2 có một công thức hợp lệ (số 10), không còn lỗi màu đỏ.

## Tệp chạm trong lượt này

Source/test:

1. `web/edutwin-web/src/utils/studentAcademicNavigation.ts`
2. `web/edutwin-web/src/components/student/StudentAcademicContext.tsx`
3. `web/edutwin-web/src/layouts/StudentLayout.tsx`
4. `web/edutwin-web/src/components/reviews/RubricGradeView.tsx`
5. `web/edutwin-web/src/components/math/mathPreviewUtils.ts`
6. `web/edutwin-web/tests/gradingWorkflow.test.ts` — cập nhật harness cho renderer mới.
7. `web/edutwin-web/tests/studentDetailScopeNavigation.test.ts` — ba regression test điều hướng.
8. `web/edutwin-web/tests/mathPreviewDelimiters.test.ts` — bốn regression test renderer/rubric/an toàn.
9. `scripts/verification/create_geometry_image_only_fixture.py` — dựng ảnh đề tự soạn bằng Pillow, không API ngoài.

Tệp này là báo cáo bổ sung. Asset PNG nằm trong `storage/verification`, không commit/push trong lượt này.

## Câu A — toàn bộ đề và hình nằm trên ảnh

- Khối 10; Private Teacher Math; chủ đề `MATH10-TRIANGLE-METRICS`; Essay/Manual, 10 điểm; yêu cầu lập luận tiếng Việt; thời gian dự kiến 420 giây.
- Ảnh: `storage/verification/geometry10-full-statement.png`, tự soạn, không ảnh sách/bản quyền bên ngoài. 1600×1200, khoảng 105 KB; mọi dữ kiện và yêu cầu nằm trên ảnh.
- Nội dung đề chữ để trống khi tạo; hệ thống lưu câu dẫn chuẩn “Đọc đề bài trong ảnh đính kèm.”, không chép lại dữ kiện vào ô đề chữ.
- Đề trong ảnh: ABC có AB=13 cm, AC=15 cm, BC=14 cm; M là trung điểm BC, H là chân đường cao từ A. Tính diện tích/AH, trung tuyến AM chính xác và bán kính ngoại tiếp R.
- Đáp án: S=84 cm²; AH=12 cm; AM=2√37 cm; R=65/8 cm (8,125 cm).
- Lời giải tiếng Việt: Nửa chu vi p=21 cm. Heron cho S=√(21×8×6×7)=84 cm². Từ S=BC×AH/2 suy ra AH=2S/BC=12 cm. Công thức trung tuyến AM²=(2AB²+2AC²−BC²)/4=(338+450−196)/4=148, nên AM=2√37 cm. Công thức S=AB×AC×BC/(4R) cho R=13×15×14/(4×84)=65/8 cm.
- Rubric 4–3–3: diện tích/đường cao 4 (2+2), trung tuyến 3, bán kính ngoại tiếp 3. Chấp nhận lời giải tọa độ/cosin hoặc phương pháp tương đương hợp lệ; không trừ điểm vì không vẽ lại hình đã cung cấp, không suy đoán theo tỉ lệ ảnh.
- Lỗi thực sự: sai p, lấy AM=BC/2 cho tam giác không vuông, nhầm trung tuyến với đường cao, nhầm bán kính/đường kính, sai đơn vị diện tích.

## Câu B — bắt buộc vẽ hình trong Vẽ nháp

- Khối 10; Private Teacher Math; chủ đề `MATH10-TRIANGLE-METRICS`; Essay/Manual, 10 điểm; yêu cầu lập luận tiếng Việt; thời gian dự kiến 360 giây.
- Đề: Cho tam giác ABC vuông tại A, AB=6 cm, AC=8 cm. M là trung điểm BC. Dùng Vẽ nháp dựng tam giác, đánh dấu góc vuông tại A, ghi tên A/B/C/M, đặt M trên BC, nối AM và đánh dấu BM=MC. Đính kèm ảnh nháp; tính BC, AM và giải thích bằng tiếng Việt. Hình có thể xoay/lật, không cần đúng tỉ lệ, nhưng phải đúng quan hệ hình học và tên điểm.
- Đáp án: BC=10 cm; AM=5 cm; kèm hình đúng yêu cầu.
- Lời giải: Hai cạnh AB và AC vuông góc tại A; BC là cạnh huyền. Pythagore: BC²=6²+8²=100, BC=10 cm. Trung tuyến từ đỉnh góc vuông đến cạnh huyền bằng nửa cạnh huyền, AM=BC/2=5 cm. Cũng có thể chứng minh/tính AM bằng tọa độ hoặc công thức trung tuyến hợp lệ.
- Rubric 4–2–4:
  - Hình dựng và quan hệ (4): ảnh thực tế cho thấy tam giác ABC vuông tại A, M nằm trên BC và AM được nối. Không có ảnh hoặc không đọc được ảnh: không tự bịa hình; giải thích thiếu bằng chứng/đề nghị giáo viên xem xét. Không dùng lời tự khai “đã vẽ” để thay ảnh.
  - Ký hiệu hình (2): tên điểm A/B/C/M rõ, góc vuông tại A, dấu BM=MC hoặc hai đoạn BM/MC đều ghi 5 cm. Chấp nhận cách ký hiệu tương đương, không chấm độ đẹp của nét vẽ.
  - Tính toán/lập luận (4): BC=10 bằng Pythagore 2 điểm; AM=5 có căn cứ hợp lệ 2 điểm. Tính đúng vẫn được điểm phần này nếu hình thiếu; không tự cho trọn điểm phần hình chỉ vì đáp số đúng.
- Không thêm ràng buộc DB mới “bắt buộc ảnh” trong lượt này: yêu cầu được thể hiện ở đề và rubric; AI đưa điểm đề xuất theo bằng chứng, giáo viên duyệt điểm chính thức.

## Bằng chứng

- `storage/verification/test4-history-scope-fixed.jpg`
- `storage/verification/test4-rubric-math-fixed.jpg`
- `storage/verification/test4-answer-math-fixed.jpg`

Cập nhật: đã thử hai câu trong Test 5, không thay câu trong Test 4 đã có bài làm. Server hoàn tất hai câu sau 11,97 giây, 0 fallback. Câu đề ảnh chấm đúng trường hợp a/b đúng, c sai (7/10); câu vẽ cố ý thiếu nhãn/ký hiệu lại được AI cho 10/10, nên chưa thể kết luận tính năng chấm bản vẽ đạt. Chi tiết và ảnh bằng chứng nằm trong `GEOMETRY-IMAGE-TEST5-2026-10-09.md`.
