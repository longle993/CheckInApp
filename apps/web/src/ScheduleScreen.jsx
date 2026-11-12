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
import BulkDelete from './BulkDelete';
export function SessionScheduleEditor({session,onClose}) {
  const app=useApp(),local=s=>new Date(new Date(s.endsWith("Z")?s:s+"Z").getTime()+7*3600000).toISOString().slice(11,16);
  const [date,setDate]=useState(session.date),[open,setOpen]=useState(local(session.opensAtUtc)),[close,setClose]=useState(local(session.closesAtUtc)),[hs,setHs]=useState(session.studentAttendanceEnabled),[glv,setGlv]=useState(session.catechistAttendanceEnabled),[reason,setReason]=useState(""),[error,setError]=useState(""),[busy,setBusy]=useState(false);
  async function save(e){e.preventDefault();setBusy(true);setError("");try{await api(`/attendance-sessions/${session.id}/schedule`,{method:"PUT",body:{date,opensAt:open+":00",closesAt:close+":00",studentAttendanceEnabled:hs,catechistAttendanceEnabled:glv,reason,version:session.version}});app.refresh();app.toast("Đã cập nhật lịch buổi học.");onClose(date);}catch(e){setError(e.message);}finally{setBusy(false);}}
  return <Modal title={"Chỉnh lịch · "+session.className+" · "+fmtDate(session.date)} onClose={()=>{if(!busy)onClose();}}><form onSubmit={save} className="stack"><p className="small muted">Giờ Việt Nam. Ngày giờ bắt đầu mới phải lớn hơn hiện tại.</p><label className="field">Ngày học<input type="date" className="input" required value={date} onChange={e=>setDate(e.target.value)}/></label><label className="field">Giờ bắt đầu<input className="input" required type="time" value={open} onChange={e=>setOpen(e.target.value)}/></label><label className="field">Giờ kết thúc<input className="input" required type="time" value={close} onChange={e=>setClose(e.target.value)}/></label><label className="row"><input type="checkbox" className="check" checked={hs} onChange={e=>setHs(e.target.checked)}/>HS — Điểm danh học sinh</label><label className="row"><input type="checkbox" className="check" checked={glv} onChange={e=>setGlv(e.target.checked)}/>GLV — Điểm danh giáo lý viên</label><label className="field">Lý do điều chỉnh<input className="input" required maxLength={500} value={reason} onChange={e=>setReason(e.target.value)}/></label>{error&&<p className="form-error" role="alert">{error}</p>}<div className="modal-actions"><Button disabled={busy} onClick={onClose}>Hủy</Button><Button primary type="submit" disabled={busy}>Lưu lịch</Button></div></form></Modal>;
}
export function Schedule() {
  const [selecting,setSelecting]=useState(false),[selected,setSelected]=useState([]),[confirmDelete,setConfirmDelete]=useState(false);
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
      app.yearId?"/attendance-sessions?" + query({ from, until, classId: app.classId,yearId:app.yearId }):null,
    ),
    schedules = useData(
      app.yearId?"/catalog/schedules?" + query({ classId: app.classId,yearId:app.yearId }):null,
    );
  const cls = app.classes.find((c) => c.id === app.classId),
    year = app.years.find((y) => y.id === cls?.academicYearId);
  const activeSchedule = schedules.data?.items?.find(
    (s) => s.classRoomId === cls?.id,
  );
  const exceptionSchedule=schedules.data?.items?.find(s=>s.id===exception)||activeSchedule;
  useEffect(()=>{setSelected([]);setSelecting(false)},[from,app.yearId,app.classId]);
  const frames=schedules.data?.items||[],sessions=(data.data?.items||[]).filter(s=>!s.cancelled);
  const groups=frames.map(frame=>({id:frame.id,frame,name:app.classes.find(c=>c.id===frame.classRoomId)?.name||'Khung lịch',children:sessions.filter(s=>s.scheduleId===frame.id)}));
  const extra=sessions.filter(s=>!frames.some(f=>f.id===s.scheduleId));if(extra.length)groups.push({id:'extra',name:'Lịch bổ sung',children:extra});
  function toggleFrame(group){setSelected(v=>v.includes(group.id)?v.filter(x=>x!==group.id):[...v.filter(x=>!group.children.some(s=>s.id===x)),group.id])}
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
            <Button onClick={()=>{setSelecting(v=>!v);setSelected([])}}>{selecting?'Hủy chọn':'Xóa'}</Button>
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
      {selecting&&<div className="selection-toolbar"><span>Đã chọn {selected.length}</span><Button disabled={!selected.length} onClick={()=>setConfirmDelete(true)}>Xóa ({selected.length})</Button></div>}
      {confirmDelete&&<BulkDelete kind="schedule-items" ids={selected} onClose={()=>setConfirmDelete(false)} onDeleted={()=>{setSelected([]);setSelecting(false);setConfirmDelete(false);app.refresh()}}/>}
      <State loading={data.loading||schedules.loading} error={data.error||schedules.error} onRetry={()=>{data.reload();schedules.reload()}} empty={!data.loading&&!schedules.loading&&!groups.length}>
      <div className="schedule-tree">{groups.map(group=><section className="schedule-group" key={group.id}>
        <div className="schedule-group-heading">{selecting&&group.frame&&<input type="checkbox" aria-label={'Chọn khung '+group.name} checked={selected.includes(group.id)} ref={el=>{if(el)el.indeterminate=!selected.includes(group.id)&&group.children.some(x=>selected.includes(x.id))}} onChange={()=>toggleFrame(group)}/>}
        <details><summary><span><strong>{group.name}</strong>{group.frame&&<small>{fmtDate(group.frame.from)} – {fmtDate(group.frame.until)} · {group.frame.opensAt.slice(0,5)}–{group.frame.closesAt.slice(0,5)} · {['CN','T2','T3','T4','T5','T6','T7'].filter((_,i)=>group.frame.daysMask&(1<<i)).join(', ')} · {[group.frame.studentAttendanceEnabled&&'HS',group.frame.catechistAttendanceEnabled&&'GLV'].filter(Boolean).join(' + ')}</small>}<small>{group.children.length} buổi trong tháng đang xem</small></span><span aria-hidden="true">⌄</span></summary>
        {group.frame&&can(app.user,9)&&<div className="row wrap schedule-tools"><Button onClick={()=>setEdit({...group.frame,opensAt:group.frame.opensAt.slice(0,5),closesAt:group.frame.closesAt.slice(0,5)})}>Sửa khung lịch</Button><Button onClick={()=>setException(group.id)}>Thêm lịch chi tiết / ngày nghỉ</Button></div>}
        {!group.children.length&&<p>Chưa có buổi trong tháng này.</p>}
        {group.children.map(s=><div className={'calendar-row '+(highlighted===s.id?'updated-session':'')} key={s.id}>
          {selecting&&<input type="checkbox" aria-label={'Chọn buổi '+s.date+' '+fmtTime(s.opensAtUtc)} disabled={selected.includes(group.id)} checked={selected.includes(group.id)||selected.includes(s.id)} onChange={()=>setSelected(v=>v.includes(s.id)?v.filter(x=>x!==s.id):[...v,s.id])}/>}
          <div className="datebox"><small>{s.date.slice(5,7)}</small><strong>{s.date.slice(8)}</strong></div><div><h3>{s.className}</h3><p>{fmtDate(s.date)} · {fmtTime(s.opensAtUtc)}–{fmtTime(s.closesAtUtc)}</p><p>{[s.studentAttendanceEnabled&&'HS',s.catechistAttendanceEnabled&&'GLV'].filter(Boolean).join(' + ')}</p></div><div className="row wrap"><Status value={s.status}/>{can(app.user,9)&&<Button disabled={s.cancelled||new Date(s.opensAtUtc.endsWith('Z')?s.opensAtUtc:s.opensAtUtc+'Z')<=new Date()} onClick={()=>setSessionEdit(s)}><Edit3/>Sửa lịch điểm danh</Button>}</div>
        </div>)}
        </details></div>
      </section>)}</div></State>
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