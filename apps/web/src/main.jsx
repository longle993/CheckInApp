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
  Eye,
  EyeOff,
} from "lucide-react";



import { displayPerson, api, can, setToken, clearToken, today, fmtDate, fmtTime } from "./api";
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

const BatchPrint=React.lazy(()=>import("./BatchPrint"));
const TimingReport=React.lazy(()=>import("./TimingReport"));
const DashboardPage = React.lazy(()=>import("./DashboardPage"));
class RouteBoundary extends React.Component{state={error:false};static getDerivedStateFromError(){return {error:true}}render(){return this.state.error?<section role="alert"><p>Không tải được màn hình. Kiểm tra kết nối rồi tải lại.</p><Button onClick={()=>window.location.reload()}>Tải lại</Button></section>:this.props.children}}
const permissionLabels = {
  UserManage: "Sửa thông tin tài khoản / xem xét lại",
  WebSettingsView: "Xem thông tin web",
  WebSettingsManage: "Sửa tên và logo web",
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
  ["quet", "Điểm danh thiếu nhi", ScanLine, 10],
  ["quet-glv", "Điểm danh GLV", UserRoundCheck, 10],
  ["lop", "Lớp học", School, 0],
  ["hocsinh", "Học sinh", UsersRound, 2],
  ["lich", "Lịch", CalendarDays, 8],
  ["baocao", "Báo cáo chuyên cần", ChartNoAxesCombined, 13],
  ["baocao-gio", "Đi muộn về sớm", Clock3, 13],
  ["glv", "Giáo lý viên", UserRoundCheck, 4],
  ["namhoc", "Năm học", CalendarRange, 6],
  ["admin", "Admin Console", ShieldCheck, 14],
];
function useData(path, delay = 0) {
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
    const controller = new AbortController();
    const timer = setTimeout(()=>api(path,{signal:controller.signal})
      .then((data) => live && setState({ data, path, loading: false, error: null }))
      .catch(
        (error) => live && error.name!=="AbortError" && setState({ data: null, path, loading: false, error }),
      ),delay);
    return () => {live=false;clearTimeout(timer);controller.abort()};
  }, [path, revision, again, delay]);
  return { ...state, data:state.path===path?state.data:null, reload: () => setAgain((x) => x + 1) };
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
    <strong className="long">{displayPerson(r)}</strong>
    <small>{r.code || r.email}</small>
  </>
);
const rate = (r) =>
  r == null
    ? "Chưa có buổi"
    : Number(r).toLocaleString("vi-VN", { maximumFractionDigits: 2 }) + "%";
export function Stats({ items }) {
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
  const [showPassword,setShowPassword]=useState(false);
  const desktop=typeof window!=="undefined"&&window.matchMedia("(min-width:960px)").matches;
  async function submit(e) {
    if(busy){e.preventDefault();return;}
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
    <div className="auth auth-redesign"><div className="auth-shell"><section className="auth-identity" aria-label="Đơn vị sử dụng"><div className="auth-identity-content"><Brand auth/><p className="auth-parish">THIẾU NHI THÁNH THỂ GIÁO XỨ BÙI PHÁT</p><p className="auth-unit">XỨ ĐOÀN THIÊN THẦN RAPHAEL</p></div><svg className="auth-wave" viewBox="0 0 100 600" preserveAspectRatio="none" aria-hidden="true"><path fill="#ab6575" d="M45 0H100V600H45Q85 550 45 500T45 400T45 300T45 200T45 100T45 0"/><path fill="#d9b5bd" d="M66 0H100V600H66Q106 550 66 500T66 400T66 300T66 200T66 100T66 0"/><path fill="white" d="M85 0H100V600H85Q125 550 85 500T85 400T85 300T85 200T85 100T85 0"/></svg></section><section className="authbox">
        <h1>{register ? "Đăng ký tài khoản" : "Đăng nhập"}</h1>
        <p className="muted">
          {register
            ? "Admin cần duyệt tài khoản trước khi sử dụng."
            : "Dùng tài khoản đã được admin duyệt."}
        </p>
        {notice && <p role="status" className="callout">{notice}</p>}
        <form onSubmit={submit} aria-busy={busy}>
          {register && (
            <div className="field">
              <label htmlFor="fullName">
                Họ và tên <em>*</em>
              </label>
              <input
                autoFocus={desktop}
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
              autoFocus={desktop&&!register}
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
            <div className="auth-password"><input
              id="password"
              className="input"
              name="password"
              type={showPassword?"text":"password"}
              autoComplete={register ? "new-password" : "current-password"}
              minLength={register ? 12 : 1}
              maxLength={128}
              placeholder={
                register ? "Từ 12 ký tự, có chữ và số" : "Nhập mật khẩu"
              }
              required
            /><Button className="auth-password-toggle" aria-label={showPassword?"Ẩn mật khẩu":"Hiện mật khẩu"} aria-pressed={showPassword} onClick={()=>setShowPassword(v=>!v)}>{showPassword?<EyeOff size={20}/>:<Eye size={20}/>}</Button></div>
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
          disabled={busy}
          onClick={() => {
            setRegister(!register);
            setShowPassword(false);
            setMessage("");
            setError("");
          }}
        >
          {register
            ? "Đã có tài khoản? Đăng nhập"
            : "Chưa có tài khoản? Đăng ký"}
        </Button>
        <p className="small muted">Quên mật khẩu: liên hệ admin của đơn vị.</p>
      </section></div>
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
  const app=useApp();
  const { go } = app;
  const [date, setDate] = useState(today());
  const data = useData(
    app.yearId ? "/attendance-sessions?" + query({ from: date, until: date,yearId:app.yearId,classId:app.classId }) : null,
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
          Điểm danh thiếu nhi
        </Button><Button onClick={()=>go("quet-glv")}><UserRoundCheck/>Điểm danh giáo lý viên</Button>
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
const Scan=React.lazy(()=>import("./ScanScreen").then(m=>({default:m.Scan})));
const CatalogPage=React.lazy(()=>import("./CatalogScreens").then(m=>({default:m.CatalogPage})));
const CredentialCard=React.lazy(()=>import("./CatalogScreens").then(m=>({default:m.CredentialCard})));
const Assignments=React.lazy(()=>import("./CatalogScreens").then(m=>({default:m.Assignments})));
const SessionScheduleEditor=React.lazy(()=>import("./ScheduleScreen").then(m=>({default:m.SessionScheduleEditor})));
const Schedule=React.lazy(()=>import("./ScheduleScreen").then(m=>({default:m.Schedule})));
const Report=React.lazy(()=>import("./ReportScreen").then(m=>({default:m.Report})));
const AccountProfileEditor=React.lazy(()=>import("./AdminScreens").then(m=>({default:m.AccountProfileEditor})));
const Users=React.lazy(()=>import("./AdminScreens").then(m=>({default:m.Users})));
const Roles=React.lazy(()=>import("./AdminScreens").then(m=>({default:m.Roles})));
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
const AdminConsole=React.lazy(()=>import("./AdminScreens").then(m=>({default:m.AdminConsole})));
const ReleaseVersion=React.lazy(()=>import("./AdminScreens").then(m=>({default:m.ReleaseVersion})));
const WebSettingsEditor=React.lazy(()=>import("./AdminScreens").then(m=>({default:m.WebSettingsEditor})));
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
      api("/catalog/years?pageSize=1000")
        .then((d) => {
          if (live) {
            setYears(d.items);
            setYearId(v=>{
              if(v&&d.items.some(y=>y.id===v))return v;
              const active=d.items.filter(y=>y.isActive); if(active.length) return active.length===1?active[0].id:""; const valid=d.items.filter(y=>y.startDate<=today()&&y.endDate>=today());return valid.length===1?valid[0].id:"";
            });
          }
        })
        .catch(() => {});
    return () => (live = false);
  }, [user, revision]);
  useEffect(()=>{
    if(!user||!can(user,0)||!yearId){setClasses([]);return}
    let live=true;const controller=new AbortController();setClasses([]);
    api('/catalog/classes?pageSize=1000&yearId='+yearId,{signal:controller.signal}).then(d=>live&&setClasses(d.items)).catch(()=>{});
    return()=>{live=false;controller.abort()};
  },[user,revision,yearId]);
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
      setYearId("");setClassId("");setSessionChoice("");
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
  let visibleNav = nav.filter((n) => n[0]==="admin" ? [14,16,28,29].some(bit=>can(user,bit)) : can(user,n[3]));
  const side = (
    <>
      <Brand />
      <nav>
        {visibleNav.filter(n=>!["quet","quet-glv","baocao","baocao-gio"].includes(n[0])).map(([key,label,Icon])=><a href={"#"+key} key={key} className="navitem" aria-current={route===key?"page":undefined}><Icon/><span>{label}</span></a>)}
        {[["Điểm danh",["quet","quet-glv"]],["Báo cáo",["baocao","baocao-gio"]]].map(([label,keys])=>visibleNav.some(n=>keys.includes(n[0]))&&<details key={label} open><summary className="navitem">{label==="Điểm danh"?<ScanLine/>:<ChartNoAxesCombined/>}<span>{label}</span><span className="nav-chevron" aria-hidden="true">⌄</span></summary>{visibleNav.filter(n=>keys.includes(n[0])).map(([key,name,Icon])=><a style={{paddingLeft:30}} className="navitem" key={key} href={"#"+key} aria-current={route===key?"page":undefined}><Icon/><span>{name}</span></a>)}</details>)}

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
        <div className="profile-actions"><Button ghost className="icon" aria-label="Đổi mật khẩu" title="Đổi mật khẩu" onClick={()=>setPasswordOpen(true)}><KeyRound /></Button>
        <Button ghost className="icon" aria-label="Đăng xuất" onClick={logout}>
          <LogOut />
        </Button></div>
      </div>
    </>
  );
  let allowed = route==="diemdanh" || route==="khac" || visibleNav.some((n) => n[0] === route) || (route==="duyet"&&can(user,14)) || (route==="quyen"&&can(user,16));
  return (
    <Context.Provider value={value}>
      {passwordOpen&&<PasswordEditor onClose={()=>setPasswordOpen(false)} onChanged={()=>{setPasswordOpen(false);clearToken();setUser(null);setClasses([]);setYears([]);setYearId("");setClassId("");setSessionChoice("");setPasswordNotice("Đổi mật khẩu thành công. Vui lòng đăng nhập lại bằng mật khẩu mới.");}}/>}
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
            {!yearId&&<span role="status" className="small muted">{years.filter(y=>y.startDate<=today()&&y.endDate>=today()).length>1?"Có nhiều năm học trùng ngày hiện tại. Hãy chọn năm.":"Chưa có năm học phù hợp hôm nay. Hãy chọn năm."}</span>}
            <span className="spacer" />
            {route==="tongquan"&&<div id="dashboard-customize-slot"/>}
            <span className="small muted smallhide">{fmtDate(today())}</span>
          </header>
          <div className="content"><RouteBoundary key={route}><React.Suspense fallback={<p role="status">Đang tải màn hình…</p>}>
            {!allowed ? (
              <div className="empty muted">
                Bạn chưa có quyền truy cập màn này.
              </div>
            ) : route === "diemdanh" ? (<><Head title="Điểm danh"/><div className="dashboard-actions">{visibleNav.filter(n=>["quet","quet-glv"].includes(n[0])).map(([key,label,Icon])=><Button key={key} onClick={()=>go(key)}><Icon/>{label}</Button>)}</div></>) : route === "khac" ? (<><Head title="Chức năng khác"/><div className="stack">{visibleNav.filter(n=>!["tongquan","quet","quet-glv","lich"].includes(n[0])).map(([key,label,Icon])=><Button key={key} onClick={()=>go(key)}><Icon/>{label}</Button>)}<Button onClick={()=>setPasswordOpen(true)}>Đổi mật khẩu</Button><Button onClick={logout}>Đăng xuất</Button></div></>) : route === "tongquan" ? (
              <React.Suspense fallback={<p>Đang tải tổng quan…</p>}><DashboardPage app={value}/></React.Suspense>
            ) : ["quet","quet-glv"].includes(route) ? (
              <Scan key={route} teacher={route==="quet-glv"}/>
            ) : ["lop", "hocsinh", "glv", "namhoc"].includes(route) ? (
              <CatalogPage key={route} type={route} />
            ) : route === "lich" ? (
              <Schedule />
            ) : route === "baocao-gio" ? (<React.Suspense fallback={<p>Đang tải báo cáo…</p>}><TimingReport app={value}/></React.Suspense>) : route === "baocao" ? (
              <Report />
            ) : (
              <AdminConsole key={route} initial={route==="duyet"?"users":route==="quyen"?"roles":null} />
            )}
          </React.Suspense></RouteBoundary></div>
        </main>
      </div>
      <nav className="bottom-nav" aria-label="Điều hướng chính">{[["tongquan","Tổng quan",LayoutDashboard],["diemdanh","Điểm danh",ScanLine],["lich","Lịch",CalendarDays],["baocao","Báo cáo",ChartNoAxesCombined],["khac","Khác",Menu]].filter(([key])=>key==="khac"||key==="diemdanh"||visibleNav.some(n=>n[0]===key)).map(([key,label,Icon])=><a key={key} href={"#"+key} aria-current={route===key?"page":undefined}><Icon size={20}/><span>{label}</span></a>)}</nav>
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

export {useApp,useData,query,nameCell,rate,Status,statusNames,permissionLabels,Editor,previewDays};
