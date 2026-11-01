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
export function AccountProfileEditor({user,onClose}){const app=useApp(),roles=useData(can(app.user,16)?"/catalog/roles":null),teachers=useData(can(app.user,4)?"/catalog/catechists?pageSize=1000":null),[roleId,setRoleId]=useState(""),[teacherId,setTeacherId]=useState(""),[name,setName]=useState(user.fullName),[email,setEmail]=useState(user.email),[reason,setReason]=useState(""),[error,setError]=useState(""),[busy,setBusy]=useState(false);return <Modal title="Sửa thông tin tài khoản" onClose={onClose}><form className="stack" onSubmit={async e=>{e.preventDefault();setBusy(true);try{await api("/users/"+user.id+"/profile",{method:"PUT",body:{fullName:name,email,reason,expectedFullName:user.fullName,expectedEmail:user.email,profileVersion:user.profileVersion||null,roleId:roleId||null,catechistId:teacherId||null}});app.refresh();app.toast("Đã lưu và thu hồi phiên đăng nhập cũ.");onClose()}catch(e){setError(e.message)}finally{setBusy(false)}}}><label>Họ và tên<input required value={name} onChange={e=>setName(e.target.value)}/></label><label>Email<input type="email" required value={email} onChange={e=>setEmail(e.target.value)}/></label>{can(app.user,16)&&<><Select label="Đổi vai trò (để trống để giữ nguyên)" value={roleId} onChange={setRoleId} options={[{value:"",label:"Giữ nguyên vai trò"},...(roles.data?.items||[]).map(r=>({value:r.id,label:r.name}))]}/><Select label="GLV liên kết khi đổi vai trò" value={teacherId} onChange={setTeacherId} options={[{value:"",label:"Không chọn"},...(teachers.data?.items||[]).filter(t=>!t.userId||t.userId===user.id).map(t=>({value:t.id,label:displayPerson(t)}))]}/></>}<label>Lý do<input required maxLength={500} value={reason} onChange={e=>setReason(e.target.value)}/></label>{error&&<p role="alert">{error}</p>}<Button type="submit" primary disabled={busy}>Lưu thông tin</Button></form></Modal>}
export function Users() {
  const app = useApp(),
    users = useData("/catalog/users"),
    roles = useData(can(app.user, 16) ? "/catalog/roles" : null),
    catechists = useData(can(app.user, 4) ? "/catalog/catechists" : null);
  const [create, setCreate] = useState(false);
  const [profileEdit,setProfileEdit]=useState(null);
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
      <Head level={2}
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
      {profileEdit&&<AccountProfileEditor user={profileEdit} onClose={()=>setProfileEdit(null)}/>}
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
            {label:"Chỉnh sửa",render:r=>can(app.user,30)?<Button onClick={()=>setProfileEdit(r)}>Sửa thông tin</Button>:null},
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
                    .map((c) => ({ value: c.id, label: displayPerson(c) })),
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
export function Roles() {
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
      <Head level={2}
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
export function AdminConsole({initial}) {
 const {user}=useApp();const tabs=[can(user,14)&&["users","Tài khoản"],can(user,16)&&["roles","Vai trò & phân quyền"],(can(user,28)||can(user,29))&&["settings","Thông tin web"]].filter(Boolean);const [tab,setTab]=useState(initial||tabs[0]?.[0]);
 return <><Head title="Admin Console"/><nav className="row wrap" aria-label="Admin Console">{tabs.map(([id,label])=><Button key={id} primary={id===tab} aria-current={id===tab?"page":undefined} onClick={()=>setTab(id)}>{label}</Button>)}</nav>{tab==="users"?<Users/>:tab==="roles"?<Roles/>:<WebSettingsEditor/>}</>;
}
export function ReleaseVersion(){const [version,setVersion]=useState(null);useEffect(()=>{api("/version").then(setVersion).catch(()=>{})},[]);return <section className="card"><h2>Phiên bản hệ thống</h2><p>Web: 0.2.0</p><p>API: {version?.version||"Không tải được"}</p><p>Build: {version?.build||"—"}</p></section>}
export function WebSettingsEditor(){const app=useApp(),[state,setState]=useState(null),[name,setName]=useState(""),[logo,setLogo]=useState(null),[remove,setRemove]=useState(false),[error,setError]=useState(""),[busy,setBusy]=useState(false);async function load(){try{const s=await api("/web-settings/admin");setState(s);setName(s.name);setLogo(null);setRemove(false);}catch(e){setError(e.message);}}useEffect(()=>{load();},[]);
 async function file(f){if(!f)return;try{if(!["image/png","image/jpeg","image/webp"].includes(f.type)||f.size>2000000)throw new Error("Chọn PNG/JPEG/WebP tối đa 2 MB.");const bitmap=await createImageBitmap(f);if(bitmap.width>2048||bitmap.height>2048){bitmap.close();throw new Error("Logo tối đa 2048×2048 pixel.");}const canvas=document.createElement("canvas");canvas.width=bitmap.width;canvas.height=bitmap.height;canvas.getContext("2d").drawImage(bitmap,0,0);bitmap.close();setLogo(canvas.toDataURL("image/png"));setRemove(false);setError("");}catch(e){setError(e.message);}}
 async function save(e){e.preventDefault();setBusy(true);setError("");try{const s=await api("/web-settings",{method:"PUT",body:{name,version:state.version,logoBase64:logo?.split(",")[1]||null,removeLogo:remove}});setState(s);setLogo(null);setRemove(false);window.dispatchEvent(new Event("branding-updated"));app.toast("Đã lưu thông tin web.");}catch(e){setError(e.message);}finally{setBusy(false);}}
 return <section className="card" style={{marginTop:20}}><h2>Thông tin web</h2><ReleaseVersion/>{error&&<p className="form-error" role="alert">{error}</p>}{state&&<form onSubmit={save} className="stack"><label className="field">Tên web<input className="input" required maxLength={100} value={name} disabled={!can(app.user,29)} onChange={e=>setName(e.target.value)}/></label><label className="field">Logo<input type="file" accept="image/png,image/jpeg,image/webp" disabled={!can(app.user,29)} onChange={e=>file(e.target.files[0])}/></label><div className="row">{!remove&&(logo||state.logoUrl)&&<img src={logo||state.logoUrl} alt="Logo xem trước" style={{width:64,height:64,objectFit:"contain"}}/>}<strong>{name}</strong></div>{can(app.user,29)&&<><Button onClick={()=>{setRemove(true);setLogo(null);}}>Xóa logo tùy chỉnh</Button><div className="modal-actions"><Button disabled={busy} onClick={load}>Hủy thay đổi</Button><Button primary type="submit" disabled={busy}>Lưu thông tin web</Button></div></>}</form>}</section>;
}