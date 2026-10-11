/* GreyHaven landing page. Small, dependency-free enhancements; the page works without them. */
"use strict";

const $ = (s, el = document) => el.querySelector(s);
const $$ = (s, el = document) => [...el.querySelectorAll(s)];
const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
const REPO = "VernsMagerns/greyhaven";
const SITE = "https://greyhaven.pages.dev/";

function toast(text) {
  const t = $("#toast");
  t.textContent = text;
  t.hidden = false;
  clearTimeout(toast.timer);
  toast.timer = setTimeout(() => (t.hidden = true), 2600);
}

/* ---------- Latest release: version and size under the Download button ---------- */
async function releaseInfo() {
  try {
    const r = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, { headers: { Accept: "application/vnd.github+json" } });
    if (!r.ok) return;
    const rel = await r.json();
    const setup = rel.assets.find(a => a.name === "GreyHaven-Setup.exe");
    if (!setup) return;
    const mb = Math.round(setup.size / 1e6);
    $("#release-meta").textContent = `Version ${rel.tag_name.replace(/^v/, "")} · ${mb} MB · Free and open source · Windows 10 and 11, 64-bit`;
  } catch { /* keep the static line */ }
}

/* ---------- Not on Windows? Say so, gently ---------- */
function osNote() {
  const platform = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || "";
  const ua = navigator.userAgent || "";
  const windows = /win/i.test(platform) || /Windows NT/.test(ua);
  if (!windows) $("#os-note").hidden = false;
}

/* ---------- Share (native share sheet, or copy the link) ---------- */
async function share() {
  const data = { title: "GreyHaven", text: "Will Linux work on your PC? GreyHaven checks, then walks you through the switch.", url: SITE };
  if (navigator.share) {
    try { await navigator.share(data); return; } catch (e) { if (e.name === "AbortError") return; }
  }
  try { await navigator.clipboard.writeText(SITE); toast("Link copied"); }
  catch { prompt("Copy this link:", SITE); }
}

/* ---------- A warm glow that follows the pointer over cards ---------- */
function cardGlow() {
  if (!matchMedia("(hover: hover) and (pointer: fine)").matches || reducedMotion) return;
  document.addEventListener("pointermove", e => {
    const card = e.target.closest && e.target.closest(".card");
    if (!card) return;
    const r = card.getBoundingClientRect();
    card.style.setProperty("--mx", `${e.clientX - r.left}px`);
    card.style.setProperty("--my", `${e.clientY - r.top}px`);
  }, { passive: true });
}

/* ---------- Scroll effects where CSS scroll timelines aren't supported ---------- */
function scrollFallbacks() {
  if (reducedMotion || CSS.supports("animation-timeline: scroll()")) return;
  const io = new IntersectionObserver(entries => entries.forEach(en => {
    if (en.isIntersecting) { en.target.classList.add("in"); io.unobserve(en.target); }
  }), { rootMargin: "0px 0px -10% 0px" });
  $$(".reveal").forEach(el => { el.classList.add("io"); io.observe(el); });
  const boat = $(".boat");
  if (!boat) return;
  boat.classList.add("js-sail");
  let ticking = false;
  addEventListener("scroll", () => {
    if (ticking) return;
    ticking = true;
    requestAnimationFrame(() => {
      const t = Math.min(1, scrollY / (innerHeight * .7));
      boat.style.transform = `translate(${250 + 68 * t}px, ${318 - 14 * t}px) scale(${1 - .45 * t})`;
      ticking = false;
    });
  }, { passive: true });
}

/* ---------- Savings calculator ---------- */
function calculator() {
  const pcs = $("#calc-pcs"), price = $("#calc-price"), out = $("#calc-out");
  const money = n => "$" + Math.round(n).toLocaleString("en-US");
  const update = () => {
    const n = Math.min(50, Math.max(1, parseInt(pcs.value, 10) || 1));
    const p = Math.max(0, parseFloat(price.value) || 0);
    out.innerHTML = `Keep ${n === 1 ? "your PC" : `your ${n} PCs`} and switch to Linux, and you keep <strong>${money(n * p)}</strong> you'd have spent on ${n === 1 ? "a new one" : "new ones"}.`;
  };
  $$("[data-step]", $("#calc")).forEach(b => b.addEventListener("click", () => {
    pcs.value = Math.min(50, Math.max(1, (parseInt(pcs.value, 10) || 1) + Number(b.dataset.step)));
    update();
  }));
  pcs.addEventListener("input", update);
  price.addEventListener("input", update);
  update();
}

/* ---------- Windows 10: days since support ended, days until security updates stop ---------- */
function windows10() {
  const ended = Date.UTC(2025, 9, 14), esuEnds = Date.UTC(2027, 9, 12); // Microsoft's dates
  const now = new Date(), today = Date.UTC(now.getFullYear(), now.getMonth(), now.getDate());
  const day = 86400000;
  const since = Math.round((today - ended) / day);
  const until = Math.round((esuEnds - today) / day);
  const n = x => x.toLocaleString("en-US");
  if (since < 0) {
    $("#w10-days").textContent = `${n(-since)} days`;
    $("#w10-label").textContent = "until Windows 10 support ends";
  } else {
    $("#w10-days").textContent = `${n(since)} days`;
    $("#w10-label").textContent = "since Windows 10 support ended";
  }
  $("#w10-text").textContent = until > 0
    ? `Microsoft stopped updating Windows 10 on 14 October 2025. Security updates now only come through Extended Security Updates, and those stop on 12 October 2027, ${n(until)} days from now.`
    : "Microsoft stopped updating Windows 10 on 14 October 2025, and Extended Security Updates ended on 12 October 2027. Windows 10 PCs no longer get security fixes.";
}

/* ---------- Before / after slider ---------- */
function compare() {
  const stage = $(".compare-stage"), range = $("#compare-range");
  const set = () => stage.style.setProperty("--pos", `${range.value}%`);
  range.addEventListener("input", set);
  set();
}

/* ---------- Scan demo: replays the app's real result for a sample laptop ---------- */
function scanDemo() {
  const demo = $("#scan-demo");
  const rows = $$(".parts li", demo), verdict = $(".verdict", demo), status = $(".scan-status", demo);
  let timers = [];
  const clear = () => { timers.forEach(clearTimeout); timers = []; };
  const finish = () => {
    clear();
    demo.classList.remove("scanning");
    status.textContent = "Done: every part checked.";
    rows.forEach(r => r.classList.add("shown"));
    verdict.classList.add("shown");
  };
  const run = () => {
    if (reducedMotion) return finish();
    clear();
    demo.classList.add("scanning");
    rows.forEach(r => r.classList.remove("shown"));
    verdict.classList.remove("shown");
    status.textContent = "Looking at the parts inside your PC…";
    demo.style.setProperty("--p", "0%");
    const step = 650;
    rows.forEach((r, i) => timers.push(setTimeout(() => {
      r.classList.add("shown");
      demo.style.setProperty("--p", `${Math.round(((i + 1) / rows.length) * 100)}%`);
      if (i === 2) status.textContent = "Checking how each part works with Linux…";
    }, 400 + i * step)));
    timers.push(setTimeout(() => { verdict.classList.add("shown"); status.textContent = "Done: every part checked."; }, 400 + rows.length * step + 300));
  };
  $("#demo-replay").addEventListener("click", run);
  const io = new IntersectionObserver(es => es.forEach(e => { if (e.isIntersecting) { run(); io.disconnect(); } }), { threshold: .45 });
  io.observe(demo);
}

/* ---------- Easy / Advanced toggle (tabs, with arrow keys) ---------- */
function pathTabs() {
  const tabs = $$('.seg [role="tab"]');
  const select = tab => {
    tabs.forEach(t => {
      const on = t === tab;
      t.setAttribute("aria-selected", on);
      t.tabIndex = on ? 0 : -1;
      $("#" + t.getAttribute("aria-controls")).hidden = !on;
    });
  };
  tabs.forEach((t, i) => {
    t.addEventListener("click", () => select(t));
    t.addEventListener("keydown", e => {
      if (e.key !== "ArrowRight" && e.key !== "ArrowLeft") return;
      const next = tabs[(i + (e.key === "ArrowRight" ? 1 : tabs.length - 1)) % tabs.length];
      select(next); next.focus();
    });
  });
}

/* ---------- Game checker: the same ProtonDB data and anti-cheat list the app ships ---------- */
// Rows: [name, verdict text, verdict, summary, link, reports, anti-cheat], from "greyhaven game-index".
const BADGE = { Works: "works", WorksWithLayer: "layer", UseWeb: "layer", WontWork: "no", NotNeeded: "works", NotSure: "unknown" };
const norm = s => s.replace(/[®™©]/g, "").replace(/\([^)]*\)|\[[^\]]*\]/g, " ").replace(/\s+/g, " ").trim().toLowerCase();
let games = null, keys = null, loading = null;

function loadGames() {
  loading ??= fetch("data/games.json").then(r => r.json()).then(rows => { games = rows; keys = rows.map(g => norm(g[0])); });
  return loading;
}

function searchGames(q) {
  q = norm(q);
  if (q.length < 2 || !games) return [];
  const words = q.split(" "), hits = [];
  games.forEach((g, i) => {
    const k = keys[i];
    const rank = k === q ? 0 : k.startsWith(q) ? 1 : words.every(w => k.includes(w)) ? 2 : -1;
    if (rank >= 0) hits.push([rank, g]);
  });
  hits.sort((a, b) => a[0] - b[0] || (b[1][6] - a[1][6]) || b[1][5] - a[1][5] || a[1][0].localeCompare(b[1][0]));
  return hits.slice(0, 4).map(h => h[1]);
}

function gameItem([name, text, verdict, summary, link]) {
  const li = document.createElement("li");
  li.className = "game";
  const appId = link && /protondb\.com\/app\/(\d+)/.exec(link)?.[1];
  if (appId) {
    const img = document.createElement("img");
    img.src = `https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/${appId}/header.jpg`;
    img.alt = "";
    img.width = 460; img.height = 215;
    img.onerror = () => { const d = document.createElement("div"); d.className = "noimg"; img.replaceWith(d); };
    li.append(img);
  } else {
    const d = document.createElement("div"); d.className = "noimg"; li.append(d);
  }
  const body = document.createElement("div");
  const h = document.createElement("h5"); h.textContent = name;
  const badge = document.createElement("span"); badge.className = `badge ${BADGE[verdict] || "unknown"}`; badge.textContent = text;
  const p = document.createElement("p"); p.textContent = summary;
  body.append(h, badge, p);
  if (link) {
    const a = document.createElement("a");
    a.href = link;
    a.textContent = link.includes("protondb.com") ? "See full reports on ProtonDB" : "See it on Are We Anti-Cheat Yet";
    a.className = "small";
    body.append(a);
  }
  li.append(body);
  return li;
}

function gameChecker() {
  const input = $("#game-q"), list = $("#game-results"), msg = $("#game-msg");
  const show = async () => {
    const q = input.value.trim();
    if (q.length < 2) { list.replaceChildren(); msg.textContent = ""; return; }
    if (!games) { msg.textContent = "Loading the game list…"; await loadGames(); }
    const hits = searchGames(q);
    list.replaceChildren(...hits.map(gameItem));
    msg.textContent = hits.length ? (hits.length === 1 ? "1 game found." : `${hits.length} closest matches.`)
      : `No game called "${q}" in the list. Check the spelling, or search ProtonDB.`;
    $$(".chips button").forEach(b => b.setAttribute("aria-pressed", norm(b.dataset.game) === norm(q)));
  };
  let t;
  input.addEventListener("focus", () => loadGames(), { once: true });
  input.addEventListener("input", () => { clearTimeout(t); t = setTimeout(show, 200); });
  $$(".chips button").forEach(b => b.addEventListener("click", () => { input.value = b.dataset.game; show(); }));
}

/* ---------- Start ---------- */
function start() {
  $$("[data-share]").forEach(b => b.addEventListener("click", share));
  osNote();
  releaseInfo();
  cardGlow();
  scrollFallbacks();
  calculator();
  windows10();
  compare();
  scanDemo();
  pathTabs();
  gameChecker();
}
start();
