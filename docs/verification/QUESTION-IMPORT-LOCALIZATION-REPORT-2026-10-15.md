# Bảng LaTeX và mẫu import tiếng Việt — 15/10/2026

Hoàn thành yêu cầu bổ sung cho [import V1](../plans/QUESTION-BANK-IMPORT-PLAN-2026-10-12.md).

## Thay đổi

- Thêm nút **Bảng cú pháp LaTeX** trong modal import. Mở/thu gọn ngay tại chỗ; không reset file, defaults hoặc preview.
- Có 12 mẫu: phân số, căn bậc hai/căn bậc n, lũy thừa, chỉ số dưới, tích phân, giới hạn, tổng, logarit, lượng giác, đạo hàm và hệ phương trình. Mỗi mẫu có source `$...$`, nút sao chép và KaTeX xem trước.
- Hướng dẫn ô Text, backslash, math inline và đáp án số/tọa độ. Nếu clipboard bị từ chối, hiển thị cách sao chép thủ công.
- Mẫu XLSX/CSV V1 và CSV legacy tải mới dùng tiêu đề tiếng Việt: `Độ khó`, `Nội dung câu hỏi`, `Mã chủ đề`, `Thời gian (giây)`… XLSX có nhãn tiếng Việt trên Topics/Guide, hướng dẫn từng cột và bảng LaTeX dùng chung với UI.
- Parser vẫn nhận mẫu tiếng Anh cũ. Metadata Việt/Anh của Guide được chuẩn hóa trước kiểm tra trùng, version và môn nguồn; không cho đổi nhãn để bỏ qua kiểm tra sai môn.
- Canonical DTO/JSON, enum values, tên các trang `Questions`/`Topics`/`Guide`, templateVersion 1.0, dropdown, định dạng Text và quy tắc Draft giữ nguyên. Guide giải thích các mã như `MultipleChoice`, `Manual`, `TRUE/FALSE` bằng tiếng Việt.
- Nhãn trường lỗi/defaults trên preview dùng cùng bảng tên cột tiếng Việt.

## Kiểm chứng

| Kiểm tra | Kết quả |
| --- | --- |
| Toàn bộ frontend tests | **760 pass, 0 fail** |
| TypeScript | Pass |
| Vite production build | Pass; cảnh báo chunk lớn hiện có, bundle budget vẫn đạt |
| Bundle regression | **4 pass, 0 fail** |
| Lint 5 source files thay đổi | Pass |
| Backend import/controller Release | **126 pass, 1 skip, 0 fail** |
| Parser + môn nguồn trong nhóm trên | **48 pass**, gồm file frontend thật, header Việt/Anh, metadata và duplicate aliases |
| Browser production frontend + Release API + MySQL tạm | Tải mẫu tiếng Việt → preview 3/3 hợp lệ → tạo 3 Draft thành công |

Một test skip là benchmark/transaction MySQL 500 câu cần biến môi trường riêng; không chạy lại benchmark đó cho thay đổi nhãn/hướng dẫn. Luồng browser dưới đây đã dùng MySQL thật và confirm Draft thực tế. Kết quả benchmark trước thuộc [báo cáo V1](QUESTION-IMPORT-V1-REPORT-2026-10-13.md).

Fixture `tests/EduTwin.BLL.Tests/CurriculumAndQuestions/Fixtures/Import/frontend-template-v1-vi.xlsx` do generator mới tạo trực tiếp, giữ cả fixture tiếng Anh cũ để kiểm tra tương thích. Test xác nhận đầy đủ 26 cột được map đúng và metadata môn nguồn không bị bỏ qua.

Logs local ignored: `scratch/import-tools/frontend-localized-final-20261015.log`, `build-localized-20261015.log`, `backend-localized-20261015.log`; TRX `scratch/import-tools/test-results/question-import-localized-20261015.trx`.

## Browser QA

- Dùng frontend riêng cổng 3302, API riêng cổng 5501 và MySQL riêng cổng 33307, tài khoản/dữ liệu seed tổng hợp.
- Mở bảng LaTeX: đủ source và rendered math; “Sao chép mã phân số” trả thông báo thành công.
- Tải workbook từ nút **Tải mẫu Excel V1 theo môn**; kiểm tra thực tế toàn bộ 26 header tiếng Việt, metadata Guide và bảng LaTeX trong file.
- Upload chính workbook đó; mở/thu gọn bảng hướng dẫn vẫn giữ file và khối 12.
- Preview: 3 câu hợp lệ, 0 lỗi/cảnh báo; các topic riêng `MATH-FUNCTIONS`, `MATH-EXP-LOG`, `MATH-ANTIDERIVATIVE`.
- Mở/thu gọn hướng dẫn sau preview giữ nguyên phiên và giờ hết hạn; confirm tạo Draft `20033`, `20034`, `20035`.

Ảnh local (ignored): [Bảng LaTeX](evidence/question-import-localized-20261015/latex-guide.jpg), [Import mẫu tiếng Việt thành công](evidence/question-import-localized-20261015/localized-import-result.jpg).

Không thêm package/migration, không commit/push. Dịch vụ QA tạm được dừng sau kiểm tra; stack Docker chính không bị rebuild/restart. Để dùng trên stack chính, build lại ứng dụng rồi tải mẫu Excel mới; file đã tải trước đó vẫn được nhận.
