import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { questionsApi } from "../../api/questionsApi";
import { organizationApi } from "../../api/organizationApi";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import { useAuthStore } from "../../stores/authStore";
import type { QuestionImportConfirmDataDto, QuestionImportItemDto, QuestionImportPreviewDataDto, QuestionImportPreviewRequest, QuestionImportRowErrorDto } from "../../types/questions";
import { RichMathText } from "../math/RichMathText";
import { TeacherModal } from "./TeacherOverlays";
import { MATH_EQUIVALENT_HELP } from "../../utils/questionEvaluationModes";
import { createQuestionImportCsvTemplate, QUESTION_IMPORT_LEGACY_CSV_TEMPLATE } from "../../utils/questionImportTemplate";
import { buildImportConfirmRequest, importConfirmationError, questionImportFileError, validateImportQuestionMath } from "../../utils/questionImportValidation";
import "./teacherDesignSystem.css";

interface QuestionImportModalProps { isOpen: boolean; onClose: () => void; onSuccess: () => void; }
const inputClass = "th-select w-full rounded-lg border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-800 p-2.5 text-xs text-slate-900 dark:text-slate-100 disabled:opacity-50";
const buttonClass = "th-button-secondary rounded-lg border border-slate-300 dark:border-slate-600 px-3 py-2 text-xs font-semibold disabled:opacity-50 disabled:cursor-not-allowed";
const defaults: QuestionImportPreviewRequest = { subjectId: "", templateVersion: "1.0", legacyMode: false, difficulty: 3, maxScore: 10, estimatedTimeSeconds: 120, reasoningRequired: false, visibility: "Private", languageCode: "vi", shortAnswerEvaluationMode: "Manual" };

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="block space-y-1.5 text-xs text-slate-700 dark:text-slate-300"><span className="font-semibold">{label}</span>{children}</label>;
}

function QuestionDetail({ question: q, blocked }: { question: QuestionImportItemDto; blocked: boolean }) {
  const content = (label: string, text?: string | null) => <div className="space-y-1"><strong className="text-xs text-slate-600 dark:text-slate-400">{label}</strong><div className="whitespace-pre-wrap break-words text-sm">{blocked ? text || "—" : <RichMathText text={text || "—"} />}</div></div>;
  return <div className="space-y-4 border-t border-slate-200 dark:border-slate-700 p-4">
    <div className="grid grid-cols-2 md:grid-cols-4 gap-2 text-xs">
      <span>Loại: {q.questionType}</span><span>Khối: {q.gradeLevel}</span><span>Độ khó: {q.difficulty}/5</span><span>Điểm tối đa: {q.maxScore}</span>
      <span>Thời gian: {q.estimatedTimeSeconds} giây</span><span>Giải trình: {q.reasoningRequired ? "Bắt buộc" : "Không bắt buộc"}</span><span>Phạm vi: {q.visibility}</span><span>Ngôn ngữ: {q.languageCode}</span>
      <span className="col-span-2">Topic: {q.topicCode} — {q.topicName} (ID {q.primaryTopicNodeId})</span><span className="col-span-2">Chế độ chấm: {q.answerEvaluationMode}</span>
    </div>
    <p className="text-xs text-slate-500 dark:text-slate-400">Mặc định đã áp: {q.defaultedFields.length ? q.defaultedFields.join(", ") : "Không có; file khai báo đủ."}</p>
    {content("Đề bài", q.questionText)}
    {q.options.map((option) => <div key={option.optionLabel} className={`rounded-lg border p-3 ${option.isCorrect ? "border-emerald-500/50" : "border-slate-200 dark:border-slate-700"}`}>
      {content(`Phương án ${option.optionLabel}${option.isCorrect ? " • Đáp án đúng" : ""}`, option.optionText)}
      {option.misconception && content("Quan niệm sai", option.misconception)}
    </div>)}
    {content("Đáp án chuẩn / đáp án mẫu", q.correctAnswer)}{content("Lời giải", q.solution)}{content("Tư duy mong đợi", q.expectedReasoning)}{content("Ghi chú chấm", q.scoringNotes)}
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
      <div><strong className="text-xs">Ý cần đạt</strong>{q.requiredIdeas.length ? q.requiredIdeas.map((value, index) => <div key={index}>{content(`${index + 1}.`, value)}</div>) : <p className="text-xs">—</p>}</div>
      <div><strong className="text-xs">Lỗi thường gặp</strong>{q.commonErrors.length ? q.commonErrors.map((value, index) => <div key={index}>{content(`${index + 1}.`, value)}</div>) : <p className="text-xs">—</p>}</div>
    </div>
    <details className="text-xs"><summary className="cursor-pointer text-teal-600 dark:text-teal-400">Xem dữ liệu nguồn và metadata sẽ lưu</summary><pre className="mt-2 overflow-x-auto whitespace-pre-wrap break-words">{JSON.stringify(q, null, 2)}</pre></details>
  </div>;
}

function Diagnostics({ title, errors, warning = false }: { title: string; errors: readonly QuestionImportRowErrorDto[]; warning?: boolean }) {
  if (!errors.length) return null;
  return <div className={`max-h-64 space-y-2 overflow-y-auto rounded-xl border p-4 text-xs ${warning ? "border-amber-400/40 bg-amber-50 dark:bg-amber-950/20" : "border-rose-500/30 bg-rose-50 dark:bg-rose-950/20"}`} role={warning ? undefined : "alert"}>
    <h4 className="font-bold">{title} ({errors.length})</h4>
    {errors.map((error, index) => <div key={index} className="whitespace-pre-wrap break-words"><strong>{error.sheetName || "File"}{error.rowIndex > 0 ? ` • dòng ${error.rowIndex}` : ""}{error.questionKey ? ` • ${error.questionKey}` : ""} • {error.columnName || error.field}</strong>: {error.errorMessage}{error.rawValue && <code className="ml-1">({error.rawValue})</code>}</div>)}
  </div>;
}
function errorMessage(error: unknown, fallback: string): string {
  const value = error as { response?: { data?: { detail?: string; message?: string } }; message?: string };
  return value.response?.data?.detail || value.response?.data?.message || value.message || fallback;
}

export function QuestionImportModal({ isOpen, onClose, onSuccess }: QuestionImportModalProps) {
  const actor = useAuthStore((state) => state.user);
  const canImport = actor?.accountType === "Teacher";
  const fileInput = useRef<HTMLInputElement>(null);
  const generation = useRef(0);
  const [settings, setSettings] = useState<QuestionImportPreviewRequest>({ ...defaults });
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<QuestionImportPreviewDataDto | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [confirmError, setConfirmError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [dragOver, setDragOver] = useState(false);
  const [explicitSubset, setExplicitSubset] = useState(false);
  const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
  const [expandedKeys, setExpandedKeys] = useState<string[]>([]);
  const [result, setResult] = useState<QuestionImportConfirmDataDto | null>(null);
  const { data: subjects, isLoading: subjectsLoading, error: subjectsError } = useQuery({ queryKey: ["subjects", "for-question-import", actor?.centerId], queryFn: () => organizationApi.listSubjects(true), enabled: isOpen && canImport });
  const { data: graph, isLoading: topicsLoading, error: topicsError } = useQuery({ queryKey: ["topics", "for-question-import", actor?.centerId, settings.subjectId], queryFn: () => knowledgeGraphApi.getGraph(settings.subjectId), enabled: isOpen && canImport && Boolean(settings.subjectId) });
  const topics = graph?.nodes.filter((node) => node.isActive && node.nodeType === "Topic") ?? [];
  const clientErrors = useMemo(() => preview?.validQuestions.flatMap(validateImportQuestionMath) ?? [], [preview]);
  const blockedKeys = new Set(clientErrors.map((error) => error.questionKey));
  const readyQuestions = preview?.validQuestions.filter((question) => !blockedKeys.has(question.questionKey)) ?? [];
  const keysToImport = explicitSubset ? selectedKeys : readyQuestions.map((question) => question.questionKey);
  const confirmationError = preview ? importConfirmationError(preview, settings.subjectId, keysToImport, explicitSubset, clientErrors) : null;
  const skippedCount = preview ? Math.max(0, preview.totalRows - keysToImport.length) : 0;
  const invalidate = useCallback(() => {
    generation.current++;
    setPreview(null); setLoading(false); setPreviewError(null); setConfirmError(null); setResult(null);
    setExplicitSubset(false); setSelectedKeys([]); setExpandedKeys([]);
  }, []);
  useEffect(() => { invalidate(); }, [isOpen, actor?.userId, actor?.centerId, invalidate]);
  function changeSettings(value: Partial<QuestionImportPreviewRequest>) { invalidate(); setSettings((previous) => ({ ...previous, ...value })); }
  function chooseFile(next: File | null) {
    invalidate(); setFile(null);
    if (!next) return;
    const issue = questionImportFileError(next);
    if (issue) { setPreviewError(issue); if (fileInput.current) fileInput.current.value = ""; return; }
    setFile(next);
  }
  function resetFile() { chooseFile(null); if (fileInput.current) fileInput.current.value = ""; }
  function close() { if (!confirming) { resetFile(); onClose(); } }
  async function uploadPreview() {
    if (!file || !settings.subjectId || (settings.legacyMode && !settings.defaultTopicNodeId)) return;
    const current = ++generation.current;
    setLoading(true); setPreviewError(null); setConfirmError(null); setPreview(null);
    try {
      const response = await questionsApi.previewImport(file, settings);
      if (generation.current !== current) return;
      if (response.data.subjectId !== settings.subjectId) throw new Error("Môn của phiên xem trước không trùng môn đã chọn. Vui lòng kiểm tra lại.");
      setPreview(response.data); setSelectedKeys([]); setExplicitSubset(false);
    } catch (error) { if (generation.current === current) setPreviewError(errorMessage(error, "Không thể đọc file. Kiểm tra cấu trúc và tải lại.")); }
    finally { if (generation.current === current) setLoading(false); }
  }
  async function confirmImport() {
    if (!preview || confirming) return;
    const issue = importConfirmationError(preview, settings.subjectId, keysToImport, explicitSubset, clientErrors);
    if (issue) { setConfirmError(issue); return; }
    setConfirming(true); setConfirmError(null);
    try {
      const response = await questionsApi.confirmImport(buildImportConfirmRequest(preview, keysToImport, explicitSubset));
      setResult(response.data); onSuccess();
    } catch (error) { setConfirmError(errorMessage(error, "Không thể xác nhận lô. Kiểm tra trạng thái trước khi thử lại.")); }
    finally { setConfirming(false); }
  }
  function saveDownload(data: BlobPart, name: string, type: string) {
    const url = URL.createObjectURL(new Blob([data], { type }));
    const link = document.createElement("a"); link.href = url; link.download = name; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  async function downloadXlsx() {
    const subject = subjects?.data.find((item) => item.subjectId === settings.subjectId);
    if (!subject) return;
    setDownloading(true); setPreviewError(null);
    try {
      const { createQuestionImportWorkbook } = await import("../../utils/questionImportWorkbook");
      const bytes = await createQuestionImportWorkbook(subject, topics);
      saveDownload(bytes, `EduTwin_Questions_${subject.subjectCode}_V1.xlsx`, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    } catch (error) { setPreviewError(errorMessage(error, "Không thể tạo mẫu Excel.")); }
    finally { setDownloading(false); }
  }
  return <TeacherModal isOpen={isOpen} title="Nhập câu hỏi từ Excel / CSV" description="Chọn môn, khai báo chủ đề theo từng câu, kiểm tra đầy đủ và lưu vào ngân hàng ở trạng thái Draft." onClose={close} maxWidth="max-w-6xl">
    {!canImport ? <p className="text-sm">Chức năng import hiện dành cho tài khoản giáo viên theo quyền tạo câu hỏi.</p> : <div className="space-y-5 text-slate-900 dark:text-slate-100">
      {!result && <fieldset disabled={confirming} className="space-y-4 rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40 p-4">
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <Field label="Môn học đích *"><select className={inputClass} value={settings.subjectId} disabled={subjectsLoading} onChange={(event) => changeSettings({ subjectId: event.target.value, defaultTopicNodeId: undefined })}><option value="">Chọn môn học trước khi kiểm tra file</option>{subjects?.data.map((subject) => <option key={subject.subjectId} value={subject.subjectId}>{subject.subjectName} ({subject.subjectCode})</option>)}</select></Field>
          <Field label="Khối mặc định"><select className={inputClass} value={settings.gradeLevel ?? ""} onChange={(event) => changeSettings({ gradeLevel: event.target.value ? Number(event.target.value) : undefined })}><option value="">Dùng GradeLevel từng dòng trong file</option>{[10, 11, 12].map((grade) => <option key={grade} value={grade}>Khối {grade}</option>)}</select></Field>
        </div>
        <p className="text-xs text-slate-500 dark:text-slate-400">Mỗi câu dùng TopicCode riêng thuộc môn đã chọn. Giá trị từng dòng ưu tiên; các ô trống được áp mặc định bên dưới.</p>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <Field label="Độ khó mặc định"><select className={inputClass} value={settings.difficulty} onChange={(event) => changeSettings({ difficulty: Number(event.target.value) })}>{[1, 2, 3, 4, 5].map((level) => <option key={level} value={level}>{level}/5</option>)}</select></Field>
          <Field label="Điểm tối đa mặc định"><input type="number" min="0.01" max="999.99" step="0.01" className={inputClass} value={settings.maxScore} onChange={(event) => changeSettings({ maxScore: Number(event.target.value) })} /></Field>
          <Field label="Thời gian mặc định (giây)"><input type="number" min="1" max="86400" step="1" className={inputClass} value={settings.estimatedTimeSeconds} onChange={(event) => changeSettings({ estimatedTimeSeconds: Number(event.target.value) })} /></Field>
          <Field label="Phạm vi mặc định"><select className={inputClass} value={settings.visibility} onChange={(event) => changeSettings({ visibility: event.target.value as "Private" | "Shared" })}><option value="Private">Private — riêng giáo viên</option><option value="Shared">Shared — dùng chung khi kích hoạt</option></select></Field>
          <Field label="Ngôn ngữ mặc định"><select className={inputClass} value={settings.languageCode} onChange={(event) => changeSettings({ languageCode: event.target.value })}><option value="vi">Tiếng Việt (vi)</option><option value="en">English (en)</option></select></Field>
          <Field label="Chấm trả lời ngắn mặc định"><select className={inputClass} value={settings.shortAnswerEvaluationMode} onChange={(event) => changeSettings({ shortAnswerEvaluationMode: event.target.value as QuestionImportPreviewRequest["shortAnswerEvaluationMode"] })}>{["Manual", "TextExact", "NumericRational", "Coordinate2D", "MathEquivalent"].map((mode) => <option key={mode} value={mode}>{mode}</option>)}</select></Field>
          <label className="col-span-2 flex items-center gap-2 text-xs"><input type="checkbox" checked={settings.reasoningRequired} onChange={(event) => changeSettings({ reasoningRequired: event.target.checked })} />Mặc định yêu cầu học sinh trình bày lời giải</label>
        </div>
        <label className="flex items-center gap-2 text-xs"><input type="checkbox" checked={settings.legacyMode} onChange={(event) => changeSettings({ legacyMode: event.target.checked, defaultTopicNodeId: undefined })} />Dùng file legacy với một chủ đề chung (không có TopicCode)</label>
        {settings.legacyMode && <Field label="Chủ đề chung cho file legacy *"><select className={inputClass} disabled={!settings.subjectId || topicsLoading} value={settings.defaultTopicNodeId ?? ""} onChange={(event) => changeSettings({ defaultTopicNodeId: event.target.value || undefined })}><option value="">Chọn chủ đề chung</option>{topics.map((topic) => <option key={topic.nodeId} value={topic.nodeId}>{topic.nodeCode} — {topic.nodeName}</option>)}</select></Field>}
      </fieldset>}
      {(subjectsError || topicsError) && <p role="alert" className="text-xs text-rose-600 dark:text-rose-400">Không tải được danh mục môn/chủ đề. Kiểm tra quyền truy cập và mở lại.</p>}
      {!preview && !result && <div className="space-y-3">
        <div role="button" tabIndex={0} aria-label="Chọn hoặc kéo thả file Excel / CSV" onClick={() => !loading && fileInput.current?.click()} onKeyDown={(event) => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); fileInput.current?.click(); } }} onDragOver={(event) => { event.preventDefault(); setDragOver(true); }} onDragLeave={() => setDragOver(false)} onDrop={(event) => { event.preventDefault(); setDragOver(false); if (event.dataTransfer.files.length !== 1) { invalidate(); setFile(null); setPreviewError("Mỗi lô chỉ nhận một file."); return; } chooseFile(event.dataTransfer.files[0]); }} className={`rounded-2xl border-2 border-dashed p-8 text-center cursor-pointer ${dragOver ? "border-teal-500 bg-teal-500/10" : "border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-800/40"}`}>
          <input type="file" ref={fileInput} accept=".xlsx,.csv" className="hidden" onChange={(event) => chooseFile(event.target.files?.[0] ?? null)} />
          <p className="text-sm font-semibold">{file ? file.name : "Nhấn để chọn file hoặc kéo thả file vào đây"}</p><p className="mt-2 text-xs text-slate-500 dark:text-slate-400">XLSX hoặc CSV UTF-8 • tối đa 10 MiB, 500 câu. Công thức là văn bản LaTeX $...$ trong ô Text; ảnh/Equation và Excel formula chưa được hỗ trợ.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button className={buttonClass} disabled={!settings.subjectId || topicsLoading || !!topicsError || downloading} onClick={downloadXlsx}>{downloading ? "Đang tạo mẫu..." : "Tải mẫu Excel V1 theo môn"}</button>
          <button className={buttonClass} disabled={!settings.subjectId || topicsLoading || !!topicsError} onClick={() => saveDownload("\uFEFF" + createQuestionImportCsvTemplate(topics), "EduTwin_Questions_V1.csv", "text/csv;charset=utf-8")}>Tải CSV V1</button>
          {settings.legacyMode && <button className={buttonClass} onClick={() => saveDownload("\uFEFF" + QUESTION_IMPORT_LEGACY_CSV_TEMPLATE, "EduTwin_Questions_Legacy.csv", "text/csv;charset=utf-8")}>Tải CSV legacy</button>}
          <button className="th-button-primary ml-auto rounded-lg px-4 py-2 text-xs disabled:opacity-50" disabled={!file || !settings.subjectId || loading || (settings.legacyMode && !settings.defaultTopicNodeId)} onClick={uploadPreview}>{loading ? "Đang kiểm tra file..." : "Kiểm tra & xem trước"}</button>
        </div>
      </div>}
      {previewError && <p role="alert" className="rounded-lg bg-rose-50 dark:bg-rose-950/20 p-3 text-xs text-rose-700 dark:text-rose-300">{previewError}</p>}
      {preview && !result && <div className="space-y-4">
        <div className="flex flex-wrap items-center gap-4 rounded-xl bg-slate-50 dark:bg-slate-800/40 p-4 text-xs"><strong>Tổng {preview.totalRows} câu</strong><span className="text-emerald-600 dark:text-emerald-400">Đủ điều kiện: {readyQuestions.length}</span><span className="text-rose-600 dark:text-rose-400">Câu lỗi: {preview.invalidCount + blockedKeys.size}</span><span>Lỗi: {preview.errorCount + clientErrors.length} • Cảnh báo: {preview.warningCount}</span><button disabled={confirming} className={`${buttonClass} ml-auto`} onClick={resetFile}>Sửa file / tải lại</button></div>
        <p className="text-xs text-slate-500 dark:text-slate-400">Mẫu {preview.templateVersion} • phiên {preview.revision} • hết hạn {new Date(preview.expiresAt).toLocaleString("vi-VN", { timeZone: "Asia/Bangkok" })}. Công thức được kiểm tra cú pháp trên trình duyệt; tất cả câu nhập sẽ là Draft để rà soát.</p>
        <Diagnostics title="Lỗi trong file" errors={preview.errors} /><Diagnostics title="Lỗi công thức phát hiện trên trình duyệt" errors={clientErrors} /><Diagnostics title="Cảnh báo cần rà soát" errors={preview.warnings} warning />
        <p className="text-xs text-slate-500 dark:text-slate-400">TextExact so khớp văn bản. MathEquivalent: {MATH_EQUIVALENT_HELP}</p>
        <label className="flex items-center gap-2 rounded-lg border border-teal-500/30 p-3 text-xs"><input type="checkbox" disabled={confirming} checked={explicitSubset} onChange={(event) => { setExplicitSubset(event.target.checked); setSelectedKeys([]); setConfirmError(null); }} />Chủ động nhập các câu hợp lệ được chọn; các câu còn lại sẽ bỏ qua trong lô này.</label>
        {explicitSubset && <div className="flex gap-2"><button className={buttonClass} disabled={confirming} onClick={() => setSelectedKeys(readyQuestions.map((question) => question.questionKey))}>Chọn tất cả câu hợp lệ ({readyQuestions.length})</button><button className={buttonClass} disabled={confirming} onClick={() => setSelectedKeys([])}>Bỏ chọn</button></div>}
        <div className="max-h-[32rem] overflow-y-auto space-y-3">{preview.validQuestions.map((question) => {
          const blocked = blockedKeys.has(question.questionKey);
          return <div key={question.questionKey} className={`rounded-xl border bg-white dark:bg-slate-900 ${blocked ? "border-rose-500/40" : "border-slate-200 dark:border-slate-700"}`}>
            {explicitSubset && <label className="flex items-center gap-2 px-4 pt-3 text-xs"><input type="checkbox" aria-label={`Chọn ${question.questionKey}`} disabled={blocked || confirming} checked={selectedKeys.includes(question.questionKey)} onChange={(event) => setSelectedKeys((keys) => event.target.checked ? [...keys, question.questionKey] : keys.filter((key) => key !== question.questionKey))} />{blocked ? "Có lỗi — chưa thể nhập" : `Chọn ${question.questionKey}`}</label>}
            <details onToggle={(event) => { const open = event.currentTarget.open; setExpandedKeys((keys) => open ? keys.includes(question.questionKey) ? keys : [...keys, question.questionKey] : keys.filter((key) => key !== question.questionKey)); }}><summary className="cursor-pointer p-4 text-xs"><strong>{question.sheetName} • dòng {question.rowIndex} • {question.questionKey}</strong> — {question.topicCode} — {question.questionType}{blocked ? " • Lỗi công thức" : ""}<p className="mt-2 whitespace-pre-wrap break-words">{question.questionText}</p><span className="mt-2 inline-block text-teal-600 dark:text-teal-400">Mở toàn bộ đề, đáp án, lời giải và tiêu chí chấm</span></summary>{expandedKeys.includes(question.questionKey) && <QuestionDetail question={question} blocked={blocked} />}</details>
          </div>;
        })}</div>
        {(confirmError || confirmationError) && <p role="alert" className="text-xs text-rose-600 dark:text-rose-400">{confirmError || confirmationError}</p>}
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 dark:border-slate-700 pt-4"><p className="text-xs">Lưu <strong>{keysToImport.length} câu Draft</strong>{explicitSubset ? `; bỏ qua ${skippedCount} câu (gồm câu lỗi và chưa chọn).` : "; sau đó rà soát và kích hoạt."}</p><button className="th-button-primary rounded-lg px-5 py-2.5 text-xs disabled:opacity-50 disabled:cursor-not-allowed" disabled={confirming || !!confirmationError} onClick={confirmImport}>{confirming ? "Đang lưu lô..." : `Xác nhận lưu Draft (${keysToImport.length})`}</button></div>
      </div>}
      {result && <div className="space-y-4 rounded-xl border border-emerald-500/30 bg-emerald-50 dark:bg-emerald-950/20 p-8 text-center"><h3 className="font-bold">Đã nhập {result.importedCount} câu hỏi Draft</h3><p className="text-xs">{result.skippedCount > 0 ? `Đã bỏ qua ${result.skippedCount} câu trong lô theo lựa chọn của bạn. ` : ""}Mở từng câu để rà soát trước khi kích hoạt. Có thể thêm ảnh và rubric nâng cao bằng editor.</p><div className="flex flex-wrap justify-center gap-2">{result.questionIds.slice(0, 10).map((id) => <Link key={id} className="text-xs text-teal-700 dark:text-teal-300 underline" to={`/giao-vien/cau-hoi/${id}`}>Mở câu #{id}</Link>)}</div><button className="th-button-primary rounded-lg px-5 py-2 text-xs" onClick={close}>Hoàn tất & xem ngân hàng</button></div>}
    </div>}
  </TeacherModal>;
}
