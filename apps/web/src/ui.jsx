import React, { useState, useEffect, useRef, useLayoutEffect } from "react";
import {
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  X,
  Check,
  Menu,
} from "lucide-react";
import { fmtDate } from "./api";
import { createPortal } from "react-dom";
export function Brand({ auth = false }) {
  const [branding,setBranding]=useState({name:"CheckInApp",logoUrl:null});
  useEffect(()=>{let live=true;async function load(){try{const r=await fetch("/api/v1/web-settings");if(r.ok){const s=await r.json();if(live){setBranding(s);document.title=s.name;}}}catch{}}load();window.addEventListener("branding-updated",load);return()=>{live=false;window.removeEventListener("branding-updated",load);};},[]);

  return (
    <a href="#tongquan" className={auth ? "brand auth-brand" : "brand"} aria-label={branding.name}>
      {branding.logoUrl ? <img src={branding.logoUrl} alt="" onError={()=>setBranding(s=>({...s,logoUrl:null}))} style={{width:32,height:32,objectFit:"contain"}}/> : <span className="mark">
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <rect x="4" y="4" width="7" height="7" rx="1" />
          <path d="M15 4h5v5M4 15v5h5m6-3 2 2 4-5" />
        </svg>
      </span>}
      {!auth && branding.name}
    </a>
  );
}
export function Button({
  children,
  primary = false,
  ghost = false,
  className = "",
  ...props
}) {
  return (
    <button
      type="button"
      {...props}
      className={`btn ${primary ? "primary" : ""} ${ghost ? "ghost" : ""} ${className}`}
    >
      {children}
    </button>
  );
}
export function Badge({ children, kind = "neutral" }) {
  return <span className={"badge " + kind}>{children}</span>;
}
export function Head({ title, subtitle, children, level=1 }) {
  const Title=level===2?"h2":"h1";
  return (
    <div className="pagehead">
      <div>
        <Title>{title}</Title>
        {subtitle && <p>{subtitle}</p>}
      </div>
      {children && <div className="head-actions">{children}</div>}
    </div>
  );
}
export function Select({
  value,
  onChange,
  options,
  label = "Chọn",
  disabled = false,
}) {
  return <div className="filter select-control"><select className="select-trigger" aria-label={label} value={value || ""} disabled={disabled} onChange={e => onChange(e.target.value)}>
    {!options.some(o => o.value === value) && <option value="" disabled>{label}</option>}
    {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
  </select></div>;
}
export function DatePicker({ value, onChange, label = "Chọn ngày" }) {
  const [open, setOpen] = useState(false);
  const [month, setMonth] = useState(
    value?.slice(0, 7) || new Date().toISOString().slice(0, 7),
  );
  const anchor = useRef(null), popup = useRef(null);
  const [position, setPosition] = useState({left:8,top:8,width:300});
  useEffect(() => {
    if (open) popup.current?.querySelector("button.selected,button")?.focus({preventScroll:true});
  }, [open]);
  useLayoutEffect(() => {
    if (!open) return;
    function place() {
      if (!anchor.current || !popup.current) return;
      const box = anchor.current.getBoundingClientRect();
      const width = Math.min(300, window.innerWidth - 16);
      const height = Math.min(popup.current.getBoundingClientRect().height, window.innerHeight - 16);
      const below = window.innerHeight - box.bottom - 8;
      const top = below >= height || below >= box.top - 8 ? box.bottom + 4 : box.top - height - 4;
      setPosition({width, left:Math.max(8, Math.min(box.left,window.innerWidth-width-8)), top:Math.max(8,Math.min(top,window.innerHeight-height-8))});
    }
    place(); window.addEventListener("resize",place);window.addEventListener("scroll",place,true);
    return () => {window.removeEventListener("resize",place);window.removeEventListener("scroll",place,true);};
  },[open,month]);
  let first = new Date(month + "-01T12:00:00");
  const total = new Date(
    first.getFullYear(),
    first.getMonth() + 1,
    0,
  ).getDate();
  function change(n) {
    const d = new Date(first);
    d.setMonth(d.getMonth() + n);
    setMonth(`${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}`);
  }
  return (
    <div ref={anchor} className="filter datepicker">
      <button
        type="button"
        className="select-trigger"
        onClick={() => { if(!open && value) setMonth(value.slice(0,7)); setOpen(!open); }}
        aria-label={label}
        aria-expanded={open}
      >
        {fmtDate(value)}
        <ChevronDown />
      </button>
      {open && createPortal(
        <div data-calendar-layer onKeyDown={e => {
          if(e.key === "Escape") {e.stopPropagation();setOpen(false);anchor.current?.querySelector("button")?.focus();}
          if(e.key === "Tab") {
            const buttons = [...popup.current.querySelectorAll("button:not([disabled])")];
            if(e.shiftKey && document.activeElement === buttons[0]) { e.preventDefault(); buttons.at(-1)?.focus(); }
            else if(!e.shiftKey && document.activeElement === buttons.at(-1)) { e.preventDefault(); buttons[0]?.focus(); }
          }
        }}>
          <button
            className="select-backdrop"
            style={{zIndex:200}}
            type="button"
            aria-label="Đóng lịch"
            onClick={() => setOpen(false)}
          />
          <div ref={popup} className="popover calendar-pop" role="dialog" aria-label={label + " — lịch"} style={{position:"fixed",...position,right:"auto",bottom:"auto",minWidth:0,maxHeight:"calc(100vh - 16px)",overflowY:"auto",zIndex:201}}>
            <div className="between">
              <Button
                ghost
                className="icon"
                onClick={() => change(-1)}
                aria-label="Tháng trước"
              >
                <ChevronLeft />
              </Button>
              <strong>{month.split("-").reverse().join("/")}</strong>
              <Button
                ghost
                className="icon"
                onClick={() => change(1)}
                aria-label="Tháng sau"
              >
                <ChevronRight />
              </Button>
            </div>
            <div className="dategrid">
              {["CN", "T2", "T3", "T4", "T5", "T6", "T7"].map((s) => (
                <span key={s} className="muted small">
                  {s}
                </span>
              ))}
              {Array.from({ length: first.getDay() }, (_, i) => (
                <span key={"pad" + i} />
              ))}
              {Array.from({ length: total }, (_, i) => {
                const date = month + "-" + String(i + 1).padStart(2, "0");
                return (
                  <button
                    key={i}
                    type="button"
                    className={date === value ? "selected" : ""}
                    onClick={() => {
                      onChange(date);
                      setOpen(false);
                      anchor.current?.querySelector("button")?.focus();
                    }}
                  >
                    {i + 1}
                  </button>
                );
              })}
            </div>
          </div>
        </div>, document.body
      )}
    </div>
  );
}
export function Modal({ title, children, onClose, wide = false }) {
  const container = useRef(null);
  useEffect(() => {
    const previous = document.activeElement,
      overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const dialog = container.current;
    const first =
      dialog.querySelector("input:not([disabled])") ||
      dialog.querySelector("button");
    first?.focus();
    function trap(e) {
      if (e.key !== "Tab") return;
      const items = [
        ...dialog.querySelectorAll(
          "button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),a[href]",
        ),
      ].filter((x) => x.getClientRects().length);
      if (!items.length) return;
      const first = items[0],
        last = items.at(-1);
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    }
    dialog.addEventListener("keydown", trap);
    return () => {
      document.body.style.overflow = overflow;
      dialog.removeEventListener("keydown", trap);
      if (previous?.isConnected) previous.focus();
    };
  }, []);
  return (
    <div
      className="modal-backdrop"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
      onKeyDown={(e) => {
        if (e.key === "Escape") onClose();
      }}
    >
      <section
        ref={container}
        role="dialog"
        aria-modal="true"
        aria-labelledby="dialog-title"
        className={"modal " + (wide ? "wide" : "")}
      >
        <div className="between">
          <h2 id="dialog-title">{title}</h2>
          <Button ghost className="icon" onClick={onClose} aria-label="Đóng">
            <X />
          </Button>
        </div>
        {children}
      </section>
    </div>
  );
}
export function State({ loading, error, empty, onRetry, children }) {
  if (loading)
    return (
      <div className="card" aria-busy="true">
        {[1, 2, 3, 4].map((n) => (
          <div className="skeleton-row" key={n}>
            <div className="skeleton" />
            <div className="skeleton" />
          </div>
        ))}
      </div>
    );
  if (error)
    return (
      <div className="card empty" role="alert">
        <p>{error.message}</p>
        <Button onClick={onRetry}>Thử lại</Button>
      </div>
    );
  if (empty)
    return (
      <div className="card empty">
        <p>
          {typeof empty === "string"
            ? empty
            : "Chưa có dữ liệu trong phạm vi đã chọn."}
        </p>
      </div>
    );
  return children;
}
export function Table({ columns, rows, rowKey = "id" }) {
  return (
    <div className="table-wrap">
      <table
        className={
          "adaptive " + (columns.at(-1)?.label === "" ? "action-table" : "")
        }
      >
        <thead>
          <tr>
            {columns.map((c, i) => (
              <th key={i}>{c.label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r, i) => (
            <tr key={r[rowKey] || i}>
              {columns.map((c, j) => (
                <td key={j} data-label={c.label}>
                  {c.render ? c.render(r) : (r[c.key] ?? "—")}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
