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
import {Stats,useApp,useData,query,nameCell,rate,Status,statusNames,permissionLabels,Editor,previewDays} from "./main";
export function Report() {
  const app = useApp(),
    [from, setFrom] = useState(
      app.years.find((y) => y.id === app.yearId)?.startDate ||
        today().slice(0, 4) + "-01-01",
    ),
    [until, setUntil] = useState(today()),
    [tab, setTab] = useState("all"),
    [missingDate, setMissingDate] = useState(today()),
    [sid, setSid] = useState("");
  useEffect(()=>{const y=app.years.find(y=>y.id===app.yearId);if(y){setFrom(y.startDate);setUntil(today()<y.startDate?y.endDate:today()>y.endDate?y.endDate:today());}setSid('')},[app.yearId]);
  const report = useData(
    app.yearId ? "/reports/attendance?" +
      query({
        from,
        until,
        yearId: app.yearId,
        classId: tab === "all" ? "" : app.classId,
      }) : null,
  );
  const sessions = useData(
    tab === "missing" && app.yearId
      ? "/attendance-sessions?" +
          query({ from: missingDate, until: missingDate, classId: app.classId,yearId:app.yearId })
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
      ["Tên Thánh và họ tên", "Mã", "Có mặt (buổi)", "Vắng (buổi)", "Buổi hợp lệ", "Tỷ lệ", "Ngày đủ", "Ngày chưa đủ", "Ngày một phần"],
      ...rows.map((x) => [
        displayPerson(x),
        x.code,
        x.present,
        x.absent,
        x.expected,
        rate(x.rate),x.presentDays,x.absentDays,x.partialDays,
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
  if(!app.yearId)return <><Head title="Báo cáo chuyên cần"/><p>Chọn năm học để xem báo cáo.</p></>;
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
            { label: "Có mặt (buổi)", key: "present" },
            { label: "Vắng (buổi)", key: "absent" },
            ...(tab!=="all"?[{label:"Ngày đủ",key:"presentDays"},{label:"Ngày chưa đủ",key:"absentDays"}]:[]),
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
                  <strong>{displayPerson(s)}</strong>
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
