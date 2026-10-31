import { chromium } from "file:///C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs";
import fs from "node:fs/promises";
import assert from "node:assert/strict";
const credentials = JSON.parse(
  (
    await fs.readFile(
      process.env.BOOTSTRAP_FILE ||
        new URL(
          "../backend/CheckInApp.Api/.local/bootstrap-admin.json",
          import.meta.url,
        ),
      "utf8",
    )
  ).replace(/^\uFEFF/, ""),
);
const out = new URL("../.local/ui-qa/", import.meta.url);
await fs.mkdir(out, { recursive: true });
let browser;
try {
  browser = await chromium.launch();
} catch {
  browser = await chromium.launch({ channel: "chrome" });
}
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } }),
  errors = [];
page.on("pageerror", (e) => errors.push(e.message));
const webUrl = process.env.WEB_URL || "http://127.0.0.1:5173";
await page.goto(webUrl, { waitUntil: "networkidle" });
await page.getByLabel("Email").fill(credentials.email);
await page.getByLabel("Mật khẩu").fill(credentials.password);
await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
await page
  .getByRole("heading", { name: "Điểm danh hôm nay", exact: true })
  .waitFor();
const results = [];
for (const width of [1440, 768, 375]) {
  await page.setViewportSize({ width, height: 1000 });
  for (const route of [
    "tongquan",
    "lop",
    "hocsinh",
    "lich",
    "baocao",
    "glv",
    "namhoc",
    "duyet",
    "quyen",
    "quet",
  ]) {
    await page.evaluate((r) => (location.hash = r), route);
    await page.waitForTimeout(450);
    await page.locator(".content h1").waitFor();
    await page.waitForTimeout(250);
    const issue = await page.evaluate(() => ({
      overflow: document.documentElement.scrollWidth > innerWidth + 2,
      title: document.querySelector("h1")?.textContent,
      errors: [...document.querySelectorAll("[role=alert]")].map(
        (e) => e.textContent,
      ),
    }));
    results.push({ route, width, ...issue });
    if (["tongquan", "quet", "baocao"].includes(route))
      await page.screenshot({
        path: new URL(`${route}-${width}.png`, out).pathname.replace(
          /^\/(\w:)/,
          "$1",
        ),
        fullPage: true,
      });
  }
}
await page.setViewportSize({ width: 1440, height: 1000 });
await page.goto(webUrl + "/#lop", { waitUntil: "networkidle" });
await page.getByRole("button", { name: "Tạo lớp học", exact: true }).click();
await page.getByRole("dialog").waitFor();
await page.getByLabel("Tên lớp").fill("UI smoke " + Date.now());
await page.getByLabel("Mã lớp").fill("UI" + Date.now());
await page.getByRole("button", { name: "Lưu", exact: true }).click();
await page.getByRole("dialog").waitFor({ state: "hidden" });
await fs.writeFile(
  new URL("results.json", out),
  JSON.stringify({ errors, results }, null, 2),
);
console.log(
  JSON.stringify({
    pageErrors: errors,
    overflows: results.filter((x) => x.overflow),
    apiErrors: results.filter((x) => x.errors.length),
    screens: results.length,
    createClass: true,
  }),
);
assert.equal(errors.length, 0);
assert.equal(results.filter((x) => x.overflow).length, 0);
assert.equal(results.filter((x) => x.errors.length).length, 0);
const resized = page.waitForResponse(r => r.url().includes("/catalog/classes?") && r.url().includes("pageSize=10"));
await page.getByLabel("Số dòng mỗi trang", {exact:true}).selectOption("10");
assert.ok((await (await resized).json()).items.length <= 10);
console.log("PASS pagination: pageSize=10 sent to API and row limit respected");
await browser.close();
