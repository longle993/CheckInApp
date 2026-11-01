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
import AvatarEditor,{PersonAvatar} from "./PersonAvatar";
import { QRCodeSVG } from "qrcode.react";
import JsBarcode from "jsbarcode";
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
const BatchPrint=React.lazy(()=>import("./BatchPrint"));
export function CatalogPage({ type }) {
  const [avatarEdit,setAvatarEdit]=useState(null),[batchOpen,setBatchOpen]=useState(false);
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
        ...(["lop","hocsinh"].includes(type) ? { yearId } : {}),
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
  if(type==="hocsinh"||type==="glv") cols.unshift({label:"Ảnh",render:r=><PersonAvatar kind={type==="glv"?"catechists":"students"} id={r.id} name={displayPerson(r)} revision={app.revision}/>});
  cols.push({
    label: "",
    render: (r) => (
      <div className="row wrap">{["hocsinh","glv"].includes(type)&&can(user,permission)&&<Button className="sm" onClick={()=>setAvatarEdit(r)}>Ảnh đại diện</Button>}
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
      {["hocsinh","glv"].includes(type)&&can(user,17)&&<Button onClick={()=>setBatchOpen(true)}>In thẻ hàng loạt</Button>}
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
      {batchOpen&&<React.Suspense fallback={<p>Đang tải công cụ in…</p>}><BatchPrint kind={kind} rows={rows} path={"/catalog/"+kind+"?"+query({q:search,page:1,pageSize:100,yearId,classId})} yearId={yearId} yearName={years.find(y=>y.id===yearId)?.name} className={classes.find(c=>c.id===classId)?.name} onClose={()=>setBatchOpen(false)} onIssued={app.refresh}/></React.Suspense>}
      {avatarEdit&&<AvatarEditor kind={type==="glv"?"catechists":"students"} person={avatarEdit} onClose={()=>setAvatarEdit(null)} onSaved={app.refresh}/>}
      {credential && (
        <CredentialCard
          student={credential}
          onClose={() => setCredential(null)}
        />
      )}
    </>
  );
}
export function CredentialCard({ student, onClose }) {
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
        <div className="card-information"><PersonAvatar kind={teacher?"catechists":"students"} id={student.id} name={displayPerson(student)}/><strong>CheckInApp · {teacher ? "THẺ GIÁO LÝ VIÊN" : "THẺ HỌC SINH"}</strong><h3>{displayPerson(student)}</h3>{!teacher && <p>Ngày sinh: {fmtDate(student.dateOfBirth)}</p>}<p>Mã {teacher ? "GLV" : "HS"}: {student.code}</p><p>Lớp: {student.className || cls?.name || "—"}</p><p>Năm học: {student.academicYearName || year?.name || "—"}</p></div>
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
export function Assignments({ data }) {
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
                {displayPerson(data?.items.find((c) => c.id === a.catechistId))}
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
                label: displayPerson(c),
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