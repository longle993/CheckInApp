import React,{useEffect,useState} from 'react';
import {api} from './api';
import {Button,Modal} from './ui';
export const deleteWarning='Thực hiện xóa dữ liệu sẽ xóa luôn cả các dữ liệu đã phát sinh, bạn có chắc chắn muốn xóa?';
export default function BulkDelete({kind,ids,onClose,onDeleted}){
 const [preview,setPreview]=useState(null),[error,setError]=useState(''),[busy,setBusy]=useState(false),[revision,setRevision]=useState(0);
 useEffect(()=>{let live=true;setPreview(null);setError('');api('/bulk-delete/preview',{method:'POST',body:{kind,ids}}).then(x=>live&&setPreview(x)).catch(e=>live&&setError(e.message));return()=>{live=false}},[kind,ids,revision]);
 async function remove(){setBusy(true);setError('');try{await api('/bulk-delete',{method:'POST',body:{kind,ids,fingerprint:preview.fingerprint}});onDeleted();}catch(e){setError(e.message);setPreview(null)}finally{setBusy(false)}}
 return <Modal title="Xác nhận xóa dữ liệu" onClose={()=>!busy&&onClose()}><p>{deleteWarning}</p>{(kind==='schedules'||kind==='schedule-items')&&<p>Toàn bộ buổi thuộc khung sẽ bị xóa, kể cả ngoài tháng đang xem.</p>}{preview&&<><ul>{preview.names.map((n,i)=><li key={i}>{n}</li>)}</ul><dl className="delete-counts">{Object.entries(preview.counts).map(([label,count])=><div key={label}><dt>{label}</dt><dd>{count}</dd></div>)}</dl></>}{!preview&&!error&&<p>Đang kiểm tra dữ liệu liên quan…</p>}{error&&<p role="alert">{error}</p>}<div className="modal-actions"><Button data-initial-focus disabled={busy} onClick={onClose}>Hủy</Button>{error&&<Button onClick={()=>setRevision(x=>x+1)}>Xem lại dữ liệu</Button>}<Button disabled={!preview||busy} onClick={remove}>{busy?'Đang xóa…':'Xóa dữ liệu'}</Button></div></Modal>
}
