"""Real Office/UTF-8 files for import acceptance, never production data."""
import csv,json,zipfile
from pathlib import Path
from openpyxl import Workbook,load_workbook
from docx import Document

target=Path(__file__).resolve().parents[2]/'docs/verification/import-fixtures-2026-10-12'
target.mkdir(parents=True,exist_ok=True)
headers=['Nội dung câu hỏi','Loại câu hỏi','Đáp án đúng','Lời giải','MaxScore','ReasoningRequired']
rows=[['Dòng thứ nhất\nDòng thứ hai','ShortAnswer','2','Có dấu "nháy"',10,'true'],
      ['Câu sai điểm','ShortAnswer','2','Giải thích',1000,'false'],
      ['Câu sai boolean','ShortAnswer','2','Giải thích',10,'tru']]
for name,encoding in [('mixed-utf8.csv','utf-8'),('mixed-utf8-bom.csv','utf-8-sig')]:
    with (target/name).open('w',encoding=encoding,newline='') as f:
        w=csv.writer(f,lineterminator='\n');w.writerow(headers);w.writerows(rows)
wb=Workbook();ws=wb.active;ws.title='Câu hỏi';ws.append(headers)
xrows=[r.copy() for r in rows];xrows[0][0]='XLSX dòng thứ nhất\nXLSX dòng thứ hai'
for row in xrows:ws.append(row)
wb.save(target/'mixed-inline.xlsx')
assert load_workbook(target/'mixed-inline.xlsx').active['A2'].value==xrows[0][0]
with zipfile.ZipFile(target/'mixed-inline.xlsx') as source,zipfile.ZipFile(target/'mixed-custom-sheet.xlsx','w') as dest:
    for item in source.infolist():
        data=source.read(item.filename)
        if item.filename.endswith('.rels') or item.filename=='[Content_Types].xml':data=data.replace(b'worksheets/sheet1.xml',b'worksheets/questions-custom.xml')
        dest.writestr(item.filename.replace('worksheets/sheet1.xml','worksheets/questions-custom.xml'),data)
doc=Document();table=doc.add_table(rows=1,cols=6);table.style='Table Grid'
for cell,value in zip(table.rows[0].cells,headers):cell.text=value
drows=[r.copy() for r in rows];drows[0][0]='DOCX dòng thứ nhất\nDOCX dòng thứ hai'
for row in drows:
    for cell,value in zip(table.add_row().cells,row):cell.text=str(value)
doc.save(target/'mixed-table.docx')
assert Document(target/'mixed-table.docx').tables[0].rows[1].cells[0].text==drows[0][0]
doc=Document();doc.add_paragraph('Không có bảng câu hỏi hợp lệ.');doc.save(target/'no-table.docx')
(target/'malformed.csv').write_text('QuestionText,QuestionType,CorrectAnswer\n"Chưa đóng quote,ShortAnswer,2',encoding='utf-8')
(target/'wrong-headers.csv').write_text('Foo,Bar\nKhông phải câu hỏi,X',encoding='utf-8')
(target/'corrupt.xlsx').write_bytes(b'not a ZIP workbook')
(target/'empty.csv').write_bytes(b'')
with (target/'scores.csv').open('w',encoding='utf-8',newline='') as f:
    w=csv.writer(f,lineterminator='\n');w.writerow(headers)
    for score in [0,-1,1.001,1000,0.01,999.99]:w.writerow([f'Điểm biên {score}','ShortAnswer','2','Giải thích',score,'true'])
manifest={'csv':rows[0][0],'xlsx':xrows[0][0],'docx':drows[0][0],'headers':headers}
(target/'fixture-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Created and reopened CSV/XLSX/DOCX fixtures: {target}')
