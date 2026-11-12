import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { filterDraftTopics, lockedTopicIds } from "../src/utils/knowledgeDraftTopics.ts";
import { codeMatchesPrefix, resolveEditedNodeCode } from "../src/utils/knowledgeNodeCode.ts";
import type { KnowledgeGraphNodeDto, KnowledgeGraphVersion } from "../src/types/knowledgeGraph.ts";
import type { Curriculum } from "../src/types/curriculum.ts";

const topic = (nodeId: string, nodeName: string, nodeCode: string, gradeLevels?: number[]): KnowledgeGraphNodeDto => ({
  nodeId, nodeName, nodeCode, gradeLevels, nodeType: "Topic", parentNodeId: null, isActive: true, orderIndex: 0,
  examImportance: 10, estimatedLearningMinutes: 45, rowVersion: "1", description: null,
});
const topics = [
  topic("1", "Giải tích thực và phức", "MATH12_REALNFAKE", [12]),
  topic("2", "Đại số trừu tượng", "MATH12_DSTT"),
  topic("3", "Phương trình bậc hai", "MATH10_T_PTBH_01", [10]),
  topic("4", "Toán cao cấp 1", "MATH12CC_CC1", [12]),
  topic("5", "Hàm số mũ", "MATH11_T_HSM_01", [11]),
];
const versions = [
  { graphVersionId: "pub10", status: "Published", nodeMappings: [{ nodeId: "3", parentNodeId: null }] },
  { graphVersionId: "draft12", status: "Draft", nodeMappings: [{ nodeId: "1", parentNodeId: null }, { nodeId: "2", parentNodeId: "1" }] },
] as Pick<KnowledgeGraphVersion, "graphVersionId" | "status" | "nodeMappings">[];
const curricula = [
  { curriculumId: "c11", reviewStatus: "Archived", nodeIds: ["5"] },
  { curriculumId: "c12", reviewStatus: "Draft", nodeIds: ["4", "1"] },
] as Pick<Curriculum, "curriculumId" | "reviewStatus" | "nodeIds">[];
const ids = (list: KnowledgeGraphNodeDto[]) => list.map(n => n.nodeId);

test("locked topics come from approved/archived frameworks and published/archived curricula only", () => {
  assert.deepEqual([...lockedTopicIds(versions, curricula)].sort(), ["3", "5"]);
  assert.deepEqual([...lockedTopicIds()], []);
});

test("topic filters: grade (default), selected, unselected, draft-only, all, by framework, by curriculum, search", () => {
  const base = { topics, gradeLevel: 12, selectedIds: new Set(["1", "3"]), lockedIds: lockedTopicIds(versions, curricula), versions, curricula };
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "grade" })), ["1", "2", "3", "4"], "grade 12, no-grade and already selected");
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "selected" })), ["1", "3"]);
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "unselected" })), ["2", "4"]);
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "draftOnly" })), ["1", "2", "4"]);
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "all" })), ["1", "2", "3", "4", "5"]);
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "version:pub10" })), ["3"]);
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "version:draft12" })), ["1", "2"]);
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "curriculum:c12" })), ["1", "4"]);
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "curriculum:missing" })), []);
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "all", search: "  đại SỐ " })), ["2"]);
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "all", search: "math12_t" })), [], "search matches name or code");
  assert.deepEqual(ids(filterDraftTopics({ ...base, filter: "all", search: "MATH12CC" })), ["4"]);
});

test("edited code: kept until name/level change, same sequence when prefix unchanged, server suggestion otherwise", () => {
  const base = { currentCode: "MATH12_T_GTTP_03", prefix: "MATH12_T_GTTP", manualCode: "", suggestion: null };
  assert.deepEqual(resolveEditedNodeCode({ ...base, mode: "follow", nameOrTypeChanged: false }), { code: "MATH12_T_GTTP_03", reason: "kept" });
  assert.deepEqual(resolveEditedNodeCode({ ...base, mode: "follow", nameOrTypeChanged: true }), { code: "MATH12_T_GTTP_03", reason: "sameSequence" });
  const renamed = { ...base, prefix: "MATH12_C_GTTP", nameOrTypeChanged: true };
  assert.deepEqual(resolveEditedNodeCode({ ...renamed, mode: "follow" }), { code: null, reason: "pending" });
  assert.deepEqual(resolveEditedNodeCode({ ...renamed, mode: "follow", suggestion: { prefix: "MATH12_T_GTTP", code: "stale" } }), { code: null, reason: "pending" },
    "a suggestion for another prefix is never used");
  assert.deepEqual(resolveEditedNodeCode({ ...renamed, mode: "follow", suggestion: { prefix: "MATH12_C_GTTP", code: "MATH12_C_GTTP_01" } }), { code: "MATH12_C_GTTP_01", reason: "suggested" });
  assert.deepEqual(resolveEditedNodeCode({ ...renamed, mode: "keep" }), { code: "MATH12_T_GTTP_03", reason: "kept" });
  assert.deepEqual(resolveEditedNodeCode({ ...renamed, mode: "manual", manualCode: " math12_c_x_09 " }), { code: "MATH12_C_X_09", reason: "manual" });
  // A legacy code is only replaced when asked (or when the name/level changes).
  const legacy = { currentCode: "MATH12_REALNFAKE", prefix: "MATH12_T_GTTP", manualCode: "", nameOrTypeChanged: false };
  assert.equal(resolveEditedNodeCode({ ...legacy, mode: "follow" }).code, "MATH12_REALNFAKE");
  assert.equal(resolveEditedNodeCode({ ...legacy, mode: "standard" }).reason, "pending");
  assert.equal(resolveEditedNodeCode({ ...legacy, mode: "follow", prefix: null, nameOrTypeChanged: true }).code, "MATH12_REALNFAKE", "no prefix (empty name): keep");
});

test("prefix match requires the exact prefix and a numeric sequence", () => {
  assert.ok(codeMatchesPrefix("MATH12_T_GTTP_03", "MATH12_T_GTTP"));
  assert.ok(codeMatchesPrefix("math12_t_gttp_100", "MATH12_T_GTTP"));
  assert.ok(!codeMatchesPrefix("MATH12_T_GTTPX_03", "MATH12_T_GTTP"));
  assert.ok(!codeMatchesPrefix("MATH12_T_GTTP_3", "MATH12_T_GTTP"));
  assert.ok(!codeMatchesPrefix("MATH12_T_GTTP_A1", "MATH12_T_GTTP"));
});

test("framework editor wiring: filters, edit/delete per topic, add buttons in place, created items ticked, sticky toolbar", () => {
  const panel = fs.readFileSync(new URL("../src/components/teacher/KnowledgeVersionPanel.tsx", import.meta.url), "utf8");
  assert.match(panel, /aria-label="Lọc chủ đề theo khung tri thức hoặc giáo trình"/);
  assert.match(panel, /<optgroup label="Theo khung tri thức">/);
  assert.match(panel, /<optgroup label="Theo giáo trình">/);
  assert.match(panel, /<option value="all">Tất cả chủ đề của môn<\/option>/);
  assert.match(panel, /canDeleteTopics && draftOnly &&/, "only draft topics offer deletion");
  assert.match(panel, /disabled=\{dirty\}/, "no delete while the draft has unsaved changes");
  assert.match(panel, /<KnowledgeNodeEditDialog node=\{editingTopic\}/);
  assert.match(panel, /includeRequest\.nonce < mountedAt/, "requests made for another version are ignored");
  assert.match(panel, /\+ Thêm chủ đề mới/);
  assert.match(panel, /\+ Thêm liên kết/);
  assert.match(panel, /sticky bottom-0/, "save bar stays visible while there are unsaved changes");
  const view = fs.readFileSync(new URL("../src/pages/teacher/TeacherKnowledgeGraphView.tsx", import.meta.url), "utf8");
  assert.match(view, /data-testid="kg-draft-toolbar"/);
  assert.match(view, /className="sticky top-0 z-20/);
  assert.match(view, /jumpTo\("kg-version-panel"\)/);
  assert.match(view, /jumpTo\("kg-graph-area"\)/);
  assert.match(view, /id="kg-graph-area"/);
  assert.match(view, /setDraftInclude\(\{ kind: "node", id: created\.nodeId/);
  assert.match(view, /setDraftInclude\(\{ kind: "edge", id: created\.edgeId/);
  assert.match(view, /includeRequest=\{draftInclude\}/);
  const dialog = fs.readFileSync(new URL("../src/components/teacher/KnowledgeNodeEditDialog.tsx", import.meta.url), "utf8");
  assert.match(dialog, /\.\.\.\(codeChanged \? \{ nodeCode: code! \} : \{\}\)/, "code is only sent when it changes");
  assert.match(dialog, /\.\.\.\(type !== node\.nodeType \? \{ nodeType: type \} : \{\}\)/);
  const api = fs.readFileSync(new URL("../src/api/knowledgeGraphApi.ts", import.meta.url), "utf8");
  assert.match(api, /\.\.\.\(request\.nodeCode \? \{ nodeCode: request\.nodeCode\.trim\(\) \} : \{\}\)/);
});

test("framework mode: no duplicate header add buttons, scope box hidden, explicit exit back to the grade view", () => {
  const view = fs.readFileSync(new URL("../src/pages/teacher/TeacherKnowledgeGraphView.tsx", import.meta.url), "utf8");
  assert.doesNotMatch(view, /Thêm Liên Kết Tiên Quyết|Thêm Chủ Đề Mới/, "add actions live only in the editor toolbar and lists");
  assert.match(view, /const frameworkMode = !!scope\.graphVersionId && !!scope\.gradeLevel && !scope\.classId && !scope\.curriculumId;/);
  assert.match(view, /\{!frameworkMode && <div className="th-surface rounded-xl p-4 my-4 space-y-3" aria-label="Phạm vi đồ thị tri thức">/,
    "grade/year/class/curriculum pickers would leave the editor, so they are hidden there");
  assert.match(view, /\{frameworkMode && selectedSubjectId && \(\s*<div role="toolbar"/);
  assert.match(view, /onClick=\{\(\) => updateScope\(\{ gradeLevel: scope\.gradeLevel \}\)\}>Thoát chế độ soạn<\/button>/);
  assert.match(view, /disabled=\{versionDirty\}\s*title=\{versionDirty \? "Lưu hoặc hủy cấu trúc nháp trước khi thoát\."/);
});

test("refused actions are never silent: delete reasons stay in the dialog, create errors show inside the modal", () => {
  const panel = fs.readFileSync(new URL("../src/components/teacher/KnowledgeVersionPanel.tsx", import.meta.url), "utf8");
  // 409 on DELETE /knowledge/nodes/{id}: the dialog stays open with the server's reason.
  assert.match(panel, /onError: err => setDeleteError\(extractProblemDetails\(err\)\.detail/);
  assert.doesNotMatch(panel, /onError: err => \{ setDeletingTopic\(null\); error\(err\); \}/);
  assert.match(panel, /error=\{deleteError\}/);
  // Relations/children already known to the library are listed up front and block the confirm button.
  assert.match(panel, /relations: \(catalog\.data\?\.edges \?\? \[\]\)\.filter\(e => e\.sourceNodeId === deletingTopic\.nodeId \|\| e\.targetNodeId === deletingTopic\.nodeId\)/);
  assert.match(panel, /confirmDisabled=\{!!deleteBlockers && \(deleteBlockers\.relations\.length > 0 \|\| deleteBlockers\.children\.length > 0\)\}/);
  // Other panel errors are alerts (corner toast), not a status line at the bottom.
  assert.match(panel, /<p role="alert" className="text-sm font-semibold text-\[var\(--th-danger\)\]">\{message\}<\/p>/);

  const dialog = fs.readFileSync(new URL("../src/components/teacher/TeacherOverlays.tsx", import.meta.url), "utf8");
  assert.match(dialog, /\{error && \(\s*<p role="alert"/);
  assert.match(dialog, /disabled=\{isConfirming \|\| confirmDisabled\}/);

  const view = fs.readFileSync(new URL("../src/pages/teacher/TeacherKnowledgeGraphView.tsx", import.meta.url), "utf8");
  // 409 on POST /knowledge/nodes with a hand-typed code: specific message rendered inside both create modals.
  assert.equal((view.match(/\{feedbackMsg\?\.type === "error" && <ModalError message=\{feedbackMsg\.text\}/g) ?? []).length, 2);
  assert.match(view, /details\.errorCode === "DUPLICATE_RESOURCE"\s*\? `Mã \$\{req\.nodeCode\} đã được dùng cho một điểm tri thức khác trong môn\./);
  assert.match(view, /const sameCode = nodes\.find\(n => n\.nodeCode\.toUpperCase\(\) === trimmedCode\);/);
  assert.match(view, /const openAddNode = \(\) => \{ setFeedbackMsg\(null\); setIsAddNodeOpen\(true\); \};/, "stale errors are cleared when a dialog opens");
});
