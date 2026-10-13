# Kiểm tra import F10–F16 — 12/10/2026

## Lỗi phát hiện và đã sửa

1. DOCX dạng bảng mất xuống dòng trong ô khi Word dùng `w:br`. Parser hiện giữ xuống dòng, tab và các đoạn văn; có test hồi quy.
2. Sau import thành công, mở lại modal vẫn giữ kết quả/preview cũ. Modal hiện reset khi mở lại để chọn file tiếp theo.
3. Dropdown chủ đề import hiển thị cả chủ đề inactive. Giao diện hiện chỉ cho chọn chủ đề active; API tiếp tục kiểm tra scope.

## Kết quả xác minh

- 42/42 ca HTTP qua nginx của stack riêng đạt: CSV UTF-8 có/không BOM, XLSX thường/worksheet path đặc biệt, DOCX bảng; preview 3 hàng, 1 hợp lệ/2 lỗi; giữ xuống dòng và dấu nháy; confirm chỉ tạo một hàng hợp lệ.
- Replay/race confirm, scope inactive, quyền Student/Manager, file sai cấu trúc, giới hạn số hàng/dung lượng và ràng buộc điểm khi tạo/sửa đều được kiểm tra trong bộ HTTP.
- 26/26 test `QuestionImportUseCaseTests` đạt.
- Frontend `npm run verify`: 711 test đạt, kiểm tra TypeScript/build thành công; 3 kiểm tra bundle đạt. Lint có 26 warning, không có error.
- Docker API và web build thành công.
- Trên giao diện giáo viên đã import CSV, xác nhận số câu tăng đúng một, nội dung/đáp án/lời giải/điểm đúng; mở lại modal có thể chọn file tiếp theo.

XLSX đã được chọn trên giao diện nhưng chưa preview/confirm; DOCX chưa thực hiện qua giao diện trong lượt này vì người dùng yêu cầu kết thúc kiểm thử và push. Hai định dạng đã được kiểm tra bằng file thật qua API và đối chiếu dữ liệu lưu. Không coi kiểm thử đã chạy là bảo đảm tuyệt đối không còn lỗi.

## File và cách chạy lại

- Bộ file: [import-fixtures-2026-10-12/README.md](import-fixtures-2026-10-12/README.md). Giữ cả file trống/hỏng vì đây là ca kiểm thử âm tính.
- Tạo lại bộ file bằng `scripts/e2e/create_import_file_fixtures.py` (cần `openpyxl`, `python-docx`).
- Khởi tạo stack riêng bằng `node scripts/e2e/acceptance_stack.cjs start`, chạy `node scripts/e2e/import_file_acceptance.cjs`, rồi dọn bằng `node scripts/e2e/acceptance_stack.cjs stop`.
- Bằng chứng/log cục bộ nằm trong `docs/verification/evidence/import_f10_f16_20261012/`, được gitignore.

Stack riêng và database thử đã được dọn. Confirm lại cùng preview bị chặn; upload mới cùng nội dung vẫn là preview khác, chưa có deduplicate toàn cục theo nội dung file.
