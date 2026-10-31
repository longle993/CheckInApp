import fs from "node:fs/promises";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { chromium } from "file:///C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs";
const root = new URL("../", import.meta.url);
const info = JSON.parse(
  await fs.readFile(
    new URL(".local/android-download/build-info.json", root),
    "utf8",
  ),
);
const fixture = JSON.parse(
  await fs.readFile(
    new URL(".local/mobile-uat/credentials-and-fixture.json", root),
    "utf8",
  ),
);
const base = new URL(info.apiUrl);
const origin = new URL(base);
origin.port = "8765";
origin.pathname = "/";
const response = await fetch(new URL(info.file, origin));
assert.equal(response.status, 200);
assert.equal(
  createHash("sha256")
    .update(Buffer.from(await response.arrayBuffer()))
    .digest("hex"),
  info.sha256,
);
assert.equal((await fetch(new URL("signing/password", origin))).status, 404);
assert.equal((await fetch(new URL("secrets.json", origin))).status, 404);
const healthUrl = new URL(base);
healthUrl.pathname = "/health";
assert.equal((await (await fetch(healthUrl)).json()).status, "ok");
const login = await fetch(info.apiUrl + "/auth/login", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    email: fixture.email,
    password: fixture.password,
    client: "mobile",
  }),
});
assert.equal(login.status, 200);
const auth = await login.json();
const day = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Asia/Ho_Chi_Minh",
}).format(new Date());
const sessionResponse = await fetch(
  info.apiUrl + `/attendance-sessions?from=${day}&until=${day}`,
  { headers: { Authorization: "Bearer " + auth.accessToken } },
);
assert.equal(sessionResponse.status, 200);
const sessions = (await sessionResponse.json()).items;
assert.equal(sessions.length, 1);
assert.equal(sessions[0].classRoomId, fixture.classes[0].id);
assert.equal(sessions[0].expected, 3);
assert.equal(
  sessions[0].present,
  0,
  "Thẻ UAT phải còn mới để người dùng quét trước.",
);
let browser;
try {
  browser = await chromium.launch();
} catch {
  browser = await chromium.launch({ channel: "chrome" });
}
try {
  const page = await browser.newPage({
    viewport: { width: 800, height: 1000 },
  });
  await page.goto(origin.toString(), { waitUntil: "networkidle" });
  await page.getByRole("link", { name: /Tải APK/ }).waitFor();
  await page
    .locator("svg")
    .screenshot({
      path: new URL(
        ".local/android-download/install-qr.png",
        root,
      ).pathname.replace(/^\/(\w:)/, "$1"),
    });
  await page.screenshot({
    path: new URL(
      ".local/android-preview/download-page.png",
      root,
    ).pathname.replace(/^\/(\w:)/, "$1"),
    fullPage: true,
  });
} finally {
  await browser.close();
}
await fs.writeFile(
  new URL(".local/android-preview/delivery-qa.json", root),
  JSON.stringify(
    {
      apkHttp: 200,
      downloadHashMatches: true,
      privateFilesNotServed: true,
      apiLanHealth: true,
      teacherLoginViaLan: true,
      teacherClassScope: true,
      roster: { expected: 3, present: 0 },
      cameraTest: "Pending physical device",
    },
    null,
    2,
  ),
);
console.log(
  JSON.stringify({
    downloadHashMatches: true,
    privateFilesNotServed: true,
    apiLanHealth: true,
    teacherLoginViaLan: true,
    teacherClassScope: true,
    freshStudents: 3,
  }),
);
