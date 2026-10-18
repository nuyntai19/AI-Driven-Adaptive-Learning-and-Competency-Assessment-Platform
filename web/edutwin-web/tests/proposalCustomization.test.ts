import assert from "node:assert/strict";
import test from "node:test";
import fs from "node:fs";
import vm from "node:vm";
import { createRequire } from "node:module";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import * as recommendation from "../src/utils/learningPathRecommendation.ts";
import type { CustomizePathProposalRequest } from "../src/types/learningPath.ts";

const require = createRequire(import.meta.url);
const proposal = { proposalId: "original", goalTitle: "Củng cố Tiếng Anh", availableMinutesPerDay: 25, availableDaysPerWeek: 4,
  practiceQuestionsPerSet: 7, deadlineDate: null, startingBaselineSummary: "Dựa trên bài làm", milestones: [
    { order: 1, title: "Thì", skillIds: ["101"], missions: [] }, { order: 2, title: "Đọc hiểu", skillIds: ["103"], missions: [] }] };
const CustomForm = () => null;

function load(file: string, dependencies: Record<string, any> = {}) {
  const states: any[] = []; let cursor = 0;
  const react = { ...React, useState: (initial: any) => {
    const index = cursor++;
    if (!(index in states)) states[index] = typeof initial === "function" ? initial() : initial;
    return [states[index], (next: any) => { states[index] = typeof next === "function" ? next(states[index]) : next; }];
  }, useRef: (initial: any) => ({ current: initial }), useEffect: () => {} };
  const exports = {};
  const source = fs.readFileSync(new URL(`../src/components/learningPathV4/${file}.tsx`, import.meta.url), "utf8");
  vm.runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, target: ts.ScriptTarget.ES2020, esModuleInterop: true } }).outputText,
    { exports, require: (name: string) => {
      if (name === "react") return react;
      if (name === "react/jsx-runtime") return require(name);
      const key = Object.keys(dependencies).find(key => name.endsWith(key));
      if (key) return dependencies[key];
      if (name.endsWith("TimeBudgetSlider")) return { TimeBudgetSlider: () => null };
      throw new Error(`Missing test dependency ${name}`);
    } });
  return { render: (props: any) => { cursor = 0; return (exports as any)[file](props); } };
}
function nodes(node: any): any[] {
  if (Array.isArray(node)) return node.flatMap(nodes);
  if (!node || typeof node !== "object") return [];
  return [node, ...nodes(node.props?.children)];
}
function button(tree: any, title: string) {
  const found = nodes(tree).find(node => node.type === "button" && renderToStaticMarkup(node).includes(title));
  assert.ok(found, `Missing button ${title}`); return found;
}

test("customization starts with the proposal values and submits changes to its workload, schedule and sections", () => {
  const form = load("ProposalCustomizationForm"); let submitted: CustomizePathProposalRequest | null = null;
  const props = { proposal, pending: false, onSubmit: (value: CustomizePathProposalRequest) => { submitted = value; }, onCancel: () => {} };
  let tree = form.render(props);
  const number = nodes(tree).find(node => node.type === "input" && node.props.type === "number");
  assert.equal(number.props.value, "7");
  assert.equal(nodes(tree).find(node => node.type === "select").props.value, 4);
  assert.equal(nodes(tree).filter(node => node.props.type === "checkbox" && node.props.checked).length, 2);
  number.props.onChange({ target: { value: "3" } });
  nodes(tree).find(node => node.type === "select").props.onChange({ target: { value: "2" } });
  nodes(tree).filter(node => node.props.type === "checkbox")[1].props.onChange({ target: { checked: false } });
  tree = form.render(props); tree.props.onSubmit({ preventDefault() {} });
  assert.deepEqual(JSON.parse(JSON.stringify(submitted)), { availableMinutesPerDay: 25, availableDaysPerWeek: 2,
    practiceQuestionsPerSet: 3, targetSkillIds: ["101"], deadlineDate: null });
});

test("empty scope, invalid count and saving state cannot submit a customized preview", () => {
  for (const value of ["", "2", "21", "3.5"]) {
    let calls = 0; const form = load("ProposalCustomizationForm");
    const props = { proposal, pending: false, onSubmit: () => { calls++; }, onCancel: () => {} };
    let tree = form.render(props);
    nodes(tree).find(node => node.props.type === "number").props.onChange({ target: { value } });
    tree = form.render(props); tree.props.onSubmit({ preventDefault() {} });
    assert.equal(calls, 0);
  }
  let calls = 0; const form = load("ProposalCustomizationForm");
  const props = { proposal, pending: false, onSubmit: () => { calls++; }, onCancel: () => {} };
  let tree = form.render(props);
  for (const checkbox of nodes(tree).filter(node => node.props.type === "checkbox")) checkbox.props.onChange({ target: { checked: false } });
  tree = form.render(props); tree.props.onSubmit({ preventDefault() {} }); assert.equal(calls, 0);
  tree = form.render({ ...props, pending: true }); assert.equal(button(tree, "Hủy tùy chỉnh").props.disabled, true);
});

function modal() {
  let index = 0; const mutations: any[] = []; const accepted: string[] = []; const customized: any[] = [];
  const component = load("GoalAndProposalModal", {
    "@tanstack/react-query": { useQueryClient: () => ({ invalidateQueries: async () => {} }), useMutation: (config: any) => {
      const position = index++;
      const entry = mutations[position] ??= { isPending: false, error: null, reset() { entry.error = null; },
        async mutate(value?: any) { entry.isPending = true; try { const result = await entry.config.mutationFn(value); await entry.config.onSuccess?.(result); }
          catch (error) { entry.error = error; } finally { entry.isPending = false; } } };
      entry.config = config; return entry;
    } },
    "learningPathApi": { createOrUpdateLearningGoal: async () => ({ goalId: "goal" }), generatePathProposal: async () => proposal,
      customizePathProposal: async (id: string, request: any) => { customized.push({ id, request }); return { ...proposal, ...request, proposalId: "customized", milestones: proposal.milestones.filter(m => m.skillIds.some(id => request.targetSkillIds.includes(id))) }; },
      acceptPathProposal: async (id: string) => { accepted.push(id); return {}; } },
    "learningPathRecommendation": recommendation, "learningMission": { pathChangeBlockersFromError: () => [] },
    "problemDetails": { extractProblemDetails: () => ({ message: "Không lưu được" }) },
    "studyTime": { formatStudyMinutes: (value: number) => `${value} phút` },
    "ProposalCustomizationForm": { ProposalCustomizationForm: CustomForm },
  });
  const props = { isOpen: true, onClose() {}, onAccepted() {}, subjectId: "english", subjectName: "Tiếng Anh", skills: [] };
  const render = () => { index = 0; return component.render(props); };
  return { render, mutations, accepted, customized };
}

test("customizing the existing preview never opens goal editing, and acceptance uses the updated proposal", async () => {
  const h = modal(); h.render(); await h.mutations[0].mutate();
  button(h.render(), "Tùy chỉnh tuyến học").props.onClick();
  const tree = h.render();
  assert.doesNotMatch(renderToStaticMarkup(tree), /Tên mục tiêu/);
  const form = nodes(tree).find(node => node.type === CustomForm);
  assert.equal(form.props.proposal.proposalId, "original");
  form.props.onSubmit({ availableMinutesPerDay: 30, availableDaysPerWeek: 3, practiceQuestionsPerSet: 3, targetSkillIds: ["101"], deadlineDate: null });
  // Let the mocked request and mutation success callback settle.
  await new Promise(resolve => setTimeout(resolve, 0));
  assert.equal(h.customized[0].id, "original"); assert.equal(h.accepted.length, 0);
  assert.match(renderToStaticMarkup(h.render()), /3 câu\/bộ luyện/);
  button(h.render(), "Chọn tuyến học này").props.onClick();
  await new Promise(resolve => setTimeout(resolve, 0));
  assert.deepEqual(h.accepted, ["customized"]);
});

test("cancel returns to the same preview without saving; goal editing remains a separate action", async () => {
  const h = modal(); h.render(); await h.mutations[0].mutate();
  button(h.render(), "Tùy chỉnh tuyến học").props.onClick();
  nodes(h.render()).find(node => node.type === CustomForm).props.onCancel();
  assert.equal(h.customized.length, 0);
  assert.ok(button(h.render(), "Chọn tuyến học này"));
  button(h.render(), "Thay đổi mục tiêu học").props.onClick();
  assert.match(renderToStaticMarkup(h.render()), /Tên mục tiêu/);
});


test("a section omitted from the current preview can be added back from the original route", () => {
  let submitted: CustomizePathProposalRequest | null = null;
  const form = load("ProposalCustomizationForm");
  const props = { proposal: { ...proposal, practiceQuestionsPerSet: 3, milestones: proposal.milestones.slice(0, 1) },
    availableMilestones: proposal.milestones, pending: false, onSubmit: (value: CustomizePathProposalRequest) => { submitted = value; }, onCancel() {} };
  let tree = form.render(props);
  const checkboxes = nodes(tree).filter(node => node.props.type === "checkbox");
  assert.equal(checkboxes.length, 2); assert.equal(checkboxes[1].props.checked, false);
  checkboxes[1].props.onChange({ target: { checked: true } });
  tree = form.render(props); tree.props.onSubmit({ preventDefault() {} });
  assert.deepEqual(JSON.parse(JSON.stringify(submitted)).targetSkillIds, ["101", "103"]);
});
