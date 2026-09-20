import { useState, useRef, type ChangeEvent } from "react";
import { useQuery } from "@tanstack/react-query";
import { questionsApi } from "../../api/questionsApi";
import { organizationApi } from "../../api/organizationApi";
import { knowledgeGraphApi } from "../../api/knowledgeGraphApi";
import type { QuestionImportPreviewDataDto } from "../../types/questions";
import { TeacherModal } from "./TeacherOverlays";

interface QuestionImportModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export function QuestionImportModal({ isOpen, onClose, onSuccess }: QuestionImportModalProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [selectedSubjectId, setSelectedSubjectId] = useState("");
  const [selectedTopicNodeId, setSelectedTopicNodeId] = useState("");

  const [isLoadingPreview, setIsLoadingPreview] = useState(false);
  const [previewData, setPreviewData] = useState<QuestionImportPreviewDataDto | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);

  const [isConfirming, setIsConfirming] = useState(false);
  const [confirmError, setConfirmError] = useState<string | null>(null);
  const [importResult, setImportResult] = useState<{ importedCount: number; message: string } | null>(null);

  const [showErrorsOnly, setShowErrorsOnly] = useState(false);

  // Load active subjects
  const { data: subjectsData, isLoading: isLoadingSubjects } = useQuery({
    queryKey: ["subjects", "for-question-import"],
    queryFn: () => organizationApi.listSubjects(true),
    enabled: isOpen,
  });

  // Load knowledge nodes for subject
  const { data: topicsData, isLoading: isLoadingTopics } = useQuery({
    queryKey: ["topics", "for-question-import", selectedSubjectId],
    queryFn: () => (selectedSubjectId ? knowledgeGraphApi.getGraph(selectedSubjectId) : null),
    enabled: isOpen && Boolean(selectedSubjectId),
  });

  const handleFileChange = (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setSelectedFile(file);
      setPreviewData(null);
      setPreviewError(null);
      setConfirmError(null);
      setImportResult(null);
    }
  };

  const handleUploadAndPreview = async () => {
    if (!selectedFile) {
      setPreviewError("Vui lòng chọn một file Excel (.xlsx) hoặc CSV (.csv).");
      return;
    }

    try {
      setIsLoadingPreview(true);
      setPreviewError(null);
      const res = await questionsApi.previewImport(selectedFile);
      setPreviewData(res.data);
    } catch (err: any) {
      const msg =
        err?.response?.data?.detail ||
        err?.response?.data?.message ||
        err?.message ||
        "Không thể đọc file hoặc cấu trúc file không hợp lệ.";
      setPreviewError(msg);
    } finally {
      setIsLoadingPreview(false);
    }
  };

  const handleConfirmImport = async () => {
    if (!previewData) return;
    if (!selectedSubjectId) {
      setConfirmError("Vui lòng chọn môn học đích cho các câu hỏi nhập vào.");
      return;
    }
    if (!selectedTopicNodeId) {
      setConfirmError("Vui lòng chọn chủ đề Cây Tri thức (Topic Node) cho các câu hỏi.");
      return;
    }

    try {
      setIsConfirming(true);
      setConfirmError(null);
      const res = await questionsApi.confirmImport({
        previewToken: previewData.previewToken,
        subjectId: selectedSubjectId,
        primaryTopicNodeId: selectedTopicNodeId,
        questions: previewData.validQuestions,
      });

      setImportResult(res.data);
      onSuccess();
    } catch (err: any) {
      const msg =
        err?.response?.data?.detail ||
        err?.response?.data?.message ||
        err?.message ||
        "Lỗi trong quá trình lưu dữ liệu câu hỏi.";
      setConfirmError(msg);
    } finally {
      setIsConfirming(false);
    }
  };

  const handleReset = () => {
    setSelectedFile(null);
    setPreviewData(null);
    setPreviewError(null);
    setConfirmError(null);
    setImportResult(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  const downloadCsvTemplate = () => {
    const csvContent =
      "QuestionType,Difficulty,QuestionText,OptionA,MisconceptionA,OptionB,MisconceptionB,OptionC,MisconceptionC,OptionD,MisconceptionD,CorrectAnswer,Solution,ExpectedReasoning,MaxScore,EstimatedTimeSeconds,ReasoningRequired\n" +
      'MultipleChoice,3,"Cho hàm số $y=x^2$. Tính $y\'(2)$.","2","Nhầm đạo hàm của hằng số","4","","8","Nhầm mũ thành nhân","0","Nhầm cực trị","B","Ta có $y\'=2x$, thay $x=2$ được $y\'(2)=4$.","Tính đạo hàm cơ bản và thế số",10,120,TRUE\n' +
      'ShortAnswer,2,"Giải phương trình $2x - 6 = 0$.","","","","","","","","","3","Ta có $2x=6 \Rightarrow x=3$.","Chuyển vế đổi dấu",10,60,TRUE\n';

    const blob = new Blob(["\uFEFF" + csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.setAttribute("download", "EduTwin_Question_Import_Template.csv");
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <TeacherModal
      isOpen={isOpen}
      title="Nhập Câu Hỏi Từ File (Excel / CSV)"
      description="Tải lên danh sách câu hỏi hàng loạt, kiểm tra tính hợp lệ trước khi đưa vào ngân hàng câu hỏi."
      onClose={onClose}
      maxWidth="max-w-4xl"
    >
      <div className="space-y-6">
        {/* Step 1: Destination Selection */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 p-4 rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)]">
          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Môn học đích <span className="text-rose-400">*</span>
            </label>
            <select
              value={selectedSubjectId}
              onChange={(e) => {
                setSelectedSubjectId(e.target.value);
                setSelectedTopicNodeId("");
              }}
              className="th-select w-full text-xs"
              disabled={isLoadingSubjects}
            >
              <option value="">-- Chọn môn học --</option>
              {subjectsData?.data?.map((sub) => (
                <option key={sub.subjectId} value={sub.subjectId}>
                  {sub.subjectName} ({sub.subjectCode})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-semibold uppercase tracking-wider text-[var(--th-text-muted)] mb-1.5">
              Chủ đề Cây Tri Thức (Topic Node) <span className="text-rose-400">*</span>
            </label>
            <select
              value={selectedTopicNodeId}
              onChange={(e) => setSelectedTopicNodeId(e.target.value)}
              className="th-select w-full text-xs"
              disabled={!selectedSubjectId || isLoadingTopics}
            >
              <option value="">
                {!selectedSubjectId
                  ? "-- Vui lòng chọn môn học trước --"
                  : isLoadingTopics
                  ? "Đang tải danh sách chủ đề..."
                  : "-- Chọn chủ đề cây tri thức --"}
              </option>
              {topicsData?.nodes?.map((node: any) => (
                <option key={node.id} value={node.id}>
                  {node.label || node.name || `Node ${node.id}`}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Step 2: File Upload */}
        {!previewData && !importResult && (
          <div className="space-y-4">
            <div
              onClick={() => fileInputRef.current?.click()}
              className="border-2 border-dashed border-[var(--th-border-subtle)] hover:border-[var(--th-teal)] rounded-2xl p-8 text-center cursor-pointer transition-colors bg-[var(--th-surface-subtle)]/40 hover:bg-[var(--th-surface-subtle)]"
            >
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx,.csv,.tsv"
                onChange={handleFileChange}
                className="hidden"
              />
              <div className="w-12 h-12 mx-auto rounded-full bg-teal-500/10 text-[var(--th-teal)] flex items-center justify-center text-2xl font-bold mb-3">
                📥
              </div>
              <p className="text-sm font-semibold text-[var(--th-text)]">
                {selectedFile ? selectedFile.name : "Nhấn để chọn file hoặc kéo thả file vào đây"}
              </p>
              <p className="text-xs text-[var(--th-text-muted)] mt-1">
                Hỗ trợ định dạng Microsoft Excel (.xlsx) và file CSV (.csv) mã hóa UTF-8
              </p>
              {selectedFile && (
                <div className="mt-3 inline-flex items-center gap-2 px-3 py-1 rounded-full bg-teal-500/10 text-[var(--th-teal)] text-xs font-semibold">
                  <span>✓ Đã chọn: {selectedFile.name} ({(selectedFile.size / 1024).toFixed(1)} KB)</span>
                </div>
              )}
            </div>

            <div className="flex items-center justify-between">
              <button
                type="button"
                onClick={downloadCsvTemplate}
                className="text-xs text-[var(--th-teal)] hover:underline flex items-center gap-1 font-semibold"
              >
                <span>📄</span> Tải file mẫu (.CSV) có cấu trúc chuẩn & quan niệm sai
              </button>

              <button
                type="button"
                disabled={!selectedFile || isLoadingPreview}
                onClick={handleUploadAndPreview}
                className="th-button-primary text-xs font-semibold px-5 py-2.5 disabled:opacity-50 flex items-center gap-2"
              >
                {isLoadingPreview ? (
                  <>
                    <span className="animate-spin">⏳</span> Đang đọc và phân tích file...
                  </>
                ) : (
                  <>
                    <span>🔍</span> Kiểm tra & Xem trước dữ liệu
                  </>
                )}
              </button>
            </div>
          </div>
        )}

        {/* Preview Error Alert */}
        {previewError && (
          <div className="p-3.5 rounded-xl border border-rose-500/30 bg-rose-500/10 text-rose-300 text-xs flex items-start gap-2">
            <span className="font-bold">⚠</span>
            <div className="flex-1">{previewError}</div>
          </div>
        )}

        {/* Step 3: Preview and Validation Results */}
        {previewData && !importResult && (
          <div className="space-y-4">
            {/* Stats Summary Bar */}
            <div className="flex flex-wrap items-center justify-between gap-3 p-4 rounded-xl border border-[var(--th-border-subtle)] bg-[var(--th-surface-subtle)]">
              <div className="flex items-center gap-4">
                <div>
                  <span className="text-[11px] text-[var(--th-text-muted)] uppercase font-semibold block">Tổng số dòng</span>
                  <span className="text-lg font-bold text-[var(--th-text)]">{previewData.totalRows}</span>
                </div>
                <div className="h-8 w-px bg-[var(--th-border-subtle)]" />
                <div>
                  <span className="text-[11px] text-emerald-400 uppercase font-semibold block">Hợp lệ</span>
                  <span className="text-lg font-bold text-emerald-400">{previewData.validCount}</span>
                </div>
                {previewData.invalidCount > 0 && (
                  <>
                    <div className="h-8 w-px bg-[var(--th-border-subtle)]" />
                    <div>
                      <span className="text-[11px] text-rose-400 uppercase font-semibold block">Lỗi không hợp lệ</span>
                      <span className="text-lg font-bold text-rose-400">{previewData.invalidCount}</span>
                    </div>
                  </>
                )}
              </div>

              <div className="flex items-center gap-2">
                {previewData.errors.length > 0 && (
                  <button
                    type="button"
                    onClick={() => setShowErrorsOnly(!showErrorsOnly)}
                    className={`text-xs px-3 py-1.5 rounded-lg border font-semibold transition-colors ${
                      showErrorsOnly
                        ? "bg-rose-500/20 text-rose-300 border-rose-500/40"
                        : "bg-slate-700/40 text-slate-300 border-slate-600 hover:bg-slate-700"
                    }`}
                  >
                    {showErrorsOnly ? "Hiện tất cả" : `Chỉ xem lỗi (${previewData.errors.length})`}
                  </button>
                )}
                <button
                  type="button"
                  onClick={handleReset}
                  className="text-xs px-3 py-1.5 rounded-lg border border-[var(--th-border-subtle)] text-[var(--th-text-secondary)] hover:bg-[var(--th-surface-subtle)]"
                >
                  Chọn file khác
                </button>
              </div>
            </div>

            {/* Error Details Box */}
            {previewData.errors.length > 0 && (
              <div className="rounded-xl border border-rose-500/30 bg-rose-500/5 p-4 space-y-2 max-h-48 overflow-y-auto">
                <h4 className="text-xs font-bold uppercase text-rose-400 tracking-wider">
                  Danh sách lỗi cần khắc phục ({previewData.errors.length} cảnh báo)
                </h4>
                <div className="space-y-1.5">
                  {previewData.errors.map((err, i) => (
                    <div key={i} className="text-xs text-rose-300 flex items-start gap-2">
                      <span className="font-mono font-bold bg-rose-500/20 px-1.5 py-0.5 rounded text-[10px]">
                        Dòng {err.rowIndex}
                      </span>
                      <span>
                        <strong className="text-rose-200">[{err.field}]</strong> {err.errorMessage}
                        {err.rawValue && <span className="opacity-75 font-mono ml-1">("{err.rawValue}")</span>}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Valid Questions Preview Table */}
            {!showErrorsOnly && previewData.validQuestions.length > 0 && (
              <div className="rounded-xl border border-[var(--th-border-subtle)] overflow-hidden">
                <div className="max-h-64 overflow-y-auto">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-[var(--th-surface-subtle)] text-[var(--th-text-muted)] font-semibold uppercase text-[10px] sticky top-0">
                      <tr>
                        <th className="p-2.5">Dòng</th>
                        <th className="p-2.5">Loại</th>
                        <th className="p-2.5">Độ khó</th>
                        <th className="p-2.5">Nội dung câu hỏi</th>
                        <th className="p-2.5">Đáp án</th>
                        <th className="p-2.5">Tư duy / Quan niệm sai</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[var(--th-border-subtle)]">
                      {previewData.validQuestions.map((q) => {
                        const miscs = q.options.filter((o) => Boolean(o.misconception)).length;
                        return (
                          <tr key={q.rowIndex} className="hover:bg-[var(--th-surface-subtle)]/40">
                            <td className="p-2.5 font-mono text-[var(--th-text-muted)]">{q.rowIndex}</td>
                            <td className="p-2.5">
                              <span className="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-slate-700/50 text-slate-300">
                                {q.questionType}
                              </span>
                            </td>
                            <td className="p-2.5 font-semibold text-[var(--th-teal)]">Lv {q.difficulty}</td>
                            <td className="p-2.5 max-w-xs truncate text-[var(--th-text)]" title={q.questionText}>
                              {q.questionText}
                            </td>
                            <td className="p-2.5 font-bold text-emerald-400 font-mono">{q.correctAnswer}</td>
                            <td className="p-2.5">
                              <span className="text-[11px] text-[var(--th-text-muted)]">
                                {q.reasoningRequired ? "Bắt buộc tư duy" : "Không"}
                                {miscs > 0 && ` • ${miscs} lỗi sai`}
                              </span>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Confirm Error Alert */}
            {confirmError && (
              <div className="p-3.5 rounded-xl border border-rose-500/30 bg-rose-500/10 text-rose-300 text-xs flex items-start gap-2">
                <span className="font-bold">⚠</span>
                <div className="flex-1">{confirmError}</div>
              </div>
            )}

            {/* Confirmation Actions */}
            <div className="flex items-center justify-between pt-2">
              <span className="text-xs text-[var(--th-text-muted)]">
                {previewData.validCount > 0
                  ? `Sẵn sàng nhập ${previewData.validCount} câu hỏi hợp lệ vào hệ thống.`
                  : "Không có câu hỏi hợp lệ để nhập."}
              </span>

              <div className="flex items-center gap-3">
                <button
                  type="button"
                  onClick={handleReset}
                  className="th-button-secondary text-xs px-4 py-2"
                >
                  Hủy
                </button>
                <button
                  type="button"
                  disabled={previewData.validCount === 0 || isConfirming || !selectedSubjectId || !selectedTopicNodeId}
                  onClick={handleConfirmImport}
                  className="th-button-primary text-xs font-semibold px-6 py-2.5 disabled:opacity-50 flex items-center gap-2"
                >
                  {isConfirming ? (
                    <>
                      <span className="animate-spin">⏳</span> Đang lưu vào cơ sở dữ liệu...
                    </>
                  ) : (
                    <>
                      <span>✓</span> Xác nhận nhập ({previewData.validCount} câu)
                    </>
                  )}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Step 4: Import Success Screen */}
        {importResult && (
          <div className="p-8 text-center space-y-4 rounded-2xl border border-emerald-500/30 bg-emerald-500/10">
            <div className="w-14 h-14 mx-auto rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center text-3xl">
              ✓
            </div>
            <h3 className="text-base font-bold text-emerald-300">
              Nhập câu hỏi thành công!
            </h3>
            <p className="text-xs text-[var(--th-text)] max-w-md mx-auto">
              Đã nhập thành công <strong>{importResult.importedCount} câu hỏi</strong> vào ngân hàng câu hỏi môn học.
            </p>
            <div className="pt-2">
              <button
                type="button"
                onClick={onClose}
                className="th-button-primary text-xs font-semibold px-6 py-2.5"
              >
                Hoàn tất & Xem danh sách
              </button>
            </div>
          </div>
        )}
      </div>
    </TeacherModal>
  );
}
