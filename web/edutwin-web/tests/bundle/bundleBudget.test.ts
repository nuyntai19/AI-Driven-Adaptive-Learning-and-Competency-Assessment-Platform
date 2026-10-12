import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const distDir = fileURLToPath(new URL("../../dist", import.meta.url));
const distAssetsDir = join(distDir, "assets");
const indexHtmlPath = join(distDir, "index.html");

function getEntryAssetName(indexHtml: string): string {
  const scripts = indexHtml.match(/<script\b[^>]*>/gi) ?? [];
  const entries = scripts.filter((tag) => /\btype\s*=\s*(["'])module\1/i.test(tag));
  assert.equal(entries.length, 1, "Production HTML must identify exactly one module entry");
  const source = /\bsrc\s*=\s*(["'])(.*?)\1/i.exec(entries[0])?.[2];
  assert.ok(source, "Production module entry must reference an asset");
  const url = new URL(source, "http://bundle.local");
  assert.equal(url.origin, "http://bundle.local", "Module entry must be local");
  assert.match(url.pathname, /^\/assets\/[\w.-]+\.js$/, "Module entry must be a JavaScript asset");
  return url.pathname.slice("/assets/".length);
}

test("Entry selection follows index.html rather than the first index-named chunk", () => {
  assert.equal(getEntryAssetName('<script type="module" src="/assets/index-real.js"></script>'), "index-real.js");
  assert.equal(getEntryAssetName("<script src='/assets/index--hash.js' crossorigin type='module'></script>"), "index--hash.js");
});

test("Missing or ambiguous module entries fail closed", () => {
  assert.throws(() => getEntryAssetName('<link rel="modulepreload" href="/assets/index-other.js">'));
  assert.throws(() => getEntryAssetName('<script type="module" src="/assets/a.js"></script><script type="module" src="/assets/b.js"></script>'));
});

function getAssetFiles(): { name: string; sizeKb: number }[] {
  assert.ok(
    existsSync(distAssetsDir),
    `Production build directory (${distAssetsDir}) does not exist. Run 'npm run build' before executing bundle budget tests.`
  );
  assert.ok(
    existsSync(indexHtmlPath),
    `Production index.html (${indexHtmlPath}) does not exist. A clean build artifact is required.`
  );

  return readdirSync(distAssetsDir)
    .filter((file) => file.endsWith(".js"))
    .map((file) => {
      const stats = statSync(join(distAssetsDir, file));
      return {
        name: file,
        sizeKb: Number((stats.size / 1024).toFixed(2)),
      };
    });
}

test("Bundle Budget Regression: Verifies initial preload budget, deferred code-split routes, and on-demand vendor chunks", () => {
  const assets = getAssetFiles();
  assert.ok(assets.length > 0, "dist/assets must contain generated JavaScript chunks");

  const indexHtml = readFileSync(indexHtmlPath, "utf8");

  // 1. Initial critical preload budget check
  // index.html must only preload critical entry chunks (index, react, query, katex)
  assert.doesNotMatch(
    indexHtml,
    /modulepreload[^>]*vendor-mathlive/,
    "Heavy library vendor-mathlive MUST NOT be in initial modulepreload"
  );
  assert.doesNotMatch(
    indexHtml,
    /modulepreload[^>]*vendor-xlsx/,
    "Heavy library vendor-xlsx MUST NOT be in initial modulepreload"
  );
  assert.doesNotMatch(
    indexHtml,
    /modulepreload[^>]*vendor-recharts/,
    "Heavy library vendor-recharts MUST NOT be in initial modulepreload"
  );

  // 2. Main entry chunk: must stay strictly under 150 KB (currently ~109 KB)
  const entryName = getEntryAssetName(indexHtml);
  const mainBundle = assets.find((a) => a.name === entryName);
  assert.ok(mainBundle, `Module entry ${entryName} referenced by index.html must exist in dist/assets`);
  assert.ok(
    mainBundle.sizeKb <= 150,
    `Main entry chunk size ${mainBundle.sizeKb} KB exceeds budget limit of 150 KB`
  );

  // 3. Deferred route chunks: verify heavy libraries are NOT baked into route chunks
  const learningPlayer = assets.find((a) => a.name.startsWith("LearningPlayerPage-"));
  assert.ok(learningPlayer, "LearningPlayerPage chunk must exist");
  assert.ok(
    learningPlayer.sizeKb <= 200,
    `LearningPlayerPage size ${learningPlayer.sizeKb} KB exceeds budget limit of 200 KB (MathLive must be dynamic)`
  );

  const studentDashboard = assets.find((a) => a.name.startsWith("StudentDashboardPage-"));
  assert.ok(studentDashboard, "StudentDashboardPage chunk must exist");
  assert.ok(
    studentDashboard.sizeKb <= 60,
    `StudentDashboardPage size ${studentDashboard.sizeKb} KB exceeds budget limit of 60 KB (Recharts must be lazy)`
  );

  const teacherMgmt = assets.find((a) => a.name.startsWith("TeacherStudentManagementView-"));
  assert.ok(teacherMgmt, "TeacherStudentManagementView chunk must exist");
  assert.ok(
    teacherMgmt.sizeKb <= 80,
    `TeacherStudentManagementView size ${teacherMgmt.sizeKb} KB exceeds budget limit of 80 KB (XLSX must be dynamic)`
  );

  // 4. On-demand vendor chunks: isolated properly into dedicated chunks
  const mathliveChunk = assets.find((a) => a.name.startsWith("vendor-mathlive-"));
  assert.ok(mathliveChunk, "MathLive must be isolated in vendor-mathlive chunk");

  const xlsxChunk = assets.find((a) => a.name.startsWith("vendor-xlsx-"));
  assert.ok(xlsxChunk, "XLSX must be isolated in vendor-xlsx chunk");

  const rechartsChunk = assets.find((a) => a.name.startsWith("vendor-recharts-"));
  assert.ok(rechartsChunk, "Recharts must be isolated in vendor-recharts chunk");
});
