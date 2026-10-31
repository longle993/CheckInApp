import { chromium } from "file:///C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs";
import fs from "node:fs/promises";
import assert from "node:assert/strict";
const root = new URL("../", import.meta.url);
const fixture = JSON.parse(
  await fs.readFile(
    new URL(".local/mobile-uat/credentials-and-fixture.json", root),
    "utf8",
  ),
);
let browser;
try {
  browser = await chromium.launch();
} catch {
  browser = await chromium.launch({ channel: "chrome" });
}
try {
  const page = await browser.newPage({
    viewport: { width: 900, height: 1100 },
  });
  await page.goto("http://127.0.0.1:8765/cards.html", {
    waitUntil: "networkidle",
  });
  await page.waitForFunction(() =>
    [...document.querySelectorAll(".barcode")].every(
      (svg) => svg.querySelectorAll("rect").length > 0,
    ),
  );
  await page.addScriptTag({
    path: new URL(
      "apps/web/node_modules/@zxing/library/umd/index.min.js",
      root,
    ).pathname.replace(/^\/(\w:)/, "$1"),
  });
  const decoded = [];
  for (let i = 0; i < fixture.cards.length; i++) {
    for (const kind of ["qr", "barcode"]) {
      const element = page
        .locator(".card")
        .nth(i)
        .locator(kind === "qr" ? ".qr svg" : ".barcode");
      const png = await element.screenshot({ scale: "css" });
      const result = await page
        .evaluate(
          async ({ data, kind }) => {
            const hints = new Map([
              [ZXing.DecodeHintType.TRY_HARDER, true],
              [
                ZXing.DecodeHintType.POSSIBLE_FORMATS,
                [
                  kind === "qr"
                    ? ZXing.BarcodeFormat.QR_CODE
                    : ZXing.BarcodeFormat.CODE_128,
                ],
              ],
            ]);
            const reader = new ZXing.BrowserMultiFormatReader(hints);
            const result = await reader.decodeFromImageUrl(
              "data:image/png;base64," + data,
            );
            return result.getText();
          },
          { data: png.toString("base64"), kind },
        )
        .catch((error) => {
          throw new Error(`Card ${i + 1} ${kind}: ${error.message}`);
        });
      assert.equal(
        result,
        fixture.cards[i].credential,
        `Card ${i + 1} ${kind}`,
      );
      decoded.push({ card: i + 1, kind, decoded: true });
    }
  }
  await page.screenshot({
    path: new URL(".local/mobile-uat/cards-preview.png", root).pathname.replace(
      /^\/(\w:)/,
      "$1",
    ),
    fullPage: true,
  });
  await fs.writeFile(
    new URL(".local/mobile-uat/card-qa.json", root),
    JSON.stringify(
      {
        decoded,
        note: "Software image decode only; not a physical camera test.",
      },
      null,
      2,
    ),
  );
  console.log(
    JSON.stringify({
      cards: fixture.cards.length,
      decodedImages: decoded.length,
    }),
  );
} finally {
  await browser.close();
}
