import fs from "node:fs/promises";
import { createHash } from "node:crypto";
import { fileURLToPath } from "node:url";
import { createRequire } from "node:module";
import assert from "node:assert/strict";
const root = fileURLToPath(new URL("../", import.meta.url));
const source = root + ".local/android-preview/";
const target = root + ".local/android-download/";
const verification = await fs.readFile(source + "apk-verification.txt", "utf8");
assert.match(verification, /Verified using v2 scheme.*true/);
const badging = await fs.readFile(source + "apk-info.txt", "utf8");
assert.ok(
  badging.includes("com.checkinapp.mobile.preview") &&
    !badging.includes("application-debuggable"),
);
const apk = await fs.readFile(source + "CheckInApp-LAN.apk");
const sha256 = createHash("sha256").update(apk).digest("hex");
const apiUrl =
  process.env.EXPO_PUBLIC_API_URL ||
  JSON.parse(
    (await fs.readFile(source + "build-context.json", "utf8")).replace(
      /^\uFEFF/,
      "",
    ),
  ).apiUrl;
const artifactUrl = new URL(apiUrl);
artifactUrl.port = "8765";
const origin = artifactUrl.origin;
const metadata = {
  file: "CheckInApp-LAN.apk",
  sizeBytes: apk.length,
  sha256,
  apiUrl,
  builtAt: new Date().toISOString(),
  package: "com.checkinapp.mobile.preview",
  minAndroid: "7.0",
  architectures: ["arm64-v8a", "armeabi-v7a"],
};
await fs.mkdir(target, { recursive: true });
await fs.copyFile(source + "CheckInApp-LAN.apk", target + "CheckInApp-LAN.apk");
await fs.writeFile(
  target + "SHA256SUMS.txt",
  `${sha256}  CheckInApp-LAN.apk\n`,
);
await fs.writeFile(
  target + "build-info.json",
  JSON.stringify(metadata, null, 2),
);
const requireWeb = createRequire(root + "apps/web/package.json");
const React = requireWeb("react");
const { renderToStaticMarkup } = requireWeb("react-dom/server");
const { QRCodeSVG } = requireWeb("qrcode.react");
const qr = renderToStaticMarkup(
  React.createElement(QRCodeSVG, {
    value: origin + "/CheckInApp-LAN.apk",
    size: 200,
    marginSize: 4,
  }),
);
await fs.writeFile(
  target + "index.html",
  `<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>CheckInApp LAN</title><style>body{font:16px system-ui;margin:32px auto;padding:16px;max-width:640px;color:#222;background:#f4f4f6}main{background:white;padding:24px;border-radius:16px}a{color:#4338ca}.button{display:inline-block;padding:14px 20px;background:#4f46e5;color:white;border-radius:8px;text-decoration:none}code{overflow-wrap:anywhere;font-size:12px}small{color:#666}h1{font-size:26px}</style><main><h1>CheckInApp LAN</h1><p>Bản cài thử Android, có bundle sẵn; không cần Metro hoặc Expo Go.</p><p><a class="button" href="CheckInApp-LAN.apk">Tải APK · ${(apk.length / 1048576).toFixed(1)} MB</a></p>${qr}<p>Quét mã trên bằng camera điện thoại để tải APK.</p><p>API: <a href="${apiUrl.replace("/api/v1", "/health")}">${apiUrl.replace("/api/v1", "/health")}</a></p><p><a href="cards.html">Mở / in thẻ kiểm thử QR và Barcode</a></p><p>Android 7.0 trở lên, ARM64 hoặc ARM32. Hai thiết bị dùng cùng Wi-Fi. Nếu được hỏi, cho phép trình duyệt cài ứng dụng từ tệp này.</p><small>SHA-256</small><p><code>${sha256}</code></p><p><small>APK ký bằng khóa thử riêng trên máy này. Không có tài khoản hoặc mật khẩu được nhúng trong APK/trang tải.</small></p></main></html>`,
);
console.log(JSON.stringify(metadata));
