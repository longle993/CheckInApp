import fs from "node:fs/promises";
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import { randomBytes } from "node:crypto";
import assert from "node:assert/strict";

const root = fileURLToPath(new URL("../", import.meta.url));
const output = root + ".local/mobile-uat/";
await fs.mkdir(output, { recursive: true });
await fs.mkdir(root + ".local/android-download/", { recursive: true });
const admin = JSON.parse(
  (
    await fs.readFile(root + ".local/docker-integration/secrets.json", "utf8")
  ).replace(/^\uFEFF/, ""),
);
const base = "http://127.0.0.1:5082/api/v1";
let token;
async function api(path, body, bearer = token) {
  const response = await fetch(base + path, {
    method: body ? "POST" : "GET",
    headers: {
      "Content-Type": "application/json",
      ...(bearer ? { Authorization: "Bearer " + bearer } : {}),
    },
    ...(body ? { body: JSON.stringify(body) } : {}),
  });
  const data = response.status === 204 ? null : await response.json();
  assert.ok(response.ok, `${path}: ${response.status} ${data?.code || ""}`);
  return data;
}
const login = await api("/auth/login", {
  email: admin.email,
  password: admin.password,
  client: "mobile",
});
token = login.accessToken;
const today = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Asia/Ho_Chi_Minh",
}).format(new Date());
const until = new Date(Date.parse(today + "T12:00:00Z") + 7 * 86400000)
  .toISOString()
  .slice(0, 10);
const run = Date.now().toString(36);
const create = (kind, data) => api("/catalog/" + kind, data);
const year = await create("years", {
  name: "Mobile UAT " + run,
  startDate: today,
  endDate: until,
  isActive: false,
});
const classes = [];
for (const suffix of ["A", "B"])
  classes.push(
    await create("classes", {
      academicYearId: year.id,
      name: "Mobile UAT " + suffix + " " + run,
      code: "MU" + suffix + run,
    }),
  );
const labels = [
  "01 - Thử QR",
  "02 - Thử Barcode",
  "03 - Thử mất mạng",
  "04 - Thẻ lớp khác",
];
const cards = [];
for (let i = 0; i < labels.length; i++) {
  const student = await create("students", {
    classRoomId: classes[i === 3 ? 1 : 0].id,
    fullName: labels[i],
    saintName: "Giuse",
    dateOfBirth: "2016-01-01",
  });
  const credential = await api("/students/" + student.id + "/credential", {});
  cards.push({
    ...student,
    credential: credential.credential,
    label: labels[i],
    className: classes[i === 3 ? 1 : 0].name,
  });
}
const catechist = await create("catechists", {
  fullName: "Giáo lý viên Mobile UAT " + run,
});
await create("assignments", {
  classRoomId: classes[0].id,
  catechistId: catechist.id,
  from: today,
});
const user = {
  email: "mobile-" + run + "@example.test",
  password: "Ci!9" + randomBytes(12).toString("hex"),
  fullName: "GLV cài thử điện thoại",
};
const createdUser = await create("users", user);
const roles = await api("/catalog/roles");
await api("/users/" + createdUser.id + "/approve", {
  roleId: roles.items.find((x) => x.name === "Giáo lý viên").id,
  catechistId: catechist.id,
});
for (const c of classes)
  await create("schedules", {
    classRoomId: c.id,
    from: today,
    until,
    daysMask: 127,
    opensAt: "00:00",
    closesAt: "23:59",
  });
const sessions = await api(
  "/attendance-sessions?from=" + today + "&until=" + until,
);
const saved = {
  ...user,
  year,
  classes,
  cards,
  from: today,
  until,
  sessions: sessions.items.filter((x) =>
    classes.some((c) => c.id === x.classRoomId),
  ),
};
await fs.writeFile(
  output + "credentials-and-fixture.json",
  JSON.stringify(saved, null, 2),
);
await fs.writeFile(
  output + "login.txt",
  `Email: ${user.email}\nMật khẩu: ${user.password}\nNăm học: ${year.name}\nLớp: ${classes[0].name}\nLịch: ${today} đến ${until}\n`,
);
const requireWeb = createRequire(root + "apps/web/package.json");
const React = requireWeb("react");
const { renderToStaticMarkup } = requireWeb("react-dom/server");
const { QRCodeSVG } = requireWeb("qrcode.react");
const escape = (text) =>
  text.replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );
const barcodeScript = await fs.readFile(
  root + "apps/web/node_modules/jsbarcode/dist/JsBarcode.all.min.js",
  "utf8",
);
const html = `<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Thẻ thử CheckInApp</title><style>
*{box-sizing:border-box}body{font:16px Arial,sans-serif;background:#f4f4f6;color:#222;margin:24px}main{max-width:760px;margin:auto}.card{background:white;border:1px solid #ddd;border-radius:12px;padding:24px;margin:20px 0;break-inside:avoid}h1{font-size:24px}h2{margin:0 0 8px}small{color:#555}.codes{display:flex;align-items:center;gap:24px;flex-wrap:wrap}.qr{background:white;padding:12px}.barcode{max-width:100%;height:auto;shape-rendering:crispEdges}button{padding:12px;border:0;background:#4f46e5;color:white;border-radius:8px}@media print{body{background:white;margin:0}.card{width:180mm;max-width:100%;border:1px solid #aaa;border-radius:0;margin:6mm 0;padding:6mm}.screen{display:none}.codes{flex-wrap:nowrap;gap:6mm}.barcode{width:110mm;height:auto}}@page{size:A4;margin:12mm}
</style><main><div class="screen"><h1>Thẻ kiểm thử quét QR / Code 128</h1><p>Dữ liệu giả, dành riêng cho Mobile UAT. In tỷ lệ 100%, đen trắng, giữ nguyên viền trắng. Có thể quét trực tiếp từ màn hình máy tính.</p><button onclick="print()">In thẻ thử</button></div>${cards.map((c, i) => `<article class="card"><h2>${escape(c.label)}</h2><small>${escape(c.className)} · ${escape(c.code)}</small><div class="codes"><div class="qr">${renderToStaticMarkup(React.createElement(QRCodeSVG, { value: c.credential, size: 144, level: "M", marginSize: 4 }))}</div><svg class="barcode" id="barcode${i}"></svg></div></article>`).join("")}<script>${barcodeScript}</script><script>${cards.map((c, i) => `JsBarcode('#barcode${i}',${JSON.stringify(c.credential)},{format:'CODE128',width:1,height:72,displayValue:false,margin:16});`).join("")}</script></main></html>`;
await fs.writeFile(root + ".local/android-download/cards.html", html);
await (await import("./static-mobile-barcodes.mjs")).makeStaticCards(cards);
console.log(
  JSON.stringify({
    year: year.name,
    teacherEmail: user.email,
    class: classes[0].name,
    studentsInClass: 3,
    cards: cards.length,
    validUntil: until,
    fixtureFile: ".local/mobile-uat/credentials-and-fixture.json",
  }),
);
