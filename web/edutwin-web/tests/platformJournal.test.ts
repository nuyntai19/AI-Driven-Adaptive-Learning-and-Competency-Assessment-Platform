import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import { createRequire } from "node:module";
import ts from "typescript";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { platformJournalRoutes, platformJournalTabs } from "../src/utils/platformJournalNavigation.ts";
import { permissions } from "../src/auth/permissions.ts";
import { canAccess } from "../src/auth/capabilities.ts";
import { auditActionLabel, auditTargetLabel } from "../src/utils/platformPresentation.ts";
import type { AccountType } from "../src/types/auth.ts";

const read = (file: string) => fs.readFileSync(new URL(`../src/${file}`, import.meta.url), "utf8");
const both = [permissions.platformActivityRead, permissions.platformAuditRead];
const require = createRequire(import.meta.url);

test("journal tab and sidebar landing selection follow independent permissions", () => {
  assert.deepEqual(platformJournalTabs(null), []);
  assert.deepEqual(platformJournalTabs({ accountType: "PlatformAdmin", permissions: [] }), []);
  for (const permission of both) {
    const tabs = platformJournalTabs({ accountType: "PlatformAdmin", permissions: [permission] });
    assert.equal(tabs.length, 1);
    assert.equal(tabs[0].permission, permission);
  }
  assert.deepEqual(platformJournalTabs({ accountType: "PlatformAdmin", permissions: both }).map(x => x.path), Object.values(platformJournalRoutes));
  for (const accountType of ["Student", "Teacher", "CenterManager"] as const) {
    assert.deepEqual(platformJournalTabs({ accountType, permissions: both }), []);
  }
});

function renderJournal(accountType: AccountType, granted: string[], path: string) {
  const code = ts.transpileModule(read("pages/PlatformJournalPage.tsx"), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX, esModuleInterop: true },
  }).outputText;
  const exports: Record<string, React.ComponentType> = {};
  vm.runInNewContext(code, { exports, require: (name: string) => {
    if (name === "../stores/authStore") return { useAuthStore: (select: (state: unknown) => unknown) => select({ user: { accountType, permissions: granted } }) };
    if (name === "../utils/platformJournalNavigation") return { platformJournalTabs };
    if (name === "react-router-dom") return {
      useLocation: () => ({ pathname: path }),
      Link: ({ to, ...props }: { to: string }) => React.createElement("a", { href: to, ...props }),
      Outlet: () => React.createElement("div", { "data-route-outlet": "true" }),
    };
    return require(name);
  } });
  return renderToStaticMarkup(React.createElement(exports.PlatformJournalPage));
}

test("actual journal shell renders URL-driven selected tab without fetching either data source", () => {
  for (const path of Object.values(platformJournalRoutes)) {
    const html = renderJournal("PlatformAdmin", both, path);
    assert.equal((html.match(/<h1/g) ?? []).length, 1);
    assert.equal((html.match(/<a /g) ?? []).length, 2);
    assert.equal((html.match(/aria-current="page"/g) ?? []).length, 1);
    assert.match(html, new RegExp(`href="${path}" aria-current="page"`));
    assert.match(html, /data-route-outlet="true"/);
  }
});

test("actual journal shell never renders a tab for a missing permission or wrong actor", () => {
  const html = renderJournal("PlatformAdmin", [permissions.platformAuditRead], platformJournalRoutes.audit);
  assert.match(html, /Kiểm toán chi tiết/);
  assert.doesNotMatch(html, /Tổng quan/);
  assert.equal((renderJournal("Teacher", both, platformJournalRoutes.audit).match(/<a /g) ?? []).length, 0);
});

test("new and legacy journal URLs retain their own PlatformAdmin route guard", () => {
  const app = read("App.tsx");
  const ast = ts.createSourceFile("App.tsx", app, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const guardedPaths = new Map<string, string[]>();
  function visit(node: ts.Node, guards: string[] = []) {
    if (ts.isJsxElement(node) || ts.isJsxSelfClosingElement(node)) {
      const opening = ts.isJsxElement(node) ? node.openingElement : node;
      if (opening.tagName.getText(ast) === "Route") {
        const attributes = opening.attributes.properties;
        const element = attributes.find(x => ts.isJsxAttribute(x) && x.name.getText(ast) === "element");
        if (element?.getText(ast).includes("PermissionRoute")) guards = [...guards, element.getText(ast)];
        const path = attributes.find(x => ts.isJsxAttribute(x) && x.name.getText(ast) === "path");
        if (path && ts.isJsxAttribute(path) && path.initializer && ts.isStringLiteral(path.initializer)) {
          guardedPaths.set(path.initializer.text, guards);
        }
      }
    }
    ts.forEachChild(node, child => visit(child, guards));
  }
  visit(ast);
  for (const [path, permission] of [
    [platformJournalRoutes.overview, "platformActivityRead"], ["/quan-tri-nen-tang/hoat-dong", "platformActivityRead"],
    [platformJournalRoutes.audit, "platformAuditRead"], ["/quan-tri-nen-tang/nhat-ky", "platformAuditRead"],
  ]) {
    assert.ok(guardedPaths.get(path)?.some(x => x.includes(`allOf={[permissions.${permission}]}`) && x.includes('accountTypes={["PlatformAdmin"]}')), path);
  }
  assert.match(app, /path="\/quan-tri-nen-tang\/hoat-dong" element=\{<Navigate to="\/quan-tri-nen-tang\/nhat-ky\/tong-quan" replace/);
  assert.match(app, /path="\/quan-tri-nen-tang\/nhat-ky" element=\{<Navigate to="\/quan-tri-nen-tang\/nhat-ky\/kiem-toan" replace/);
});

test("activity permission cannot grant audit access and vice versa", () => {
  for (const permission of both) {
    const user = { accountType: "PlatformAdmin" as const, permissions: [permission] };
    assert.equal(canAccess(user, { accountTypes: ["PlatformAdmin"], allOf: [permission] }), true);
    assert.equal(canAccess(user, { accountTypes: ["PlatformAdmin"], allOf: both }), false);
    assert.equal(canAccess({ ...user, accountType: "CenterManager" }, { accountTypes: ["PlatformAdmin"], allOf: [permission] }), false);
  }
});

test("Admin has one journal menu and one content gutter without width caps", () => {
  const layout = read("layouts/PlatformLayout.tsx");
  assert.equal((layout.match(/<span>Nhật ký<\/span>/g) ?? []).length, 1);
  assert.doesNotMatch(layout, /<span>Nhật ký (?:kiểm toán|hoạt động)<\/span>/);
  assert.match(layout, /to=\{journalTabs\[0\].path\}/);
  assert.match(layout, /<main className="min-w-0 flex-1 p-4 sm:p-6"/);
  for (const file of ["pages/PlatformCentersPage.tsx", "pages/PlatformAuditLogsPage.tsx", "pages/PlatformJournalPage.tsx"]) {
    assert.match(read(file), /w-full min-w-0 space-y-6/);
    assert.doesNotMatch(read(file), /max-w-7xl/);
  }
  assert.match(read("pages/NotificationsPage.tsx"), /user\?\.accountType === "PlatformAdmin" \? "" : "mx-auto max-w-\[1500px\] p-4 sm:p-6 lg:p-8"/);
  assert.match(read("pages/ActivityPage.tsx"), /embedded \? "" : "mx-auto max-w-\[1500px\] p-4 sm:p-6 lg:p-8"/);
});

test("audit list uses readable labels, keeps codes in detail and horizontal scroll within table", () => {
  assert.equal(auditActionLabel("PlatformDefaultPermissionsGranted"), "Bổ sung quyền mặc định nền tảng");
  assert.equal(auditActionLabel("FutureLongInternalActionName"), "Cập nhật quản trị");
  assert.equal(auditTargetLabel("Role"), "Vai trò");
  assert.equal(auditTargetLabel("InternalSecretType"), "Tài nguyên quản trị");
  const source = read("pages/PlatformAuditLogsPage.tsx");
  assert.match(source, /min-w-\[1100px\] table-fixed/);
  assert.match(source, /role="region" aria-label="Bảng kiểm toán/);
  assert.match(source, /auditActionLabel\(audit.actionType\)/);
  assert.match(source, /<p className="break-all font-mono text-xs text-slate-500">\{selectedAudit.actionType\}/);
  assert.doesNotMatch(source, /\{audit.actionType\}<\/span>|min-w-\[190px\]/);
});
