import React, {
  createContext,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import { createRoot } from "react-dom/client";
import {
  LayoutDashboard,
  ScanLine,
  School,
  UsersRound,
  CalendarDays,
  ChartNoAxesCombined,
  UserRoundCheck,
  CalendarRange,
  UserRoundCog,
  ShieldCheck,
  LogOut,
  Plus,
  ArrowUpRight,
  Download,
  Camera,
  Menu,
  CheckCircle2,
  AlertCircle,
  Clock3,
  RefreshCw,
  Edit3,
  QrCode,
  ArrowRightLeft,
  Settings2,
  KeyRound,
} from "lucide-react";
import { QRCodeSVG } from "qrcode.react";
import JsBarcode from "jsbarcode";
import { api, can, setToken, clearToken, today, fmtDate, fmtTime } from "./api";
import {
  Brand,
  Button,
  Badge,
  Head,
  Select,
  DatePicker,
  Modal,
  State,
  Table,
} from "./ui";
import "./styles.css";
import "./production.css";
import { ExcelActions, PresencePanel } from "./extensions";

const permissionLabels = {
  ClassView: "Xem lớp học",
  ClassManage: "Quản lý lớp học",
  StudentView: "Xem học sinh",
  StudentManage: "Quản lý học sinh / chuyển lớp",
  CatechistView: "Xem giáo lý viên",
  CatechistManage: "Quản lý giáo lý viên",
  YearView: "Xem năm học",
  YearManage: "Quản lý năm học",
  ScheduleView: "Xem lịch điểm danh",
  ScheduleManage: "Cấu hình lịch",
  AttendanceView: "Xem điểm danh",
  CheckIn: "Quét điểm danh",
  AttendanceCorrect: "Điều chỉnh điểm danh",
  ReportView: "Xem và xuất báo cáo",
  UserView: "Xem tài khoản",
  UserApprove: "Duyệt / khóa tài khoản",
  RoleManage: "Quản lý vai trò và quyền",
  CredentialManage: "Cấp lại thẻ HS / GLV",
  AssignmentManage: "Phân công lớp",
  AuditView: "Xem nhật ký",
  UserCreate: "Tạo tài khoản",
  StudentImport: "Nhập Excel học sinh", StudentExport: "Xuất Excel học sinh",
  CatechistImport: "Nhập Excel giáo lý viên", CatechistExport: "Xuất Excel giáo lý viên",
  ClassImport: "Nhập Excel lớp học", ClassExport: "Xuất Excel lớp học",
  CatechistAttendance: "Điểm danh / điều chỉnh giờ giáo lý viên",
};
const Context = createContext();
const useApp = () => useContext(Context);
const nav = [
  ["tongquan", "Tổng quan", LayoutDashboard, 10],
  ["quet", "Điểm danh", ScanLine, 10],
  ["lop", "Lớp học", School, 0],
  ["hocsinh", "Học sinh", UsersRound, 2],
  ["lich", "Lịch", CalendarDays, 8],
  ["baocao", "Báo cáo", ChartNoAxesCombined, 13],
  ["glv", "Giáo lý viên", UserRoundCheck, 4],
  ["namhoc", "Năm học", CalendarRange, 6],
  ["duyet", "Tài khoản", UserRoundCog, 14],
  ["quyen", "Phân quyền", ShieldCheck, 16],
];
function useData(path) {
  const { revision } = useApp();
  const [state, setState] = useState({
    loading: !!path,
    data: null,
    error: null,
  });
  const [again, setAgain] = useState(0);
  useEffect(() => {
    let live = true;
    if (!path) {
      setState({ loading: false, data: null, error: null });
      return;
    }
    setState((s) => ({ ...s, loading: true, error: null }));
    api(path)
      .then((data) => live && setState({ data, loading: false, error: null }))
      .catch(
        (error) => live && setState({ data: null, loading: false, error }),
      );
    return () => (live = false);
  }, [path, revision, again]);
  return { ...state, reload: () => setAgain((x) => x + 1) };
}
const query = (obj) =>
  new URLSearchParams(
    Object.fromEntries(
      Object.entries(obj).filter(([, v]) => v !== "" && v != null),
    ),
  ).toString();
const statusNames = {
  Open: ["Đang mở", "ok"],
  Closed: ["Đã kết thúc", "neutral"],
  Scheduled: ["Sắp tới", "neutral"],
  Cancelled: ["Đã hủy", "neutral"],
  PendingApproval: ["Chờ duyệt", "wait"],
  Active: ["Hoạt động", "ok"],
  Rejected: ["Từ chối", "error"],
  Suspended: ["Đã khóa", "error"],
};
const Status = ({ value }) => (
  <Badge kind={statusNames[value]?.[1]}>
    {statusNames[value]?.[0] || value}
  </Badge>
);
const nameCell = (r) => (
  <>
    <strong className="long">{r.fullName || r.name}</strong>
    <small>{r.code || r.email}</small>
  </>
);
const rate = (r) =>
  r == null
    ? "Chưa có buổi"
    : Number(r).toLocaleString("vi-VN", { maximumFractionDigits: 2 }) + "%";
function Stats({ items }) {
  return (
    <div className="stats">
      {items.map(([label, value, desc, Icon]) => (
        <div className="stat" key={label}>
          <div className="stathead">
            <span>{label}</span>
            <span className="iconbox">
              <Icon />
            </span>
          </div>
          <strong className="num">{value ?? "—"}</strong>
          <p>{desc}</p>
        </div>
      ))}
    </div>
  );
}
function Login({ onLogin, notice }) {
  const [register, setRegister] = useState(false),
    [error, setError] = useState(""),
    [message, setMessage] = useState(""),
    [busy, setBusy] = useState(false);
  async function submit(e) {
    e.preventDefault();
    setError("");
    setMessage("");
    setBusy(true);
    let values = Object.fromEntries(new FormData(e.currentTarget));
    try {
      if (register) {
        let r = await api("/auth/register", { method: "POST", body: values });
        setMessage(r.message);
        setRegister(false);
      } else {
        let d = await api("/auth/login", {
          method: "POST",
          body: { ...values, client: "web" },
        });
        setToken(d.accessToken);
        onLogin(d.user);
      }
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="auth">
      <div className="authbox">
        <Brand />
        <h1>{register ? "Đăng ký tài khoản" : "Đăng nhập"}</h1>
        <p className="muted">
          {register
            ? "Admin cần duyệt tài khoản trước khi sử dụng."
            : "Dùng tài khoản đã được admin duyệt."}
        </p>
        {notice && <p role="status" className="callout">{notice}</p>}
        <form onSubmit={submit}>
          {register && (
            <div className="field">
              <label htmlFor="fullName">
                Họ và tên <em>*</em>
              </label>
              <input
                autoFocus
                id="fullName"
                className="input"
                name="fullName"
                required
                maxLength={150}
              />
            </div>
          )}
          <div className="field">
            <label htmlFor="email">
              Email <em>*</em>
            </label>
            <input
              autoFocus={!register}
              id="email"
              className="input"
              name="email"
              type="email"
              autoComplete="username"
              placeholder="ban@example.com"
              required
            />
          </div>
          <div className="field">
            <label htmlFor="password">
              Mật khẩu <em>*</em>
            </label>
            <input
              id="password"
              className="input"
              name="password"
              type="password"
              autoComplete={register ? "new-password" : "current-password"}
              minLength={register ? 12 : 1}
              maxLength={128}
              placeholder={
                register ? "Từ 12 ký tự, có chữ và số" : "Nhập mật khẩu"
              }
              required
            />
          </div>
          {error && (
            <p className="form-error" role="alert">
              {error}
            </p>
          )}
          {message && (
            <p className="form-success" role="status">
              {message}
            </p>
          )}
          <Button primary type="submit" disabled={busy}>
            {busy ? "Đang xử lý…" : register ? "Gửi đăng ký" : "Đăng nhập"}
          </Button>
        </form>
        <Button
          ghost
          onClick={() => {
            setRegister(!register);
            setError("");
          }}
        >
          {register
            ? "Đã có tài khoản? Đăng nhập"
            : "Chưa có tài khoản? Đăng ký"}
        </Button>
        <p className="small muted">Quên mật khẩu: liên hệ admin của đơn vị.</p>
      </div>
    </div>
  );
}
function Editor({
  title,
  fields,
  initial = {},
  kind,
  id,
  onClose,
  onSaved,
  validate,
}) {
  const [values, setValues] = useState(initial),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  const app = useApp();
  async function submit(e) {
    e.preventDefault();
    setError("");
    if (validate) {
      let message = validate(values);
      if (message) {
        setError(message);
        return;
      }
    }
    setBusy(true);
    try {
      const saved = await api("/catalog/" + kind + (id ? "/" + id : ""), {
        method: id ? "PUT" : "POST",
        body: values,
      });
      app.refresh();
      app.toast("Đã lưu thành công.");
      onSaved?.(saved);
      onClose();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <Modal title={title} onClose={onClose}>
      <form onSubmit={submit}>
        <div className="formgrid">
          {fields.map((f, i) => (
            <div className="field" key={f.key}>
              <label htmlFor={"edit-" + f.key}>
                {f.label}
                {f.required && <em> *</em>}
              </label>
              {f.type === "select" ? (
                <Select
                  label={f.label}
                  options={f.options}
                  value={values[f.key] || ""}
                  onChange={(v) => setValues({ ...values, [f.key]: v })}
                />
              ) : f.type === "date" ? (
                <DatePicker
                  label={f.label}
                  value={values[f.key] || today()}
                  onChange={(v) => setValues({ ...values, [f.key]: v })}
                />
              ) : f.type === "boolean" ? (
                <label className="row">
                  <input
                    className="check"
                    type="checkbox"
                    id={"edit-" + f.key}
                    aria-label={f.label}
                    checked={!!values[f.key]}
                    onChange={(e) =>
                      setValues({ ...values, [f.key]: e.target.checked })
                    }
                  />
                  {f.hint || "Có"}
                </label>
              ) : f.type === "days" ? (
                <div className="day-options">
                  {["CN", "T2", "T3", "T4", "T5", "T6", "T7"].map((d, i) => (
                    <label key={d}>
                      <input
                        type="checkbox"
                        className="check"
                        checked={!!((values.daysMask || 0) & (1 << i))}
                        onChange={(e) =>
                          setValues({
                            ...values,
                            daysMask: e.target.checked
                              ? (values.daysMask || 0) | (1 << i)
                              : (values.daysMask || 0) & ~(1 << i),
                          })
                        }
                      />
                      {d}
                    </label>
                  ))}
                </div>
              ) : (
                <input
                  className="input"
                  id={"edit-" + f.key}
                  autoFocus={i === 0}
                  type={f.type || "text"}
                  required={f.required}
                  maxLength={f.key === "reason" ? 500 : 200}
                  value={values[f.key] ?? ""}
                  onChange={(e) =>
                    setValues({ ...values, [f.key]: e.target.value })
                  }
                />
              )}
            </div>
          ))}
        </div>
        {kind === "schedules" && (
          <div className="callout" style={{ marginTop: 20 }}>
            <CalendarDays />
            <div>
              <strong>Chỉ cập nhật các buổi tương lai</strong>
              <p>
                {previewDays(values).join(" · ") ||
                  "Chọn thứ và khoảng ngày để xem trước."}
              </p>
            </div>
          </div>
        )}
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        <div className="modal-actions">
          <Button onClick={onClose}>Hủy</Button>
          <Button type="submit" primary disabled={busy}>
            {busy ? "Đang lưu…" : "Lưu"}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
function previewDays(v) {
  let result = [];
  if (!v.from || !v.until) return result;
  let d = new Date((v.from > today() ? v.from : today()) + "T12:00:00");
  for (let i = 0; i < 370 && result.length < 6; i++) {
    let iso = d.toISOString().slice(0, 10);
    if (iso > v.until) break;
    if ((v.daysMask || 0) & (1 << d.getDay())) result.push(fmtDate(iso));
    d.setDate(d.getDate() + 1);
  }
  return result;
}
function Dashboard() {
  const { go } = useApp();
  const [date, setDate] = useState(today());
  const data = useData(
    "/attendance-sessions?" + query({ from: date, until: date }),
  );
  const rows = (data.data?.items || []).filter((s) => !s.cancelled);
  let expected = rows.reduce((a, r) => a + r.expected, 0),
    present = rows.reduce((a, r) => a + r.present, 0);
  return (
    <>
      <Head
        title="Điểm danh hôm nay"
        subtitle={fmtDate(date) + " · Chỉ các lớp có lịch điểm danh"}
      >
        <Button primary onClick={() => go("quet")}>
          <ScanLine />
          Mở điểm danh
        </Button>
      </Head>
      <div className="filters">
        <DatePicker value={date} onChange={setDate} />
      </div>
      <Stats
        items={[
          ["Lớp có lịch", rows.length, "Có lịch trong ngày", CalendarDays],
          ["Học sinh", expected, "Theo roster từng buổi", UsersRound],
          ["Đã điểm danh", present, "Server đã xác nhận", UserRoundCheck],
          [
            "Chưa đến",
            expected - present,
            "Chưa kết luận vắng khi buổi đang mở",
            Clock3,
          ],
        ]}
      />
      <div className="between">
        <h2>Các lớp có lịch hôm nay</h2>
        <Button ghost onClick={() => go("lich")}>
          Xem lịch
        </Button>
      </div>
      <State
        {...data}
        onRetry={data.reload}
        empty={
          !data.loading &&
          !data.error &&
          !rows.length &&
          "Không có buổi điểm danh trong ngày đã chọn."
        }
      >
        <Table
          columns={[
            {
              label: "Lớp học",
              render: (r) => (
                <>
                  <strong>{r.className}</strong>
                  <small>
                    {r.classCode} · {fmtTime(r.opensAtUtc)}–
                    {fmtTime(r.closesAtUtc)}
                  </small>
                </>
              ),
            },
            {
              label: "Giáo lý viên",
              render: (r) => r.teacherName || "Chưa phân công",
            },
            {
              label: "Đã đến / Sĩ số",
              render: (r) => `${r.present} / ${r.expected}`,
            },
            {
              label: "Chưa đến",
              render: (r) => <Badge kind="wait">{r.missing}</Badge>,
            },
            { label: "Buổi học", render: (r) => <Status value={r.status} /> },
            {
              label: "",
              render: (r) => (
                <Button className="sm" onClick={() => go("quet", r.id)}>
                  <ArrowUpRight />
                  Điểm danh
                </Button>
              ),
            },
          ]}
          rows={rows}
        />
      </State>
      <div className="callout">
        <CalendarDays />
        <div>
          <strong>Chỉ hiển thị ngày có lịch điểm danh</strong>
          <p>Buổi tương lai, buổi hủy và ngày nghỉ không cộng vào số vắng.</p>
        </div>
      </div>
    </>
  );
}
function Scan() {
  const ask = useApp().ask;
  const { sessionChoice, user, toast, refresh } = useApp();
  const [date, setDate] = useState(today()),
    [id, setId] = useState(sessionChoice || ""),
    [result, setResult] = useState(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [camera, setCamera] = useState(false),
    [tab, setTab] = useState("recent"),
    [code, setCode] = useState("");
  const controls = useRef(null),
    locked = useRef(false),
    video = useRef(null),
    pending = useRef(null);
  const sessions = useData(
    "/attendance-sessions?" + query({ from: date, until: date }),
  );
  const all = sessions.data?.items || [];
  useEffect(() => {
    if (all.length && !all.some((s) => s.id === id)) setId(all[0].id);
    if (!all.length) setId("");
  }, [sessions.data]);
  const session = all.find((s) => s.id === id);
  const roster = useData(id ? `/attendance-sessions/${id}/roster` : null);
  useEffect(() => () => controls.current?.stop(), []);
  useEffect(() => {
    setResult(null);
    setError("");
    controls.current?.stop();
    setCamera(false);
    locked.current = false;
    pending.current = null;
  }, [id]);
  async function submit(credential, method = "QR", retry = false) {
    if (locked.current && !retry) return;
    locked.current = true;
    controls.current?.stop();
    setCamera(false);
    setBusy(true);
    setResult(null);
    setError("");
    pending.current = retry
      ? pending.current
      : {
          credential: credential.trim(),
          clientRequestId: crypto.randomUUID(),
          method,
        };
    try {
      const r = await api(`/attendance-sessions/${id}/check-ins`, {
        method: "POST",
        body: pending.current,
      });
      setResult(r);
      pending.current = null;
      refresh();
    } catch (e) {
      setError(e.message);
      if (e.status) pending.current = null;
    } finally {
      setBusy(false);
    }
  }
  async function startCamera() {
    setResult(null);
    setError("");
    locked.current = false;
    setCamera(true);
    try {
      await new Promise((r) => setTimeout(r, 60));
      const { BrowserMultiFormatReader } = await import("@zxing/browser");
      const reader = new BrowserMultiFormatReader();
      controls.current = await reader.decodeFromVideoDevice(
        undefined,
        video.current,
        (r) => {
          if (r && !locked.current)
            submit(r.getText(), r.getBarcodeFormat() === 11 ? "QR" : "Barcode");
        },
      );
    } catch {
      setCamera(false);
      setError(
        "Không mở được camera. Kiểm tra quyền camera hoặc dùng đầu đọc USB.",
      );
    }
  }
  const students = roster.data?.items || [],
    missing = students.filter((s) => !s.present),
    recent = students
      .filter((s) => s.present)
      .sort((a, b) =>
        String(b.recordedAtUtc).localeCompare(String(a.recordedAtUtc)),
      );
  return (
    <>
      <Head
        title={"Điểm danh" + (session ? " " + session.className : "")}
        subtitle={`${fmtDate(date)}${session ? " · " + fmtTime(session.opensAtUtc) + "–" + fmtTime(session.closesAtUtc) : ""}`}
      />
      <div className="filters">
        <DatePicker value={date} onChange={setDate} />
        <Select
          label="Chọn buổi"
          value={id}
          options={all.map((s) => ({
            value: s.id,
            label: s.className + " · " + fmtTime(s.opensAtUtc),
          }))}
          onChange={setId}
        />
        {session && <Status value={session.status} />}
      </div>
      <State
        loading={sessions.loading}
        error={sessions.error}
        onRetry={sessions.reload}
        empty={
          !sessions.loading &&
          !sessions.error &&
          !all.length &&
          "Không có lịch điểm danh trong ngày này."
        }
      >
        {session && (
          <div className="split">
            <section className="card">
              <div className="between" style={{ marginBottom: 16 }}>
                <h2>Quét thẻ {session.studentAttendanceEnabled && session.catechistAttendanceEnabled ? "HS / GLV" : session.catechistAttendanceEnabled ? "giáo lý viên" : "học sinh"}</h2>
                <span className="small muted">QR / Code 128</span>
              </div>
              <div className="camera">
                <video
                  ref={video}
                  className={camera ? "live-video" : "hidden"}
                  muted
                  playsInline
                />
                {!camera && (
                  <>
                    <div className="aim">
                      <ScanLine size={68} />
                    </div>
                    <small>
                      {session.status === "Open"
                        ? "Sẵn sàng quét thẻ"
                        : "Buổi chưa mở hoặc đã kết thúc"}
                    </small>
                  </>
                )}
              </div>
              {can(user, 11) && (
                <div className="row" style={{ marginTop: 16 }}>
                  <Button
                    primary
                    disabled={session.status !== "Open" || busy}
                    onClick={startCamera}
                  >
                    <Camera />
                    {result || error ? "Quét tiếp" : "Bật camera"}
                  </Button>
                  {camera && (
                    <Button
                      onClick={() => {
                        controls.current?.stop();
                        setCamera(false);
                      }}
                    >
                      Dừng camera
                    </Button>
                  )}
                </div>
              )}
              {result && (
                <div className="scan-result" role="status">
                  <CheckCircle2 />
                  <div>
                    <strong>
                      {result.resultCode === "Created"
                        ? "Đã điểm danh"
                        : "Đã có kết quả điểm danh"}
                    </strong>
                    <p>
                      {result.personType === "Catechist" ? "GLV · " : "HS · "}{result.studentName} · {result.studentCode}
                    </p>
                    <p>
                      {result.present ? "Có mặt" : "Kết quả hiện tại: vắng"} ·{" "}
                      {fmtTime(result.recordedAtUtc)}
                    </p>
                  </div>
                </div>
              )}
              {error && (
                <div className="form-error" role="alert">
                  <AlertCircle />
                  {error}
                  {!pending.current ? (
                    ""
                  ) : (
                    <Button onClick={() => submit("", undefined, true)}>
                      Thử lại yêu cầu chưa lưu
                    </Button>
                  )}
                </div>
              )}
              <form
                className="manual-input row"
                style={{ marginTop: 20 }}
                onSubmit={(e) => {
                  e.preventDefault();
                  locked.current = false;
                  submit(code, "Barcode");
                  setCode("");
                }}
              >
                <input
                  className="input"
                  aria-label="Mã thẻ từ đầu đọc USB"
                  placeholder="Mã thẻ / đầu đọc USB…"
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                  required
                />
                <Button
                  type="submit"
                  disabled={
                    busy || !id || session.status !== "Open" || !can(user, 11)
                  }
                >
                  {busy ? "Đang lưu…" : "Kiểm tra"}
                </Button>
              </form>
              <p className="small muted" style={{ marginTop: 12 }}>
                Chỉ báo có mặt sau khi server xác nhận. Mất kết nối sẽ chưa tính
                điểm danh.
              </p>
            </section>
            {session.studentAttendanceEnabled !== false && <section className="card flush">
              <div className="compactstats">
                <div>
                  <strong>{students.length - missing.length}</strong>
                  <small>Đã đến</small>
                </div>
                <div>
                  <strong>{missing.length}</strong>
                  <small>Chưa đến</small>
                </div>
                <div>
                  <strong>{students.length}</strong>
                  <small>Sĩ số</small>
                </div>
              </div>
              <div className="cardhead">
                <div className="tabs">
                  <a
                    href="#"
                    aria-current={tab === "recent" ? "page" : undefined}
                    onClick={(e) => {
                      e.preventDefault();
                      setTab("recent");
                    }}
                  >
                    Vừa điểm danh
                  </a>
                  <a
                    href="#"
                    aria-current={tab === "missing" ? "page" : undefined}
                    onClick={(e) => {
                      e.preventDefault();
                      setTab("missing");
                    }}
                  >
                    Chưa đến ({missing.length})
                  </a>
                </div>
              </div>
              <State {...roster} onRetry={roster.reload}>
                <div className="list">
                  {(tab === "missing" ? missing : recent).map((s) => (
                    <div key={s.studentId} className="listrow">
                      <span className="iconbox">
                        <UserRoundCheck />
                      </span>
                      <div className="grow">
                        <strong>{s.fullName}</strong>
                        <p>
                          {s.code} · {s.saintName || "Chưa có Tên Thánh"}
                        </p>
                      </div>
                      <span className="small muted num">
                        {tab === "missing"
                          ? "Chưa đến"
                          : fmtTime(s.recordedAtUtc)}
                      </span>
                      {s.recordId && can(user, 12) && (
                        <Button
                          ghost
                          className="icon"
                          aria-label={"Điều chỉnh " + s.fullName}
                          onClick={async () => {
                            let reason = await ask({
                              title: "Điều chỉnh điểm danh",
                              message:
                                s.fullName + " — đổi kết quả thành vắng.",
                              input: true,
                              inputLabel: "Lý do điều chỉnh",
                            });
                            if (!reason) return;
                            try {
                              await api("/attendance-records/" + s.recordId, {
                                method: "PATCH",
                                body: {
                                  present: false,
                                  version: s.version,
                                  reason,
                                },
                              });
                              refresh();
                              toast("Đã điều chỉnh, có lưu nhật ký.");
                            } catch (e) {
                              toast(e.message);
                            }
                          }}
                        >
                          <Edit3 />
                        </Button>
                      )}
                    </div>
                  ))}
                  {!(tab === "missing" ? missing : recent).length && (
                    <p className="pad muted">
                      Chưa có học sinh trong danh sách này.
                    </p>
                  )}
                </div>
              </State>
            </section>}
          </div>
        )}
      </State>
      {session && session.studentAttendanceEnabled === false && <p className="callout">Buổi này chỉ điểm danh giáo lý viên.</p>}
      {session && <PresencePanel session={session} students={students} user={user} refresh={refresh} toast={toast} />}
    </>
  );
}
function CatalogPage({ type }) {
  const app = useApp(),
    [search, setSearch] = useState(""),
    [page, setPage] = useState(1),
    [pageSize, setPageSize] = useState(25),
    [edit, setEdit] = useState(null),
    [credential, setCredential] = useState(null);
  let { classes, years, classId, setClassId, yearId, user } = app;
  const kinds = {
      lop: "classes",
      hocsinh: "students",
      glv: "catechists",
      namhoc: "years",
    },
    kind = kinds[type];
  const permission = { lop: 1, hocsinh: 3, glv: 5, namhoc: 7 }[type];
  let data = useData(
    "/catalog/" +
      kind +
      "?" +
      query({
        q: search,
        page,
        pageSize,
        ...(type === "lop" ? { yearId } : {}),
        ...(type === "hocsinh" ? { classId } : {}),
      }),
  );
  useEffect(()=>{setPage(1);},[classId,yearId]);
  let rows = data.data?.items || [];
  const yearOptions = years.map((y) => ({ value: y.id, label: y.name })),
    classOptions = classes.map((c) => ({ value: c.id, label: c.name }));
  const fields = {
    lop: [
      { key: "name", label: "Tên lớp", required: true },
      { key: "code", label: "Mã lớp", required: true },
      {
        key: "academicYearId",
        label: "Năm học",
        type: "select",
        options: yearOptions,
      },
    ],
    hocsinh: [
      { key: "saintName", label: "Tên Thánh" },
      { key: "familyName", label: "Họ và tên đệm" },
      { key: "givenName", label: "Tên", required: true },
      { key: "dateOfBirth", label: "Ngày sinh", type: "date" },
      {
        key: "classRoomId",
        label: "Lớp học",
        type: "select",
        options: classOptions,
      },
    ],
    glv: [
      { key: "saintName", label: "Tên Thánh" },
      { key: "familyName", label: "Họ và tên đệm" },
      { key: "givenName", label: "Tên", required: true },
    ],
    namhoc: [
      { key: "name", label: "Tên năm học", required: true },
      { key: "startDate", label: "Ngày bắt đầu", type: "date" },
      { key: "endDate", label: "Ngày kết thúc", type: "date" },
      { key: "isActive", label: "Năm hiện hành", type: "boolean" },
    ],
  }[type];
  function open(row = {}) {
    setEdit({
      ...row,
      academicYearId: row.academicYearId || yearId || years[0]?.id,
      classRoomId:
        classId || row.enrollments?.[0]?.classRoomId || classes[0]?.id,
      dateOfBirth: row.dateOfBirth || "2016-01-01",
      startDate: row.startDate || today(),
      endDate: row.endDate || today(),
    });
  }
  const title = {
    lop: "Lớp học",
    hocsinh: "Học sinh",
    glv: "Giáo lý viên",
    namhoc: "Năm học",
  }[type];
  const cols =
    type === "lop"
      ? [
          { label: "Lớp học", render: nameCell },
          { label: "Sĩ số", key: "studentCount" },
        ]
      : type === "hocsinh"
        ? [
            { label: "Họ và tên đệm", key: "familyName" },
            { label: "Tên", key: "givenName" },
            { label: "Mã học sinh", key: "code" },
            { label: "Tên Thánh", key: "saintName" },
            { label: "Ngày sinh", render: (r) => fmtDate(r.dateOfBirth) },
            {
              label: "Lớp học",
              render: (r) =>
                classes.find((c) => c.id === r.enrollments?.[0]?.classRoomId)
                  ?.name || "—",
            },
          ]
        : type === "glv"
          ? [
              { label: "Họ và tên đệm", key: "familyName" },
              { label: "Tên", key: "givenName" },
              { label: "Tên Thánh", key: "saintName" },
              {
                label: "Tài khoản",
                render: (r) => (
                  <Badge kind={r.userId ? "ok" : "wait"}>
                    {r.userId ? "Đã liên kết" : "Chưa liên kết"}
                  </Badge>
                ),
              },
            ]
          : [
              { label: "Năm học", key: "name" },
              {
                label: "Thời gian",
                render: (r) => fmtDate(r.startDate) + "–" + fmtDate(r.endDate),
              },
              {
                label: "Trạng thái",
                render: (r) => (
                  <Badge kind={r.isActive ? "ok" : "neutral"}>
                    {r.isActive ? "Hiện hành" : "Không hiện hành"}
                  </Badge>
                ),
              },
            ];
  cols.push({
    label: "",
    render: (r) => (
      <div className="row wrap">
        {can(user, permission) && (
          <Button className="sm" onClick={() => open(r)}>
            Chỉnh sửa
          </Button>
        )}
        {type === "lop" && (
          <Button
            className="sm"
            onClick={() => {
              setClassId(r.id);
              app.go("hocsinh");
            }}
          >
            Học sinh
          </Button>
        )}
        {(type === "hocsinh" || type === "glv") && can(user, 17) && (
          <Button
            className="sm"
            onClick={async () => {
              try {
                setCredential({
                  ...(await api((type === "glv" ? "/catechists/" : "/students/") + r.id + "/credential", {
                    method: "POST",
                  })),
                  ...r,
                  ...(type === "glv" ? { personType: "Catechist", code: "GLV-" + r.id.replaceAll("-", "").slice(0,8).toUpperCase(), enrollments: (data.data?.assignments || []).filter(a => a.catechistId === r.id && !a.until).map(a => ({classRoomId:a.classRoomId})) } : {}),
                });
                app.refresh();
              } catch (e) {
                app.toast(e.message);
              }
            }}
          >
            <QrCode />
            Cấp thẻ
          </Button>
        )}
        {type === "hocsinh" && can(user, 3) && (
          <Button
            className="sm"
            onClick={() =>
              setEdit({
                transfer: true,
                enrollmentId: r.enrollments?.[0]?.id,
                classRoomId: "",
                from: today(),
              })
            }
          >
            <ArrowRightLeft />
            Chuyển lớp
          </Button>
        )}
      </div>
    ),
  });
  return (
    <>
      <Head
        title={title}
        subtitle="Quản lý dữ liệu theo năm học và phạm vi được phân công."
      >
        {can(user, permission) && (
          <Button primary onClick={() => open()}>
            <Plus />
            {type === "lop" ? "Tạo lớp học" : "Thêm " + title.toLowerCase()}
          </Button>
        )}
      </Head>

      <div className="filters">
        <input
          className="input search"
          aria-label={"Tìm " + title.toLowerCase()}
          placeholder="Tìm tên hoặc mã…"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(1);
          }}
        />
        {type === "hocsinh" && (
          <Select
            label="Lớp học"
            value={classId}
            onChange={(v) => {
              setClassId(v);
              setPage(1);
            }}
            options={[{ value: "", label: "Toàn bộ lớp" }, ...classOptions]}
          />
        )}
      <ExcelActions kind={kind} user={user} classId={classId} className={classes.find(c => c.id === classId)?.name} classes={classes} onClassChange={setClassId} yearId={yearId} refresh={app.refresh} toast={app.toast} />
      </div>
      <div className="catalog-pagination">
        <span>Số dòng mỗi trang</span><Select label="Số dòng mỗi trang" value={String(pageSize)} onChange={v=>{setPageSize(Number(v));setPage(1);}} options={[...new Set([10,25,50,100,...Array.from({length:Math.min(18,Math.ceil((data.data?.total||0)/50))},(_,i)=>(i+3)*50),pageSize])].sort((a,b)=>a-b).map(n=>({value:String(n),label:String(n)}))}/>
        <nav className="row wrap" aria-label="Phân trang danh sách">
          <Button className="sm" disabled={data.loading || page <= 1} onClick={()=>setPage(p=>p-1)}>Trước</Button>
          {Array.from({length:Math.max(1,Math.ceil((data.data?.total ?? rows.length)/pageSize))},(_,i)=>i+1).filter(n=>n===1 || n===Math.max(1,Math.ceil((data.data?.total ?? rows.length)/pageSize)) || Math.abs(n-page)<=2).map((n,i,list)=><React.Fragment key={n}>{i>0 && n-list[i-1]>1 && <span aria-hidden="true">…</span>}<Button className="sm" primary={n===page} aria-label={"Trang "+n} aria-current={n===page?"page":undefined} disabled={data.loading} onClick={()=>setPage(n)}>{n}</Button></React.Fragment>)}
          <Button className="sm" disabled={data.loading || page>=Math.max(1,Math.ceil((data.data?.total ?? rows.length)/pageSize))} onClick={()=>setPage(p=>p+1)}>Sau</Button>
        </nav>
        <span className="small muted" aria-live="polite">{data.data?.total ?? rows.length} bản ghi · Trang {page}/{Math.max(1,Math.ceil((data.data?.total ?? rows.length)/pageSize))}</span>
      </div>
      <State
        {...data}
        empty={!data.loading && !data.error && !rows.length}
        onRetry={data.reload}
      >
        <Table rows={rows} columns={cols} />
      </State>
      {type === "glv" && can(user, 18) && <Assignments data={data.data} />}{" "}
      {edit && (
        <Editor
          title={
            edit.transfer
              ? "Chuyển lớp"
              : (edit.id ? "Chỉnh sửa " : "Thêm ") + title.toLowerCase()
          }
          fields={
            edit.transfer
              ? [
                  {
                    key: "classRoomId",
                    label: "Lớp mới",
                    type: "select",
                    options: classOptions,
                  },
                  { key: "from", label: "Hiệu lực từ", type: "date" },
                ]
              : fields
          }
          initial={edit}
          kind={edit.transfer ? "transfers" : kind}
          id={edit.id}
          onSaved={(saved) => {
            if (type === "lop" && !edit.id) {
              app.setYearId(saved.academicYearId);
              setSearch(saved.code);
              setPage(1);
            }
          }}
          onClose={() => setEdit(null)}
        />
      )}{" "}
      {credential && (
        <CredentialCard
          student={credential}
          onClose={() => setCredential(null)}
        />
      )}
    </>
  );
}
function CredentialCard({ student, onClose }) {
  const barcode = useRef();
  const teacher = student.personType === "Catechist";
  const { classes, years } = useApp();
  const [format, setFormat] = useState("QR");
  const cls = classes.find(c => c.id === student.enrollments?.[0]?.classRoomId);
  const year = years.find(y => y.id === cls?.academicYearId);
  useEffect(() => {
    if (barcode.current) JsBarcode(barcode.current, student.credential, {
      format: "CODE128",
      height: 60,
      width: 1.5,
      displayValue: false,
      margin: 10,
    });
  }, [format]);
  return (
    <Modal title={(teacher ? "Thẻ giáo lý viên" : "Thẻ học sinh") + " · 86 × 50 mm"} onClose={onClose}>
      <Select label="Định dạng thẻ" value={format} onChange={setFormat} options={[{value:"QR",label:"QR Code"},{value:"Barcode",label:"Barcode (Code 128)"}]} />
      <div id="print-card" className={"student-card " + (format === "QR" ? "qr" : "barcode")}>
        <div className="card-information"><strong>CheckInApp · {teacher ? "THẺ GIÁO LÝ VIÊN" : "THẺ HỌC SINH"}</strong><h3>{student.fullName}</h3><p>Tên Thánh: {student.saintName || "—"}</p>{!teacher && <p>Ngày sinh: {fmtDate(student.dateOfBirth)}</p>}<p>Mã {teacher ? "GLV" : "HS"}: {student.code}</p><p>Lớp: {student.className || cls?.name || "—"}</p><p>Năm học: {student.academicYearName || year?.name || "—"}</p></div>
        <div className="card-symbol">{format === "QR" ? <QRCodeSVG value={student.credential} size={100} marginSize={4} level="M" /> : <svg ref={barcode} />}</div>
      </div>
      <p className="small muted">In tỷ lệ 100%, tắt header/footer; chọn Lưu PDF để xuất file thẻ.</p>
      <p className="small muted">
        Mã chỉ hiển thị một lần. Thẻ cũ đã được thu hồi. In hoặc lưu thẻ trước
        khi đóng.
      </p>
      <div className="modal-actions">
        <Button onClick={onClose}>Đóng</Button>
        <Button primary onClick={() => window.print()}>
          In thẻ
        </Button>
      </div>
    </Modal>
  );
}
function Assignments({ data }) {
  const { classes, user } = useApp(),
    [edit, setEdit] = useState(false);
  return (
    <div className="card">
      <div className="between">
        <h2>Phân công phụ trách lớp</h2>
        <Button onClick={() => setEdit(true)}>
          <Plus />
          Phân công
        </Button>
      </div>
      <div className="list" style={{ marginTop: 16 }}>
        {(data?.assignments || []).map((a) => (
          <div className="listrow" key={a.id}>
            <div className="grow">
              <strong>
                {data?.items.find((c) => c.id === a.catechistId)?.fullName}
              </strong>
              <p>
                {classes.find((c) => c.id === a.classRoomId)?.name} · Từ{" "}
                {fmtDate(a.from)}
              </p>
            </div>
          </div>
        ))}
      </div>
      {edit && (
        <Editor
          title="Phân công giáo lý viên"
          kind="assignments"
          initial={{
            from: today(),
            classRoomId: classes[0]?.id,
            catechistId: data?.items[0]?.id,
          }}
          fields={[
            {
              key: "classRoomId",
              label: "Lớp",
              type: "select",
              options: classes.map((c) => ({ value: c.id, label: c.name })),
            },
            {
              key: "catechistId",
              label: "Giáo lý viên",
              type: "select",
              options: (data?.items || []).map((c) => ({
                value: c.id,
                label: c.fullName,
              })),
            },
            { key: "from", label: "Từ ngày", type: "date" },
          ]}
          onClose={() => setEdit(false)}
        />
      )}
    </div>
  );
}
function SessionScheduleEditor({session,onClose}) {
  const app=useApp(),local=s=>new Date(new Date(s.endsWith("Z")?s:s+"Z").getTime()+7*3600000).toISOString().slice(11,16);
  const [open,setOpen]=useState(local(session.opensAtUtc)),[close,setClose]=useState(local(session.closesAtUtc)),[hs,setHs]=useState(session.studentAttendanceEnabled),[glv,setGlv]=useState(session.catechistAttendanceEnabled),[reason,setReason]=useState(""),[error,setError]=useState(""),[busy,setBusy]=useState(false);
  async function save(e){e.preventDefault();setBusy(true);setError("");try{await api(`/attendance-sessions/${session.id}/schedule`,{method:"PUT",body:{opensAt:open+":00",closesAt:close+":00",studentAttendanceEnabled:hs,catechistAttendanceEnabled:glv,reason,version:session.version}});app.refresh();app.toast("Đã cập nhật lịch buổi học.");onClose();}catch(e){setError(e.message);}finally{setBusy(false);}}
  return <Modal title={"Chỉnh lịch · "+session.className+" · "+fmtDate(session.date)} onClose={()=>{if(!busy)onClose();}}><form onSubmit={save} className="stack"><p className="small muted">Giờ Việt Nam. Chỉ sửa trước khi buổi bắt đầu; giờ bắt đầu mới phải lớn hơn thời gian hiện tại.</p><label className="field">Giờ bắt đầu<input className="input" required type="time" value={open} onChange={e=>setOpen(e.target.value)}/></label><label className="field">Giờ kết thúc<input className="input" required type="time" value={close} onChange={e=>setClose(e.target.value)}/></label><label className="row"><input type="checkbox" className="check" checked={hs} onChange={e=>setHs(e.target.checked)}/>HS — Điểm danh học sinh</label><label className="row"><input type="checkbox" className="check" checked={glv} onChange={e=>setGlv(e.target.checked)}/>GLV — Điểm danh giáo lý viên</label><label className="field">Lý do điều chỉnh<input className="input" required maxLength={500} value={reason} onChange={e=>setReason(e.target.value)}/></label>{error&&<p className="form-error" role="alert">{error}</p>}<div className="modal-actions"><Button disabled={busy} onClick={onClose}>Hủy</Button><Button primary type="submit" disabled={busy}>Lưu lịch</Button></div></form></Modal>;
}
function Schedule() {
  const app = useApp(),
    [edit, setEdit] = useState(null),
    [sessionEdit,setSessionEdit]=useState(null),
    [exception, setException] = useState(false),
    [from, setFrom] = useState(today().slice(0, 8) + "01");
  const until = new Date(
    Number(from.slice(0, 4)),
    Number(from.slice(5, 7)),
    0,
    12,
  )
    .toISOString()
    .slice(0, 10);
  const data = useData(
      "/attendance-sessions?" + query({ from, until, classId: app.classId }),
    ),
    schedules = useData(
      "/catalog/schedules?" + query({ classId: app.classId }),
    );
  const cls = app.classes.find((c) => c.id === app.classId) || app.classes[0],
    year = app.years.find((y) => y.id === cls?.academicYearId);
  const activeSchedule = schedules.data?.items?.find(
    (s) => s.classRoomId === cls?.id,
  );
  return (
    <>
      <Head
        title="Lịch điểm danh"
        subtitle="Chỉ những ngày có buổi; ngày nghỉ không tính chuyên cần."
      >
        {can(app.user, 9) && (
          <>
            <Button disabled={!cls} onClick={() => setException(true)}>
              Ngày nghỉ / bổ sung
            </Button>
            <Button
              primary
              disabled={!cls}
              onClick={() =>
                setEdit({
                  ...activeSchedule,
                  classRoomId: cls.id,
                  from: activeSchedule?.from || year?.startDate || today(),
                  until: activeSchedule?.until || year?.endDate || today(),
                  daysMask: activeSchedule?.daysMask ?? 1,
                  studentAttendanceEnabled: activeSchedule?.studentAttendanceEnabled ?? true,
                  catechistAttendanceEnabled: activeSchedule?.catechistAttendanceEnabled ?? true,
                  opensAt: activeSchedule?.opensAt?.slice(0, 5) || "09:00",
                  closesAt: activeSchedule?.closesAt?.slice(0, 5) || "09:30",
                })
              }
            >
              <Settings2 />
              Cấu hình lịch
            </Button>
          </>
        )}
      </Head>
      <div className="filters">
        <Select
          label="Lớp"
          value={app.classId}
          onChange={app.setClassId}
          options={[
            { value: "", label: "Toàn bộ lớp" },
            ...app.classes.map((c) => ({ value: c.id, label: c.name })),
          ]}
        />
        <DatePicker
          value={from}
          onChange={(d) => setFrom(d.slice(0, 8) + "01")}
          label="Tháng xem lịch"
        />
      </div>
      <State
        {...data}
        onRetry={data.reload}
        empty={!data.loading && !data.error && !data.data?.items?.length}
      >
        <div className="card flush calendar-list">
          {data.data?.items.map((s) => (
            <div className="calendar-row" key={s.id}>
              <div className="datebox">
                <small>{s.date.slice(5, 7)}</small>
                <strong>{s.date.slice(8, 10)}</strong>
              </div>
              <div>
                <h3>{s.className}</h3>
                <p>
                  {fmtTime(s.opensAtUtc)}–{fmtTime(s.closesAtUtc)}
                </p>
                <p>Điểm danh: {[s.studentAttendanceEnabled !== false && "HS", s.catechistAttendanceEnabled !== false && "GLV"].filter(Boolean).join(" + ")}</p>
                {s.cancelled && <p>Buổi hủy · không tính chuyên cần</p>}
              </div>
              <div className="row wrap"><Status value={s.status} />{can(app.user,9)&&<Button className="sm" disabled={s.cancelled || new Date(s.opensAtUtc.endsWith("Z")?s.opensAtUtc:s.opensAtUtc+"Z").getTime()<=Date.now()} onClick={()=>setSessionEdit(s)}><Edit3/>Chỉnh lịch</Button>}</div>
            </div>
          ))}
        </div>
      </State>
      {sessionEdit&&<SessionScheduleEditor session={sessionEdit} onClose={()=>setSessionEdit(null)}/>}
      {edit && (
        <Editor
          title="Cấu hình lịch điểm danh"
          kind="schedules"
          initial={edit}
          fields={[
            { key: "studentAttendanceEnabled", label: "HS — Điểm danh học sinh", type: "boolean" },
            { key: "catechistAttendanceEnabled", label: "GLV — Điểm danh giáo lý viên", type: "boolean" },
            { key: "daysMask", label: "Các thứ cần điểm danh", type: "days" },
            { key: "from", label: "Hiệu lực từ", type: "date" },
            { key: "until", label: "Đến ngày", type: "date" },
            { key: "opensAt", label: "Giờ mở (HH:mm)", required: true },
            { key: "closesAt", label: "Giờ đóng (HH:mm)", required: true },
          ]}
          onClose={() => setEdit(null)}
        />
      )}{" "}
      {exception && (
        <Editor
          title="Ngày nghỉ hoặc buổi bổ sung"
          kind="exceptions"
          initial={{
            classRoomId: cls?.id,
            date: today(),
            cancelled: true,
            studentAttendanceEnabled: activeSchedule?.studentAttendanceEnabled ?? true,
            catechistAttendanceEnabled: activeSchedule?.catechistAttendanceEnabled ?? true,
            reason: "",
          }}
          fields={[
            { key: "date", label: "Ngày", type: "date" },
            {
              key: "cancelled",
              label: "Nghỉ học",
              type: "boolean",
              hint: "Hủy buổi ngày này (bỏ chọn để thêm buổi)",
            },
            { key: "studentAttendanceEnabled", label: "HS — Điểm danh học sinh", type: "boolean" },
            { key: "catechistAttendanceEnabled", label: "GLV — Điểm danh giáo lý viên", type: "boolean" },
            { key: "reason", label: "Lý do", required: true },
          ]}
          onClose={() => setException(false)}
        />
      )}
    </>
  );
}
function Report() {
  const app = useApp(),
    [from, setFrom] = useState(
      app.years.find((y) => y.id === app.yearId)?.startDate ||
        today().slice(0, 4) + "-01-01",
    ),
    [until, setUntil] = useState(today()),
    [tab, setTab] = useState("all"),
    [missingDate, setMissingDate] = useState(today()),
    [sid, setSid] = useState("");
  const report = useData(
    "/reports/attendance?" +
      query({
        from,
        until,
        yearId: app.yearId,
        classId: tab === "all" ? "" : app.classId,
      }),
  );
  const sessions = useData(
    tab === "missing"
      ? "/attendance-sessions?" +
          query({ from: missingDate, until: missingDate, classId: app.classId })
      : null,
  );
  useEffect(() => {
    let items = sessions.data?.items || [];
    setSid(items[0]?.id || "");
  }, [sessions.data]);
  const roster = useData(
    tab === "missing" && sid ? "/attendance-sessions/" + sid + "/roster" : null,
  );
  let r = report.data,
    rows =
      tab === "all"
        ? r?.classes || []
        : tab === "missing"
          ? (roster.data?.items || [])
              .filter((s) => !s.present)
              .map((s) => ({
                ...s,
                expected: 0,
                present: 0,
                absent: 0,
                rate: null,
                ...r?.students?.find(
                  (st) =>
                    st.studentId === s.studentId &&
                    st.classId ===
                      sessions.data?.items?.find(
                        (session) => session.id === sid,
                      )?.classRoomId,
                ),
              }))
          : r?.students || [];
  function csv() {
    let output = [
      ["Tên", "Mã", "Có mặt", "Vắng", "Buổi hợp lệ", "Tỷ lệ"],
      ...rows.map((x) => [
        x.fullName || x.name,
        x.code,
        x.present,
        x.absent,
        x.expected,
        rate(x.rate),
      ]),
    ]
      .map((row) =>
        row
          .map(
            (x) =>
              '"' +
              String(x ?? "")
                .replace(/^[=+@-]/, "'$&")
                .replaceAll('"', '""') +
              '"',
          )
          .join(","),
      )
      .join("\r\n");
    let url = URL.createObjectURL(
      new Blob(["\ufeff" + output], { type: "text/csv;charset=utf-8" }),
    );
    let a = document.createElement("a");
    a.href = url;
    a.download = "chuyen-can.csv";
    a.click();
    URL.revokeObjectURL(url);
  }
  return (
    <>
      <Head
        title={
          tab === "all"
            ? "Chuyên cần các lớp"
            : tab === "missing"
              ? "Học sinh chưa điểm danh"
              : "Chuyên cần từng lớp"
        }
        subtitle="Chỉ tính buổi hợp lệ đã kết thúc; mẫu số theo roster từng buổi."
      >
        <Button disabled={!rows.length} onClick={csv}>
          <Download />
          Xuất CSV
        </Button>
      </Head>
      <div className="filters">
        <DatePicker label="Từ ngày" value={from} onChange={setFrom} />
        <DatePicker label="Đến ngày" value={until} onChange={setUntil} />
        <Select
          label="Lớp học"
          value={app.classId}
          onChange={app.setClassId}
          options={[
            { value: "", label: "Toàn bộ lớp" },
            ...app.classes.map((c) => ({ value: c.id, label: c.name })),
          ]}
        />
      </div>
      <div className="tabs">
        {[
          ["all", "Các lớp"],
          ["class", "Từng lớp"],
          ["missing", "Chưa điểm danh"],
        ].map(([key, label]) => (
          <a
            key={key}
            href="#"
            aria-current={tab === key ? "page" : undefined}
            onClick={(e) => {
              e.preventDefault();
              setTab(key);
              if (key !== "all" && !app.classId && app.classes.length)
                app.setClassId(app.classes[0].id);
            }}
          >
            {label}
          </a>
        ))}
      </div>
      {tab === "missing" ? (
        <div className="filters">
          <DatePicker
            label="Ngày cần xem chưa điểm danh"
            value={missingDate}
            onChange={setMissingDate}
          />
          <Select
            label="Buổi cần xem"
            value={sid}
            onChange={setSid}
            options={(sessions.data?.items || []).map((s) => ({
              value: s.id,
              label: s.className + " · " + fmtTime(s.opensAtUtc),
            }))}
          />
          <p className="small muted">
            Chưa đến trong buổi đang mở chưa kết luận là vắng.
          </p>
        </div>
      ) : (
        <Stats
          items={[
            ["Lượt hợp lệ", r?.totalExpected, "Theo roster", CalendarDays],
            ["Có mặt", r?.totalPresent, "Lượt đã xác nhận", UserRoundCheck],
            ["Vắng", r?.totalAbsent, "Buổi hợp lệ − có mặt", UsersRound],
            [
              "Chuyên cần",
              rate(r?.rate),
              "Tỷ lệ tổng lượt, không trung bình tỷ lệ lớp",
              ChartNoAxesCombined,
            ],
          ]}
        />
      )}
      <State
        loading={
          report.loading ||
          (tab === "missing" && (sessions.loading || roster.loading))
        }
        error={report.error || sessions.error || roster.error}
        onRetry={() => {
          report.reload();
          sessions.reload();
          roster.reload();
        }}
        empty={!report.loading && !rows.length}
      >
        <Table
          rows={rows}
          rowKey={tab === "all" ? "classId" : "studentId"}
          columns={[
            { label: tab === "all" ? "Lớp học" : "Học sinh", render: nameCell },
            ...(tab === "all"
              ? []
              : [{ label: "Tên Thánh", key: "saintName" }]),
            { label: "Có mặt", key: "present" },
            { label: "Vắng", key: "absent" },
            { label: "Buổi hợp lệ", key: "expected" },
            { label: "Tỷ lệ", render: (r) => rate(r.rate) },
          ]}
        />
        {tab === "class" && (
          <div className="card" style={{ marginTop: 24 }}>
            <h2>Chi tiết theo buổi</h2>
            <p className="small muted">Chỉ những ngày có lịch đã kết thúc.</p>
            {rows.map((s) => (
              <details className="report-person" key={s.studentId}>
                <summary>
                  <strong>{s.fullName}</strong>
                  <span className="muted small">
                    {s.present}/{s.expected} có mặt · {s.absent} vắng
                  </span>
                </summary>
                <div className="date-results">
                  {s.days?.map((d) => (
                    <div key={d.date} className="between">
                      <span>{fmtDate(d.date)}</span>
                      <Badge kind={d.present ? "ok" : "wait"}>
                        {d.present ? "Có mặt" : "Vắng"}
                      </Badge>
                    </div>
                  ))}
                  {!s.days?.length && (
                    <p className="muted small">Chưa có buổi hợp lệ.</p>
                  )}
                </div>
              </details>
            ))}
          </div>
        )}
      </State>
      <p className="small muted">
        Có mặt + Vắng = Lượt hợp lệ. Không tính buổi hủy, tương lai hoặc trước
        ngày ghi danh.
      </p>
    </>
  );
}
function Users() {
  const app = useApp(),
    users = useData("/catalog/users"),
    roles = useData(can(app.user, 16) ? "/catalog/roles" : null),
    catechists = useData(can(app.user, 4) ? "/catalog/catechists" : null);
  const [create, setCreate] = useState(false);
  const [review, setReview] = useState(null),
    [roleId, setRoleId] = useState(""),
    [catechistId, setCatechistId] = useState(""),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [filter, setFilter] = useState("PendingApproval");
  const rows = users.data?.items?.filter((x) => x.status === filter) || [];
  async function action(id, type, body) {
    setBusy(true);
    try {
      await api("/users/" + id + "/" + type, { method: "POST", body });
      app.refresh();
      app.toast("Đã cập nhật tài khoản.");
      setReview(null);
    } catch (e) {
      setError(e.message);
      app.toast(e.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <Head
        title="Tài khoản"
        subtitle="Tài khoản chờ duyệt chưa được sử dụng nghiệp vụ."
      >
        {can(app.user, 20) && (
          <Button primary onClick={() => setCreate(true)}>
            <Plus />
            Tạo tài khoản
          </Button>
        )}
      </Head>
      {create && (
        <Editor
          title="Tạo tài khoản"
          kind="users"
          initial={{ fullName: "", email: "", password: "" }}
          fields={[
            { key: "fullName", label: "Họ và tên", required: true },
            { key: "email", label: "Email", type: "email", required: true },
            {
              key: "password",
              label: "Mật khẩu khởi tạo (từ 12 ký tự)",
              type: "password",
              required: true,
            },
          ]}
          onClose={() => setCreate(false)}
        />
      )}
      <div className="tabs">
        {["PendingApproval", "Active", "Suspended", "Rejected"].map((s) => (
          <a
            key={s}
            href="#"
            aria-current={filter === s ? "page" : undefined}
            onClick={(e) => {
              e.preventDefault();
              setFilter(s);
            }}
          >
            {statusNames[s][0]}
          </a>
        ))}
      </div>
      <State
        {...users}
        onRetry={users.reload}
        empty={!users.loading && !rows.length}
      >
        <Table
          rows={rows}
          columns={[
            { label: "Người đăng ký", render: nameCell },
            { label: "Ngày đăng ký", render: (r) => fmtDate(r.createdAtUtc) },
            { label: "Trạng thái", render: (r) => <Status value={r.status} /> },
            {
              label: "",
              render: (r) =>
                can(app.user, 15) && (
                  <div className="row">
                    {r.status === "PendingApproval" ? (
                      <Button
                        className="sm"
                        onClick={() => {
                          setReview(r);
                          setRoleId(
                            roles.data?.items.find((x) => !x.globalScope)?.id ||
                              "",
                          );
                          setError("");
                        }}
                      >
                        Xem xét
                      </Button>
                    ) : r.status === "Active" && r.id !== app.user.id ? (
                      <Button
                        className="sm danger"
                        onClick={async () => {
                          if (
                            await app.ask({
                              title: "Khóa tài khoản",
                              message:
                                r.fullName +
                                " sẽ không thể sử dụng ứng dụng; các phiên sẽ bị thu hồi.",
                              danger: true,
                            })
                          )
                            action(r.id, "lock");
                        }}
                      >
                        Khóa
                      </Button>
                    ) : null}
                  </div>
                ),
            },
          ]}
        />
      </State>
      {review && (
        <Modal title="Xem xét tài khoản" onClose={() => setReview(null)}>
          <h3 style={{ marginTop: 16 }}>{review.fullName}</h3>
          <p className="muted">{review.email}</p>
          <div className="stack" style={{ marginTop: 20 }}>
            <div className="field">
              <label>Vai trò</label>
              <Select
                label="Chọn vai trò"
                value={roleId}
                onChange={setRoleId}
                options={(roles.data?.items || []).map((r) => ({
                  value: r.id,
                  label: r.name,
                }))}
              />
            </div>
            <div className="field">
              <label>Gắn hồ sơ giáo lý viên</label>
              <Select
                label="Chọn giáo lý viên"
                value={catechistId}
                onChange={setCatechistId}
                options={[
                  { value: "", label: "Không liên kết" },
                  ...(catechists.data?.items || [])
                    .filter((c) => !c.userId)
                    .map((c) => ({ value: c.id, label: c.fullName })),
                ]}
              />
            </div>
          </div>
          {error && <p className="form-error">{error}</p>}
          <div className="modal-actions">
            <Button disabled={busy} onClick={() => action(review.id, "reject")}>
              Từ chối
            </Button>
            <Button
              primary
              disabled={busy || !roleId}
              onClick={() =>
                action(review.id, "approve", {
                  roleId,
                  catechistId: catechistId || null,
                })
              }
            >
              Duyệt tài khoản
            </Button>
          </div>
        </Modal>
      )}
    </>
  );
}
function Roles() {
  const app = useApp(),
    data = useData("/catalog/roles");
  const [id, setId] = useState(""),
    [name, setName] = useState(""),
    [mask, setMask] = useState(0n),
    [global, setGlobal] = useState(false),
    [busy, setBusy] = useState(false);
  function choose(value) {
    setId(value);
    let role = data.data?.items.find((r) => r.id === value);
    setName(role?.name || "");
    setMask(BigInt(role?.permissions || 0));
    setGlobal(!!role?.globalScope);
  }
  async function save() {
    setBusy(true);
    try {
      await api("/catalog/roles" + (id ? "/" + id : ""), {
        method: id ? "PUT" : "POST",
        body: { name, permissions: mask.toString(), globalScope: global },
      });
      app.refresh();
      app.toast("Đã lưu vai trò.");
    } catch (e) {
      app.toast(e.message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <Head
        title="Phân quyền chức năng"
        subtitle="Quyền chức năng và phạm vi lớp đều được kiểm tra tại API."
      >
        <Button onClick={() => choose("")}>
          <Plus />
          Vai trò mới
        </Button>
      </Head>
      <State {...data} onRetry={data.reload}>
        <div className="card">
          <div className="formgrid">
            <div className="field">
              <label>Vai trò</label>
              <Select
                label="Chọn vai trò"
                value={id}
                onChange={choose}
                options={(data.data?.items || []).map((r) => ({
                  value: r.id,
                  label: r.name,
                }))}
              />
            </div>
            <div className="field">
              <label htmlFor="role-name">Tên vai trò</label>
              <input
                id="role-name"
                className="input"
                value={name}
                onChange={(e) => setName(e.target.value)}
                disabled={name === "Admin"}
              />
            </div>
          </div>
          <label className="row" style={{ marginTop: 20 }}>
            <input
              className="check"
              type="checkbox"
              checked={global}
              onChange={(e) => setGlobal(e.target.checked)}
              disabled={name === "Admin"}
            />
            Phạm vi toàn đơn vị (bỏ chọn: chỉ lớp đang phụ trách)
          </label>
          <div className="permission-grid">
            {data.data?.permissions.map((p) => (
              <label className="row" key={p.name}>
                <input
                  className="check"
                  type="checkbox"
                  disabled={name === "Admin"}
                  checked={(mask & BigInt(p.value)) !== 0n}
                  onChange={(e) =>
                    setMask(
                      e.target.checked
                        ? mask | BigInt(p.value)
                        : mask & ~BigInt(p.value),
                    )
                  }
                />
                {permissionLabels[p.name] || p.name}
              </label>
            ))}
          </div>
          <div className="modal-actions">
            <Button
              primary
              disabled={busy || !name.trim() || name === "Admin"}
              onClick={save}
            >
              {busy ? "Đang lưu…" : "Lưu quyền"}
            </Button>
          </div>
        </div>
      </State>
    </>
  );
}
function Question({ question, onAnswer }) {
  const [answer, setAnswer] = useState("");
  return (
    <Modal title={question.title} onClose={() => onAnswer(null)}>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          onAnswer(question.input ? answer.trim() : true);
        }}
      >
        <p className="muted">{question.message}</p>
        {question.input && (
          <div className="field" style={{ marginTop: 20 }}>
            <label htmlFor="question-answer">{question.inputLabel}</label>
            <input
              id="question-answer"
              className="input"
              required
              maxLength={500}
              value={answer}
              onChange={(e) => setAnswer(e.target.value)}
            />
          </div>
        )}
        <div className="modal-actions">
          <Button onClick={() => onAnswer(null)}>Hủy</Button>
          <Button
            type="submit"
            primary={!question.danger}
            className={question.danger ? "danger" : ""}
            disabled={question.input && !answer.trim()}
          >
            Xác nhận
          </Button>
        </div>
      </form>
    </Modal>
  );
}
function PasswordEditor({onClose,onChanged}) {
  const [currentPassword,setCurrent]=useState(""),[newPassword,setNew]=useState(""),[confirmPassword,setConfirm]=useState(""),[busy,setBusy]=useState(false),[error,setError]=useState("");
  async function submit(e){e.preventDefault();setError("");if(newPassword!==confirmPassword){setError("Xác nhận mật khẩu mới không khớp.");return;}setBusy(true);try{await api("/auth/change-password",{method:"POST",body:{currentPassword,newPassword,confirmPassword}});onChanged();}catch(e){setError(e.message);}finally{setBusy(false);}}
  return <Modal title="Đổi mật khẩu" onClose={()=>{if(!busy)onClose();}}><form onSubmit={submit} className="stack"><p className="small muted">Mật khẩu mới cần 12–128 ký tự, có chữ và số. Sau khi đổi, các phiên đăng nhập sẽ kết thúc; bạn đăng nhập lại bằng mật khẩu mới.</p><label className="field">Mật khẩu hiện tại<input className="input" type="password" autoComplete="current-password" required maxLength={128} value={currentPassword} onChange={e=>setCurrent(e.target.value)}/></label><label className="field">Mật khẩu mới<input className="input" type="password" autoComplete="new-password" required minLength={12} maxLength={128} value={newPassword} onChange={e=>setNew(e.target.value)}/></label><label className="field">Xác nhận mật khẩu mới<input className="input" type="password" autoComplete="new-password" required minLength={12} maxLength={128} value={confirmPassword} onChange={e=>setConfirm(e.target.value)}/></label>{error&&<p role="alert" className="form-error">{error}</p>}<div className="modal-actions"><Button disabled={busy} onClick={onClose}>Hủy</Button><Button primary type="submit" disabled={busy}>{busy?"Đang lưu…":"Đổi mật khẩu"}</Button></div></form></Modal>;
}
function App() {
  const [passwordOpen,setPasswordOpen]=useState(false),[passwordNotice,setPasswordNotice]=useState("");
  const [question, setQuestion] = useState(null);
  const ask = (options) =>
    new Promise((resolve) => setQuestion({ ...options, resolve }));
  const [user, setUser] = useState(null),
    [ready, setReady] = useState(false),
    [route, setRoute] = useState(location.hash.slice(1) || "tongquan"),
    [revision, setRevision] = useState(0),
    [toastMessage, setToastMessage] = useState(""),
    [menu, setMenu] = useState(false),
    [sessionChoice, setSessionChoice] = useState(""),
    [classId, setClassId] = useState(""),
    [yearId, setYearId] = useState("");
  const [classes, setClasses] = useState([]),
    [years, setYears] = useState([]);
  useEffect(() => { setClassId(""); }, [yearId]);
  useEffect(() => {
    api("/auth/refresh", { method: "POST", body: { client: "web" } })
      .then((d) => {
        setToken(d.accessToken);
        setUser(d.user);
      })
      .catch(() => {})
      .finally(() => setReady(true));
    const expired = () => {
      setUser(null);
      clearToken();
    };
    window.addEventListener("session-expired", expired);
    return () => window.removeEventListener("session-expired", expired);
  }, []);
  useEffect(() => {
    let cb = () => {
      setRoute(location.hash.slice(1) || "tongquan");
      setMenu(false);
    };
    window.addEventListener("hashchange", cb);
    return () => window.removeEventListener("hashchange", cb);
  }, []);
  useEffect(() => {
    if (!user) return;
    let live = true;
    if (can(user, 6))
      api("/catalog/years")
        .then((d) => {
          if (live) {
            setYears(d.items);
            setYearId(
              (v) =>
                v ||
                d.items.find((y) => y.isActive)?.id ||
                d.items[0]?.id ||
                "",
            );
          }
        })
        .catch(() => {});
    if (can(user, 0))
      api("/catalog/classes?pageSize=100" + (yearId ? "&yearId=" + yearId : ""))
        .then((d) => live && setClasses(d.items))
        .catch(() => {});
    return () => (live = false);
  }, [user, revision, yearId]);
  useEffect(() => {
    if (!toastMessage) return;
    const t = setTimeout(() => setToastMessage(""), 4000);
    return () => clearTimeout(t);
  }, [toastMessage]);
  function go(r, sid) {
    if (sid) setSessionChoice(sid);
    location.hash = r;
    setRoute(r);
    setMenu(false);
  }
  async function logout() {
    try {
      await api("/auth/logout", { method: "POST" });
    } finally {
      clearToken();
      setUser(null);
      setClasses([]);
      setYears([]);
    }
  }
  const value = {
    ask,
    user,
    revision,
    refresh: () => setRevision((v) => v + 1),
    toast: setToastMessage,
    go,
    classes,
    years,
    classId,
    setClassId,
    yearId,
    setYearId,
    sessionChoice,
  };
  if (!ready)
    return <div className="auth muted">Đang kiểm tra phiên đăng nhập…</div>;
  if (!user)
    return (
      <Login notice={passwordNotice}
        onLogin={(u) => {
          setPasswordNotice("");
          setUser(u);
          go("tongquan");
        }}
      />
    );
  let visibleNav = nav.filter((n) => can(user, n[3]));
  const side = (
    <>
      <Brand />
      <nav>
        {visibleNav.map(([key, label, Icon]) => (
          <a
            href={"#" + key}
            key={key}
            className="navitem"
            aria-current={route === key ? "page" : undefined}
          >
            <Icon />
            <span>{label}</span>
          </a>
        ))}
      </nav>
      <div className="profile">
        <div className="user-mark">
          {user.fullName?.slice(0, 2).toUpperCase()}
        </div>
        <div className="min0">
          <strong>{user.fullName}</strong>
          <p className="muted">
            {user.globalScope ? "Toàn đơn vị" : "Lớp được phân công"}
          </p>
        </div>
        <Button ghost className="icon" aria-label="Đổi mật khẩu" title="Đổi mật khẩu" onClick={()=>setPasswordOpen(true)}><KeyRound /></Button>
        <Button ghost className="icon" aria-label="Đăng xuất" onClick={logout}>
          <LogOut />
        </Button>
      </div>
    </>
  );
  let allowed = visibleNav.some((n) => n[0] === route);
  return (
    <Context.Provider value={value}>
      {passwordOpen&&<PasswordEditor onClose={()=>setPasswordOpen(false)} onChanged={()=>{setPasswordOpen(false);clearToken();setUser(null);setClasses([]);setYears([]);setPasswordNotice("Đổi mật khẩu thành công. Vui lòng đăng nhập lại bằng mật khẩu mới.");}}/>}
      <div className="app live-app">
        <aside className="sidebar">{side}</aside>
        <main className="main">
          <header className="topbar">
            <Button
              ghost
              className="icon mobile-menu"
              aria-label="Mở điều hướng"
              onClick={() => setMenu(true)}
            >
              <Menu />
            </Button>
            <span className="crumb">Năm học</span>
            <Select
              label="Năm học"
              value={yearId}
              options={years.map((y) => ({ value: y.id, label: y.name }))}
              onChange={(v) => {
                setYearId(v);
                setClassId("");
              }}
            />
            <span className="spacer" />
            <span className="small muted smallhide">{fmtDate(today())}</span>
          </header>
          <div className="content">
            {!allowed ? (
              <div className="empty muted">
                Bạn chưa có quyền truy cập màn này.
              </div>
            ) : route === "tongquan" ? (
              <Dashboard />
            ) : route === "quet" ? (
              <Scan />
            ) : ["lop", "hocsinh", "glv", "namhoc"].includes(route) ? (
              <CatalogPage key={route} type={route} />
            ) : route === "lich" ? (
              <Schedule />
            ) : route === "baocao" ? (
              <Report />
            ) : route === "duyet" ? (
              <Users />
            ) : (
              <Roles />
            )}
          </div>
        </main>
      </div>
      {menu && (
        <div
          className="mobile-overlay open"
          onClick={(e) => {
            if (e.target === e.currentTarget) setMenu(false);
          }}
        >
          <aside className="sidebar">{side}</aside>
        </div>
      )}
      {question && (
        <Question
          question={question}
          onAnswer={(answer) => {
            question.resolve(answer);
            setQuestion(null);
          }}
        />
      )}
      {toastMessage && (
        <div className="toast" role="status">
          {toastMessage}
        </div>
      )}
    </Context.Provider>
  );
}
createRoot(document.getElementById("root")).render(<App />);
