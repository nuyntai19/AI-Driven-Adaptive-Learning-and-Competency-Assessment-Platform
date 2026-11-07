# Bộ file thử import F10–F16

- `mixed-utf8.csv`: đúng mẫu người dùng, CSV UTF-8 không BOM; 3 hàng, 1 hợp lệ và 2 lỗi.
- `mixed-utf8-bom.csv`: cùng dữ liệu, có BOM.
- `mixed-inline.xlsx`: workbook Excel thật, có text xuống dòng; 3/1/2.
- `mixed-custom-sheet.xlsx`: workbook với đường dẫn worksheet đổi tên; 3/1/2.
- `mixed-table.docx`: Word thật dạng bảng; ô câu hỏi có xuống dòng bằng Word line break; 3/1/2.
- `scores.csv`: 6 hàng điểm 0, -1, 1.001, 1000, 0.01, 999.99; 2 hợp lệ/4 lỗi.
- `malformed.csv`, `corrupt.xlsx`, `empty.csv`, `no-table.docx`: file âm tính, phải bị từ chối.
- `wrong-headers.csv`: thiếu cột đúng; có lỗi preview, không có hàng hợp lệ và không confirm được.

Chọn môn/chủ đề Active và chỉ confirm hàng hợp lệ. Confirm lại cùng preview phải bị chặn. Upload mới cùng nội dung là một preview khác; hệ thống chưa deduplicate theo nội dung/file hash.

Các file không chứa dữ liệu người dùng thật. Dùng stack kiểm thử ở 3002 hoặc dữ liệu thử của bạn. Không upload file ZIP tổng hợp trực tiếp; giải nén rồi chọn từng file CSV/XLSX/DOCX.
