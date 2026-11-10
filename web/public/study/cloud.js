(function(){
'use strict';
class CloudSync {
 constructor(options){Object.assign(this,{options,cache:null,key:'',ready:false,applying:false,blocked:true,busy:false,timer:null,conflict:null,lastJSON:'',storageError:false,retryDelay:3000,endpoint:'/api/state'});}
 status(text,kind='ok',description=''){const n=document.getElementById('sync-status');n.textContent=text;n.dataset.state=kind;n.title=description||text;n.setAttribute('aria-label',text+'，点击立即同步');}
 lock(value){this.blocked=value;const main=document.getElementById('main');main.inert=value;main.setAttribute('aria-busy',String(value));document.body.classList.toggle('cloud-locked',value);}
 persist(){try{localStorage.setItem(this.key,JSON.stringify(this.cache));this.storageError=false;}catch{this.storageError=true;this.status('本机备份失败','error','请点击备份，导出当前进度。');}}
 backup(text,label){if(!text)return;try{const key=this.key+':backups',items=JSON.parse(localStorage.getItem(key)||'[]');items.unshift({at:new Date().toISOString(),label,json:text});localStorage.setItem(key,JSON.stringify(items.slice(0,3)));}catch{this.storageError=true;}}
 exportBackup(){const blob=new Blob([this.options.getState()],{type:'application/json'}),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='VocXuan_完整进度_'+new Date().toISOString().replace(/[:.]/g,'-')+'.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
 showBackups(){this.options.pause();this.lock(true);const panel=document.getElementById('sync-panel');panel.replaceChildren();panel.hidden=false;const card=document.createElement('section');card.className='sync-dialog';card.setAttribute('role','dialog');card.setAttribute('aria-modal','true');const h=document.createElement('h2');h.textContent='进度备份';const p=document.createElement('p');p.textContent='完整存档包含学习进度、复习日期和每日记录。下载的 JSON 文件可从首页“导入”恢复。';const actions=document.createElement('div');actions.className='sync-actions';const current=document.createElement('button');current.className='primary';current.textContent='下载当前完整存档';current.onclick=()=>this.exportBackup();actions.append(current);let backups=[];try{backups=JSON.parse(localStorage.getItem(this.key+':backups')||'[]');}catch{}for(const item of backups){const b=document.createElement('button');b.textContent=new Date(item.at).toLocaleString()+' · '+item.label;b.onclick=()=>{const url=URL.createObjectURL(new Blob([item.json],{type:'application/json'})),a=document.createElement('a');a.href=url;a.download='VocXuan_草稿_'+item.at.replace(/[:.]/g,'-')+'.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};actions.append(b);}const close=document.createElement('button');close.textContent='返回';close.onclick=()=>{panel.hidden=true;this.lock(false);};actions.append(close);card.append(h,p,actions);panel.append(card);current.focus();}
 async response(response){let data;try{data=await response.json();}catch{throw Error('服务器暂时没有返回存档，请稍后重试。');}if(response.status===401){this.lock(true);this.showLogin();throw Error('请重新登录。');}if(response.status!==409&&!response.ok)throw Error(data.error||'同步暂时失败。');return data;}
 apply(state){this.applying=true;try{this.options.loadState(state);this.cache.json=this.options.getState();this.lastJSON=this.cache.json;}finally{this.applying=false;}}
 async start(){this.lock(true);this.status('连接云端…','busy');try{
   const remote=await this.response(await fetch(this.endpoint,{cache:'no-store'}));
   this.key='vocxuan-sites-v1:'+location.host+':'+remote.userId;
   let saved=null;try{saved=JSON.parse(localStorage.getItem(this.key)||'null');}catch{}
   this.cache=saved&&typeof saved.json==='string'&&Number.isSafeInteger(saved.revision)?saved:{revision:remote.revision,json:'',dirty:false,pending:null};this.ready=true;
   if(this.cache.dirty){this.apply(JSON.parse(this.cache.json));if(this.cache.pending||this.cache.revision===remote.revision){this.lock(false);await this.flush();}else this.showConflict(remote);}
   else{this.apply(remote.state);this.cache.revision=remote.revision;this.cache.dirty=!!remote.initial;this.cache.pending=null;this.persist();this.lock(false);if(remote.initial)await this.flush();else this.status('已同步');}
   this.bind();
 }catch(error){this.status('连接失败','error',error.message);if(!this.ready){this.options.onError(error.message,()=>this.start());this.lock(false);}else if(!this.conflict)this.lock(false);}}
 record(text){if(!this.ready||this.applying||text===this.lastJSON)return;this.lastJSON=text;this.cache.json=text;this.cache.dirty=true;this.persist();if(this.conflict)return;this.status(navigator.onLine?'等待同步':'待联网同步','pending');this.schedule();}
 schedule(delay=this.retryDelay){if(this.timer||this.busy||this.blocked)return;this.timer=setTimeout(()=>{this.timer=null;void this.flush();},delay);}
 async compressedBody(payload){const text=JSON.stringify(payload);if(typeof CompressionStream==='function'){const body=await new Response(new Blob([text]).stream().pipeThrough(new CompressionStream('gzip'))).arrayBuffer();return {body,headers:{'Content-Type':'application/json','Content-Encoding':'gzip'}};}return {body:text,headers:{'Content-Type':'application/json'}};}
 async flush(){if(!this.ready||this.applying||this.busy||this.conflict||!this.cache.dirty)return;clearTimeout(this.timer);this.timer=null;
   if(!navigator.onLine){this.status(this.storageError?'未联网，请导出备份':'待联网同步','pending');return;}this.busy=true;this.status('同步中…','busy');
   try{
     if(!this.cache.pending){this.cache.pending={revision:this.cache.revision,mutationId:crypto.randomUUID(),json:this.cache.json};this.persist();}
     const pending=this.cache.pending,wire=await this.compressedBody({revision:pending.revision,mutationId:pending.mutationId,state:JSON.parse(pending.json)});
     const bytes=typeof wire.body==='string'?new TextEncoder().encode(wire.body).length:wire.body.byteLength;
     const response=await fetch(this.endpoint,{method:'POST',...wire,keepalive:bytes<60000}),remote=await this.response(response);
     if(response.status===409){this.showConflict(remote);return;}
     if(remote.state){if(this.cache.json!==pending.json){this.showConflict(remote);return;}this.apply(remote.state);}
     const more=this.cache.json!==pending.json&&!remote.state;this.cache.revision=remote.revision;this.cache.pending=null;this.cache.dirty=more;this.persist();this.retryDelay=3000;
     this.status(more?'等待同步':'已同步',more?'pending':'ok',remote.updatedAt?'最后同步：'+new Date(remote.updatedAt).toLocaleTimeString():'');
   }catch(error){this.retryDelay=Math.min(60000,this.retryDelay*2);this.status(this.storageError?'保存未完成，请导出备份':'待重试','error',error.message);}
   finally{this.busy=false;if(this.cache.dirty&&!this.conflict&&!this.blocked)this.schedule();}}
 async check(){if(!this.ready||this.busy||this.conflict||document.hidden)return;this.options.checkpoint();if(this.cache.dirty){await this.flush();if(this.cache.dirty)return;}
   this.busy=true;try{const remote=await this.response(await fetch(this.endpoint+'?revision='+this.cache.revision,{cache:'no-store'}));
     if(!remote.unchanged){this.options.checkpoint();if(this.cache.dirty){this.showConflict(remote);return;}this.apply(remote.state);this.cache.revision=remote.revision;this.cache.pending=null;this.cache.dirty=false;this.persist();}
     this.status(this.cache.dirty?'等待同步':'已同步',this.cache.dirty?'pending':'ok');
   }catch(error){this.status(navigator.onLine?'暂时离线':'待联网同步','pending',error.message);}finally{this.busy=false;if(this.cache.dirty&&!this.conflict)this.schedule();}}
 showConflict(remote){this.options.pause();this.conflict=remote;this.backup(this.cache.json,'同步冲突时的本机草稿');this.lock(true);this.status('需要选择进度','error');
   const panel=document.getElementById('sync-panel');panel.replaceChildren();panel.hidden=false;const card=document.createElement('section');card.className='sync-dialog';card.setAttribute('role','dialog');card.setAttribute('aria-modal','true');
   const title=document.createElement('h2');title.textContent='另一台设备已有更新';const copy=document.createElement('p');copy.textContent='这台设备的草稿已保留。选择要继续使用的进度；切换前也可以导出本机完整备份。';
   const actions=document.createElement('div');actions.className='sync-actions';const cloud=document.createElement('button');cloud.className='primary';cloud.textContent='继续云端进度';
   cloud.onclick=()=>{this.backup(this.cache.json,'切换云端前的草稿');this.conflict=null;this.cache.revision=remote.revision;this.cache.pending=null;this.apply(remote.state);this.cache.dirty=false;this.persist();panel.hidden=true;this.lock(false);this.status('已同步');};
   const local=document.createElement('button');local.textContent='继续此设备进度';local.onclick=()=>{this.backup(JSON.stringify(remote.state),'替换前的云端存档');this.conflict=null;this.cache.revision=remote.revision;this.cache.pending=null;this.cache.dirty=true;this.persist();panel.hidden=true;this.lock(false);void this.flush();};
   const download=document.createElement('button');download.textContent='导出本机备份';download.onclick=()=>this.exportBackup();actions.append(cloud,local,download);card.append(title,copy,actions);panel.append(card);cloud.focus();}
 showLogin(){if(this.ready)this.options.pause();const panel=document.getElementById('sync-panel');panel.replaceChildren();panel.hidden=false;const card=document.createElement('section');card.className='sync-dialog';const p=document.createElement('p');p.textContent='请重新登录你的 ChatGPT 账号。本机草稿会继续保留。';const a=document.createElement('a');a.href='/';a.target='_top';a.textContent='重新打开登录';card.append(p,a);panel.append(card);}
 bind(){if(this.bound)return;this.bound=true;document.getElementById('sync-status').onclick=()=>void this.check();window.addEventListener('online',()=>void this.check());window.addEventListener('focus',()=>void this.check());
   document.addEventListener('visibilitychange',()=>{if(document.hidden)void this.flush();else void this.check();});window.addEventListener('pagehide',()=>void this.flush());this.poll=setInterval(()=>{if(!document.hidden)void this.check();},30000);}
}
window.VocCloud=CloudSync;
})();
