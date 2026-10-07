// Design preview, round 4 (Part B): the white-glove walkthrough in five parts.
// Every word comes from the app's own JourneyBuilder (C#), exported by build.py as JSON. This file
// only lays it out and SIMULATES what the app will really do: finding the USB stick,
// downloading, checking and writing, and restarting. Nothing here touches your PC.
"use strict";

const walk = { i: 0, install: null, done: {}, tasks: {}, usb: { stage: "waiting" }, restarted: false };

const INSTALL_KEY = { keepWindows: "keep", replaceWindows: "replace" };
const pathKind = () => state.path ?? state.report.recommendation.paths[0]?.kind;
const journey = () => window.GH_DATA.journeys[state.sample]?.[pathKind()]?.[INSTALL_KEY[walk.install] ?? "none"];

// App screens: parts 1-4, then one "after you restart" screen previewing part 5 (the guide).
function screens() {
  const j = journey();
  const app = j.steps.filter(s => s.phase !== "onTheStick");
  return [...app, { id: "after-restart", phase: "onTheStick", title: "After you restart", lead: "From here, you follow your guide on your phone or on paper." }];
}

// ---- Document blocks (same shapes as GreyHaven.Reports' Document.cs) --------------------------
function inl(list) {
  return (list ?? []).map(i => {
    switch (i.type) {
      case "strong": return `<strong>${esc(i.value)}</strong>`;
      case "link": return /^https?:\/\//.test(i.url) ? `<a href="${esc(i.url)}" target="_blank" rel="noopener">${esc(i.label)}</a>` : esc(i.label);
      case "code": return `<code>${esc(i.value)}</code>`;
      case "rating": return badge(i.rating);
      default: return esc(i.value);
    }
  }).join("");
}

function blocks(list, key) {
  return (list ?? []).map((b, n) => block(b, `${key}-${n}`)).join("");
}

function block(b, key) {
  switch (b.type) {
    case "heading": return `<h${Math.min(4, Math.max(2, b.level))}>${esc(b.text)}</h${Math.min(4, Math.max(2, b.level))}>`;
    case "paragraph": return `<p>${inl(b.content)}</p>`;
    case "callout": {
      if (b.kind === "tip") return `<aside class="tip"><p class="strong">${esc(b.title)}</p><p>${inl(b.content)}</p></aside>`;
      const cls = b.kind === "danger" ? "warn-box big-warn" : b.kind === "warning" ? "warn-box" : "note-box";
      return `<div class="${cls}"><p class="strong">${esc(b.title)}</p><p>${inl(b.content)}</p></div>`;
    }
    case "list":
      if (b.kind === "checkboxes") return checklist(key, b.items.map(inl));
      return `<${b.kind === "numbered" ? "ol" : "ul"} class="steps">${b.items.map(it => `<li>${inl(it)}</li>`).join("")}</${b.kind === "numbered" ? "ol" : "ul"}>`;
    case "commands": return b.lines.map(l => `<div class="command"><code>${esc(l)}</code></div>`).join("");
    case "details": return more(b.summary, blocks(b.content, key));
    case "pictures": return pictures(b.steps);
    case "keyfact": return `<div class="card keycard"><p class="secondary">${esc(b.label)}</p><p class="bigkey">${esc(b.value)}</p></div>`;
    case "table": return `<p>${b.rows.map(r => r.map(inl).join(" · ")).join("<br>")}</p>`;
    default: return "";
  }
}

function checklist(id, items) {
  return `<ul class="todo">${items.map((t, i) => {
    const k = `${id}-${i}`;
    return `<li><label><input type="checkbox" data-done="${k}" ${walk.done[k] ? "checked" : ""}> <span>${t}</span></label></li>`;
  }).join("")}</ul>`;
}

// Pictures: the app's real drawings. Click (or Enter) to enlarge; Escape or Close to go back.
function pictures(steps) {
  const cat = window.GH_DATA.pictures;
  return `<ol class="picture-steps">${steps.map(st => {
    const id = st.pictureId;
    const pic = id && cat[id]
      ? `<button class="pic-zoom" data-pic="${esc(id)}" aria-label="Enlarge picture: ${esc(cat[id].alt)}"><img src="pics/${esc(id)}.svg" alt=""></button>`
      : `<div class="picture-frame placeholder" role="img" aria-label="Picture coming soon: ${esc(st.pictureCaption)}"><span>Picture coming soon</span></div>`;
    return `<li><figure class="picture">${pic}<figcaption class="secondary small">${esc(st.pictureCaption)}</figcaption></figure><p class="strong">${esc(st.text)}</p></li>`;
  }).join("")}</ol>`;
}

function openPicture(btn) {
  const id = btn.dataset.pic, alt = window.GH_DATA.pictures[id].alt;
  let d = $("#zoom");
  if (!d) {
    d = document.createElement("dialog");
    d.id = "zoom";
    d.className = "zoom";
    d.setAttribute("aria-label", "Enlarged picture");
    d.innerHTML = `<button class="plain zoom-close" id="zoomclose">Close picture</button><img alt=""><p class="zoom-caption"></p>`;
    document.body.append(d);
    $("#zoomclose").addEventListener("click", () => d.close());
    d.addEventListener("click", e => { if (e.target === d) d.close(); }); // tap outside the picture
  }
  $("img", d).src = `pics/${id}.svg`;
  $("img", d).alt = alt;
  $(".zoom-caption", d).textContent = alt;
  d.onclose = () => btn.focus();
  d.showModal();
  $("#zoomclose").focus();
}

// ---- Screens ----------------------------------------------------------------------------------
const CHECK_ICON = { ok: ["✓", "c-works", "Good"], attention: ["!", "c-partial", "Note"], blocker: ["✗", "c-unsupported", "Can't do this yet"] };

function chooseScreen(s) {
  return `${blocks(s.body, s.id)}
    <div class="paths" role="radiogroup" aria-label="How to install">
      ${s.choices.map(c => {
        const on = walk.install === c.choice;
        return `<label class="option${on ? " selected" : ""}${c.possible ? "" : " disabled"}"><span class="path-label">
          <input type="radio" name="install" value="${c.choice}" ${on ? "checked" : ""} ${c.possible ? "" : "disabled"}>
          <span><span class="path-head"><span class="strong">${esc(c.title)}</span>${c.recommended ? `<span class="tag">Suggested</span>` : ""}</span>
          ${c.lines.map(l => `<span class="path-line">${esc(l)}</span>`).join("")}
          <ul class="checks-list">${c.checks.map(k => {
            const [g, cls, word] = CHECK_ICON[k.state];
            return `<li><span class="glyph ${cls}" aria-hidden="true">${g}</span><span><span class="visually-hidden">${word}: </span>${esc(k.text)}</span></li>`;
          }).join("")}</ul></span></span></label>`;
      }).join("")}
    </div>`;
}

function taskCard(t, stepId) {
  const k = `${stepId}-${t.id}`;
  const st = walk.tasks[k] ?? (t.done ? "already" : "todo");
  let action = "";
  if (st === "already") action = `<p class="c-works strong"><span aria-hidden="true">✓</span> Already done on your PC.</p>`;
  else if (st === "done") action = `
    <p class="c-works strong"><span aria-hidden="true">✓</span> Done.</p>
    <p class="secondary">${esc(t.undo ?? "")}</p>
    ${t.action === "oneClick" ? `<button class="plain" data-undo="${k}">Undo</button>` : ""}`;
  else if (st === "confirm") action = `
    <div class="note-box" role="group" aria-label="Check before GreyHaven changes this">
      <p>${esc(t.confirm)}</p>
      <p class="secondary small">Windows will ask for permission. Simulated in this preview.</p>
      <div class="row"><button class="accent" data-yes="${k}">Yes, do it</button><button class="plain" data-no="${k}">Cancel</button></div>
    </div>`;
  else if (t.action === "oneClick") action = `
    <button class="accent" data-ask="${k}">${esc(t.buttonLabel)}</button>
    ${blocks(t.how, k)}`;
  else action = `
    ${blocks(t.how, k)}
    <label class="check"><input type="checkbox" data-manual="${k}"> <span>I've done this</span></label>`;
  return `<article class="card task">
    <h2 class="task-title">${esc(t.title)}${t.action === "oneClick" && st === "todo" ? `<span class="tag">GreyHaven can do this</span>` : ""}</h2>
    <p>${esc(t.why)}</p>${action}</article>`;
}

function tasksScreen(s) {
  return `${s.tasks.map(t => taskCard(t, s.id)).join("")}${blocks(s.body, s.id)}`;
}

// The USB stick: one screen that finds the stick, then downloads, checks, writes and checks again.
const USB_STAGES = ["Downloading", "Checking the file", "Writing to the stick", "Checking the stick"];
function usbScreen(s) {
  const u = walk.usb, plan = s.usb;
  const gb = plan.installerBytes ? (plan.installerBytes / 1e9).toFixed(1) : "6";
  const widget = {
    waiting: `
      <p class="strong" aria-live="polite">Looking for USB sticks… Plug one in.</p>
      <div class="sim-controls"><span class="secondary small">Preview only:</span>
        <button class="plain" data-plug="16">Plug in a 16 GB stick</button>
        <button class="plain" data-plug="4">Plug in a 4 GB stick</button></div>`,
    small: `
      <div class="warn-box"><p class="strong">This stick is too small: Kingston DataTraveler, 4 GB.</p>
      <p>You need one that holds at least 8 GB. Unplug it and plug in a bigger one.</p></div>
      <div class="sim-controls"><span class="secondary small">Preview only:</span><button class="plain" data-plug="16">Swap for a 16 GB stick</button></div>`,
    found: `
      <div class="option selected stick"><span class="strong">SanDisk Ultra</span> <span class="secondary">USB stick · 16 GB</span></div>
      <div class="warn-box big-warn"><p class="strong">This erases everything on the SanDisk Ultra (16 GB).</p>
        <label class="check"><input type="checkbox" id="usbconfirm" ${u.confirm ? "checked" : ""}> <span>I understand everything on this stick will be erased.</span></label></div>
      <button class="accent big" id="usbgo" ${u.confirm ? "" : "disabled"}>Make my USB stick</button>`,
    working: `
      <ol class="stages">${USB_STAGES.map((t, n) => `<li class="${n < u.stageIndex ? "done" : n === u.stageIndex ? "now" : ""}">
        <span aria-hidden="true">${n < u.stageIndex ? "✓" : n === u.stageIndex ? "●" : "○"}</span> ${t}${n === 0 && u.stageIndex === 0 ? ` <span class="secondary">(${(u.pct / 100 * gb).toFixed(1)} of ${gb} GB)</span>` : ""}</li>`).join("")}</ol>
      <div class="progress-fill" role="progressbar" aria-label="${USB_STAGES[u.stageIndex]}" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${u.pct}"><span style="width:${u.pct}%"></span></div>
      <p class="secondary small">${esc(plan.verification)}</p>
      <button class="plain" id="usbcancel">Cancel</button>`,
    stopped: `
      <div class="note-box"><p class="strong">Stopped.</p><p>Nothing on your PC changed. The stick isn't ready, so make it again when you're ready.</p></div>
      <button class="accent" id="usbagain">Start again</button>`,
    done: `
      <div class="done-box"><p class="bigdone"><span aria-hidden="true">✓</span> Your USB stick is ready.</p>
      <p>The file was checked before and after writing. Leave the stick plugged in.</p></div>`,
  }[u.stage];
  return `${blocks(s.body, s.id)}<section class="card sim" aria-label="Your USB stick">${widget}</section>
    ${plan.verificationSources?.length ? `<p class="secondary small">How the check works: ${plan.verificationSources.map(x => `<a href="${esc(x.url)}" target="_blank" rel="noopener">${esc(x.title)}</a>`).join(" · ")}</p>` : ""}`;
}

function runUsb() {
  const u = walk.usb;
  u.stage = "working"; u.stageIndex = 0; u.pct = 0;
  const lengths = [40, 10, 35, 15];
  u.timer = setInterval(() => {
    u.pct = Math.min(100, u.pct + 100 / lengths[u.stageIndex] * 1.5);
    if (u.pct >= 100) {
      u.stageIndex++; u.pct = 0;
      if (u.stageIndex < USB_STAGES.length) announce(USB_STAGES[u.stageIndex]);
    }
    if (u.stageIndex >= USB_STAGES.length) { clearInterval(u.timer); u.stage = "done"; announce("Your USB stick is ready."); }
    // Keep going in the background, but only redraw while the USB screen is showing.
    if (screen.querySelector('[aria-label="Your USB stick"]')) showWalkthrough(walk.i, false);
  }, 120);
}

function guideScreen(s) {
  const j = journey();
  if (!j.qr) return `${blocks(s.body, s.id)}<div class="note-box"><p>Choose how to install first (part 1), so the guide shows the right steps.</p></div>`;
  return `${blocks(s.body, s.id)}
    <div class="guide-handoff">
      <figure class="qr"><div class="qr-code" role="img" aria-label="QR code that opens your guide on a phone">${j.qr.svg}</div>
        <figcaption>Scan with your phone's camera</figcaption></figure>
      <div>
        <p class="strong">Or keep it another way:</p>
        <p><button class="plain" id="printguide">Print the guide</button></p>
        <p><a href="${esc(j.phoneGuide)}" target="_blank" rel="noopener">Open the guide</a> <span class="secondary small">(to save it, or to see it as your phone will)</span></p>
      </div>
    </div>
    <p class="preview-note-inline secondary small">Preview: the QR code opens <code>${esc(j.qr.url)}</code>, the hosted guide.
      Everything after "#" stays on the phone. "Open the guide" shows a local copy of the same page.</p>`;
}

function restartScreen(s) {
  const r = s.restart;
  const ticked = r.finalChecks.every((_, n) => walk.done[`restart-final-${n}`]);
  return `${blocks(s.body, s.id)}
    <h2>Before you restart</h2>
    ${checklist("restart-final", r.finalChecks.map(esc))}
    <button class="accent big" id="restartnow" ${ticked ? "" : "disabled"} aria-describedby="restarthint">${esc(r.buttonLabel)}</button>
    <p id="restarthint" class="secondary small">${ticked ? "GreyHaven restarts your PC to Windows' blue Advanced startup menu. No administrator password needed." : "Tick each item first."}</p>
    ${walk.restarted ? `<div class="note-box" role="status"><p class="strong">Simulated: your PC would restart now.</p><p>This is what you'll see:</p></div>` : `<h2>What you'll see</h2>`}
    ${blocks(r.afterRestart, "restart-after")}
    ${more("USB stick not in the list?", blocks(r.fallback, "restart-fallback"))}`;
}

function afterRestartScreen() {
  const steps = journey().steps.filter(s => s.phase === "onTheStick");
  return `<p>These steps are in your guide. GreyHaven can't help on screen once your PC restarts, so here's a look ahead.</p>
    <ol class="ahead">${steps.map(s => `<li><span class="strong">${esc(s.title)}.</span> ${esc(s.lead)}</li>`).join("")}</ol>
    ${steps.map(s => more(`Read “${s.title}”`, `<p class="strong">${esc(s.lead)}</p>${blocks(s.body, `ahead-${s.id}`)}${s.tip ? block({ type: "callout", kind: "tip", title: "What could go wrong?", content: [{ type: "text", value: s.tip }] }) : ""}`)).join("")}
    <p><a href="${esc(journey().phoneGuide)}" target="_blank" rel="noopener">Open the whole guide</a></p>`;
}

// ---- Navigation -------------------------------------------------------------------------------
function canGoOn(s) {
  if (s.id === "choose") return walk.install !== null && s.choices.find(c => c.choice === walk.install)?.possible;
  if (s.checklistRequired) return s.body.filter(b => b.type === "list" && b.kind === "checkboxes")
    .every(b => b.items.every((_, i) => walk.done[`${s.id}-${s.body.indexOf(b)}-${i}`]));
  if (s.id === "usb") return walk.usb.stage === "done";
  return true;
}

function blockedHint(s) {
  if (s.id === "choose") return "Choose how to install to go on.";
  if (s.checklistRequired) return "Tick every item to go on.";
  if (s.id === "usb") return "Make your USB stick to go on.";
  return "";
}

function showWalkthrough(index, focus = true) {
  rerender = () => showWalkthrough(walk.i, false);
  if (walk.install === null) walk.install = journey().suggestedInstall ?? null;
  const all = screens();
  walk.i = Math.max(0, Math.min(all.length - 1, index ?? walk.i));
  const s = all[walk.i];
  const phases = journey().phases;
  const phaseNo = phases.findIndex(p => p.phase === s.phase);
  const next = all[walk.i + 1];
  const ok = canGoOn(s);
  const body = {
    choose: chooseScreen, windows: tasksScreen, "startup-settings": tasksScreen, usb: usbScreen,
    guide: guideScreen, restart: restartScreen, "after-restart": afterRestartScreen,
  }[s.id]?.(s) ?? blocks(s.body, s.id);

  screen.innerHTML = `
    ${demoBanner()}
    <nav class="walk-top" aria-label="Walkthrough"><button class="linkbutton" id="toresults">← Back to results</button></nav>
    <ol class="phases" aria-label="Your progress">${phases.map((p, n) => `
      <li class="${n < phaseNo ? "done" : n === phaseNo ? "now" : ""}"${n === phaseNo ? ` aria-current="step"` : ""}>
        <span class="phase-no" aria-hidden="true">${n < phaseNo ? "✓" : n + 1}</span><span class="phase-name">${esc(p.name)}</span>
        <span class="visually-hidden">${n < phaseNo ? " (done)" : n === phaseNo ? " (you are here)" : ""}</span></li>`).join("")}</ol>
    <h1>${esc(s.title)}</h1>
    <p class="lead">${esc(s.lead)}</p>
    <div class="step-body">${body}</div>
    ${s.tip ? block({ type: "callout", kind: "tip", title: "What could go wrong?", content: [{ type: "text", value: s.tip }] }) : ""}
    <div class="walk-nav">
      ${walk.i > 0 ? `<button class="plain" id="prev">← Back</button>` : "<span></span>"}
      ${next ? `<button class="accent big" id="nextstep" ${ok ? "" : "disabled"} aria-describedby="nexthint">Next: ${esc(next.title)} →</button>`
             : `<button class="accent big" id="finish">Done</button>`}
    </div>
    <p id="nexthint" class="secondary small walk-hint">${ok ? "" : esc(blockedHint(s))}</p>`;

  $("#toresults").addEventListener("click", () => showResults(true));
  $("#prev")?.addEventListener("click", () => showWalkthrough(walk.i - 1));
  $("#nextstep")?.addEventListener("click", () => showWalkthrough(walk.i + 1));
  $("#finish")?.addEventListener("click", () => showResults(true));
  screen.querySelectorAll("[data-done]").forEach(c => c.addEventListener("change", () => {
    walk.done[c.dataset.done] = c.checked;
    const id = c.dataset.done;
    showWalkthrough(walk.i, false);
    $(`[data-done="${id}"]`)?.focus();
  }));
  screen.querySelectorAll('input[name="install"]').forEach(i => i.addEventListener("change", () => {
    walk.install = i.value; walk.done = {}; walk.tasks = {};
    showWalkthrough(walk.i, false);
    $(`input[name="install"][value="${i.value}"]`).focus();
  }));
  screen.querySelectorAll(".pic-zoom").forEach(b => b.addEventListener("click", () => openPicture(b)));
  const act = (attr, fn) => screen.querySelectorAll(`[${attr}]`).forEach(b => b.addEventListener("click", () => { fn(b.getAttribute(attr)); }));
  act("data-ask", k => { walk.tasks[k] = "confirm"; showWalkthrough(walk.i, false); $(`[data-yes="${k}"]`)?.focus(); });
  act("data-no", k => { walk.tasks[k] = "todo"; showWalkthrough(walk.i, false); $(`[data-ask="${k}"]`)?.focus(); });
  act("data-yes", k => { walk.tasks[k] = "done"; announce("Done."); showWalkthrough(walk.i, false); $(`[data-undo="${k}"]`)?.focus(); });
  act("data-undo", k => { walk.tasks[k] = "todo"; announce("Undone."); showWalkthrough(walk.i, false); $(`[data-ask="${k}"]`)?.focus(); });
  screen.querySelectorAll("[data-manual]").forEach(c => c.addEventListener("change", () => (walk.tasks[c.dataset.manual] = c.checked ? "manual-done" : "todo")));
  act("data-plug", size => { walk.usb = { stage: size === "4" ? "small" : "found", confirm: false }; announce(size === "4" ? "This stick is too small." : "Found a USB stick: SanDisk Ultra, 16 GB."); showWalkthrough(walk.i, false); });
  $("#usbconfirm")?.addEventListener("change", e => { walk.usb.confirm = e.target.checked; showWalkthrough(walk.i, false); $("#usbconfirm").focus(); });
  $("#usbgo")?.addEventListener("click", () => { runUsb(); $("#usbcancel")?.focus(); });
  $("#usbcancel")?.addEventListener("click", () => { clearInterval(walk.usb.timer); walk.usb = { stage: "stopped" }; announce("Stopped."); showWalkthrough(walk.i, false); $("#usbagain")?.focus(); });
  $("#usbagain")?.addEventListener("click", () => { walk.usb = { stage: "found", confirm: false }; showWalkthrough(walk.i, false); });
  $("#printguide")?.addEventListener("click", () => { const w = window.open(journey().phoneGuide, "_blank"); w?.addEventListener("load", () => w.print()); });
  $("#restartnow")?.addEventListener("click", () => { walk.restarted = true; announce("Simulated restart."); showWalkthrough(walk.i, false); });
  if (focus) { const h = $("h1", screen); h.tabIndex = -1; h.focus(); announce(`${phases[phaseNo].name}: ${s.title}`); }
}

// Old shortcut (?screen=allsteps): the take-along guide covers this now.
function showAllSteps() { showWalkthrough(screens().length - 1); }
