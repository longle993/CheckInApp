export const columns={desktop:12,tablet:6,mobile:2};
export function size(key,mode){return {w:key==='attendance'?Math.min(6,columns[mode]):mode==='desktop'?6:columns[mode],h:key==='attendance'?(mode==='mobile'?8:7):key==='greeting'?2:key==='schedule'?6:key==='profile'?3:2}}
export function fits(c,grid,mode){return [c.x,c.y,c.w,c.h].every(Number.isInteger)&&c.x>=0&&c.y>=0&&c.y<=100&&c.x+c.w<=columns[mode]&&!grid.some(b=>b.key!==c.key&&c.x<b.x+b.w&&b.x<c.x+c.w&&c.y<b.y+b.h&&b.y<c.y+c.h)}
export function normalize(layout){const grids={};for(const mode of Object.keys(columns)){const cells=[];for(const key of layout.widgets){let c=layout.grids?.[mode]?.find(x=>x.key===key);if(c&&key==='attendance')c={...c,h:mode==='mobile'?8:7};if(!c||!fits(c,cells,mode)){c={key,x:0,y:0,...size(key,mode)};while(!fits(c,cells,mode)){c.x++;if(c.x+c.w>columns[mode]){c.x=0;c.y++}}}cells.push(c)}grids[mode]=cells}return {...layout,grids}}

export function placeOrSwap(cell,grid,mode){
 if(fits(cell,grid,mode))return grid.map(x=>x.key===cell.key?cell:x);
 const old=grid.find(x=>x.key===cell.key);if(!old)return null;
 const hits=grid.filter(b=>b.key!==cell.key&&cell.x<b.x+b.w&&b.x<cell.x+cell.w&&cell.y<b.y+b.h&&b.y<cell.y+cell.h);
 if(hits.length!==1)return null;
 const other=hits[0],a={...old,x:other.x,y:other.y},b={...other,x:old.x,y:old.y};
 const next=grid.map(x=>x.key===a.key?a:x.key===b.key?b:x);
 return fits(a,next,mode)&&fits(b,next,mode)?next:null;
}
