const app = document.getElementById("app");
const toast = document.getElementById("toast");
const logoutBtn = document.getElementById("logoutBtn");
const projects = ["Music Cloud", "AI Bot", "Mining Project"];
let selectedProject = "";
let selectedRole = "";
let me = null;
let poller = null;
let lastChatId = 0;
let recorder = null;
let audioChunks = [];

function notify(message) {
  toast.textContent = message;
  toast.classList.add("show");
  setTimeout(() => toast.classList.remove("show"), 2800);
}
async function api(url, options={}) {
  const res = await fetch(url, { credentials:"same-origin", ...options,
    headers: options.body instanceof FormData ? (options.headers || {}) : {"Content-Type":"application/json", ...(options.headers||{})} });
  if (res.status === 401) throw new Error("Please sign in to continue.");
  const data = res.status === 204 ? null : await res.json().catch(()=>null);
  if (!res.ok) throw new Error(data?.error || data?.message || `Request failed (${res.status})`);
  return data;
}
function stopPolling(){ if(poller){clearInterval(poller);poller=null;} }
function showHome(){
  stopPolling(); selectedRole=""; selectedProject="";
  logoutBtn.classList.add("hidden");
  document.getElementById("topStatus").textContent="Your project workspace";
  app.innerHTML = `
  <section class="hero">
    <div><div class="eyebrow">A calmer way to work together</div><h1>Hi<span>,</span><br><em>let's build.</em></h1>
      <p class="lead">A shared space for ideas, thoughtful teamwork and getting meaningful work done — one task at a time.</p>
      <div class="pill">YOUR PROJECT WORKSPACE</div>
    </div>
    <div class="hero-art"><div class="orbit"></div><div class="orb"></div><div class="float-card one">✦ Ideas become action</div><div class="float-card two">✧ Work, together</div></div>
  </section>
  <div class="section-head"><div><div class="eyebrow">Choose where to begin</div><h2>Select a project</h2></div><span class="muted">01 — 03</span></div>
  <section class="project-grid">${projects.map((p,i)=>`<button class="project-card" data-project="${p}"><div class="project-icon">${["♫","✳","◈"][i]}</div><span class="arrow">↗</span><h3>${p}</h3><p>${["A shared space for music ideas and tasks.","Build, test and collaborate on AI ideas.","Coordinate mining project work and progress."][i]}</p></button>`).join("")}</section>`;
  document.querySelectorAll("[data-project]").forEach(b=>b.addEventListener("click",()=>{selectedProject=b.dataset.project;showRole();}));
}
function showRole(){
  app.innerHTML=`<section class="auth-wrap"><div class="auth-card"><button class="back" id="back">← Back to projects</button><div class="eyebrow">${selectedProject}</div><h1>How are you joining?</h1><p class="muted">Choose your role to continue to your project workspace.</p>
  <div class="role-grid"><button class="role-card" data-role="Leader"><div style="font-size:28px">♙</div><b>Team Leader</b><span>Assign work and guide the team</span></button><button class="role-card" data-role="Member"><div style="font-size:28px">♧</div><b>Team Member</b><span>View tasks and share progress</span></button></div></div></section>`;
  document.getElementById("back").onclick=showHome;
  document.querySelectorAll("[data-role]").forEach(b=>b.onclick=()=>{selectedRole=b.dataset.role;showLogin();});
}
function showLogin(){
  app.innerHTML=`<section class="auth-wrap"><div class="auth-card"><button class="back" id="back">← Change role</button><div class="eyebrow">${selectedProject} · ${selectedRole==="Leader"?"Team Leader":"Team Member"}</div><h1>Welcome back.</h1><p class="muted">Sign in to continue to your workspace.</p>
  <form id="loginForm"><div class="field"><label>USERNAME</label><input name="username" autocomplete="username" required minlength="3" placeholder="Your username"></div>
  <div class="field"><label>PASSWORD</label><input name="password" type="password" autocomplete="current-password" required placeholder="Your password"></div>
  <button class="button button-block">Log in</button></form>
  <div class="auth-links"><button class="link-button" id="forgot">Forgot password?</button><button class="link-button" id="signup">Create an account</button></div>
  <p class="small-note">Your account is saved in the project database. Use the same project and role when signing in.</p></div></section>`;
  document.getElementById("back").onclick=showRole;
  document.getElementById("signup").onclick=showSignup;
  document.getElementById("forgot").onclick=showForgot;
  document.getElementById("loginForm").onsubmit=async e=>{e.preventDefault();const f=new FormData(e.target);try{await api("/api/auth/login",{method:"POST",body:JSON.stringify({username:f.get("username"),password:f.get("password"),role:selectedRole,project:selectedProject})});await loadMe();}catch(err){notify(err.message);}};
}
function showSignup(){
  app.innerHTML=`<section class="auth-wrap"><div class="auth-card"><button class="back" id="back">← Back to login</button><div class="eyebrow">Create your account</div><h1>Let's get started.</h1><p class="muted">Your username and password will be stored securely.</p>
  <form id="signupForm"><div class="field"><label>USERNAME</label><input name="username" required minlength="3" autocomplete="username" placeholder="Choose a username"></div>
  <div class="field"><label>NEW PASSWORD</label><input name="password" type="password" required minlength="8" autocomplete="new-password" placeholder="At least 8 characters"></div>
  <div class="field"><label>CONFIRM PASSWORD</label><input name="confirmPassword" type="password" required minlength="8" autocomplete="new-password" placeholder="Enter it again"></div>
  <button class="button button-block">Create account & continue</button></form>
  <p class="small-note">Role: ${selectedRole==="Leader"?"Team Leader":"Team Member"} · Project: ${selectedProject}</p></div></section>`;
  document.getElementById("back").onclick=showLogin;
  document.getElementById("signupForm").onsubmit=async e=>{e.preventDefault();const f=new FormData(e.target);try{await api("/api/auth/signup",{method:"POST",body:JSON.stringify({username:f.get("username"),password:f.get("password"),confirmPassword:f.get("confirmPassword"),role:selectedRole,project:selectedProject})});await loadMe();}catch(err){notify(err.message);}};
}
function showForgot(){
  app.innerHTML=`<section class="auth-wrap"><div class="auth-card"><button class="back" id="back">← Back to login</button><div class="eyebrow">Account recovery</div><h1>Forgot password?</h1><p class="muted">For account safety, password recovery needs a verified email or one-time code. This starter project has not been connected to an email/SMS provider yet.</p><p class="small-note">Ask your project administrator to reset the account during development. Before public launch, configure email OTP recovery rather than allowing password changes using only a username.</p><button class="button button-block" id="backLogin">Return to login</button></div></section>`;
  document.getElementById("back").onclick=showLogin;document.getElementById("backLogin").onclick=showLogin;
}
async function loadMe(){me=await api("/api/auth/me");logoutBtn.classList.remove("hidden");document.getElementById("topStatus").textContent=`${me.username} · ${me.project}`;showDashboard();}
logoutBtn.onclick=async()=>{try{await api("/api/auth/logout",{method:"POST"});}catch{}me=null;showHome();};
function fmtDate(d){return new Date(d).toLocaleString([], {month:"short",day:"numeric",hour:"2-digit",minute:"2-digit"});}
function remaining(d){const ms=new Date(d)-Date.now();if(ms<=0)return "Deadline passed";const h=Math.floor(ms/3600000),m=Math.floor((ms%3600000)/60000);return `${h}h ${m}m remaining`;}
async function showDashboard(){
  stopPolling();
  const isLeader=me.role==="Leader";
  app.innerHTML=`<div class="dashboard-head"><div><div class="eyebrow">${me.project} · TEAM WORKSPACE</div><h1>Hello, ${escapeHtml(me.username)}.</h1><p class="muted">${isLeader?"Coordinate tasks, support your team and keep the work moving.":"Here are your assigned tasks and team updates."}</p></div><span class="pill">${isLeader?"TEAM LEADER":"TEAM MEMBER"}</span></div>
  <div class="dash-grid"><div>
    ${isLeader?`<section class="panel"><h2>Assign a task</h2><form id="taskForm"><div class="field"><label>TASK TITLE</label><input name="title" required placeholder="e.g. Design the login page"></div><div class="field"><label>DESCRIPTION / INSTRUCTIONS</label><textarea name="description" placeholder="Add details about the work…"></textarea></div><div class="field"><label>ASSIGN TO</label><select name="assignedToId" id="memberSelect" required><option value="">Loading team members…</option></select></div><div class="field"><label>TIME LIMIT</label><select name="hours"><option value="24">24 hours · 10 points</option><option value="48">48 hours · 10 points</option><option value="72">72 hours · 10 points</option></select></div><button class="button">Assign task</button></form></section>`:""}
    <section class="panel"><h2>${isLeader?"Tasks assigned by you":"Your assigned tasks"}</h2><div id="tasks" class="task-list"><div class="empty">Loading tasks…</div></div></section>
    <section class="panel"><h2>Team chat</h2><div id="chat" class="chat-box"></div><form id="chatForm" class="chat-compose"><input name="body" placeholder="Write a message…" autocomplete="off"><button class="button">Send</button><button type="button" id="voiceBtn" class="button button-outline" title="Record voice">🎙</button></form><p class="small-note">Messages are shared with people in this project's workspace. Voice recording needs microphone permission.</p></section>
  </div><aside>
    <section class="panel"><h2>${isLeader?"Team overview":"Your grade points"}</h2>${isLeader?`<div id="members"><div class="empty">Loading team…</div></div>`:`<div class="grade-card"><div class="grade-number" id="gradeNumber">${me.gradePoints}</div><div class="grade-caption">Total grade points earned</div><p class="small-note">Each task starts at 10 points. Late completion loses 1 point for each full hour after its deadline.</p></div>`}</section>
    <section class="panel"><h2>How points work</h2><div class="member-row"><span>Completed on time</span><b>10 points</b></div><div class="member-row"><span>1 hour late</span><b>9 points</b></div><div class="member-row"><span>2 hours late</span><b>8 points</b></div><div class="member-row"><span>Each further hour</span><b>−1 point</b></div><p class="small-note">A task cannot earn fewer than 0 points. An approved extension updates its deadline.</p></section>
  </aside></div>`;
  if(isLeader) await loadMembers();
  await refreshTasks(); await refreshChat();
  document.getElementById("taskForm")?.addEventListener("submit",createTask);
  document.getElementById("chatForm").addEventListener("submit",sendChat);
  document.getElementById("voiceBtn").addEventListener("click",recordVoice);
  poller=setInterval(async()=>{try{await refreshTasks();await refreshChat();if(me.role==="Leader")await loadMembers();}catch{}},15000);
}
function escapeHtml(s){return String(s??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));}
async function loadMembers(){
  const members=await api("/api/team/members");
  const sel=document.getElementById("memberSelect");
  if(sel)sel.innerHTML=`<option value="">Select a team member</option>`+members.map(m=>`<option value="${m.id}">${escapeHtml(m.username)}</option>`).join("");
  const box=document.getElementById("members");
  if(box)box.innerHTML=members.length?members.map(m=>`<div class="member-row"><span>${escapeHtml(m.username)}</span><b>${m.gradePoints} pts</b></div>`).join(""):`<div class="empty">No members have joined this project yet.</div>`;
}
async function createTask(e){
  e.preventDefault();const f=new FormData(e.target);
  try{await api("/api/tasks",{method:"POST",body:JSON.stringify({title:f.get("title"),description:f.get("description"),assignedToId:Number(f.get("assignedToId")),hours:Number(f.get("hours"))})});e.target.reset();notify("Task assigned. The deadline has started.");await refreshTasks();}
  catch(err){notify(err.message);}
}
async function refreshTasks(){
  const tasks=await api("/api/tasks");const box=document.getElementById("tasks");if(!box)return;
  if(!tasks.length){box.innerHTML='<div class="empty">No tasks yet. New tasks will appear here.</div>';return;}
  box.innerHTML=tasks.map(t=>{
    const late=new Date()>new Date(t.deadline)&&t.status!=="Complete";
    return `<article class="task"><div class="task-top"><div><h3>${escapeHtml(t.title)}</h3><p>${escapeHtml(t.description)}</p></div><span class="status ${t.status==="Complete"?"done":late?"late":""}">${t.status==="Complete"?"Complete":late?"Overdue":"In progress"}</span></div>
    <div class="task-meta"><span>◷ ${t.status==="Complete"?"Completed "+fmtDate(t.completedAt):remaining(t.deadline)}</span><span>Due ${fmtDate(t.deadline)}</span><span>★ ${t.points}/10 points</span></div>
    ${t.extensionRequested&&me.role==="Leader"?`<div class="task-actions"><span class="status late">Extra time requested</span><button class="button" onclick="approveExtension(${t.id})">Approve +24 hours</button></div>`:""}
    ${me.role==="Member"&&t.status!=="Complete"?`<div class="task-actions"><button class="button" onclick="completeTask(${t.id})">✓ Complete</button><button class="button button-outline" onclick="requestExtension(${t.id})">Want more time</button></div>`:""}
    </article>`;
  }).join("");
}
async function completeTask(id){try{const r=await api(`/api/tasks/${id}/complete`,{method:"POST"});notify(`Task completed · ${r.points} points earned`);await refreshTasks();await loadMe();document.getElementById("gradeNumber")&&(document.getElementById("gradeNumber").textContent=me.gradePoints);}catch(e){notify(e.message);}}
async function requestExtension(id){try{await api(`/api/tasks/${id}/request-extension`,{method:"POST"});notify("Extension request sent to your Team Leader.");await refreshTasks();}catch(e){notify(e.message);}}
async function approveExtension(id){try{await api(`/api/tasks/${id}/approve-extension`,{method:"POST",body:JSON.stringify({hours:24})});notify("24 extra hours approved.");await refreshTasks();}catch(e){notify(e.message);}}
async function refreshChat(){
  const messages=await api("/api/chat");const box=document.getElementById("chat");if(!box)return;
  if(!messages.length){box.innerHTML='<div class="empty">No messages yet. Start the conversation.</div>';return;}
  box.innerHTML=messages.map(m=>`<div class="bubble ${m.senderId===me.id?"mine":""}">${m.audioUrl?`<audio controls src="${m.audioUrl}"></audio>`:escapeHtml(m.body)}<small>${m.senderId===me.id?"You":`Team member #${m.senderId}`} · ${fmtDate(m.createdAt)}</small></div>`).join("");
  box.scrollTop=box.scrollHeight;
}
async function sendChat(e){e.preventDefault();const input=e.target.elements.body;const body=input.value.trim();if(!body)return;try{await api("/api/chat",{method:"POST",body:JSON.stringify({body,receiverId:null})});input.value="";await refreshChat();}catch(e){notify(e.message);}}
async function recordVoice(){
  if(!navigator.mediaDevices?.getUserMedia||!window.MediaRecorder){notify("Voice recording is not supported in this browser.");return;}
  const btn=document.getElementById("voiceBtn");
  if(recorder&&recorder.state==="recording"){recorder.stop();btn.textContent="🎙";return;}
  try{
    const stream=await navigator.mediaDevices.getUserMedia({audio:true});audioChunks=[];
    recorder=new MediaRecorder(stream);recorder.ondataavailable=e=>{if(e.data.size)audioChunks.push(e.data);};
    recorder.onstop=async()=>{stream.getTracks().forEach(t=>t.stop());const blob=new Blob(audioChunks,{type:"audio/webm"});const form=new FormData();form.append("audio",blob,"voice.webm");try{await api("/api/chat/voice",{method:"POST",body:form});notify("Voice message sent.");await refreshChat();}catch(e){notify(e.message);}};
    recorder.start();btn.textContent="■ Stop";
    notify("Recording… select Stop when finished.");
  }catch(e){notify("Please allow microphone access to record a voice message.");}
}
document.getElementById("brandHome").onclick=e=>{e.preventDefault();if(me)showDashboard();else showHome();};
(async()=>{try{await loadMe();}catch{showHome();}})();
