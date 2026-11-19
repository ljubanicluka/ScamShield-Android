#!/usr/bin/env node
"use strict";

const fs = require("node:fs");
const vm = require("node:vm");
const path = process.argv[2] || "app/src/main/assets/index.html";

if (!fs.existsSync(path)) {
  console.error("Bundled HTML file not found: " + path);
  process.exit(1);
}

const html = fs.readFileSync(path, "utf8");
const scripts = [...html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)];
let inlineCount = 0;

for (const match of scripts) {
  const attrs = match[1] || "";
  const body = match[2] || "";
  if (/\bsrc\s*=/.test(attrs) || !body.trim()) continue;
  inlineCount += 1;
  try {
    new vm.Script(body, { filename: path + "#inline-script-" + inlineCount });
  } catch (error) {
    console.error("Inline JavaScript syntax error:", error.message);
    process.exit(1);
  }
}

if (inlineCount === 0) {
  console.error("No inline application script was found.");
  process.exit(1);
}

// Discover every action rendered in markup and require a matching delegated handler.
const renderedActions = new Set(
  [...html.matchAll(/\bdata-a\s*=\s*["']?([A-Za-z][A-Za-z0-9_-]*)/g)]
    .map((match) => match[1])
);
const handledActions = new Set(
  [...html.matchAll(/\ba\s*===\s*["']([A-Za-z][A-Za-z0-9_-]*)["']/g)]
    .map((match) => match[1])
);

if (renderedActions.size === 0) {
  console.error("No data-a actions were found in the interface.");
  process.exit(1);
}

for (const action of renderedActions) {
  if (!handledActions.has(action)) {
    console.error("Rendered action has no click-handler branch: data-a=" + action);
    process.exit(1);
  }
}

// Android bundles the decoder locally; remote CDN script loading is blocked by the WebView.
if (!/<script\b[^>]*\bsrc\s*=\s*["']jsQR\.js["']/i.test(html)) {
  console.error("Bundled local jsQR.js script reference is missing.");
  process.exit(1);
}
if (html.toLowerCase().includes("cdn.jsdelivr.net/npm/jsqr")) {
  console.error("An external jsQR CDN reference remains in the Android asset.");
  process.exit(1);
}

for (const hook of [
  "window.onNativeQrScanResult",
  "window.onNativeQrImageResult",
  "ScamShieldNative.launchQrScanner",
  "ScamShieldNative.pickQrImage"
]) {
  if (!html.includes(hook)) {
    console.error("Missing native QR integration hook: " + hook);
    process.exit(1);
  }
}

if (/target\s*=\s*["_']?_blank/i.test(html)) {
  console.error('Found target="_blank" despite multiple WebView windows being disabled.');
  process.exit(1);
}

console.log("OK: " + inlineCount + " inline script(s), required action handlers, native QR hooks, and external-link policy.");
