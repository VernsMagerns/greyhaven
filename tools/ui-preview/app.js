// Throwaway design preview of the GreyHaven app. Mirrors the WPF views (src/GreyHaven.App/Views)
// and view models (src/GreyHaven.Presentation/ViewModels) using real report JSON from the CLI.
// Wording is copied from the view models; labels come from labels.js (generated from Labels.cs).
"use strict";

const L = window.GH_LABELS;
const label = (map, key) => (L[map] && (L[map][key] ?? L[map]._default)) || key;
const category = c => label("Of.ComponentCategory", c);
const ratingLabel = r => label("Of.Rating", r);
const ratingOfficial = r => label("Official.Rating", r);
const importanceLabel = i => label("Of.Importance", i);
const importanceExplain = i => label("Explain.Importance", i);
const band = b => label("Band.ScoreBand", b);

// RatingDisplay.ColourKey and the export symbols (the app uses Segoe Fluent Icons glyphs).
const RATING = {
  worksOutOfTheBox: { glyph: "✓", colour: "works" },
  worksWithExtraDriver: { glyph: "⚙︎", colour: "extrastep" },
  partial: { glyph: "◐", colour: "partial" },
  unsupported: { glyph: "✗", colour: "unsupported" },
  unknown: { glyph: "?", colour: "unknown" },
};

const $ = (sel, el = document) => el.querySelector(sel);
const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
const screen = $("#screen");
const announce = text => { $("#live").textContent = ""; setTimeout(() => ($("#live").textContent = text), 50); };

const state = { sample: null, samples: [], envelope: null, report: null, path: null, distro: null, saved: null };

// ---- Appearance (ThemeManager): Match Windows follows the system; Light/Dark override ----
const themeBox = $("#theme");
function applyTheme(choice) {
  if (choice === "system") delete document.documentElement.dataset.theme;
  else document.documentElement.dataset.theme = choice;
  try { localStorage.setItem("gh-preview-theme", choice); } catch { /* preview only */ }
}
themeBox.addEventListener("change", () => applyTheme(themeBox.value));
try { themeBox.value = localStorage.getItem("gh-preview-theme") || "system"; } catch { themeBox.value = "system"; }
applyTheme(themeBox.value);

// ---- Sample picker (preview only) ----
async function init() {
  state.samples = window.GH_DATA.samples;
  const picker = $("#sample");
  picker.innerHTML = state.samples.map(s => `<option value="${esc(s.sample)}">${esc(s.sample)}</option>`).join("");
  const fromUrl = new URLSearchParams(location.search).get("sample");
  picker.value = state.samples.some(s => s.sample === fromUrl) ? fromUrl : "nvidia-desktop";
  picker.addEventListener("change", () => { state.sample = picker.value; showStart(); });
  state.sample = picker.value;

  // Shortcuts for links and screenshots: ?sample=..&screen=results&path=advanced&theme=dark&open=gpu
  const q = new URLSearchParams(location.search);
  if (["light", "dark", "system"].includes(q.get("theme"))) { themeBox.value = q.get("theme"); applyTheme(themeBox.value); }
  if (q.get("screen") === "results") {
    await loadReport();
    if (q.get("path") && state.report.recommendation.paths.some(p => p.kind === q.get("path"))) {
      state.path = q.get("path");
      state.distro = selectedDistroId();
    }
    showResults(true);
    if (q.get("open")) screen.querySelectorAll("details.part").forEach(d => {
      if (d.dataset.category === q.get("open")) d.open = true;
    });
    return;
  }
  showStart();
}

async function loadReport() {
  state.envelope = window.GH_DATA.reports[state.sample];
  state.report = state.envelope.report;
  state.path = state.report.recommendation.defaultPath ?? null;
  state.distro = selectedDistroId() ?? state.report.distros[0].distroId;
  state.saved = null;
}

function focusHeading() { const h = $("h1", screen); if (h) { h.tabIndex = -1; h.focus(); } }

// ---- Start (StartView.xaml) ----
function showStart() {
  document.title = `GreyHaven - demo (${state.sample})`;
  screen.innerHTML = `
    <div style="margin-top:24px">
      <h1>Will this PC work well with Linux?</h1>
      <p class="measure">GreyHaven checks each part of your PC and tells you how well it will work with Linux Mint, Ubuntu and Pop!_OS, what to do about anything that needs attention, and which one suits your PC best. It doesn't change anything on your PC.</p>
      <button class="accent big" id="scan" accesskey="s" title="Reads your PC's hardware and checks it against Linux. Usually takes under a minute."><u>S</u>can my PC</button>
      <label class="radio measure" style="margin-top:24px">
        <input type="checkbox" checked id="online">
        <span class="body">Look up unknown devices online. Sends only device IDs (like pci:8086-2723) to linux-hardware.org, and checks the distros' websites for new releases.</span>
      </label>
      <p class="secondary measure" style="margin-top:16px">GreyHaven reads device IDs and settings only. It never reads serial numbers, your name, your PC's name or network addresses.</p>
    </div>`;
  $("#scan").addEventListener("click", scan);
  focusHeading();
}

// ---- Working (WorkingView.xaml), simulated ----
async function scan() {
  let cancelled = false;
  screen.innerHTML = `
    <div style="margin-top:24px">
      <h1>Checking your PC</h1>
      <div class="progress" role="progressbar" aria-label="Checking your PC"></div>
      <p id="status">Reading your PC's hardware...</p>
      <button id="cancel" style="margin-top:20px" accesskey="c"><u>C</u>ancel</button>
    </div>`;
  $("#cancel").addEventListener("click", () => { cancelled = true; showStart(); announce("Scan cancelled."); });
  focusHeading();
  announce("Reading your PC's hardware...");
  const data = loadReport();
  await new Promise(r => setTimeout(r, 900));
  if (cancelled) return;
  $("#status").textContent = "Checking how each part works on Linux...";
  announce("Checking how each part works on Linux...");
  await new Promise(r => setTimeout(r, 900));
  if (cancelled) return;
  await data;
  showResults(true);
}

const selectedPath = () => state.report.recommendation.paths.find(p => p.kind === state.path) ?? null;
const selectedDistroId = () => selectedPath()?.distroId ?? null;
const distroById = id => state.report.distros.find(d => d.distroId === id);

// ---- Results (ResultsView.xaml + ResultsViewModel) ----
function showResults(focus) {
  const r = state.report;
  const paths = r.recommendation.paths;
  screen.innerHTML = `
    <div style="margin-top:16px">
      ${notices(r)}
      <h1>Your results</h1>
      <div class="card score" id="scorecard"></div>

      <h2>What GreyHaven recommends</h2>
      <p style="margin-bottom:12px">${esc(r.recommendation.paragraph)}</p>
      ${paths.length > 1 ? `
        <div role="radiogroup" aria-labelledby="pathlabel" style="margin-bottom:8px">
          <p class="strong" id="pathlabel" style="margin-bottom:6px">Choose how you'd like to switch</p>
          ${paths.map(p => `
            <label class="radio">
              <input type="radio" name="path" value="${esc(p.kind)}" ${p.kind === state.path ? "checked" : ""}>
              <span><span class="strong">${esc(p.title)}</span>${p.kind === r.recommendation.defaultPath ? " (Suggested for most people)" : ""}<br>
              <span class="secondary">${p.requirements?.includes("turnOffSecureBoot") ? "Needs Secure Boot turned off" : "Keeps Secure Boot on"}</span></span>
            </label>`).join("")}
        </div>` : ""}
      <div id="pathcard"></div>

      <h2 id="compare">Compare distros</h2>
      <p class="secondary" style="margin-bottom:10px">Select a distro to see how each part of your PC works on it.</p>
      <div class="distros" role="radiogroup" aria-labelledby="compare">
        ${r.distros.map(d => `
          <label class="radio">
            <input type="radio" name="distro" value="${esc(d.distroId)}" ${d.distroId === state.distro ? "checked" : ""}>
            <span><span class="strong">${esc(d.displayName)}</span><br>
            <span>${d.score.score} / 100, ${esc(band(d.score.band))}</span>
            ${d.distroId === selectedDistroId() ? `<br><span class="secondary">Your choice</span>` : ""}</span>
          </label>`).join("")}
      </div>

      <h2 id="partsheading"></h2>
      <p class="secondary" style="margin-bottom:8px">Parts labelled "Essential" are ones the PC can't be used properly without, and they count most toward the score. "Important" parts are ones you'd notice if they didn't work.</p>
      <div id="parts"></div>

      ${r.caveats.length ? `
        <h2>Test these with a live USB first</h2>
        <p class="secondary" style="margin-bottom:10px">These can't be checked from Windows. A live USB lets you try Linux without installing anything. Save your report for a step-by-step live-USB guide with a checklist for this PC.</p>
        ${r.caveats.map(c => `<div class="card"><p class="strong">${esc(c.title)}</p><p style="margin-top:4px">${esc(c.why)}</p>
          <p style="margin-top:6px"><span class="strong">What to test:</span> ${esc(c.whatToTest)}</p></div>`).join("")}` : ""}

      <h2>Save or share your report</h2>
      <p style="margin-bottom:10px">Save everything on this page, for the path you chose, plus the live-USB guide. A web page opens in any browser and can be printed. The report has no serial numbers, user names or network addresses, so it's safe to share when asking for help.</p>
      <div style="display:flex;gap:12px;align-items:center">
        <button id="save" accesskey="r">Save <u>r</u>eport…</button>
        <button class="linkbutton" id="showsaved" hidden>Show in folder</button>
      </div>
      <p id="savestatus" style="margin-top:8px" hidden></p>

      <div style="margin-top:24px">
        <p class="secondary">Hardware data from ${esc(longDate(r.data.builtOn))}</p>
        <button id="again" style="margin-top:12px" accesskey="s"><u>S</u>can again</button>
      </div>
    </div>`;

  screen.querySelectorAll('input[name="path"]').forEach(i => i.addEventListener("change", () => choosePath(i.value)));
  screen.querySelectorAll('input[name="distro"]').forEach(i => i.addEventListener("change", () => { state.distro = i.value; renderDistro(); }));
  $("#save").addEventListener("click", saveReport);
  $("#showsaved").addEventListener("click", () => window.open(state.saved.href, "_blank", "noopener"));
  $("#again").addEventListener("click", showStart);
  renderPath();
  renderDistro();
  renderSaved();
  if (focus) focusHeading();
}

function notices(r) {
  const out = [];
  out.push(["info", "Demo mode", `This report uses a sample PC ("${state.sample}"), not your hardware.`]);
  if (r.data.notice) out.push(["warning", "Check for a GreyHaven update", r.data.notice]);
  if (r.profile.system.isArm) out.push(["warning", "This PC has an ARM processor", "Linux support for ARM Windows PCs is limited, and most parts can't be checked from here."]);
  const scanNotes = (r.profile.warnings ?? []).filter(w => !w.includes("ARM processor"));
  if (scanNotes.length) out.push(["info", "Some details couldn't be read", scanNotes.join(" ")]);
  for (const n of r.contextNotes ?? []) out.push(["info", "About Windows 10", n.text, n.source]);
  for (const n of r.dataNotes ?? []) out.push(["info", "About this report", n]);
  return out.map(([kind, title, msg, link]) => `
    <div class="notice ${kind}"><p class="strong">${esc(title)}</p><p>${esc(msg)}</p>
    ${link?.url ? `<p><a href="${esc(link.url)}" target="_blank" rel="noopener">${esc(link.title)}</a></p>` : ""}</div>`).join("");
}

function choosePath(kind) {
  state.path = kind;
  state.distro = selectedDistroId();
  state.saved = null;
  showResults(false);
  $(`input[name="path"][value="${kind}"]`).focus();
}

function renderPath() {
  const p = selectedPath();
  const card = $("#pathcard");
  if (!p) { card.innerHTML = ""; return; }
  const g = p.secureBootGuide;
  const guideTitle = g ? (g.fromManufacturer ? `How to turn off Secure Boot: ${g.title}` : "How to turn off Secure Boot (GreyHaven guide)") : null;
  const guideHref = g ? (g.url ?? "https://github.com/VernsMagerns/greyhaven/blob/main/docs/secure-boot.md") : null;
  card.innerHTML = `
    <div class="card">
      <h3 style="margin-top:0;margin-bottom:6px">${esc(p.title)}</h3>
      <p>${esc(p.paragraph)}</p>
      ${p.secureBootNotice ? `
        <div class="sb-box">
          <p class="strong">Secure Boot needs to be turned off</p>
          <p style="margin:4px 0 8px">${esc(p.secureBootNotice)}</p>
          ${g ? `<a href="${esc(guideHref)}" target="_blank" rel="noopener">${esc(guideTitle)}</a>` : ""}
        </div>` : ""}
      <p class="secondary" style="margin-top:12px">You'll download: ${esc(p.editionName)}</p>
    </div>`;
}

function summarise(ratings) {
  const n = r => ratings.filter(x => x === r).length;
  const count = (k, one, many) => k === 1 ? `1 part ${one}` : `${k} parts ${many}`;
  const parts = [];
  if (n("worksOutOfTheBox")) parts.push(count(n("worksOutOfTheBox"), "works out of the box", "work out of the box"));
  if (n("worksWithExtraDriver")) parts.push(count(n("worksWithExtraDriver"), "needs an extra step", "need an extra step"));
  if (n("partial")) parts.push(count(n("partial"), "partly works", "partly work"));
  if (n("unsupported")) parts.push(count(n("unsupported"), "doesn't work", "don't work"));
  if (n("unknown")) parts.push(count(n("unknown"), "couldn't be checked", "couldn't be checked"));
  if (!parts.length) return "";
  return parts.length === 1 ? parts[0] + "." : parts.slice(0, -1).join(", ") + " and " + parts.at(-1) + ".";
}

const IMPORTANCE_ORDER = { critical: 0, important: 1, minor: 2 };

function renderDistro() {
  const r = state.report;
  const d = distroById(state.distro);
  const ratingFor = c => c.ratings.find(x => x.distroId === d.distroId);
  const chosen = selectedDistroId();
  const note = d.score.capReason ?? (d.score.incomplete ? "Some essential parts couldn't be checked, so this score is incomplete." : null);
  const comparing = chosen && chosen !== d.distroId
    ? `You're comparing ${d.displayName}. Your chosen path uses ${distroById(chosen).displayName}.` : null;

  $("#scorecard").innerHTML = `
    <div aria-label="Score for ${esc(d.displayName)}: ${d.score.score} out of 100, ${esc(band(d.score.band))}.">
      <div class="score-number">${d.score.score} <small>/ 100</small></div>
      <div class="score-band">${esc(band(d.score.band))}</div>
    </div>
    <div>
      <p class="strong" style="font-size:16px">${esc(d.displayName)}</p>
      <p style="margin-top:4px">${esc(summarise(r.components.map(c => ratingFor(c).rating)))}</p>
      ${note ? `<p class="secondary" style="margin-top:6px">${esc(note)}</p>` : ""}
      ${comparing ? `<p style="margin-top:8px">${esc(comparing)}</p><button class="linkbutton" id="showchosen">Show my chosen distro</button>` : ""}
    </div>`;
  $("#showchosen")?.addEventListener("click", () => {
    state.distro = chosen;
    $(`input[name="distro"][value="${chosen}"]`).checked = true;
    renderDistro();
  });

  $("#partsheading").textContent = `How each part works on ${d.displayName}`;
  const ordered = [...r.components].sort((a, b) =>
    IMPORTANCE_ORDER[a.importance] - IMPORTANCE_ORDER[b.importance] || categoryOrder(a) - categoryOrder(b));
  const attention = ordered.filter(c => ratingFor(c).rating !== "worksOutOfTheBox");
  const works = ordered.filter(c => ratingFor(c).rating === "worksOutOfTheBox");
  $("#parts").innerHTML = [["Needs your attention", attention], ["Works out of the box", works]]
    .filter(([, list]) => list.length)
    .map(([heading, list]) => `<h3>${heading}</h3>${list.map(c => part(c, ratingFor(c), r)).join("")}`).join("");
  $("#parts").querySelectorAll("[data-copy]").forEach(b => b.addEventListener("click", () => {
    navigator.clipboard?.writeText(b.dataset.copy).then(() => announce("Copied"), () => {});
  }));
}

// ComponentCategory enum order (the app sorts by importance, then category).
const CATEGORY_ORDER = Object.keys(L["Of.ComponentCategory"]).filter(k => k !== "_default");
const categoryOrder = c => CATEGORY_ORDER.indexOf(c.component.category);

function badge(rating) {
  const r = RATING[rating] ?? RATING.unknown;
  return `<span class="badge c-${r.colour}"><span class="glyph" aria-hidden="true">${r.glyph}</span>${esc(ratingLabel(rating))}</span>`;
}

// ComponentViewModel + ComponentTemplate
function part(c, rating, report) {
  const comp = c.component;
  const name = comp.count > 1 ? `${c.displayName} (×${comp.count})` : c.displayName;
  const title = category(comp.category);
  const imp = c.importance === "minor" ? null : importanceLabel(c.importance);
  const accessible = `${title}: ${ratingLabel(rating.rating)}.${imp ? ` ${imp} part.` : ""} ${name}`;
  const fix = rating.fix;
  const tests = report.caveats.filter(cv => (c.caveatIds ?? []).includes(cv.id)).map(cv => cv.title);
  const tech = [["Rating", ratingOfficial(rating.rating)], ["Confidence", cap(rating.confidence)]];
  if (comp.id) {
    tech.push(["Device ID", comp.id.key]);
    if (comp.id.hasSubsystem) tech.push(["Subsystem", `${comp.id.subVendor}:${comp.id.subDevice}`]);
    if (comp.id.classCode) tech.push(["PCI class", comp.id.classCode]);
  }
  if (comp.cpu) tech.push(["Processor", `${comp.cpu.vendor}, family ${comp.cpu.family}, model ${comp.cpu.model}`]);
  if (rating.linuxDriver) tech.push(["Linux driver", rating.linuxDriver]);
  if (comp.windowsDriver) tech.push(["Windows driver", comp.windowsDriver]);
  if (comp.windowsProblemCode > 0) tech.push(["Windows status", comp.windowsProblemCode === 28 ? "No Windows driver installed" : `Problem code ${comp.windowsProblemCode}`]);
  tech.push(["Importance", importanceExplain(c.importance)]);

  return `
  <details class="part" data-category="${esc(comp.category)}">
    <summary aria-label="${esc(accessible)}">
      <span style="grid-column:2"><span class="strong">${esc(title)}</span>${imp ? `<span class="importance">${esc(imp)}</span>` : ""}<br>
      <span class="secondary">${esc(name)}</span></span>
      ${badge(rating.rating)}
    </summary>
    <div class="part-body">
      <p>${esc(rating.explanation)}</p>
      ${rating.approximationNote ? `<p class="secondary" style="margin-top:6px">${esc(rating.approximationNote)}</p>` : ""}
      <p class="secondary" style="margin-top:6px">How sure GreyHaven is: ${esc(cap(rating.confidence))}${rating.isWeakData ? `<span class="strong" style="margin-left:12px">Based on limited data</span>` : ""}</p>
      ${fix ? `
        <div class="section" style="margin-top:16px">
          <p class="strong">What to do: ${esc(fix.driver)}</p>
          ${fix.steps?.length ? `<ol class="steps">${fix.steps.map(s => `<li>${esc(s)}</li>`).join("")}</ol>` : ""}
          <div style="margin-top:8px">${(fix.commands ?? []).map(cmd => `
            <div class="command"><code>${esc(cmd)}</code>
              <button data-copy="${esc(cmd)}" aria-label="Copy command: ${esc(cmd)}"><span aria-hidden="true">⧉</span> Copy</button></div>`).join("")}</div>
          ${fix.needsInternet ? `<p class="secondary" style="margin-top:6px">This step needs an internet connection.</p>` : ""}
          ${fix.note ? `<p style="margin-top:6px">${esc(fix.note)}</p>` : ""}
          ${fix.secureBootAdvice ? `<div class="sb-box" style="padding:10px 12px;margin-top:10px"><p>${esc(fix.secureBootAdvice)}</p></div>` : ""}
        </div>` : ""}
      ${tests.length ? `<div class="section"><p class="strong">Test this with a live USB</p>${tests.map(t => `<p>${esc(t)}</p>`).join("")}</div>` : ""}
      ${rating.sources?.length ? `<div class="section"><p class="strong">Sources</p>${rating.sources.map(s =>
        s.url ? `<p><a href="${esc(s.url)}" target="_blank" rel="noopener">${esc(s.title)}</a></p>` : `<p>${esc(s.title)}</p>`).join("")}</div>` : ""}
      <details class="tech"><summary>Technical details</summary>
        <div style="margin-top:6px">${tech.map(([k, v]) => `<div class="techrow"><span class="secondary">${esc(k)}</span><code>${esc(v)}</code></div>`).join("")}</div>
      </details>
    </div>
  </details>`;
}

const cap = s => s ? s[0].toUpperCase() + s.slice(1) : s;

// ---- Save report: the real app shows a Save dialog; the preview opens the generated report ----
function saveReport() {
  const entry = state.samples.find(s => s.sample === state.sample);
  const file = entry.reports.find(r => r.kind === (state.path ?? "recommended")) ?? entry.reports[0];
  const today = new Date();
  const name = `greyhaven-report-${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}.html`;
  state.saved = { name, href: file ? `../reports/${file.file}` : null };
  renderSaved();
  announce(`Report saved: ${name}`);
}

function renderSaved() {
  const status = $("#savestatus");
  const show = $("#showsaved");
  if (!state.saved) { status.hidden = true; show.hidden = true; return; }
  status.hidden = false;
  status.innerHTML = `Report saved: ${esc(state.saved.name)} <span class="secondary">(preview: the real app shows a Save dialog first; "Show in folder" opens the saved report here)</span>`;
  show.hidden = !state.saved.href;
}

function longDate(iso) {
  const [y, m, d] = iso.split("-").map(Number);
  return `${d} ${["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"][m - 1]} ${y}`;
}

init().catch(err => {
  screen.innerHTML = `<h1>Preview data is missing</h1><p class="measure">Run <code>python3 -I tools/ui-preview/build.py</code> and open the page through its local web server. (${esc(err.message)})</p>`;
});
