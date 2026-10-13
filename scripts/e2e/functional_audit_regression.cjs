// Run only after acceptance_stack start. All writes target its validated disposable schema.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const crypto = require('node:crypto');
const { execSql } = require('./fixture_manager.cjs');
const { testPassword } = require('./acceptance_stack.cjs');
const base = 'http://127.0.0.1:5001/api/v1';
const subject = '30000000-0000-0000-0000-000000000003';
const classId = '51000000-0000-0000-0000-000000000010';
const results = [];
const record = (id, evidence = {}) => { results.push({ id, passed: true, ...evidence }); console.log(`PASS ${id}`); };
async function request(token, route, method = 'GET', body, origin = base) {
  const response = await fetch(origin + route, { method, signal: AbortSignal.timeout(30000), headers: { authorization: `Bearer ${token}`, ...(body instanceof FormData ? {} : { 'content-type': 'application/json' }) },
    ...(body === undefined ? {} : { body: body instanceof FormData ? body : JSON.stringify(body) }) });
  const text = await response.text();
  let data; try { data = text ? JSON.parse(text) : {}; } catch { data = { nonJson: true, length: text.length }; }
  return { status: response.status, body: data };
}
async function login(username) {
  const response = await fetch(base + '/auth/login', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ username, centerCode: 'EDUTWIN_A', password: testPassword }) });
  assert.equal(response.status, 200); return (await response.json()).data.accessToken;
}
const preview = (token, content, filename = 'audit.csv') => {
  const form = new FormData(); form.append('file', new Blob([content]), filename);
  return request(token, '/questions/import/preview', 'POST', form);
};
function crc32(buffer) { let crc = 0xffffffff; for (const b of buffer) { crc ^= b; for (let i = 0; i < 8; i++) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0); } return (crc ^ 0xffffffff) >>> 0; }
function zip(entries) {
  const locals = [], central = []; let offset = 0;
  for (const [name, text] of entries) {
    const filename = Buffer.from(name), bytes = Buffer.from(text), crc = crc32(bytes);
    const local = Buffer.alloc(30); local.writeUInt32LE(0x04034b50, 0); local.writeUInt16LE(20, 4); local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(bytes.length, 18); local.writeUInt32LE(bytes.length, 22); local.writeUInt16LE(filename.length, 26);
    const header = Buffer.alloc(46); header.writeUInt32LE(0x02014b50, 0); header.writeUInt16LE(20, 4); header.writeUInt16LE(20, 6); header.writeUInt32LE(crc, 16);
    header.writeUInt32LE(bytes.length, 20); header.writeUInt32LE(bytes.length, 24); header.writeUInt16LE(filename.length, 28); header.writeUInt32LE(offset, 42);
    locals.push(local, filename, bytes); central.push(header, filename); offset += local.length + filename.length + bytes.length;
  }
  const dir = Buffer.concat(central), end = Buffer.alloc(22); end.writeUInt32LE(0x06054b50, 0); end.writeUInt16LE(entries.length, 8); end.writeUInt16LE(entries.length, 10); end.writeUInt32LE(dir.length, 12); end.writeUInt32LE(offset, 16);
  return Buffer.concat([...locals, dir, end]);
}
function png() {
  const width = 1500, height = 1000, pixels = crypto.randomBytes((width * 3 + 1) * height);
  for (let row = 0; row < height; row++) pixels[row * (width * 3 + 1)] = 0;
  function chunk(type, data) { const name = Buffer.from(type), length = Buffer.alloc(4), crc = Buffer.alloc(4); length.writeUInt32BE(data.length); crc.writeUInt32BE(crc32(Buffer.concat([name, data]))); return Buffer.concat([length, name, data, crc]); }
  const ihdr = Buffer.alloc(13); ihdr.writeUInt32BE(width, 0); ihdr.writeUInt32BE(height, 4); ihdr[8] = 8; ihdr[9] = 2;
  return Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]), chunk('IHDR', ihdr), chunk('IDAT', zlib.deflateSync(pixels, { level: 0 })), chunk('IEND', Buffer.alloc(0))]);
}
async function run() {
  const teacher = await login('teacher.math'), student = await login('student01'), other = await login('student02'), manager = await login('manager');
  const csv = 'QuestionText,QuestionType,CorrectAnswer,Solution,MaxScore\nAudit valid,ShortAnswer,2,Explanation,10';
  const valid = await preview(teacher, csv); assert.equal(valid.status, 200); assert.equal(valid.body.data.validCount, 1);
  const confirm = { subjectId: subject, primaryTopicNodeId: '10000', previewToken: valid.body.data.previewToken };
  const imported = await request(teacher, '/questions/import/confirm', 'POST', confirm); assert.equal(imported.status, 200);
  assert.equal((await request(teacher, '/questions/import/confirm', 'POST', confirm)).status, 400); record('IMPORT_CONFIRM_AND_REPLAY');
  assert.equal((await preview(student, csv)).status, 403); assert.equal((await preview(manager, csv)).status, 403); record('IMPORT_ROLE_BOUNDARY');
  const multiline = await preview(teacher, 'QuestionText,QuestionType,CorrectAnswer,Solution\n"Line one\nLine two",ShortAnswer,2,Explanation');
  assert.equal(multiline.body.data.totalRows, 1); assert.equal(multiline.body.data.validCount, 1); record('CSV_MULTILINE', { totalRows: 1 });
  const vietnamese = await preview(teacher, 'Nội dung câu hỏi,Loại câu hỏi,Đáp án đúng,Lời giải\nQuestion,ShortAnswer,2,Explanation');
  assert.equal(vietnamese.body.data.validCount, 1); record('VIETNAMESE_HEADERS');
  const overflow = await preview(teacher, csv.replace(',10', ',1000')); assert.equal(overflow.body.data.validCount, 0); record('PREVIEW_DECIMAL_LIMIT');
  const bool = await preview(teacher, 'QuestionText,QuestionType,CorrectAnswer,Solution,ReasoningRequired\nQuestion,ShortAnswer,2,Explanation,tru');
  assert.equal(bool.body.data.validCount, 0); assert.equal(bool.body.data.invalidCount, 1); record('BOOLEAN_VALIDATION_AND_INVALID_ROW_COUNT');
  const ns = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main';
  const row = (index, values) => `<row r="${index}">${values.map((v,i) => `<c r="${String.fromCharCode(65+i)}${index}" t="inlineStr"><is><t>${v}</t></is></c>`).join('')}</row>`;
  const xlsx = zip([['xl/workbook.xml', `<workbook xmlns="${ns}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Questions" sheetId="9" r:id="r9"/></sheets></workbook>`],
    ['xl/_rels/workbook.xml.rels', '<Relationships><Relationship Id="r9" Target="worksheets/custom.xml"/></Relationships>'],
    ['xl/worksheets/custom.xml', `<worksheet xmlns="${ns}"><sheetData>${row(1,['QuestionText','QuestionType','CorrectAnswer','Solution'])}${row(2,['Question','ShortAnswer','2','Explanation'])}</sheetData></worksheet>`]]);
  assert.equal((await preview(teacher, xlsx, 'audit.xlsx')).body.data.validCount, 1); record('XLSX_INLINE_AND_WORKBOOK_RELATIONSHIP');
  const wordRow = values => `<w:tr>${values.map(v => `<w:tc><w:p><w:r><w:t>${v}</w:t></w:r></w:p></w:tc>`).join('')}</w:tr>`;
  const docx = zip([['word/document.xml', `<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:tbl>${wordRow(['Nội dung câu hỏi','Loại câu hỏi','Đáp án đúng','Lời giải'])}${wordRow(['Question','ShortAnswer','2','Explanation'])}</w:tbl></w:body></w:document>`]]);
  assert.equal((await preview(teacher, docx, 'audit.docx')).body.data.validCount, 1); record('DOCX_TABLE');
  const generate = { subjectId: subject, classId, targetWeeks: 2, daysPerWeek: 3, minutesPerDay: 30, targetMastery: 80, forceRegenerate: true };
  const generated = await request(student, '/students/me/learning-path/generate', 'POST', generate); assert.equal(generated.status, 200, JSON.stringify(generated.body));
  for (const [id, patch, status] of [['WEEKS',{targetWeeks:0},400],['ENUMS',{goalType:'INVALID'},400],['NOTE',{note:'a'.repeat(1001)},400],['SUBJECT',{subjectId:'99999999-0000-0000-0000-000000000000'},404]]) {
    const result = await request(student, '/students/me/learning-path/generate', 'POST', { ...generate, ...patch }); assert.equal(result.status, status); assert.ok(!result.body.nonJson); record(`PATH_${id}`, { status });
  }
  const sessions = generated.body.data.phases.flatMap(p => p.weeks).flatMap(w => w.sessions), first = sessions[0];
  const allIds=[...new Set(sessions.flatMap(s=>s.recommendedQuestionIds))];
  assert.ok(allIds.length>0); allIds.forEach(id=>assert.match(id,/^\d+$/));
  assert.equal(Number(execSql(`SELECT COUNT(*) FROM questions WHERE question_id IN (${allIds.join(',')}) AND (question_type='Essay' OR answer_evaluation_mode='Manual');`).trim()),0);
  record('SESSION_EXCLUDES_MANUAL_QUESTIONS');
  const route = session => `/students/me/learning-path/sessions/${session.sessionId}?subjectId=${subject}&classId=${classId}`;
  assert.equal((await request(student, route(first), 'PATCH', { status:'Completed', classId })).status, 400); record('NO_COMPLETION_WITHOUT_EVIDENCE');
  assert.equal((await request(other, route(first))).status, 404); record('SESSION_OWNER_BOUNDARY');
  const snapshot = await request(student, route(first)); assert.equal(snapshot.status, 200);
  assert.equal((await request(student, route(first), 'PATCH', { status:'InProgress', classId, expectedRowVersion:snapshot.body.data.rowVersion })).status, 204);
  const current = await request(student, route(first));
  const next = await request(student, `/learning/next-question?subjectId=${subject}&classId=${classId}&sessionId=${first.sessionId}`);
  assert.equal(next.status, 200); assert.ok(first.recommendedQuestionIds.includes(next.body.data.question.questionId)); record('SESSION_SELECTS_ASSIGNED_QUESTION');
  const competing = await Promise.all(sessions.slice(1,3).map(s => request(student,route(s),'PATCH',{status:'InProgress',classId,expectedRowVersion:current.body.data.rowVersion})));
  assert.deepEqual(competing.map(r=>r.status).sort(), [204,409]); record('SESSION_CONCURRENCY', { statuses:competing.map(r=>r.status) });
  assert.equal((await request(student,route(first),'PATCH',{status:'Completed',classId})).status,400);
  const submitted = [];
  for (const id of first.recommendedQuestionIds) {
    assert.match(id, /^\d+$/);
    const encoded = execSql(`SELECT HEX(COALESCE((SELECT CAST(option_id AS CHAR) FROM question_options WHERE question_id=q.question_id AND is_correct=1 ORDER BY order_index LIMIT 1), q.correct_answer)) FROM questions q WHERE question_id=${id};`).trim();
    const answer = Buffer.from(encoded, 'hex').toString('utf8');
    const attempt = await request(student, '/learning/attempts', 'POST', {
      clientSubmissionId:crypto.randomUUID(), questionId:id, classId, finalAnswer:answer,
      reasoningText:'Functional regression: use the given equation and simplify each step to obtain the answer.',
      confidence:80, timeSpentSeconds:20, answerChanges:1, skipped:false,
    });
    assert.equal(attempt.status,202,JSON.stringify(attempt.body));
    submitted.push(attempt.body.data.attemptId);
  }
  assert.equal((await request(student,route(first),'PATCH',{status:'Evaluate',classId})).status,204);
  assert.equal(Number(execSql(`SELECT COUNT(*) FROM attempts WHERE attempt_id IN (${submitted.join(',')}) AND is_correct IS NULL;`).trim()),0);
  assert.equal((await request(student,route(first))).body.data.status,'Completed');
  // Attempts/teacher grades can replace the current recommendation. The started
  // session and its checkpoint must remain durable on their original path.
  const originalPath=first.sessionId.slice(0,36); assert.match(originalPath,/^[0-9a-f-]{36}$/i);
  const phases=JSON.parse(Buffer.from(execSql(`SELECT HEX(CAST(plan_json AS CHAR)) FROM learning_paths WHERE learning_path_id='${originalPath}';`).trim(),'hex').toString('utf8'));
  const week = phases.flatMap(p=>p.weeks).find(w=>w.sessions.some(s=>s.sessionId===first.sessionId));
  assert.equal(week.checkpoint.completedTasks,week.sessions.filter(s=>s.status==='Completed').length);
  assert.ok(week.progressPercentage>0); record('SESSION_COMPLETION_AND_CHECKPOINT_AFTER_REAL_ATTEMPTS');
  const ordinary = await request(student, `/learning/next-question?subjectId=${subject}&classId=${classId}`);
  const qid = ordinary.body.data.question.questionId; assert.match(qid,/^\d+$/);
  execSql(`UPDATE questions SET status='Archived' WHERE question_id=${qid};`);
  try { const after = await request(student, `/learning/next-question?subjectId=${subject}&classId=${classId}`); assert.equal(after.status,200); assert.notEqual(after.body.data.question?.questionId,qid); record('ARCHIVED_RECOMMENDATION_RESELECTED'); }
  finally { execSql(`UPDATE questions SET status='Active' WHERE question_id=${qid};`); }
  execSql("UPDATE questions SET status='Draft' WHERE question_id=10003;");
  try { const after = await request(student,'/students/me/learning-path/generate','POST',generate); assert.equal(after.status,200);
    assert.ok(!after.body.data.phases.flatMap(p=>p.weeks).flatMap(w=>w.sessions).some(s=>s.recommendedQuestionIds.includes('10003'))); record('SCHEDULE_EXCLUDES_DRAFT'); }
  finally { execSql("UPDATE questions SET status='Active' WHERE question_id=10003;"); }
  const image = png(); assert.ok(image.length > 4*1024*1024 && image.length < 5*1024*1024);
  const form = new FormData(); form.append('file',new Blob([image],{type:'image/png'}),'audit.png');
  const upload = await request(student,'/learning/attempts/attachments/prepare-upload','POST',form,'http://127.0.0.1:3002/api/v1');
  assert.equal(upload.status,200,JSON.stringify(upload.body)); record('PNG_OVER_4MIB_THROUGH_NGINX', { bytes:image.length, status:upload.status });
}
run().catch(error => { results.push({ passed:false, error:error.message }); console.error(error.message); process.exitCode=1; }).finally(()=>{
  const evidence=path.resolve(__dirname,'../../scratch/functional-fixes-http-results.json'); fs.writeFileSync(evidence,JSON.stringify({results},null,2));
});
