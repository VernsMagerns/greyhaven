// Throwaway design preview of the GreyHaven app (round 2: branding directions + plain words).
// Data: real report JSON from the CLI (data.js). Labels: labels.js (generated from Labels.cs).
// Brands: brands.css and brands.js (generated from brands.json). Nothing here ships with the app.
"use strict";

const L = window.GH_LABELS;
const label = (map, key) => (L[map] && (L[map][key] ?? L[map]._default)) || key;
const category = c => label("Of.ComponentCategory", c);
const ratingOfficial = r => label("Official.Rating", r);
const importanceLabel = i => label("Of.Importance", i);
const importanceExplain = i => label("Explain.Importance", i);

// Plain-words ratings for the new design (shorter than Labels.Of(Rating)).
const RATING = {
  worksOutOfTheBox: { glyph: "✓", colour: "works", words: "Works right away" },
  worksWithExtraDriver: { glyph: "⚙︎", colour: "extrastep", words: "Needs one extra step" },
  partial: { glyph: "◐", colour: "partial", words: "Partly works" },
  unsupported: { glyph: "✗", colour: "unsupported", words: "Won't work" },
  unknown: { glyph: "?", colour: "unknown", words: "Not sure" },
};

// Installer sizes from the official download pages (checked 2026-10-05); to move into
// distros.yaml. Rounded, in the binary GB that Windows shows.
const INSTALLER_GB = { "ubuntu/desktop": 6, "mint/cinnamon": 3, "pop-os/nvidia": 4, "pop-os/intel": 3 };

const $ = (sel, el = document) => el.querySelector(sel);
const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
const screen = $("#screen");
const announce = text => { $("#live").textContent = ""; setTimeout(() => ($("#live").textContent = text), 50); };
const query = new URLSearchParams(location.search);

const state = { sample: null, samples: [], report: null, path: null, distro: null, saved: null };
let rerender = () => {};

// ---- Look (brand) and appearance ------------------------------------------------------------
const brandBox = $("#brand");
const themeBox = $("#theme");
const BRANDS = window.GH_BRANDS;
brandBox.innerHTML = BRANDS.map(b => `<option value="${esc(b.id)}">${esc(b.name)}</option>`).join("") + `<option value="classic">Today's look</option>`;

function remember(key, value) { try { localStorage.setItem(key, value); } catch { /* preview only */ } }
function recall(key) { try { return localStorage.getItem(key); } catch { return null; } }

function applyLook() {
  const root = document.documentElement;
  root.dataset.brand = brandBox.value;
  if (themeBox.value === "system") delete root.dataset.theme; else root.dataset.theme = themeBox.value;
  const b = BRANDS.find(x => x.id === brandBox.value);
  $("#brandnote").innerHTML = b
    ? `<strong>${esc(b.name)}:</strong> ${esc(b.tagline)} Fonts: ${esc(b.headingFont.family)} and ${esc(b.bodyFont.family)} (both ${esc(b.bodyFont.licence)}). Pictures: ${esc(b.illustration)}`
    : "Today's colours and font (Segoe UI), with the new words.";
  remember("gh-brand", brandBox.value);
  remember("gh-theme", themeBox.value);
}
brandBox.value = query.get("brand") || recall("gh-brand") || "market"; // chosen 2026-10-06
themeBox.value = query.get("theme") || recall("gh-theme") || "system";
brandBox.addEventListener("change", () => { applyLook(); rerender(); });
themeBox.addEventListener("change", applyLook);
applyLook();

// ---- Words: distro names and plain-language helpers ------------------------------------------
const distroName = id => {
  const g = state.report.liveUsbGuides.find(x => x.distroId === id);
  return g ? g.distroName.replace(/\s\d.*$/, "") : id; // "Ubuntu 26.04.1 LTS" -> "Ubuntu"
};
const distroById = id => state.report.distros.find(d => d.distroId === id);
const selectedPath = () => state.report.recommendation.paths.find(p => p.kind === state.path) ?? null;
const selectedDistroId = () => selectedPath()?.distroId ?? null;
const ratingOn = (c, id) => c.ratings.find(x => x.distroId === id).rating;
const plural = (n, one, many) => `${n} ${n === 1 ? one : many}`;

// A part as a noun in a sentence ("Your graphics card needs..."); falls back to the label.
const NOUN = {
  cpu: "processor", gpu: "graphics card", storageController: "storage", nvmeDrive: "SSD", wifi: "Wi-Fi",
  ethernet: "wired network", bluetooth: "Bluetooth", audioController: "sound", audioCodec: "sound",
  displayAudio: "HDMI sound", webcam: "camera", touchpad: "touchpad", fingerprintReader: "fingerprint reader",
};
const noun = c => NOUN[c] ?? category(c).toLowerCase();
const joinAnd = list => list.length < 2 ? list.join("") : `${list.slice(0, -1).join(", ")} and ${list.at(-1)}`;
const uniq = list => [...new Set(list)];

function headline(score) {
  if (score >= 90) return "Your PC is ready for Linux";
  if (score >= 70) return "Your PC should work well with Linux";
  if (score >= 50) return "Linux will work, with a few problems";
  return "Linux may not work well on this PC";
}

// Same grouping as PartsOverview.cs: chipset in one line (not counted), small unchecked parts folded away calmly.
function overview(id) {
  const r = state.report;
  const ordered = [...r.components].sort((a, b) =>
    IMPORTANCE_ORDER[a.importance] - IMPORTANCE_ORDER[b.importance] ||
    CATEGORY_ORDER.indexOf(a.component.category) - CATEGORY_ORDER.indexOf(b.component.category));
  const quiet = c => ["worksOutOfTheBox", "unknown"].includes(ratingOn(c, id));
  const chipset = ordered.filter(c => c.component.category === "chipset" && quiet(c));
  const rest = ordered.filter(c => !chipset.includes(c));
  const working = rest.filter(c => ratingOn(c, id) === "worksOutOfTheBox");
  const unchecked = rest.filter(c => ratingOn(c, id) === "unknown" && c.importance === "minor");
  const attention = rest.filter(c => ratingOn(c, id) !== "worksOutOfTheBox" && !unchecked.includes(c));
  return { chipset, working, unchecked, attention };
}

function partsSummary(id) {
  const o = overview(id);
  const counted = [...o.working, ...o.attention, ...o.unchecked];
  const n = r => counted.filter(c => ratingOn(c, id) === r).length;
  const s = [];
  if (n("worksOutOfTheBox")) s.push(`${plural(n("worksOutOfTheBox"), "part works", "parts work")} right away.`);
  if (n("worksWithExtraDriver")) s.push(`${plural(n("worksWithExtraDriver"), "part needs", "parts need")} one extra step after you install.`);
  if (n("partial")) s.push(`${plural(n("partial"), "part only partly works", "parts only partly work")}.`);
  if (n("unsupported")) s.push(`${plural(n("unsupported"), "part won't work", "parts won't work")}.`);
  const big = counted.filter(c => ratingOn(c, id) === "unknown" && c.importance !== "minor").length;
  if (big) s.push(`We couldn't check ${plural(big, "important part", "important parts")}. Test ${big === 1 ? "it" : "them"} from the USB stick.`);
  if (o.unchecked.length) s.push(`${plural(o.unchecked.length, "small part", "small parts")} couldn't be checked, which rarely matters.`);
  return s.join(" ");
}

// What you actually download, in words a beginner understands.
function download(path) {
  const gb = INSTALLER_GB[`${path.distroId}/${path.editionId}`];
  const which = path.editionId === "nvidia" ? " made for NVIDIA graphics" : "";
  return `The ${distroName(path.distroId)} installer file${which}${gb ? ` (about ${gb} GB)` : ""}. GreyHaven will put it on a USB stick for you.`;
}

// Parts that work better on one distro than another, e.g. ["graphics"].
function betterOn(a, b) {
  const rank = { worksOutOfTheBox: 0, worksWithExtraDriver: 1, partial: 2, unknown: 3, unsupported: 4 };
  return uniq(state.report.components.filter(c => rank[ratingOn(c, a)] < rank[ratingOn(c, b)]).map(c => noun(c.component.category)));
}

function pathLines(p) {
  const needsSecureBootOff = p.requirements?.includes("turnOffSecureBoot");
  const lines = [];
  if (p.kind === "advanced") {
    const easy = state.report.recommendation.paths.find(x => x.kind === "easy");
    const better = easy ? betterOn(p.distroId, easy.distroId) : [];
    lines.push(better.length ? `Your ${joinAnd(better)} ${better.length === 1 ? "works" : "work"} right away.` : "The best match for this PC's parts.");
  } else {
    if (!needsSecureBootOff) lines.push("Nothing to change on your PC first.");
    const extra = uniq(state.report.components.filter(c => ratingOn(c, p.distroId) === "worksWithExtraDriver").map(c => noun(c.component.category)));
    if (extra.length) lines.push(`Your ${joinAnd(extra)} ${extra.length === 1 ? "needs" : "need"} one extra step after you install.`);
  }
  if (needsSecureBootOff) lines.push("First, you turn off Secure Boot in your PC's startup settings.");
  return lines;
}

// ---- Shared bits -----------------------------------------------------------------------------
const demoBanner = () => `<div class="demo-banner">Demo: this is a sample PC, not yours.</div>`;

function badge(rating) {
  const r = RATING[rating] ?? RATING.unknown;
  return `<span class="badge c-${r.colour}"><span class="glyph" aria-hidden="true">${r.glyph}</span>${esc(r.words)}</span>`;
}

function more(summary, inner, open = false) {
  return `<details class="more"${open ? " open" : ""}><summary>${esc(summary)}</summary><div class="more-body">${inner}</div></details>`;
}

const SECURE_BOOT = "Secure Boot is a safety setting. It decides what software is allowed to start your PC.";

// ---- Start -----------------------------------------------------------------------------------
function showStart() {
  rerender = showStart;
  document.title = `GreyHaven - demo (${state.sample})`;
  screen.innerHTML = `
    ${demoBanner()}
    <div class="hero">
      <div>
        <h1>Will Linux work on your PC?</h1>
        <p class="lead">GreyHaven checks the parts inside your PC. Then it tells you which version of Linux fits best. It won't change anything.</p>
        <button class="accent big" id="scan" accesskey="s"><u>S</u>can my PC</button>
        <label class="check">
          <input type="checkbox" checked id="online">
          <span>Also check online for parts we don't know<br><span class="secondary">Only sends part ID numbers.</span></span>
        </label>
        <p class="secondary small">GreyHaven never reads your files or personal details.</p>
      </div>
      <div class="hero-art" aria-hidden="true">${art("hero")}</div>
    </div>`;
  $("#scan").addEventListener("click", () => scan());
  focusHeading();
}

// ---- Checking --------------------------------------------------------------------------------
async function scan(stay) {
  rerender = () => {};
  let cancelled = false;
  screen.innerHTML = `
    ${demoBanner()}
    <h1>Checking your PC</h1>
    <div class="progress" role="progressbar" aria-label="Checking your PC"></div>
    <p id="status">Looking at the parts inside your PC…</p>
    <button id="cancel" class="plain" accesskey="c"><u>C</u>ancel</button>`;
  $("#cancel").addEventListener("click", () => { cancelled = true; showStart(); announce("Stopped."); });
  focusHeading();
  announce("Looking at the parts inside your PC");
  loadReport();
  if (stay === true) return;
  await new Promise(r => setTimeout(r, 900));
  if (cancelled) return;
  $("#status").textContent = "Checking how each part works with Linux…";
  announce("Checking how each part works with Linux");
  await new Promise(r => setTimeout(r, 900));
  if (cancelled) return;
  showResults(true);
}

function loadReport() {
  state.report = window.GH_DATA.reports[state.sample].report;
  state.path = state.report.recommendation.defaultPath ?? null;
  state.distro = selectedDistroId() ?? state.report.distros[0].distroId;
  state.saved = null;
  Object.assign(walk, { i: 0, install: null, done: {}, tasks: {}, usb: { stage: "waiting" }, restarted: false });
}

// ---- Results ---------------------------------------------------------------------------------
function showResults(focus) {
  rerender = () => showResults(false);
  const r = state.report;
  const paths = r.recommendation.paths;
  const chosen = selectedDistroId();
  const best = chosen ? distroById(chosen) : [...r.distros].sort((a, b) => b.score.score - a.score.score)[0];
  const why = (r.contextNotes ?? [])[0];

  screen.innerHTML = `
    ${demoBanner()}
    <section class="headline card">
      <div class="headline-text">
        <h1>${esc(paths.length ? headline(best.score.score) : "We can't suggest a version of Linux yet")}</h1>
        ${paths.length ? `<p class="suggest">We suggest <strong>${esc(distroName(chosen))}</strong>.</p>` : ""}
        <p>${esc(partsSummary(best.distroId))}</p>
        ${paths.length ? `<p><button class="accent big" id="howto">Show me how to switch →</button></p>` : ""}
        ${r.profile.system.isArm ? `<p class="warn-line">This PC has an ARM chip. Linux support for these PCs is still limited.</p>` : ""}
      </div>
      <div class="score-pill" role="img" aria-label="Score: ${best.score.score} out of 100">
        <span class="num">${best.score.score}</span><span class="of">out of 100</span>
      </div>
    </section>

    ${why ? more("Why switch now?", `
      <p>Microsoft stopped supporting Windows 10 in October 2025. Security updates now need a special sign-up. Those updates end in October 2027.</p>
      <p><a href="${esc(why.source.url)}" target="_blank" rel="noopener">Read Microsoft's page</a></p>`) : ""}

    ${paths.length > 1 ? `
      <h2 id="choose">Pick your version of Linux</h2>
      <p class="secondary">There are many versions of Linux. People call them "distros". Two fit this PC well.</p>
      <div class="paths" role="radiogroup" aria-labelledby="choose">${paths.map(pathCard).join("")}</div>`
    : paths.length === 1 ? `
      <h2>Your version of Linux</h2>
      <div class="paths">${pathCard(paths[0])}</div>` : ""}

    <section class="next card">
      <div>
        <h2>What's next?</h2>
        <p>We'll guide you, one step at a time.</p>
      </div>
      <button class="accent big" id="next">What's next →</button>
    </section>

    <h2>Your PC's parts on ${esc(distroName(state.distro))}</h2>
    <div id="parts"></div>

    ${r.caveats.length ? more(`${plural(r.caveats.length, "thing", "things")} to test before you install`, `
      <p>We can't check ${r.caveats.length === 1 ? "this" : "these"} from Windows. Test ${r.caveats.length === 1 ? "it" : "them"} by trying Linux from a USB stick first.</p>
      ${r.caveats.map(c => `<p><strong>${esc(c.title)}.</strong> ${esc(c.whatToTest)}</p>`).join("")}`) : ""}

    <h2 id="compare">Compare versions of Linux</h2>
    <div class="distros" role="radiogroup" aria-labelledby="compare">
      ${r.distros.map(d => `
        <label class="option distro${d.distroId === state.distro ? " selected" : ""}">
          <input type="radio" name="distro" value="${esc(d.distroId)}" ${d.distroId === state.distro ? "checked" : ""}>
          <span><span class="strong">${esc(distroName(d.distroId))}</span>
          <span class="score-small">${d.score.score} out of 100</span>
          ${d.distroId === chosen ? `<span class="tag">Your pick</span>` : ""}</span>
        </label>`).join("")}
    </div>

    <h2>Save your results</h2>
    <p>Keep a copy to read on your phone or print.</p>
    <div class="row">
      <button id="save" class="plain" accesskey="r">Save <u>r</u>eport…</button>
      <button class="linkbutton" id="showsaved" hidden>Show in folder</button>
    </div>
    <p id="savestatus" hidden></p>

    <p class="footnote">${esc(footnote(r))}</p>
    <button id="again" class="plain" accesskey="a">Scan <u>a</u>gain</button>`;

  screen.querySelectorAll('input[name="path"]').forEach(i => i.addEventListener("change", () => choosePath(i.value)));
  screen.querySelectorAll('input[name="distro"]').forEach(i => i.addEventListener("change", () => {
    state.distro = i.value; showResults(false); $(`input[name="distro"][value="${i.value}"]`).focus();
  }));
  $("#next").addEventListener("click", () => showWalkthrough(0));
  $("#howto")?.addEventListener("click", () => showWalkthrough(0));
  $("#save").addEventListener("click", saveReport);
  $("#showsaved").addEventListener("click", () => window.open(state.saved.href, "_blank", "noopener"));
  $("#again").addEventListener("click", showStart);
  renderParts();
  renderSaved();
  if (focus) focusHeading();
}

function pathCard(p) {
  const r = state.report;
  const single = r.recommendation.paths.length === 1;
  const tag = p.kind === "easy" ? "Easiest" : "Best fit";
  const g = p.secureBootGuide;
  const guide = g ? `<p><a href="${esc(g.url ?? "#")}" target="_blank" rel="noopener">How to turn off Secure Boot${g.fromManufacturer ? ` (${esc(g.title.split(" - ")[0])}'s guide)` : ""}</a></p>` : "";
  const head = `
    <span class="path-head"><span class="strong">${esc(distroName(p.distroId))}</span><span class="tag">${tag}</span>
      ${p.kind === r.recommendation.defaultPath && !single ? `<span class="secondary">Suggested</span>` : ""}</span>
    ${pathLines(p).map(l => `<span class="path-line">${esc(l)}</span>`).join("")}`;
  const details = more("Show more", `
    <p>${esc(p.paragraph)}</p>
    ${p.secureBootNotice ? `<p><strong>About Secure Boot.</strong> ${esc(SECURE_BOOT)} ${esc(p.secureBootNotice)}</p>${guide}` : ""}
    <p><strong>You'll get:</strong> ${esc(download(p))}</p>`);
  if (single) return `<div class="option path selected">${head}${details}</div>`;
  return `
    <div class="option path${p.kind === state.path ? " selected" : ""}">
      <label class="path-label"><input type="radio" name="path" value="${esc(p.kind)}" ${p.kind === state.path ? "checked" : ""}>
        <span>${head}</span></label>
      ${details}
    </div>`;
}

function choosePath(kind) {
  state.path = kind;
  walk.install = null; walk.i = 0; walk.done = {}; walk.tasks = {};
  state.distro = selectedDistroId();
  state.saved = null;
  showResults(false);
  $(`input[name="path"][value="${kind}"]`).focus();
}

const IMPORTANCE_ORDER = { critical: 0, important: 1, minor: 2 };
const CATEGORY_ORDER = Object.keys(L["Of.ComponentCategory"]).filter(k => k !== "_default");

function renderParts() {
  const r = state.report;
  const id = state.distro;
  const { chipset, working: works, unchecked, attention } = overview(id);
  const name = c => c.component.count > 1 ? `${c.displayName} (×${c.component.count})` : c.displayName;
  const rows = list => `<ul class="worklist">${list.map(c => `<li><span>${esc(category(c.component.category))}: <span class="secondary">${esc(name(c))}</span></span>${badge(ratingOn(c, id))}</li>`).join("")}</ul>`;
  const unknownChipset = chipset.filter(c => ratingOn(c, id) === "unknown").length;

  $("#parts").innerHTML = `
    ${attention.map(c => part(c, c.ratings.find(x => x.distroId === id), name(c))).join("")}
    ${works.length ? more(`${attention.length + unchecked.length ? "" : "All "}${plural(works.length, "part works", "parts work")} right away. Show them`, rows(works)) : ""}
    ${unchecked.length ? more(`${plural(unchecked.length, "small part", "small parts")} couldn't be checked. ${unchecked.length === 1 ? "It rarely matters" : "They rarely matter"}. Show ${unchecked.length === 1 ? "it" : "them"}`,
      `<p>These aren't parts you need to use your PC. If you use one of them, try it when you test Linux from the USB stick.</p>${rows(unchecked)}`) : ""}
    ${chipset.length ? more(unknownChipset
        ? `System chipset: ${plural(chipset.length, "part", "parts")}. ${chipset.length - unknownChipset} fine, ${unknownChipset} not checked (these rarely matter)`
        : `System chipset: ${plural(chipset.length, "part", "parts")}, all fine`,
      `<p>The chipset is the wiring on your PC's main board that connects everything else. Linux handles these parts by itself.</p>${rows(chipset)}`) : ""}`;
  $("#parts").querySelectorAll("[data-copy]").forEach(b => b.addEventListener("click", () => {
    navigator.clipboard?.writeText(b.dataset.copy).then(() => announce("Copied"), () => {});
  }));
}

// One short line per rating; the full explanation is behind "Show more".
function shortLine(rating) {
  const distro = distroName(rating.distroId);
  switch (rating.rating) {
    case "worksWithExtraDriver": return `Works after one extra step. It takes a few clicks once ${distro} is installed.`;
    case "partial": return `Only partly works on ${distro}.`;
    case "unsupported": return `Won't work on ${distro}.`;
    case "unknown": return "We couldn't check this part. Try it from a USB stick first.";
    default: return "Works right away.";
  }
}

function part(c, rating, name) {
  const comp = c.component;
  const imp = c.importance === "minor" ? null : importanceLabel(c.importance);
  const fix = rating.fix;
  const tech = [["Rating", ratingOfficial(rating.rating)], ["Confidence", cap(rating.confidence)]];
  if (comp.id) tech.push(["Device ID", comp.id.key]);
  if (rating.linuxDriver) tech.push(["Linux driver", rating.linuxDriver]);
  if (comp.windowsDriver) tech.push(["Windows driver", comp.windowsDriver]);
  tech.push(["Importance", importanceExplain(c.importance)]);
  return `
  <article class="card part">
    <div class="part-head">
      <div><h3>${esc(category(comp.category))}${imp ? `<span class="importance">${esc(imp)}</span>` : ""}</h3>
      <p class="secondary">${esc(name)}</p></div>
      ${badge(rating.rating)}
    </div>
    <p>${esc(shortLine(rating))}</p>
    ${more("Show more", `
      <p>${esc(rating.explanation)}</p>
      ${fix ? `
        <p class="strong">How to fix it: ${esc(fix.driver)}</p>
        ${fix.steps?.length ? `<ol class="steps">${fix.steps.map(s => `<li>${esc(s)}</li>`).join("")}</ol>` : ""}
        ${(fix.commands ?? []).map(cmd => `<div class="command"><code>${esc(cmd)}</code>
          <button class="plain" data-copy="${esc(cmd)}" aria-label="Copy command: ${esc(cmd)}">Copy</button></div>`).join("")}
        ${fix.secureBootAdvice ? `<p class="note-box">${esc(fix.secureBootAdvice)}</p>` : ""}` : ""}
      ${rating.sources?.length ? `<p class="secondary small">Sources: ${rating.sources.map(s => s.url ? `<a href="${esc(s.url)}" target="_blank" rel="noopener">${esc(s.title)}</a>` : esc(s.title)).join(" · ")}</p>` : ""}
      ${more("Technical details", tech.map(([k, v]) => `<div class="techrow"><span class="secondary">${esc(k)}</span><code>${esc(v)}</code></div>`).join(""))}`)}
  </article>`;
}

function footnote(r) {
  const notes = r.dataNotes ?? [];
  const text = `Checked with GreyHaven's built-in data from ${longDate(r.data.builtOn)}.`;
  if (notes.some(n => n.includes("turned off"))) return `${text} The online check was off.`;
  const incomplete = notes.find(n => n.startsWith("Online lookups were incomplete"));
  return incomplete ? `${text} The online check didn't finish, so a few parts may say "Not sure". ${incomplete.slice("Online lookups were incomplete: ".length).split(" Ratings use")[0]}` : text;
}

const cap = s => s ? s[0].toUpperCase() + s.slice(1) : s;

function longDate(iso) {
  const [y, m, d] = iso.split("-").map(Number);
  return `${d} ${["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"][m - 1]} ${y}`;
}

// ---- Save report (preview: opens the generated report) ---------------------------------------
function saveReport() {
  const entry = state.samples.find(s => s.sample === state.sample);
  const file = entry.reports.find(x => x.kind === (state.path ?? "recommended")) ?? entry.reports[0];
  const t = new Date();
  const name = `greyhaven-report-${t.getFullYear()}-${String(t.getMonth() + 1).padStart(2, "0")}-${String(t.getDate()).padStart(2, "0")}.html`;
  state.saved = { name, href: file ? `../reports/${file.file}` : null };
  renderSaved();
  announce(`Saved: ${name}`);
}

function renderSaved() {
  const status = $("#savestatus"), show = $("#showsaved");
  if (!status) return;
  if (!state.saved) { status.hidden = true; show.hidden = true; return; }
  status.hidden = false;
  status.textContent = `Saved: ${state.saved.name}`;
  show.hidden = !state.saved.href;
}

function focusHeading() { const h = $("h1", screen); if (h) { h.tabIndex = -1; h.focus(); } }

// ---- Illustrations: one simple inline SVG per brand, coloured by --gh-deco-* ------------------
function art(size) {
  const w = size === "hero" ? 260 : 120, h = size === "hero" ? 220 : 100;
  switch (brandBox.value) {
    case "terracotta": return `
      <svg viewBox="0 0 260 220" width="${w}" height="${h}">
        <rect x="10" y="90" width="70" height="120" fill="var(--gh-deco-1)"/>
        <polygon points="10,90 45,55 80,90" fill="var(--gh-deco-2)"/>
        <rect x="90" y="60" width="80" height="150" fill="var(--gh-deco-3)"/>
        <path d="M110 210 v-40 a20 20 0 0 1 40 0 v40z" fill="var(--gh-deco-4)"/>
        <rect x="180" y="110" width="70" height="100" fill="var(--gh-deco-2)"/>
        <rect x="195" y="125" width="14" height="18" fill="var(--gh-deco-4)"/><rect x="221" y="125" width="14" height="18" fill="var(--gh-deco-4)"/>
        <rect x="25" y="110" width="14" height="18" fill="var(--gh-deco-4)"/><rect x="51" y="110" width="14" height="18" fill="var(--gh-deco-4)"/>
        <circle cx="215" cy="45" r="22" fill="var(--gh-deco-2)"/>
      </svg>`;
    case "allotment": return `
      <svg viewBox="0 0 260 220" width="${w}" height="${h}">
        <circle cx="190" cy="60" r="40" fill="var(--gh-deco-1)"/>
        <path d="M0 170 Q65 130 130 170 T260 170 V220 H0z" fill="var(--gh-deco-2)"/>
        <path d="M0 195 Q65 165 130 195 T260 195 V220 H0z" fill="var(--gh-deco-3)"/>
        <path d="M70 160 C70 120 70 110 70 95" stroke="var(--gh-deco-2)" stroke-width="6" fill="none" stroke-linecap="round"/>
        <ellipse cx="55" cy="105" rx="18" ry="9" transform="rotate(-30 55 105)" fill="var(--gh-deco-2)"/>
        <ellipse cx="86" cy="98" rx="18" ry="9" transform="rotate(30 86 98)" fill="var(--gh-deco-2)"/>
        <circle cx="40" cy="45" r="12" fill="var(--gh-deco-4)"/><circle cx="58" cy="42" r="15" fill="var(--gh-deco-4)"/><circle cx="76" cy="46" r="11" fill="var(--gh-deco-4)"/>
      </svg>`;
    case "market": return `
      <svg viewBox="0 0 260 220" width="${w}" height="${h}">
        <defs><pattern id="dots-${size}" width="10" height="10" patternUnits="userSpaceOnUse"><circle cx="3" cy="3" r="2" fill="var(--gh-deco-4)" opacity=".35"/></pattern></defs>
        <rect x="0" y="0" width="260" height="220" fill="url(#dots-${size})"/>
        <rect x="38" y="48" width="150" height="80" rx="10" fill="var(--gh-shadow-colour, var(--gh-deco-4))"/>
        <rect x="30" y="40" width="150" height="80" rx="10" fill="var(--gh-deco-2)" stroke="var(--gh-deco-4)" stroke-width="4"/>
        <circle cx="30" cy="80" r="10" fill="var(--gh-page)" stroke="var(--gh-deco-4)" stroke-width="4"/>
        <circle cx="180" cy="80" r="10" fill="var(--gh-page)" stroke="var(--gh-deco-4)" stroke-width="4"/>
        <circle cx="198" cy="158" r="42" fill="var(--gh-shadow-colour, var(--gh-deco-4))"/>
        <circle cx="192" cy="152" r="42" fill="var(--gh-deco-1)" stroke="var(--gh-deco-4)" stroke-width="4"/>
        <path d="M172 152 l14 14 l26 -28" stroke="var(--gh-deco-4)" stroke-width="8" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
      </svg>`;
    default: return "";
  }
}

// ---- Start-up and review aids ---------------------------------------------------------------
function init() {
  state.samples = window.GH_DATA.samples;
  const picker = $("#sample");
  picker.innerHTML = state.samples.map(s => `<option value="${esc(s.sample)}">${esc(s.sample)}</option>`).join("");
  picker.value = state.samples.some(s => s.sample === query.get("sample")) ? query.get("sample") : "ryzen-rtx2070s-desktop";
  picker.addEventListener("change", () => { state.sample = picker.value; showStart(); });
  state.sample = picker.value;

  // Shortcuts: ?sample=..&screen=results|working&path=advanced&brand=market&theme=dark&open=all
  if (query.get("screen") === "working") { scan(true); return; }
  if (query.get("screen") === "walk" || query.get("screen") === "allsteps") {
    loadReport();
    if (state.report.recommendation.paths.some(p => p.kind === query.get("path"))) { state.path = query.get("path"); state.distro = selectedDistroId(); }
    if (query.get("install")) walk.install = { keep: "keepWindows", replace: "replaceWindows" }[query.get("install")] ?? null;
    if (query.get("screen") === "allsteps") showAllSteps(); else showWalkthrough(Number(query.get("step") || 1) - 1);
    if (query.get("open") === "all") screen.querySelectorAll("details").forEach(d => (d.open = true));
    return;
  }
  if (query.get("screen") === "results") {
    loadReport();
    if (state.report.recommendation.paths.some(p => p.kind === query.get("path"))) { state.path = query.get("path"); state.distro = selectedDistroId(); }
    showResults(true);
    if (query.get("open") === "all") screen.querySelectorAll("details").forEach(d => (d.open = true));
    return;
  }
  showStart();
}

// Word count and reading level of what's visible (closed "Show more" sections don't count).
// Reading level is the Flesch-Kincaid grade: a rough guide. Product names push it up a little.
function syllables(word) {
  const w = word.toLowerCase().replace(/[^a-z]/g, "");
  if (w.length <= 3) return 1;
  const groups = w.replace(/(?:[^laeiouy]es|ed|[^laeiouy]e)$/, "").replace(/^y/, "").match(/[aeiouy]{1,2}/g);
  return Math.max(1, groups ? groups.length : 1);
}
function readability() {
  const text = screen.innerText;
  const words = text.match(/[A-Za-z0-9][A-Za-z0-9'’!_.-]*/g) || [];
  const sentences = Math.max(1, (text.match(/[.!?…](\s|$)|\n/g) || []).length);
  const syl = words.reduce((n, w) => n + syllables(w), 0);
  const grade = words.length ? 0.39 * (words.length / sentences) + 11.8 * (syl / words.length) - 15.59 : 0;
  return { words: words.length, grade: Math.max(0, grade).toFixed(1) };
}
if (query.get("counts")) {
  const box = document.createElement("div");
  box.className = "counter";
  document.body.appendChild(box);
  const update = () => { const r = readability(); box.textContent = `WORDS ${r.words} · GRADE ${r.grade}`; };
  new MutationObserver(update).observe(screen, { childList: true, subtree: true, attributes: true });
  document.addEventListener("toggle", update, true);
}

// walkthrough.js loads after this file and adds the "What's next" screens.
window.addEventListener("DOMContentLoaded", init);
