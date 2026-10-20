import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import test from "node:test";
import ts from "typescript";
import React from "react";
import * as jsxRuntime from "react/jsx-runtime";
import XLSX from "xlsx";
import { createQuestionImportDocx, createQuestionImportMediaZip } from "../src/utils/questionImportV2Templates.ts";
import { QUESTION_IMPORT_COLUMN_LABELS, QUESTION_IMPORT_V1_COLUMNS } from "../src/utils/questionImportTemplate.ts";
import { buildQuestionImportV2Settings, formatQuestionImportSourceLocation, QUESTION_IMPORT_SOURCES, questionImportSourceFileError } from "../src/utils/questionImportProfiles.ts";
import type { QuestionImportPreviewRequest } from "../src/types/questions.ts";
import * as importProfiles from "../src/utils/questionImportProfiles.ts";
import * as importValidation from "../src/utils/questionImportValidation.ts";
import * as importTemplate from "../src/utils/questionImportTemplate.ts";

const subject = { subjectId: "0904f0c4-2901-4b47-8504-bbe84239cd1f", subjectCode: "MATH", subjectName: "Toán" };
const topics = [{ nodeId: "1", nodeCode: "MATH-DERIV", nodeName: "Đạo hàm" }, { nodeId: "2", nodeCode: "MATH-FRAC", nodeName: "Phân số" }, { nodeId: "3", nodeCode: "MATH-GEOM", nodeName: "Hình học" }];
const settings: QuestionImportPreviewRequest = { subjectId: subject.subjectId, templateVersion: "1.0", legacyMode: true, defaultTopicNodeId: "1", gradeLevel: 12, difficulty: 3, maxScore: 10, estimatedTimeSeconds: 120, reasoningRequired: false, visibility: "Private", languageCode: "vi", shortAnswerEvaluationMode: "Manual" };
const read = (path: string) => fs.readFileSync(new URL(path, import.meta.url), "utf8");
const unzip = (bytes: Uint8Array) => XLSX.CFB.read(bytes, { type: "buffer" });
const entry = (zip: unknown, path: string): Uint8Array => new Uint8Array(XLSX.CFB.find(zip, `/${path}`).content);
const xml = (zip: unknown, path: string): string => new TextDecoder().decode(entry(zip, path));

test("DOCX template is a native package with exact version/profile, flat question tables and inline image cell", async () => {
  const zip = unzip(await createQuestionImportDocx(subject, topics));
  const document = xml(zip, "word/document.xml");
  assert.match(document, /EduTwin Import/); assert.match(document, /docx-1\.0/);
  assert.match(document, /TemplateVersion/); assert.match(document, />2\.0<\/w:t>/);
  assert.ok(document.includes(subject.subjectId));
  assert.equal((document.match(/<w:tbl>/g) ?? []).length, 6);
  assert.equal((document.match(/>Trường<\/w:t>/g) ?? []).length, 3);
  for (const column of QUESTION_IMPORT_V1_COLUMNS) assert.ok(document.includes(QUESTION_IMPORT_COLUMN_LABELS[column]), column);
  for (const topic of topics) assert.ok(document.includes(topic.nodeCode));
  assert.equal((document.match(/<wp:inline\b/g) ?? []).length, 1);
  assert.match(document, /Ảnh đề bài/); assert.doesNotMatch(document, /<wp:anchor\b/);
  assert.match(xml(zip, "word/_rels/document.xml.rels"), /Target="media\/example\.png"/);
  assert.doesNotMatch(xml(zip, "word/_rels/document.xml.rels"), /TargetMode="External"/);
  assert.deepEqual(Array.from(entry(zip, "word/media/example.png").slice(0, 8)), [137, 80, 78, 71, 13, 10, 26, 10]);
  const withoutImage = xml(unzip(await createQuestionImportDocx(subject, topics, false)), "word/document.xml");
  assert.doesNotMatch(withoutImage, /<w:drawing>/);
});

test("media ZIP template contains one workbook plus explicitly referenced image and V2 Guide", async () => {
  const zip = unzip(await createQuestionImportMediaZip(subject, topics));
  const files = zip.FullPaths.filter((path: string, index: number) => zip.FileIndex[index].type === 2 && !path.includes("\u0001")).map((path: string) => path.replace(/^Root Entry\//, "")).sort();
  assert.deepEqual(files, ["media/example.png", "questions.xlsx"]);
  const book = XLSX.read(entry(zip, "questions.xlsx"), { type: "array", raw: true });
  const rows = XLSX.utils.sheet_to_json<Record<string, string>>(book.Sheets.Questions);
  assert.equal(rows.length, 3);
  assert.equal(rows[0]["Mã ảnh đề bài"], "media/example.png");
  assert.equal(rows[1]["Mã ảnh đề bài"], undefined);
  assert.equal(book.Sheets.Guide.B1.v, "2.0");
  assert.equal(book.Sheets.Guide.B2.v, subject.subjectId);
  assert.deepEqual(rows.map((row) => row["Mã câu hỏi"]), ["Q001", "Q002", "Q003"]);
  assert.equal(rows[1]["Nội dung câu hỏi"], String.raw`Tính $\frac{1}{2}+\frac{1}{3}$.`);
});

test("source file gate separates V1 and V2 and never enables TSV or other formats", () => {
  assert.equal(questionImportSourceFileError({ name: "questions.docx", size: 100 }, "docx"), null);
  assert.equal(questionImportSourceFileError({ name: "questions.ZIP", size: 100 }, "media"), null);
  assert.ok(questionImportSourceFileError({ name: "questions.docx", size: 100 }, "excel"));
  assert.ok(questionImportSourceFileError({ name: "questions.xlsx", size: 100 }, "media"));
  assert.ok(questionImportSourceFileError({ name: "questions.zip", size: 30 * 1024 * 1024 + 1 }, "media"));
  for (const source of Object.keys(QUESTION_IMPORT_SOURCES) as Array<keyof typeof QUESTION_IMPORT_SOURCES>) assert.ok(questionImportSourceFileError({ name: "questions.tsv", size: 100 }, source));
  const request = buildQuestionImportV2Settings(settings, "docx");
  assert.equal(request.templateVersion, "2.0"); assert.equal(request.sourceProfile, "docx-1.0");
  assert.ok(!("legacyMode" in request)); assert.ok(!("defaultTopicNodeId" in request));
  assert.equal(request.gradeLevel, 12); assert.equal(request.subjectId, subject.subjectId);
  assert.equal(settings.legacyMode, true, "building V2 metadata must not mutate V1 defaults");
  assert.equal(buildQuestionImportV2Settings(settings, "media").sourceProfile, "media-1.0");
});

test("DOCX source diagnostics retain all available original coordinates", () => {
  assert.equal(formatQuestionImportSourceLocation({ partPath: "word/document.xml", questionIndex: 2, tableIndex: 5, tableRowIndex: 7, cellIndex: 2, paragraphIndex: 1, field: "QuestionText", relationshipId: "rId7" }), "word/document.xml • câu 2 • bảng 5 • hàng 7 • ô 2 • đoạn 1 • QuestionText • quan hệ rId7");
});

function loadModule(path: string, modules: Record<string, unknown>, globals: Record<string, unknown> = {}) {
  const output = ts.transpileModule(read(path), { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020 } }).outputText;
  const result = { exports: {} as Record<string, any> };
  vm.runInNewContext(output, { module: result, exports: result.exports, ...globals, require(specifier: string) { if (specifier in modules) return modules[specifier]; throw new Error(`Unexpected import: ${specifier}`); } });
  return result.exports;
}

test("V2 API uses separate preview/confirm routes and header-only authenticated asset/session access", async () => {
  const calls: Array<{ method: string; url: string; body?: unknown; config?: any }> = [];
  const httpClient = {
    post: async (url: string, body: unknown, config?: unknown) => { calls.push({ method: "POST", url, body, config }); return { data: { data: {} } }; },
    get: async (url: string, config: unknown) => { calls.push({ method: "GET", url, config }); return { data: new Blob(["image"], { type: "image/png" }) }; },
    delete: async (url: string, config: unknown) => { calls.push({ method: "DELETE", url, config }); },
  };
  const { questionsApi } = loadModule("../src/api/questionsApi.ts", { "./httpClient": { httpClient } }, { FormData });
  await questionsApi.previewImportV2(new File(["test"], "test.docx"), buildQuestionImportV2Settings(settings, "docx"));
  await questionsApi.confirmImportV2({ previewToken: "secret", revision: 1, targetStatus: "Draft", importMode: "AllValid", selectedQuestionKeys: [] });
  await questionsApi.getImportV2Asset("asset-id", "secret", 1);
  await questionsApi.discardImportV2("secret", 1);
  assert.equal(calls[0].url, "/questions/import-v2/preview");
  assert.equal((calls[0].body as FormData).get("sourceProfile"), "docx-1.0");
  assert.equal((calls[0].body as FormData).get("templateVersion"), "2.0");
  assert.equal(calls[1].url, "/questions/import-v2/confirm");
  for (const call of calls.slice(2)) { assert.equal(call.config.headers["X-Import-Preview-Token"], "secret"); assert.equal(call.config.params.revision, 1); assert.ok(!call.url.includes("secret")); assert.ok(!JSON.stringify(call.config.params).includes("secret")); }
  assert.equal(calls[2].config.responseType, "blob");
});

function deferred<T>() { let resolve!: (value: T) => void; let reject!: (error: Error) => void; const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no; }); return { promise, resolve, reject }; }
function imageHarness() {
  const slots: any[] = []; let cursor = 0;
  const pending: Array<() => void> = []; const requests: Array<{ asset: string; token: string; revision: number; signal: AbortSignal; response: ReturnType<typeof deferred<Blob>> }> = [];
  const created: string[] = []; const revoked: string[] = [];
  const hooks = { ...React,
    useState(initial: unknown) { const index = cursor++; slots[index] ??= { value: initial }; return [slots[index].value, (next: unknown) => { slots[index].value = typeof next === "function" ? next(slots[index].value) : next; }]; },
    useEffect(callback: () => (() => void), deps: unknown[]) { const index = cursor++; const before = slots[index]; if (!before || deps.some((value, at) => value !== before.deps[at])) pending.push(() => { before?.cleanup?.(); slots[index] = { deps, cleanup: callback() }; }); },
  };
  const { ImportPreviewImage } = loadModule("../src/components/teacher/ImportPreviewImage.tsx", { react: hooks, "react/jsx-runtime": jsxRuntime, "../../api/questionsApi": { questionsApi: { getImportV2Asset(asset: string, token: string, revision: number, signal: AbortSignal) { const response = deferred<Blob>(); requests.push({ asset, token, revision, signal, response }); return response.promise; } } } }, { AbortController, URL: { createObjectURL() { const value = `blob:preview-${created.length + 1}`; created.push(value); return value; }, revokeObjectURL(value: string) { revoked.push(value); } } });
  return { requests, created, revoked, render(props = { assetId: "asset-1", previewToken: "token-1", revision: 1, questionKey: "Q001", width: 640 }) { cursor = 0; const tree = ImportPreviewImage(props); while (pending.length) pending.shift()!(); return tree; }, unmount() { slots.forEach((slot) => slot?.cleanup?.()); } };
}
function find(node: any, predicate: (element: any) => boolean): any[] { if (Array.isArray(node)) return node.flatMap((element) => find(element, predicate)); if (!node?.props) return []; return [...(predicate(node) ? [node] : []), ...find(node.props.children, predicate)]; }
const flush = () => new Promise<void>((resolve) => setImmediate(resolve));

test("image preview revokes old Blob URLs and aborts loading when token/revision changes or closes", async () => {
  const harness = imageHarness(); harness.render();
  assert.equal(harness.requests[0].token, "token-1");
  harness.requests[0].response.resolve(new Blob(["png"], { type: "image/png" })); await flush();
  let tree = harness.render(); assert.equal(find(tree, (element) => element.type === "img")[0].props.src, "blob:preview-1");
  const zoom = find(tree, (element) => element.type === "button" && "aria-expanded" in element.props)[0]; zoom.props.onClick();
  tree = harness.render(); assert.equal(find(tree, (element) => element.type === "button" && element.props["aria-expanded"] === true).length, 1);
  harness.render({ assetId: "asset-2", previewToken: "token-2", revision: 2, questionKey: "Q002", width: 640 });
  assert.equal(harness.requests[0].signal.aborted, true); assert.deepEqual(harness.revoked, ["blob:preview-1"]);
  harness.requests[1].response.resolve(new Blob(["png"], { type: "image/png" })); await flush();
  harness.unmount(); assert.equal(harness.requests[1].signal.aborted, true); assert.deepEqual(harness.revoked, ["blob:preview-1", "blob:preview-2"]);
});

test("late or non-image responses cannot create a preview image", async () => {
  const harness = imageHarness(); harness.render(); harness.unmount();
  harness.requests[0].response.resolve(new Blob(["png"], { type: "image/png" })); await flush(); assert.deepEqual(harness.created, []);
  const invalid = imageHarness(); invalid.render(); invalid.requests[0].response.resolve(new Blob(["<script>bad</script>"], { type: "text/html" })); await flush();
  assert.deepEqual(invalid.created, []); assert.equal(find(invalid.render(), (element) => element.props.role === "alert").length, 1); invalid.unmount();
});

function modalHarness(delayedDiscard: boolean, mismatchedProfile = false) {
  const slots: any[] = []; const effects: Array<() => void> = []; let cursor = 0;
  const previewCalls: Array<{ signal: AbortSignal }> = []; const discardCalls: Array<{ token: string; response: ReturnType<typeof deferred<void>> }> = [];
  const memo = (factory: () => unknown, deps: unknown[]) => { const index = cursor++; if (!slots[index] || deps.some((value, at) => value !== slots[index].deps[at])) slots[index] = { deps, value: factory() }; return slots[index].value; };
  const hooks = { ...React,
    useState(initial: unknown) { const index = cursor++; slots[index] ??= { value: initial }; return [slots[index].value, (next: unknown) => { slots[index].value = typeof next === "function" ? next(slots[index].value) : next; }]; },
    useRef(initial: unknown) { const index = cursor++; slots[index] ??= { current: initial }; return slots[index]; },
    useMemo: memo, useCallback: (callback: unknown, deps: unknown[]) => memo(() => callback, deps),
    useEffect(callback: () => void, deps: unknown[]) { const index = cursor++; const before = slots[index]; if (!before || deps.some((value, at) => value !== before.deps[at])) effects.push(() => { before?.cleanup?.(); slots[index] = { deps, cleanup: callback() }; }); },
  };
  const questionsApi = {
    async previewImportV2(_file: unknown, _settings: unknown, signal: AbortSignal) {
      previewCalls.push({ signal });
      return { data: { previewToken: `token-${previewCalls.length}`, revision: 1, sourceProfile: mismatchedProfile ? "media-1.0" : "docx-1.0", subjectId: subject.subjectId, templateVersion: "2.0", expiresAt: new Date(Date.now() + 60_000).toISOString(), totalRows: 0, validCount: 0, invalidCount: 0, errorCount: 0, warningCount: 0, validQuestions: [], errors: [], warnings: [] } };
    },
    discardImportV2(token: string) { const response = deferred<void>(); discardCalls.push({ token, response }); if (!delayedDiscard) response.resolve(); return response.promise; },
  };
  const { QuestionImportModal } = loadModule("../src/components/teacher/QuestionImportModal.tsx", {
    react: hooks, "react/jsx-runtime": jsxRuntime, "react-router-dom": { Link: "a" },
    "@tanstack/react-query": { useQuery: ({ queryKey }: any) => ({ data: queryKey[0] === "subjects" ? { data: [subject] } : { nodes: topics.map((topic) => ({ ...topic, isActive: true, nodeType: "Topic" })) }, isLoading: false }) },
    "../../stores/authStore": { useAuthStore: (selector: any) => selector({ user: { accountType: "Teacher", userId: "teacher", centerId: "center" } }) },
    "../../api/questionsApi": { questionsApi }, "../../api/organizationApi": { organizationApi: {} }, "../../api/knowledgeGraphApi": { knowledgeGraphApi: {} },
    "../math/RichMathText": { RichMathText: "span" }, "./TeacherOverlays": { TeacherModal: "div" }, "./ImportLatexGuide": { ImportLatexGuide: "aside" }, "./ImportPreviewImage": { ImportPreviewImage: "img" },
    "../../utils/questionEvaluationModes": { MATH_EQUIVALENT_HELP: "" }, "../../utils/questionImportTemplate": importTemplate,
    "../../utils/questionImportValidation": importValidation, "../../utils/questionImportProfiles": importProfiles, "./teacherDesignSystem.css": {},
  }, { AbortController, setTimeout });
  return { previewCalls, discardCalls, render() { cursor = 0; const tree = QuestionImportModal({ isOpen: true, onClose() {}, onSuccess() {} }); while (effects.length) effects.shift()!(); return tree; } };
}

function setupDocxFile(harness: ReturnType<typeof modalHarness>) {
  let tree = harness.render();
  find(tree, (element) => element.type === "select" && element.props.value === "excel")[0].props.onChange({ target: { value: "docx" } });
  tree = harness.render();
  find(tree, (element) => element.type === "select" && find(element.props.children, (option) => option.type === "option" && option.props.value === subject.subjectId).length > 0)[0].props.onChange({ target: { value: subject.subjectId } });
  tree = harness.render();
  find(tree, (element) => element.type === "input" && element.props.type === "file")[0].props.onChange({ target: { files: [new File(["docx"], "questions.docx")] } });
  return harness.render();
}

test("new preview awaits cleanup of the old media session before posting another upload", async () => {
  const harness = modalHarness(true);
  let tree = setupDocxFile(harness);
  await find(tree, (element) => element.type === "button" && element.props.children === "Kiểm tra & xem trước")[0].props.onClick();
  tree = harness.render();
  find(tree, (element) => element.type === "button" && element.props.children === "Sửa file / tải lại")[0].props.onClick();
  tree = harness.render();
  find(tree, (element) => element.type === "input" && element.props.type === "file")[0].props.onChange({ target: { files: [new File(["new"], "new.docx")] } });
  tree = harness.render();
  const next = find(tree, (element) => element.type === "button" && element.props.children === "Kiểm tra & xem trước")[0].props.onClick();
  await flush();
  assert.equal(harness.discardCalls.length, 1); assert.equal(harness.discardCalls[0].token, "token-1");
  assert.equal(harness.previewCalls.length, 1, "next upload must not race the actor's session quota cleanup");
  harness.discardCalls[0].response.resolve(); await next;
  assert.equal(harness.previewCalls.length, 2);
});

test("profile mismatch discards its token and never displays a confirmable preview", async () => {
  const harness = modalHarness(false, true);
  const tree = setupDocxFile(harness);
  await find(tree, (element) => element.type === "button" && element.props.children === "Kiểm tra & xem trước")[0].props.onClick();
  assert.equal(harness.discardCalls.length, 1); assert.equal(harness.discardCalls[0].token, "token-1");
  const after = harness.render();
  assert.equal(find(after, (element) => element.props.role === "alert" && String(element.props.children).includes("Định dạng của phiên")).length, 1);
  assert.equal(find(after, (element) => element.type === "button" && typeof element.props.children === "string" && element.props.children.startsWith("Xác nhận lưu")).length, 0);
});
