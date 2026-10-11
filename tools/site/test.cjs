// Drives the landing page's real HTML and app.js in jsdom, like a visitor would.
//   npm install --prefix /tmp/sitetest jsdom@26
//   NODE_PATH=/tmp/sitetest/node_modules node tools/site/test.cjs artifacts/site
// (run on the built site: tools/site/build.sh). Run by .github/workflows/site.yml before deploying.
const { JSDOM } = require("jsdom");
const fs = require("fs"), path = require("path");
const SITE = process.argv[2];
const html = fs.readFileSync(path.join(SITE, "index.html"), "utf8").replace('<script src="app.js" defer></script>', "");
const sleep = ms => new Promise(r => setTimeout(r, ms));
let failed = 0;
const ok = (cond, msg) => { console.log((cond ? "PASS " : "FAIL ") + msg); if (!cond) failed++; };

async function run({ platform, reduced, shareApi }) {
  const dom = new JSDOM(html, { url: "http://127.0.0.1:8766/", runScripts: "outside-only", pretendToBeVisual: true });
  const w = dom.window;
  w.matchMedia = q => ({ matches: q.includes("reduce") ? reduced : false, media: q, addEventListener() {} });
  w.IntersectionObserver = class { constructor(cb) { this.cb = cb; } observe(el) { setTimeout(() => this.cb([{ isIntersecting: true, target: el }]), 10); } unobserve() {} disconnect() {} };
  w.CSS = { supports: () => true };
  Object.defineProperty(w.navigator, "platform", { value: platform, configurable: true });
  Object.defineProperty(w.navigator, "userAgent", { value: platform === "Win32" ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" : "Mozilla/5.0 (X11; Linux x86_64)", configurable: true });
  const shared = [];
  if (shareApi) w.navigator.share = async d => shared.push(d);
  else Object.defineProperty(w.navigator, "clipboard", { value: { writeText: async t => shared.push({ copied: t }) }, configurable: true });
  w.fetch = async (u, o) => {
    if (String(u).startsWith("https://api.github.com")) return { ok: true, json: async () => ({ tag_name: "v1.0.0", assets: [{ name: "GreyHaven-Setup.exe", size: 58959097 }] }) };
    const file = path.join(SITE, new URL(u, w.location.href).pathname);
    const body = fs.readFileSync(file, "utf8");
    return { ok: true, json: async () => JSON.parse(body) };
  };
  w.eval(fs.readFileSync(path.join(SITE, "app.js"), "utf8"));
  const $ = s => w.document.querySelector(s);
  await sleep(50);
  return { w, $, shared };
}

(async () => {
  // A Windows visitor, motion allowed, no Web Share API (desktop browser).
  let { w, $, shared } = await run({ platform: "Win32", reduced: false, shareApi: false });
  ok($("#os-note").hidden === true, "Windows visitors don't see the 'runs on Windows' note");
  ok($("#release-meta").textContent.startsWith("Version 1.0.0 · 59 MB"), "release line shows version and size: " + $("#release-meta").textContent);
  ok(/^\d[\d,]* days$/.test($("#w10-days").textContent) && $("#w10-label").textContent.includes("since"), "Windows 10 counter: " + $("#w10-days").textContent + " " + $("#w10-label").textContent);
  ok($("#w10-text").textContent.includes("12 October 2027"), "ESU end date mentioned");
  // calculator
  $('#calc [data-step="1"]').click(); $('#calc [data-step="1"]').click();
  ok($("#calc-pcs").value === "3" && $("#calc-out").textContent.includes("$1,500"), "calculator: 3 PCs x $500 = " + $("#calc-out").textContent);
  $("#calc-price").value = "350"; $("#calc-price").dispatchEvent(new w.Event("input"));
  ok($("#calc-out").textContent.includes("$1,050"), "calculator updates with the price");
  for (let i = 0; i < 10; i++) $('#calc [data-step="-1"]').click();
  ok($("#calc-pcs").value === "1", "calculator never goes below 1 PC");
  // slider
  $("#compare-range").value = "30"; $("#compare-range").dispatchEvent(new w.Event("input"));
  ok($(".compare-stage").style.getPropertyValue("--pos") === "30%", "slider moves the divider");
  // tabs
  $("#tab-adv").click();
  ok($("#panel-adv").hidden === false && $("#panel-easy").hidden === true && $("#tab-adv").getAttribute("aria-selected") === "true", "Advanced tab shows Pop!_OS and the Secure Boot notice");
  ok($("#panel-adv").textContent.includes("turn off Secure Boot") && $("#panel-adv a").href.includes("secure-boot.md"), "Advanced has the Secure Boot how-to link");
  $("#tab-adv").dispatchEvent(new w.KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true }));
  ok($("#panel-easy").hidden === false, "arrow keys move between tabs");
  // scan demo (motion on): starts hidden, finishes with the verdict
  await sleep(200);
  ok($("#scan-demo").classList.contains("scanning"), "scan demo runs when scrolled into view");
  await sleep(5200);
  ok(!$("#scan-demo").classList.contains("scanning") || $(".verdict").classList.contains("shown"), "scan demo ends on the verdict");
  ok($(".verdict").classList.contains("shown") && $$count(w, ".parts li.shown") === 6, "all six parts shown");
  // games
  const chip = [...w.document.querySelectorAll(".chips button")].find(b => b.dataset.game === "ELDEN RING");
  chip.click(); await sleep(400);
  const first = $("#game-results .game");
  ok(first && first.querySelector("h5").textContent === "ELDEN RING", "Elden Ring chip finds Elden Ring");
  ok(first.querySelector(".badge").className.includes("layer") && first.querySelector("img").src.includes("/1245620/header.jpg"), "badge and Steam cover for Elden Ring");
  ok(first.querySelector("a").href === "https://www.protondb.com/app/1245620", "links to its ProtonDB page");
  [...w.document.querySelectorAll(".chips button")].find(b => b.dataset.game === "VALORANT").click(); await sleep(100);
  ok($("#game-results .game .badge").className.includes("no"), "Valorant shows as won't work (anti-cheat)");
  ok(!$("#game-results .game img"), "Valorant (not on Steam) shows no cover");
  $("#game-q").value = "zzqx nothing"; $("#game-q").dispatchEvent(new w.Event("input")); await sleep(300);
  ok($("#game-results").children.length === 0 && $("#game-msg").textContent.includes("No game called"), "no-match message");
  $("#game-q").value = "baldur"; $("#game-q").dispatchEvent(new w.Event("input")); await sleep(300);
  ok([...w.document.querySelectorAll("#game-results h5")].some(h => /Baldur's Gate 3/i.test(h.textContent)), "typing 'baldur' finds Baldur's Gate 3");
  // share falls back to copying the link
  $("[data-share]").click(); await sleep(50);
  ok(shared[0] && shared[0].copied === "https://greyhaven.pages.dev/" && $("#toast").textContent === "Link copied", "share copies the link and shows a toast");

  // A Mac visitor on a phone-like browser with Web Share, reduced motion.
  ({ w, $, shared } = await run({ platform: "MacIntel", reduced: true, shareApi: true }));
  ok($("#os-note").hidden === false, "non-Windows visitors see the note");
  await sleep(100);
  ok(!$("#scan-demo").classList.contains("scanning") && $(".verdict").classList.contains("shown"), "reduced motion: scan demo shows the result straight away");
  $("#support [data-share]").click(); await sleep(20);
  ok(shared[0] && shared[0].url === "https://greyhaven.pages.dev/", "share uses the native share sheet when there is one");

  console.log(failed ? `\n${failed} FAILED` : "\nall passed");
  process.exit(failed ? 1 : 0);
})();
function $$count(w, s) { return w.document.querySelectorAll(s).length; }
