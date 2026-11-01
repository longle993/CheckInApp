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
export function SessionScheduleEditor({session,onClose}) {
  const app=useApp(),local=s=>new Date(new Date(s.endsWith("Z")?s:s+"Z").getTime()+7*3600000).toISOString().slice(11,16);
  const [date,setDate]=useState(session.date),[open,setOpen]=useState(local(session.opensAtUtc)),[close,setClose]=useState(local(session.closesAtUtc)),[hs,setHs]=useState(session.studentAttendanceEnabled),[glv,setGlv]=useState(session.catechistAttendanceEnabled),[reason,setReason]=useState(""),[error,setError]=useState(""),[busy,setBusy]=useState(false);
  async function save(e){e.preventDefault();setBusy(true);setError("");try{await api(`/attendance-sessions/${session.id}/schedule`,{method:"PUT",body:{date,opensAt:open+":00",closesAt:close+":00",studentAttendanceEnabled:hs,catechistAttendanceEnabled:glv,reason,version:session.version}});app.refresh();app.toast("Đã cập nhật lịch buổi học.");onClose(date);}catch(e){setError(e.message);}finally{setBusy(false);}}
  return <Modal title={"Chỉnh lịch · "+session.className+" · "+fmtDate(session.date)} onClose={()=>{if(!busy)onClose();}}><form onSubmit={save} className="stack"><p className="small muted">Giờ Việt Nam. Ngày giờ bắt đầu mới phải lớn hơn hiện tại.</p><label className="field">Ngày học<input type="date" className="input" required value={date} onChange={e=>setDate(e.target.value)}/></label><label className="field">Giờ bắt đầu<input className="input" required type="time" value={open} onChange={e=>setOpen(e.target.value)}/></label><label className="field">Giờ kết thúc<input className="input" required type="time" value={close} onChange={e=>setClose(e.target.value)}/></label><label className="row"><input type="checkbox" className="check" checked={hs} onChange={e=>setHs(e.target.checked)}/>HS — Điểm danh học sinh</label><label className="row"><input type="checkbox" className="check" checked={glv} onChange={e=>setGlv(e.target.checked)}/>GLV — Điểm danh giáo lý viên</label><label className="field">Lý do điều chỉnh<input className="input" required maxLength={500} value={reason} onChange={e=>setReason(e.target.value)}/></label>{error&&<p className="form-error" role="alert">{error}</p>}<div className="modal-actions"><Button disabled={busy} onClick={onClose}>Hủy</Button><Button primary type="submit" disabled={busy}>Lưu lịch</Button></div></form></Modal>;
}
export function Schedule() {
  const app = useApp(),
    [edit, setEdit] = useState(null),
    [sessionEdit,setSessionEdit]=useState(null),
    [highlighted,setHighlighted]=useState(""),
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
      "/attendance-sessions?" + query({ from, until, classId: app.classId,yearId:app.yearId }),
    ),
    schedules = useData(
      "/catalog/schedules?" + query({ classId: app.classId,yearId:app.yearId }),
    );
  const cls = app.classes.find((c) => c.id === app.classId),
    year = app.years.find((y) => y.id === cls?.academicYearId);
  const activeSchedule = schedules.data?.items?.find(
    (s) => s.classRoomId === cls?.id,
  );
  const exceptionSchedule=schedules.data?.items?.find(s=>s.id===exception)||activeSchedule;
  return (
    <>
      <Head
        title="Lịch điểm danh"
        subtitle="Chỉ những ngày có buổi; ngày nghỉ không tính chuyên cần."
      >
        {can(app.user, 9) && (
          <>
            <Button disabled={!cls} onClick={()=>setEdit({classRoomId:cls.id,from:today(),until:year?.endDate||today(),daysMask:1,opensAt:"09:00",closesAt:"10:00",studentAttendanceEnabled:true,catechistAttendanceEnabled:true})}>Thêm khung lịch</Button>
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
              Sửa khung đầu tiên
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
            <div className={"calendar-row "+(highlighted===s.id?"updated-session":"")} key={s.id}>
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
      <div className="stack">{schedules.data?.items?.map(s=><div className="row wrap" key={s.id}><span>{app.classes.find(c=>c.id===s.classRoomId)?.name} · {s.opensAt}–{s.closesAt}</span><Button onClick={()=>setEdit({...s,opensAt:s.opensAt.slice(0,5),closesAt:s.closesAt.slice(0,5)})}>Sửa khung lịch</Button><Button onClick={()=>setException(s.id)}>Nghỉ / bổ sung khung này</Button></div>)}</div>
      {sessionEdit&&<SessionScheduleEditor session={sessionEdit} onClose={date=>{if(typeof date==="string"){setFrom(date.slice(0,8)+"01");setHighlighted(sessionEdit.id);}setSessionEdit(null);}}/>}
      {edit && (
        <Editor
          title="Cấu hình lịch điểm danh"
          kind="schedules"
          id={edit.id}
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
            classRoomId: exceptionSchedule?.classRoomId,
            scheduleId: exceptionSchedule?.id,
            date: today(),
            cancelled: true,
            studentAttendanceEnabled: activeSchedule?.studentAttendanceEnabled ?? true,
            catechistAttendanceEnabled: activeSchedule?.catechistAttendanceEnabled ?? true,
            reason: "",
          }}
          fields={[
            {key:"scheduleId",label:"Khung lịch áp dụng",type:"select",options:(schedules.data?.items||[]).filter(s=>s.classRoomId===exceptionSchedule?.classRoomId).map(s=>({value:s.id,label:s.opensAt+"–"+s.closesAt}))},
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