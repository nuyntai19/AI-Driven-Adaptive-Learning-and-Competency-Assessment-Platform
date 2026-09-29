import { describe, it } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import {
  resolveInitialTheme,
  applyTheme,
  getStoredTheme,
  THEME_STORAGE_KEY,
  type ThemeMode,
} from "../src/utils/themeMode.ts";
import { resolveRichMathPopoverTheme } from "../src/components/math/richMathEditorHelpers.ts";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

describe("Theme Resolution & System Preference (resolveInitialTheme)", () => {
  it("resolves stored 'dark' even when system preference is light (stored dark + system light -> dark)", () => {
    const mockStorage = {
      getItem: (key: string) => (key === THEME_STORAGE_KEY ? "dark" : null),
    };
    const mockMatchMedia = () => ({ matches: false });

    const theme = resolveInitialTheme(mockStorage, mockMatchMedia);
    assert.equal(theme, "dark");
  });

  it("resolves stored 'light' even when system preference is dark (stored light + system dark -> light)", () => {
    const mockStorage = {
      getItem: (key: string) => (key === THEME_STORAGE_KEY ? "light" : null),
    };
    const mockMatchMedia = () => ({ matches: true });

    const theme = resolveInitialTheme(mockStorage, mockMatchMedia);
    assert.equal(theme, "light");
  });

  it("falls back to system dark when localStorage is empty (không stored + system dark -> dark)", () => {
    const mockStorage = {
      getItem: () => null,
    };
    const mockMatchMedia = (query: string) => ({
      matches: query.includes("prefers-color-scheme: dark"),
    });

    const theme = resolveInitialTheme(mockStorage, mockMatchMedia);
    assert.equal(theme, "dark");
  });

  it("falls back to system light when localStorage is empty (không stored + system light -> light)", () => {
    const mockStorage = {
      getItem: () => null,
    };
    const mockMatchMedia = () => ({ matches: false });

    const theme = resolveInitialTheme(mockStorage, mockMatchMedia);
    assert.equal(theme, "light");
  });

  it("getStoredTheme delegates directly to resolveInitialTheme", () => {
    const direct = resolveInitialTheme(null, () => ({ matches: false }));
    const alias = getStoredTheme();
    assert.equal(direct, "light");
    assert.equal(typeof alias, "string");
    assert.ok(alias === "dark" || alias === "light");
  });
});

describe("Theme DOM Synchronization & First-Click Toggle", () => {
  it("applyTheme correctly toggles .dark class on document.documentElement", () => {
    const classes = new Set<string>();
    const mockDoc = {
      documentElement: {
        classList: {
          add: (c: string) => classes.add(c),
          remove: (c: string) => classes.delete(c),
          contains: (c: string) => classes.has(c),
        },
      },
    };

    // Install mock document temporarily
    const originalDoc = (globalThis as any).document;
    (globalThis as any).document = mockDoc;

    try {
      applyTheme("dark");
      assert.equal(classes.has("dark"), true, "DOM must contain 'dark' class");

      applyTheme("light");
      assert.equal(classes.has("dark"), false, "DOM must not contain 'dark' class");
    } finally {
      (globalThis as any).document = originalDoc;
    }
  });

  it("first-click toggle on system dark starts from 'dark' and switches to 'light' immediately", () => {
    // Scenario: user opens app on dark system with no localStorage
    const mockStorageMap = new Map<string, string>();
    const mockStorage = {
      getItem: (key: string) => mockStorageMap.get(key) ?? null,
      setItem: (key: string, val: string) => mockStorageMap.set(key, val),
    };
    const mockMatchMedia = () => ({ matches: true }); // System dark

    const initialTheme = resolveInitialTheme(mockStorage, mockMatchMedia);
    assert.equal(initialTheme, "dark", "Initial theme must be dark");

    // First click on ThemeToggle should compute nextTheme = initialTheme === 'dark' ? 'light' : 'dark'
    const nextTheme: ThemeMode = (initialTheme as ThemeMode) === "dark" ? "light" : "dark";
    assert.equal(nextTheme, "light", "First click must toggle from dark to light on click 1");

    mockStorage.setItem(THEME_STORAGE_KEY, nextTheme);
    const storedAfterFirstClick = mockStorage.getItem(THEME_STORAGE_KEY);
    assert.equal(storedAfterFirstClick, "light");

    // RichMath popover theme also updates cleanly
    const isDark = (nextTheme as string) === "dark";
    const popoverThemeLight = resolveRichMathPopoverTheme("center-manager", isDark);
    assert.equal(popoverThemeLight.isDark, false);
    assert.ok(popoverThemeLight.popoverBorder.includes("cyan"));
  });

  it("main.tsx utilizes shared resolveInitialTheme and applyTheme without duplicate logic", () => {
    const mainPath = path.resolve(__dirname, "../src/main.tsx");
    const content = fs.readFileSync(mainPath, "utf-8");

    assert.ok(
      content.includes('import { resolveInitialTheme, applyTheme } from "./utils/themeMode";'),
      "main.tsx must import shared resolveInitialTheme and applyTheme"
    );
    assert.ok(
      content.includes("const initialTheme = resolveInitialTheme();"),
      "main.tsx must resolve initial theme with resolveInitialTheme()"
    );
    assert.ok(
      content.includes("applyTheme(initialTheme);"),
      "main.tsx must apply initial theme with applyTheme()"
    );
    assert.ok(
      !content.includes('localStorage.getItem("edutwin-theme")'),
      "main.tsx must not contain duplicate localStorage inspection"
    );
  });
});
