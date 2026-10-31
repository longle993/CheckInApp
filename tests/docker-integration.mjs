import assert from "node:assert/strict";
import fs from "node:fs/promises";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../", import.meta.url));
const base = process.env.API_URL || "http://127.0.0.1:5082/api/v1";
const secretsPath =
  process.env.BOOTSTRAP_FILE ||
  fileURLToPath(
    new URL("../.local/docker-integration/secrets.json", import.meta.url),
  );
const secrets = JSON.parse(
  (await fs.readFile(secretsPath, "utf8")).replace(/^\uFEFF/, ""),
);
assert.equal(
  new URL(base).port,
  "5082",
  "Chỉ chạy trên API tích hợp cổng 5082.",
);
const composeArgs = [
  "compose",
  "-p",
  "checkinapp-integration",
  "--env-file",
  root + ".local/docker-integration/compose.env",
  "-f",
  root + "compose.yaml",
  "-f",
  root + "compose.integration.yaml",
];
const docker = (...args) =>
  execFileSync("docker", [...composeArgs, ...args], {
    encoding: "utf8",
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  }).trim();
const redis = (...args) =>
  docker(
    "exec",
    "-T",
    "redis",
    "sh",
    "-c",
    'export REDISCLI_AUTH="$REDIS_PASSWORD"; exec redis-cli "$@"',
    "sh",
    ...args,
  );
const sql = (query) =>
  docker(
    "exec",
    "-T",
    "sqlserver",
    "sh",
    "-c",
    'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd "$@"',
    "sh",
    "-S",
    "localhost",
    "-U",
    "sa",
    "-C",
    "-b",
    "-d",
    "CheckInAppIntegration",
    "-h",
    "-1",
    "-W",
    "-Q",
    "SET NOCOUNT ON; " + query,
  );
const day = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Asia/Ho_Chi_Minh",
}).format(new Date());
const shift = (n) =>
  new Date(Date.parse(day + "T12:00:00Z") + n * 86400000)
    .toISOString()
    .slice(0, 10);
const runId = Date.now().toString(36);
const checks = [];
let token, year, a, b, teacherToken;
function check(value, name) {
  assert.ok(value, name);
  checks.push(name);
}
async function request(
  path,
  { body, method = body ? "POST" : "GET", bearer = token } = {},
) {
  const response = await fetch(base + path, {
    method,
    signal: AbortSignal.timeout(20000),
    headers: {
      "Content-Type": "application/json",
      ...(bearer ? { Authorization: "Bearer " + bearer } : {}),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  const text = await response.text();
  let data;
  try {
    data = JSON.parse(text);
  } catch {
    data = null;
  }
  return { status: response.status, data };
}
async function expect(path, options = {}, status = 200) {
  const r = await request(path, options);
  assert.equal(
    r.status,
    status,
    `${options.method || (options.body ? "POST" : "GET")} ${path}: ${r.data?.code || r.data?.title || ""}`,
  );
  return r.data;
}
const create = (kind, body) => expect("/catalog/" + kind, { body });
const login = ({ email, password }) =>
  expect("/auth/login", {
    bearer: null,
    body: { email, password, client: "mobile" },
  });
const scan = (session, credential, extra = {}) =>
  request(`/attendance-sessions/${session}/check-ins`, {
    bearer: teacherToken,
    body: {
      credential,
      method: "QR",
      clientRequestId: crypto.randomUUID(),
      ...extra,
    },
  });
const reportPath = () =>
  `/reports/attendance?from=${day}&until=${shift(2)}&yearId=${year.id}`;
const safeId = (id) => {
  assert.match(id, /^[a-f0-9-]{36}$/i);
  return id;
};
let failure;
try {
  const admin = await login(secrets);
  token = admin.accessToken;
  check(!!token, "Admin đăng nhập vào SQL Server");
  check(
    Number(sql("SELECT COUNT(*) FROM [__EFMigrationsHistory]")) >= 1,
    "EF migration có trong SQL Server",
  );
  check(redis("PING") === "PONG", "Redis xác thực bằng mật khẩu");
  year = await create("years", {
    name: "Integration " + runId,
    startDate: day,
    endDate: shift(30),
    isActive: false,
  });
  a = await create("classes", {
    name: "Hội Nhập A " + runId,
    code: "ITA" + runId,
    academicYearId: year.id,
  });
  b = await create("classes", {
    name: "Hội Nhập B " + runId,
    code: "ITB" + runId,
    academicYearId: year.id,
  });
  check(a.id !== b.id, "Tạo năm học và hai lớp");
  await expect(
    "/catalog/classes",
    { body: { name: "Trùng", code: a.code, academicYearId: year.id } },
    409,
  );
  check(true, "Unique index mã lớp trong năm học");
  const students = [];
  for (let i = 0; i < 5; i++)
    students.push(
      await create("students", {
        fullName: `Nguyễn Hội Nhập ${runId}-${i}`,
        saintName: i % 2 ? "Maria" : "Giuse",
        dateOfBirth: "2016-05-12",
        classRoomId: i < 4 ? a.id : b.id,
      }),
    );
  const found = await expect(`/catalog/students?classId=${a.id}&q=hoi%20nhap`);
  check(found.items.length === 4, "Tìm học sinh không dấu và lọc lớp");
  check(
    (await expect(`/catalog/classes?yearId=${year.id}&q=Hội`)).items.length ===
      2,
    "Tìm lớp và lọc năm học",
  );
  const catechist = await create("catechists", {
    fullName: "GLV tích hợp " + runId,
    saintName: "Giuse",
  });
  await create("assignments", {
    classRoomId: a.id,
    catechistId: catechist.id,
    from: day,
  });
  const email = `integration-${runId}@example.test`,
    password = "CheckIn!Integration2026";
  await expect(
    "/auth/register",
    { bearer: null, body: { email, password, fullName: "GLV tích hợp" } },
    202,
  );
  await expect(
    "/auth/login",
    { bearer: null, body: { email, password, client: "mobile" } },
    403,
  );
  check(true, "Đăng ký chờ duyệt, không được đăng nhập");
  const roles = await expect("/catalog/roles");
  const users = await expect("/catalog/users?q=" + encodeURIComponent(email));
  const user = users.items.find((x) => x.email === email);
  const teacherRole = roles.items.find((x) => x.name === "Giáo lý viên");
  await expect(
    `/users/${user.id}/approve`,
    {
      method: "POST",
      body: { roleId: teacherRole.id, catechistId: catechist.id },
    },
    204,
  );
  const teacher = await login({ email, password });
  teacherToken = teacher.accessToken;
  check(
    (await expect("/catalog/classes", { bearer: teacherToken })).items.every(
      (x) => x.id === a.id,
    ),
    "Duyệt tài khoản, GLV chỉ thấy lớp được giao",
  );
  await expect(
    "/catalog/classes",
    {
      bearer: teacherToken,
      body: { name: "Không được phép", code: "NO", academicYearId: year.id },
    },
    403,
  );
  check(true, "Attribute/quyền chức năng chặn tạo lớp");
  const schedule = (c) => ({
    classRoomId: c.id,
    from: day,
    until: shift(2),
    daysMask: 127,
    opensAt: "00:00",
    closesAt: "23:59",
  });
  await create("schedules", schedule(a));
  await create("schedules", schedule(b));
  const listSessions = () =>
    expect(`/attendance-sessions?from=${day}&until=${shift(2)}`);
  let sessions = (await listSessions()).items.filter((x) =>
    [a.id, b.id].includes(x.classRoomId),
  );
  check(sessions.length === 6, "Sinh lịch và roster cho ba ngày");
  const sa = sessions.find((x) => x.classRoomId === a.id && x.date === day),
    sb = sessions.find((x) => x.classRoomId === b.id && x.date === day);
  check(
    sa.status === "Open" && sa.expected === 4 && sb.expected === 1,
    "Buổi đang mở có đúng danh sách ghi danh",
  );
  await expect(
    `/attendance-sessions/${sb.id}/roster`,
    { bearer: teacherToken },
    403,
  );
  check(true, "Chặn roster lớp ngoài phạm vi");
  await create("exceptions", {
    classRoomId: a.id,
    date: shift(1),
    cancelled: true,
    reason: "Nghỉ kiểm thử",
  });
  await create("schedules", schedule(a));
  sessions = (await listSessions()).items.filter((x) =>
    [a.id, b.id].includes(x.classRoomId),
  );
  check(
    sessions.length === 6 &&
      sessions.find((x) => x.classRoomId === a.id && x.date === shift(1))
        .cancelled,
    "Ngày nghỉ và đồng bộ lịch không nhân đôi buổi",
  );
  const cards = [];
  for (const s of students)
    cards.push(
      (await expect(`/students/${s.id}/credential`, { method: "POST" }))
        .credential,
    );
  const key = crypto.randomUUID();
  const first = await scan(sa.id, cards[0], { clientRequestId: key });
  check(
    first.status === 200 && first.data.present,
    "Điểm danh QR được ghi thật",
  );
  const replay = await scan(sa.id, cards[0], { clientRequestId: key });
  check(
    replay.status === 200 &&
      replay.data.attendanceRecordId === first.data.attendanceRecordId,
    "Retry cùng request id không ghi trùng",
  );
  const duplicate = await scan(sa.id, cards[0]);
  check(
    duplicate.status === 200 &&
      duplicate.data.resultCode === "AlreadyCheckedIn",
    "Quét lặp báo đã điểm danh",
  );
  check(
    (await scan(sa.id, cards[1], { clientRequestId: key })).status === 409,
    "Request id khác nội dung bị từ chối",
  );
  const barcode = await scan(sa.id, cards[1], { method: "Barcode" });
  check(barcode.status === 200 && barcode.data.present, "Điểm danh Barcode");
  check(
    (await scan(sa.id, cards[4])).data?.code === "WRONG_CLASS",
    "Từ chối thẻ của lớp khác",
  );
  const concurrent = await Promise.all(
    Array.from({ length: 6 }, () => scan(sa.id, cards[2])),
  );
  check(
    concurrent.every((x) => x.status === 200),
    "Sáu lượt quét đồng thời không lỗi server",
  );
  check(
    concurrent.filter((x) => x.data.resultCode === "Created").length === 1 &&
      new Set(concurrent.map((x) => x.data.attendanceRecordId)).size === 1,
    "Quét đồng thời chỉ tạo một bản ghi",
  );
  check(
    Number(
      sql(
        `SELECT COUNT(*) FROM [Attendance] WHERE [SessionId]='${safeId(sa.id)}' AND [StudentId]='${safeId(students[2].id)}'`,
      ),
    ) === 1,
    "SQL xác nhận chỉ một record cho học sinh/buổi",
  );
  await expect(`/students/${students[0].id}/credential`, { method: "POST" });
  check(
    (await scan(sa.id, cards[0])).data?.code === "CREDENTIAL_INVALID",
    "Cấp lại thẻ thu hồi mã cũ",
  );
  const openReport = await expect(reportPath());
  check(
    openReport.totalExpected === 0 && openReport.rate === null,
    "Báo cáo loại buổi đang mở, tương lai và ngày hủy",
  );
  // Chỉ thay khung giờ của fixture trong database Integration để không phải chờ hết ngày.
  sql(
    `UPDATE [Sessions] SET [ClosesAtUtc]=DATEADD(second,-1,SYSUTCDATETIME()),[Version]=NEWID() WHERE [Id] IN ('${safeId(sa.id)}','${safeId(sb.id)}')`,
  );
  await create("catechists", { fullName: "Mốc audit sau fixture " + runId });
  const report = await expect(reportPath());
  check(
    report.totalExpected === 5 &&
      report.totalPresent === 3 &&
      report.totalAbsent === 2 &&
      report.rate === 60,
    "Báo cáo tổng: 5 phải điểm danh, 3 có mặt, 2 vắng, 60%",
  );
  check(
    report.classes.find((x) => x.classId === a.id).rate === 75 &&
      report.classes.find((x) => x.classId === b.id).rate === 0,
    "Chuyên cần theo từng lớp",
  );
  check(
    report.students.filter((x) => x.absent > 0).length === 2,
    "Danh sách chưa điểm danh và số ngày có/vắng",
  );
  const keys = redis("--scan", "--pattern", `report:${admin.user.id}:*`)
    .split(/\r?\n/)
    .filter(Boolean);
  const cachedKey = keys.find((k) => {
    try {
      const x = JSON.parse(redis("GET", k));
      return x.totalExpected === 5 && x.totalPresent === 3;
    } catch {
      return false;
    }
  });
  check(
    !!cachedKey &&
      Number(redis("TTL", cachedKey)) > 0 &&
      Number(redis("TTL", cachedKey)) <= 30,
    "Redis có JSON báo cáo thật với TTL 30 giây",
  );
  const hitsBefore = Number(
    redis("INFO", "stats").match(/keyspace_hits:(\d+)/)[1],
  );
  check(
    (await expect(reportPath())).totalPresent === 3 &&
      Number(redis("INFO", "stats").match(/keyspace_hits:(\d+)/)[1]) >
        hitsBefore,
    "Lần đọc lặp hit cache Redis",
  );
  await expect(
    `/attendance-records/${first.data.attendanceRecordId}`,
    {
      method: "PATCH",
      body: {
        present: false,
        version: first.data.version,
        reason: "Đối soát tích hợp",
      },
    },
    204,
  );
  await expect(
    `/attendance-records/${first.data.attendanceRecordId}`,
    {
      method: "PATCH",
      body: {
        present: true,
        version: first.data.version,
        reason: "Version cũ",
      },
    },
    409,
  );
  check(
    (await expect(reportPath())).totalPresent === 2,
    "Điều chỉnh có kiểm tra version và cache bị vô hiệu ngay",
  );
  check(
    (await scan(sa.id, cards[1])).data?.code === "SESSION_CLOSED",
    "Buổi đã đóng từ chối điểm danh",
  );
  const scopedReport = await expect(reportPath(), { bearer: teacherToken });
  check(
    scopedReport.classes.length === 1 && scopedReport.totalExpected === 4,
    "Báo cáo GLV chỉ bao gồm lớp phụ trách",
  );
  const refreshed = await expect("/auth/refresh", {
    bearer: null,
    body: { client: "mobile", refreshToken: teacher.refreshToken },
  });
  await expect(
    "/auth/refresh",
    {
      bearer: null,
      body: { client: "mobile", refreshToken: teacher.refreshToken },
    },
    401,
  );
  await expect("/auth/me", { bearer: refreshed.accessToken }, 401);
  check(true, "Refresh token xoay vòng; tái sử dụng thu hồi cả phiên");
  const activeTeacher = await login({ email, password });
  await expect(`/users/${user.id}/lock`, { method: "POST" }, 204);
  await expect("/auth/me", { bearer: activeTeacher.accessToken }, 401);
  check(true, "Khóa tài khoản có hiệu lực với JWT đã cấp");
  docker("stop", "redis");
  try {
    const fallback = await expect(reportPath());
    check(
      fallback.totalExpected === 5 && fallback.totalPresent === 2,
      "Redis dừng: API vẫn trả báo cáo đúng từ SQL",
    );
  } finally {
    docker("start", "redis");
  }
  for (let i = 0; i < 20; i++) {
    try {
      if (redis("PING") === "PONG") break;
    } catch {}
    await new Promise((r) => setTimeout(r, 500));
  }
  check(
    redis("PING") === "PONG",
    "Redis hoạt động trở lại sau kiểm thử gián đoạn",
  );
  let cacheRecovered = false;
  for (let i = 0; i < 20 && !cacheRecovered; i++) {
    const recovered = await expect(reportPath());
    const recoveredKeys = redis(
      "--scan",
      "--pattern",
      `report:${admin.user.id}:*`,
    )
      .split(/\r?\n/)
      .filter(Boolean);
    cacheRecovered =
      recovered.totalPresent === 2 &&
      recoveredKeys.some((k) => {
        try {
          const x = JSON.parse(redis("GET", k));
          return x.totalExpected === 5 && x.totalPresent === 2;
        } catch {
          return false;
        }
      });
    if (!cacheRecovered) await new Promise((r) => setTimeout(r, 500));
  }
  check(
    cacheRecovered,
    "API tự kết nối lại và ghi cache Redis sau khởi động lại",
  );
  check(
    Number(sql("SELECT COUNT(*) FROM [__EFMigrationsHistory]")) === 1,
    "Migration history không bị lặp",
  );
} catch (e) {
  failure = e;
}
const result = {
  runId,
  at: new Date().toISOString(),
  api: base,
  database: "CheckInAppIntegration",
  passed: checks.length,
  checks,
  ...(failure ? { error: failure.message } : {}),
};
await fs.writeFile(
  root + ".local/docker-integration/results.json",
  JSON.stringify(result, null, 2),
);
console.log(
  JSON.stringify({
    passed: checks.length,
    api: base,
    ...(failure ? { error: failure.message } : {}),
  }),
);
if (failure) process.exitCode = 1;
