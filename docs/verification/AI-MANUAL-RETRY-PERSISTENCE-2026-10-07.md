# Khôi phục câu chấm lại bị kẹt khi lưu kết quả — 07/10/2026

## Nguyên nhân đã xác minh

- Câu 2 của lần thử 50 câu đã có phản hồi Gemini hợp lệ và checkpoint.
- Khi thay kết quả fallback, luồng replay hồ sơ năng lực ghi mã phiên bản
  `fallback-recovery-replay-v1` (27 ký tự) vào `twin_update_history.calculation_version`
  có kiểu `varchar(20)`. MySQL từ chối commit với lỗi `Data too long`.
- Transaction bị rollback, job giữ lease Processing và thử lại sau mỗi 5 phút.
  Đây là lỗi lưu kết quả, không phải bằng chứng hết quota hoặc inference kéo dài.

## Bản sửa

- Dùng mã ổn định `fallback-replay-v1` (18 ký tự), phù hợp schema hiện có.
- Không đổi schema SQL, không tắt trigger và không sửa dữ liệu bằng SQL thủ công.
- Giữ nguyên cơ chế checkpoint, transaction, lease và lịch sử append-only.
- Không thêm lượt retry, không chấm lại 50 câu và không duyệt điểm thay giáo viên.

## Kiểm chứng

- Test MySQL mới `ManualFallbackRecovery_MySqlCommitsCachedResultAndPreservesAppendOnlyHistory`
  tái hiện đúng lỗi `Data too long for column 'calculation_version'` trước bản sửa.
- Sau bản sửa, test đạt trên database tạm, không tác động database ứng dụng:
  không gọi provider, giữ nguyên đáp án/lập luận, giữ analysis ID, giữ lịch sử và
  evidence cũ, thêm evidence kế nhiệm, không đếm lượt làm hai lần, dọn checkpoint
  sau commit và không ghi lặp khi thực thi lại job đã hoàn tất.
- Test recovery in-memory cũng kiểm tra mã phiên bản và giới hạn 20 ký tự để
  bắt hồi quy trong lượt CI thông thường không bật MySQL opt-in.
- 280 test liên quan Digital Twin, processor, worker và manual retry đạt.
  Lượt trong sandbox bị timeout kết nối testhost; lượt chạy ngoài sandbox đạt.

## Kết quả local

- Image API được build/recreate thành công, image đang chạy khớp image mới.
  API readiness trả Healthy; MySQL container và volume `edutwin_mysql_data` không đổi.
- Job câu 2 tự được worker mới tiếp quản khi lease cũ hết hạn, không reset job
  bằng SQL. Job bắt đầu lúc 14:14:27.348699 UTC và hoàn tất lúc 14:14:27.777722 UTC
  (khoảng 0,43 giây lưu kết quả checkpoint, không phải thời gian inference).
- Phân tích không còn fallback: Correct / Valid, điểm AI đề xuất 10/10,
  trạng thái bài làm NeedsTeacherReview. Điểm chính thức vẫn do giáo viên duyệt.
- Fingerprint đáp án/lập luận của câu giữ nguyên. Vẫn 104 attempts / 104 analyses;
  câu này có 2 evidence (1 bản kế nhiệm) và 2 lịch sử (bản cũ + replay), checkpoint đã dọn.
- Kiểm tra Chrome sau tải lại: AI đã phân tích 50/50; câu 2 hiện AI đề xuất 10/10,
  rubric 4 + 4 + 2, nhận xét và giải thích tiếng Việt; không còn trạng thái AI đang phân tích.
- Không chạy lại benchmark 50 câu và không push trong lượt sửa này.
