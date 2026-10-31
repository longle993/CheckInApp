import fs from "node:fs/promises";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
const root = fileURLToPath(new URL("../", import.meta.url));
const requireWeb = createRequire(root + "apps/web/package.json");
const JsBarcode = requireWeb("jsbarcode");
export function barcodeSvg(value, index) {
  const target = {};
  JsBarcode(target, value, { format: "CODE128", displayValue: false });
  const bits = target.encodings.map((x) => x.data).join("");
  const width = bits.length + 32;
  let bars = "";
  for (let i = 0; i < bits.length; ) {
    if (bits[i] !== "1") {
      i++;
      continue;
    }
    const start = i;
    while (bits[i] === "1") i++;
    bars += `<rect x="${16 + start}" y="16" width="${i - start}" height="72" fill="#000"/>`;
  }
  return `<svg class="barcode" id="barcode${index}" xmlns="http://www.w3.org/2000/svg" width="${width}" height="104" viewBox="0 0 ${width} 104" role="img" aria-label="Code 128"><rect width="${width}" height="104" fill="#fff"/>${bars}</svg>`;
}
export async function makeStaticCards(cards) {
  const path = root + ".local/android-download/cards.html";
  let html = await fs.readFile(path, "utf8");
  html = html.replace(
    /<svg class="barcode" id="barcode(\d+)"><\/svg>/g,
    (_, index) => barcodeSvg(cards[Number(index)].credential, Number(index)),
  );
  html = html.replace(/<script>[\s\S]*?<\/script>/g, "");
  await fs.writeFile(path, html);
}
if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  const fixture = JSON.parse(
    await fs.readFile(
      root + ".local/mobile-uat/credentials-and-fixture.json",
      "utf8",
    ),
  );
  await makeStaticCards(fixture.cards);
}
