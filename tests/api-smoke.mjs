import assert from "node:assert/strict";
import fs from "node:fs/promises";
const base = process.env.API_URL || "http://127.0.0.1:5080/api/v1";
const bootstrap = JSON.parse(
  await fs.readFile(
    new URL(
      "../backend/CheckInApp.Api/.local/bootstrap-admin.json",
      import.meta.url,
    ),
    "utf8",
  ),
);
let count = 0;
const check = (condition, message) => {
  assert.ok(condition, message);
  count++;
};
async function request(
  path,
  { token, body, method = body ? "POST" : "GET" } = {},
) {
  const response = await fetch(base + path, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: "Bearer " + token } : {}),
    },
    ...(body ? { body: JSON.stringify(body) } : {}),
  });
  let data = null;
  try {
    data = await response.json();
  } catch {}
  return { status: response.status, data };
}
const login = await request("/auth/login", {
  body: {
    email: bootstrap.email,
    password: bootstrap.password,
    client: "mobile",
  },
});
check(login.status === 200, "Admin login");
const token = login.data.accessToken;
const classes = await request("/catalog/classes", { token });
check(classes.status === 200 && classes.data.items.length >= 2, "Class list");
const a = classes.data.items[0],
  b = classes.data.items[1];
const email = "smoke-" + Date.now() + "@example.test",
  password = "CheckIn!Smoke2026";
check(
  (
    await request("/auth/register", {
      body: { email, password, fullName: "Smoke Test Teacher" },
    })
  ).status === 202,
  "Register pending",
);
check(
  (
    await request("/auth/login", {
      body: { email, password, client: "mobile" },
    })
  ).status === 403,
  "Pending account cannot log in",
);
const users = await request("/catalog/users", { token }),
  roles = await request("/catalog/roles", { token }),
  cas = await request("/catalog/catechists", { token });
const newUser = users.data.items.find((u) => u.email === email),
  teacherRole = roles.data.items.find((r) => !r.globalScope);
const created = await request("/catalog/catechists", {
  token,
  body: { fullName: "Smoke GLV " + Date.now(), saintName: "Giuse" },
});
const free = created.data;
check(!!free.id, "Free catechist");
const assigned = await request("/catalog/assignments", {
  token,
  body: {
    classRoomId: a.id,
    catechistId: free.id,
    from: new Intl.DateTimeFormat("en-CA", {
      timeZone: "Asia/Ho_Chi_Minh",
    }).format(new Date()),
  },
});
const ownAssignment = assigned.data;
const otherClass = classes.data.items.find(
  (x) => x.id !== ownAssignment.classRoomId,
);
check(
  (
    await request("/users/" + newUser.id + "/approve", {
      token,
      body: { roleId: teacherRole.id, catechistId: free.id },
    })
  ).status === 204,
  "Approve account",
);
const teacher = await request("/auth/login", {
  body: { email, password, client: "mobile" },
});
check(teacher.status === 200, "Approved login");
const teacherToken = teacher.data.accessToken;
const ownClasses = await request("/catalog/classes", { token: teacherToken });
check(
  ownClasses.data.items.length === 1 &&
    ownClasses.data.items[0].id === ownAssignment.classRoomId,
  "Teacher class scope",
);
check(
  (
    await request("/catalog/classes", {
      token: teacherToken,
      body: {
        name: "Unauthorized",
        code: "BAD",
        academicYearId: a.academicYearId,
      },
    })
  ).status === 403,
  "Teacher cannot create class",
);
const day = new Intl.DateTimeFormat("en-CA", {
  timeZone: "Asia/Ho_Chi_Minh",
}).format(new Date());
const sessions = await request(
  "/attendance-sessions?from=" + day + "&until=" + day,
  { token },
);
const ownSession = sessions.data.items.find(
  (s) => s.classRoomId === ownAssignment.classRoomId && s.status === "Open",
);
const otherSession = sessions.data.items.find(
  (s) => s.classRoomId === otherClass.id,
);
check(!!ownSession, "Open demo session");
check(
  (
    await request("/attendance-sessions/" + otherSession.id + "/roster", {
      token: teacherToken,
    })
  ).status === 403,
  "Other class roster forbidden",
);
const roster = await request(
  "/attendance-sessions/" + ownSession.id + "/roster",
  { token },
);
const student = roster.data.items[0];
const card = await request("/students/" + student.studentId + "/credential", {
  token,
  method: "POST",
});
check(card.status === 200, "Issue credential");
const input = {
  credential: card.data.credential,
  clientRequestId: crypto.randomUUID(),
  method: "QR",
};
const first = await request(
  "/attendance-sessions/" + ownSession.id + "/check-ins",
  { token: teacherToken, body: input },
);
check(first.status === 200 && first.data.present, "Check-in persisted");
const repeat = await request(
  "/attendance-sessions/" + ownSession.id + "/check-ins",
  { token: teacherToken, body: input },
);
check(
  repeat.data.attendanceRecordId === first.data.attendanceRecordId,
  "Idempotent retry",
);
const duplicate = await request(
  "/attendance-sessions/" + ownSession.id + "/check-ins",
  {
    token: teacherToken,
    body: { ...input, clientRequestId: crypto.randomUUID() },
  },
);
check(duplicate.data.resultCode === "AlreadyCheckedIn", "Duplicate scan");
check(
  (
    await request("/attendance-sessions/" + ownSession.id + "/check-ins", {
      token: teacherToken,
      body: { ...input, credential: "CI1-other-code" },
    })
  ).status === 409,
  "Idempotency payload conflict",
);
const card2 = await request("/students/" + student.studentId + "/credential", {
  token,
  method: "POST",
});
check(card2.status === 200, "Reissue credential");
check(
  (
    await request("/attendance-sessions/" + ownSession.id + "/check-ins", {
      token: teacherToken,
      body: { ...input, clientRequestId: crypto.randomUUID() },
    })
  ).data.code === "CREDENTIAL_INVALID",
  "Old card revoked",
);
const report = await request(
  "/reports/attendance?from=" + day.slice(0, 4) + "-01-01&until=" + day,
  { token },
);
check(report.status === 200, "Report endpoint");
check(
  report.data.totalPresent + report.data.totalAbsent ===
    report.data.totalExpected,
  "Report arithmetic",
);
check(
  (await request("/users/" + newUser.id + "/lock", { token, method: "POST" }))
    .status === 204,
  "Lock user",
);
check(
  (await request("/catalog/classes", { token: teacherToken })).status === 401,
  "Previously issued token revoked immediately",
);
await request("/auth/logout", { token, method: "POST" });
console.log(JSON.stringify({ passed: count, api: base }));
