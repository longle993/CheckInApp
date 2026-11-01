import React, {
  createContext,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
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
import { ExcelActions, PresencePanel } from "./extensions";
import {useApp,useData,query,nameCell,rate,Status,statusNames,permissionLabels,Editor,previewDays} from "./main";
export function Scan({teacher=false}) {
  const app=useApp();
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
    app.yearId ? "/attendance-sessions?" + query({ from: date, until: date,yearId:app.yearId,classId:app.classId }) : null,
  );
  const [rememberPrompt,setRememberPrompt]=useState(false);
  const prefKey="ci-class-"+app.user.id+"-"+(teacher?"glv":"hs");
  useEffect(()=>{try{const raw=document.cookie.split("; ").find(x=>x.startsWith(prefKey+"="));if(raw){const pref=JSON.parse(decodeURIComponent(raw.slice(prefKey.length+1)));if(pref.v===1&&pref.year===app.yearId&&app.classes.some(c=>c.id===pref.cls))app.setClassId(pref.cls);}}catch{}},[app.yearId,app.classes]);
  const all = (sessions.data?.items || []).filter(s=>teacher?s.catechistAttendanceEnabled:s.studentAttendanceEnabled);
  useEffect(() => {
    if(!app.classId&&all.length){const preferred=all.filter(s=>s.status==="Open");const candidates=preferred.length?preferred:all.filter(s=>s.status==="Scheduled");const ids=[...new Set(candidates.map(s=>s.classRoomId))];if(ids.length===1)app.setClassId(ids[0]);else {setId("");return;}}
    if (all.length && !all.some((s) => s.id === id)) setId(all.find(s=>s.status==="Open")?.id||all[0].id);
    if (!all.length) setId("");
  }, [sessions.data]);
  const session = all.find((s) => s.id === id);
  const roster = useData(!teacher&&id ? `/attendance-sessions/${id}/roster` : null);
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
    if (!retry && (teacher ? !credential.trim().startsWith("CG1-") : credential.trim().startsWith("CG1-"))){setError(teacher?"Vui lòng quét thẻ giáo lý viên.":"Vui lòng quét thẻ thiếu nhi.");return;}
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
          personType:teacher?"Catechist":"Student",
          credential: credential.trim(),
          clientRequestId: crypto.randomUUID(),
          method,
        };
    try {
      const r = await api(`/attendance-sessions/${id}/${teacher?"catechists":"students"}/check-ins`, {
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
      <div className="filters"><Select label="Lớp điểm danh" value={app.classId} options={[{value:"",label:"Chọn lớp"},...app.classes.map(c=>({value:c.id,label:c.name}))]} onChange={v=>{app.setClassId(v);setRememberPrompt(true);}}/>

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
      {rememberPrompt&&app.classId&&<div className="callout"><span>Lưu lớp đã chọn cho lần sau?</span><Button onClick={()=>{document.cookie=prefKey+"="+encodeURIComponent(JSON.stringify({v:1,year:app.yearId,cls:app.classId}))+"; Path=/; Max-Age=2592000; SameSite=Lax"+(location.protocol==="https:"?"; Secure":"");setRememberPrompt(false);}}>Lưu lựa chọn</Button><Button onClick={()=>setRememberPrompt(false)}>Chỉ dùng lần này</Button></div>}
      <Button ghost onClick={()=>{document.cookie=prefKey+"=; Path=/; Max-Age=0; SameSite=Lax";toast("Đã xóa lớp đã nhớ.");}}>Xóa lựa chọn đã nhớ</Button>
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
                <h2>Quét thẻ {teacher?"giáo lý viên":"thiếu nhi"}</h2>
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
            {!teacher && <section className="card flush">
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
                        <strong>{displayPerson(s)}</strong>
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
      {session && <PresencePanel teacherOnly={teacher} session={session} students={students} user={user} refresh={refresh} toast={toast} />}
    </>
  );
}