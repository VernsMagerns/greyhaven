// Design preview, round 3: the guided "What's next" walkthrough (9 steps, one per screen).
// Uses helpers and state from app.js. Download and USB writing are SIMULATED here (milestone 6).
// Preview-only data below (makers, disk space) is marked; it moves into the scan and data/ later.
"use strict";

// PC and motherboard makers: boot menu key and official support (BIOS update) page.
// Preview data, to become data/makers.yaml with sources; pages marked * block automated checks.
const MAKERS = [
  { match: ["dell"], name: "Dell", boot: "F12", support: "https://www.dell.com/support/home" },                 // *
  { match: ["hp", "hewlett"], name: "HP", boot: "Esc, then F9", support: "https://support.hp.com/drivers" },
  { match: ["lenovo"], name: "Lenovo", boot: "F12 (on some IdeaPads, the small Novo button)", support: "https://pcsupport.lenovo.com/" },
  { match: ["asustek", "asus"], name: "ASUS", boot: { laptop: "Esc", desktop: "F8" }, support: "https://www.asus.com/support/download-center/" },
  { match: ["acer"], name: "Acer", boot: "F12", support: "https://www.acer.com/support" },                      // *
  { match: ["micro-star", "msi"], name: "MSI", boot: "F11", support: "https://www.msi.com/support" },            // *
  { match: ["gigabyte"], name: "GIGABYTE", boot: "F12", support: "https://www.gigabyte.com/Support" },          // *
  { match: ["asrock"], name: "ASRock", boot: "F11", support: "https://www.asrock.com/support/index.asp" },
  { match: ["microsoft"], name: "Microsoft Surface", boot: "Hold the volume-down button, then press and let go of the power button", support: "https://support.microsoft.com/surface" }, // *
];

// Free space on the Windows drive. SIMULATED for the preview; the scan will read it.
const DISK = {
  "nvidia-desktop": { free: 412, total: 931 }, "intel-vmd-laptop": { free: 38, total: 237 },
  "intel-laptop": { free: 180, total: 476 }, "broadcom-legacy-laptop": { free: 61, total: 238 },
  "snapdragon-arm-laptop": { free: 300, total: 476 },
};
const LINUX_MIN_GB = 30, WINDOWS_KEEP_GB = 20;

const HELP = {
  ubuntu: [["Ubuntu Discourse (questions and answers)", "https://discourse.ubuntu.com/"], ["Ask Ubuntu", "https://askubuntu.com/"]],
  mint: [["Linux Mint forums", "https://forums.linuxmint.com/"]],
  "pop-os": [["System76 support articles", "https://support.system76.com/"], ["Pop!_OS chat", "https://chat.pop-os.org/"]],
};

const walk = { step: 0, install: null, confirmErase: false, downloaded: false, written: false, done: {} };

function maker() {
  const s = state.report.profile.system;
  for (const field of [s.systemManufacturer, s.boardManufacturer]) {
    const v = (field ?? "").toLowerCase();
    const m = MAKERS.find(x => x.match.some(k => v.includes(k)));
    if (m) return { ...m, fromBoard: field === s.boardManufacturer && field !== s.systemManufacturer };
  }
  return null;
}
const bootKey = m => !m ? null : typeof m.boot === "string" ? m.boot : (state.report.profile.system.formFactor === "laptop" ? m.boot.laptop : m.boot.desktop);
const isWin11 = () => state.report.profile.system.windows.isWindows11;
const recoverySettings = () => isWin11() ? "Settings > System > Recovery" : "Settings > Update & Security > Recovery";
const chosenPath = () => selectedPath() ?? state.report.recommendation.paths[0];
const guideFor = id => state.report.liveUsbGuides.find(g => g.distroId === id);

function tip(text) {
  return `<aside class="tip"><p class="strong">What could go wrong?</p><p>${text}</p></aside>`;
}
function picture(caption) {
  return `<figure class="picture"><div class="picture-frame" aria-hidden="true">${art("small")}</div>
    <figcaption><span class="secondary small">Picture to come:</span> ${esc(caption)}</figcaption></figure>`;
}
function pictureSteps(items) {
  return `<ol class="picture-steps">${items.map(([text, cap]) => `<li>${picture(cap ?? text)}<p>${esc(text)}</p></li>`).join("")}</ol>`;
}
function checklist(id, items) {
  return `<ul class="todo">${items.map((t, i) => {
    const key = `${id}-${i}`;
    return `<li><label><input type="checkbox" data-done="${key}" ${walk.done[key] ? "checked" : ""}> <span>${t}</span></label></li>`;
  }).join("")}</ul>`;
}

// ---- The nine steps ---------------------------------------------------------------------------
function steps() {
  const p = chosenPath();
  const name = distroName(p.distroId);
  const s = state.report.profile.system;
  const m = maker();
  const g = guideFor(p.distroId);
  const gb = INSTALLER_GB[`${p.distroId}/${p.editionId}`] ?? 6;
  return [
    { title: "Back up your files", body: () => `
        <p>First, save a copy of anything you'd hate to lose.</p>
        ${checklist("backup", [
          "Photos, videos and music",
          "Documents and downloads",
          "Passwords saved in your web browser (or sign in to sync them)",
          "Game saves and app settings you care about",
        ])}
        ${more("Easy ways to back up", `
          <p><strong>An external drive.</strong> Plug it in. Copy your Documents, Pictures, Videos, Music and Desktop folders onto it.</p>
          <p><strong>Cloud storage.</strong> OneDrive, Google Drive or Dropbox can keep a copy online.</p>
          <p>Then open a few files from the backup on another device, to make sure they work.</p>`)}
        ${tip("Trying Linux from a USB stick won't touch your files. Installing can, if you pick the wrong option. A backup means a mistake costs you nothing.")}` },

    { title: "Get a USB stick", body: () => `
        <p>You need a USB stick that holds <strong>at least 8 GB</strong>. The ${esc(name)} installer file is about ${gb} GB.</p>
        <div class="warn-box"><p class="strong">Everything on the stick will be erased.</p><p>Use an empty stick, or copy its files somewhere safe first.</p></div>
        ${picture("A USB stick, with \"8 GB or more\" printed on it")}
        ${tip("Some cheap or old sticks fail while being written. If that happens, GreyHaven will tell you. Try a different stick.")}` },

    { title: "Get Windows ready", body: () => {
        const items = [];
        if (s.fastStartup === "enabled") items.push(`
          <strong>Turn off Fast Startup.</strong> With it on, Windows never fully shuts down. Then Linux can't safely use your drive.
          ${more("How", `<ol class="steps"><li>Open Control Panel, then <em>Power Options</em>.</li><li>Click <em>Choose what the power buttons do</em>.</li>
            <li>Click <em>Change settings that are currently unavailable</em>.</li><li>Untick <em>Turn on fast startup</em>, then <em>Save changes</em>.</li></ol>
            <p class="secondary small">A later version of GreyHaven will do this for you, with an undo button.</p>`)}`);
        if (s.bitLocker !== "notEncrypted" && s.bitLocker !== "unknown") items.push(`
          <strong>Save your BitLocker key.</strong> BitLocker locks your files so only you can read them. Windows may ask for its key after you change settings.
          ${more("How", `<p>Go to <a href="https://aka.ms/myrecoverykey" target="_blank" rel="noopener">aka.ms/myrecoverykey</a> and sign in. Write the key on paper.</p>`)}`);
        if (p.requirements?.includes("turnOffSecureBoot")) items.push(`
          <strong>Turn off Secure Boot.</strong> ${esc(SECURE_BOOT)} ${esc(name)} won't start while it's on.
          ${more("How", `<p>You change it in your PC's startup settings (sometimes called the BIOS).</p>
            ${p.secureBootGuide?.url ? `<p><a href="${esc(p.secureBootGuide.url)}" target="_blank" rel="noopener">Follow ${esc(p.secureBootGuide.title.split(" - ")[0])}'s guide</a></p>` : ""}`)}`);
        if (["intelVmd", "intelRstRaid", "raid"].includes(s.storageMode)) items.push(`
          <strong>Change a storage setting.</strong> Your drive is set up in a way Linux can't use. It needs to be switched to "AHCI".
          ${more("Why this needs care", `<p>Switching it the wrong way can stop Windows from starting. GreyHaven will give you a careful, step-by-step guide for this.</p>`)}`);
        items.push(m ? `
          <strong>Check for a BIOS update (optional).</strong> ${esc(m.name)} sometimes fixes startup problems with updates.
          ${more("How", `<p><a href="${esc(m.support)}" target="_blank" rel="noopener">Open ${esc(m.name)}'s support page</a>${m.fromBoard ? " (the maker of your motherboard)" : ""}. Look up your model. If there's a BIOS update, follow their steps.</p>
            <p>Keep the PC plugged in. Never turn it off during an update.</p>`)}` : `
          <strong>Check for a BIOS update (optional).</strong> Your PC maker sometimes fixes startup problems with updates.
          ${more("How", `<p>Search your PC maker's website for your model and "BIOS update". Keep the PC plugged in, and never turn it off during an update.</p>`)}`);
        return `<p>Tick each one when it's done. These are just for this PC.</p>
          ${checklist("windows", items)}
          ${tip(s.bitLocker !== "notEncrypted" && s.bitLocker !== "unknown"
            ? "If Windows asks for a BitLocker recovery key after you change a setting, type the key you saved. Then everything works as before."
            : "Can't find a setting? Menus look different on every PC. Follow your PC maker's guide, and skip the optional items if you're unsure.")}`;
      } },

    { title: "Download the Linux installer file", body: () => `
        <p>GreyHaven downloads the ${esc(name)} installer file for you (about ${gb} GB).</p>
        <p>Then it checks the file is genuine. That means nobody has changed it on the way to you.</p>
        <div class="card sim">
          <p class="secondary small">Simulated in this preview.</p>
          <div class="progress-fill"><span style="width:${walk.downloaded ? 100 : 0}%"></span></div>
          <p id="dlstatus">${walk.downloaded ? "✓ Downloaded and checked. This is the real file." : "Ready to download."}</p>
          <button class="accent" id="download" ${walk.downloaded ? "disabled" : ""}>Download and check</button>
        </div>
        ${tip("Slow internet? It can take a while; you can leave it running. If the check ever fails, GreyHaven throws the file away and tries again. It never uses a bad file.")}` },

    { title: "Put it on the USB stick", body: () => `
        <p>Plug in your USB stick. GreyHaven writes the installer file onto it.</p>
        <div class="card sim">
          <p class="secondary small">Simulated in this preview.</p>
          <label class="option distro selected"><input type="radio" checked> <span><span class="strong">SanDisk USB stick, 16 GB</span><br><span class="secondary">The only USB stick plugged in</span></span></label>
          <div class="warn-box"><p class="strong">This erases everything on the SanDisk stick.</p></div>
          <div class="progress-fill"><span style="width:${walk.written ? 100 : 0}%"></span></div>
          <p id="wrstatus">${walk.written ? "✓ Done. The stick is ready." : "Takes about 10 minutes."}</p>
          <button class="accent" id="write" ${walk.written ? "disabled" : ""}>Erase the stick and write</button>
        </div>
        <p class="secondary small">GreyHaven only lists USB sticks. It never shows your PC's own drives.</p>
        ${tip("Afterwards, Windows may say <em>You need to format the disk</em>. Click <strong>Cancel</strong>. That's normal: Windows just can't read Linux sticks.")}` },

    { title: "Start your PC from the USB stick", body: () => {
        const key = bootKey(m);
        return `
        <p>Leave the USB stick plugged in.</p>
        ${key ? `
          <div class="card keycard"><p class="secondary">On ${esc(m.name)} ${m.fromBoard ? "motherboards" : "PCs"}, the startup menu key is</p><p class="bigkey">${esc(key)}</p></div>
          ${pictureSteps([
            ["Restart your PC.", "The Windows restart button"],
            [`As soon as the screen goes dark, press ${key.includes("Hold") ? "and hold as shown" : key} again and again.`, `A keyboard with the ${key} key marked`],
            ["Pick your USB stick from the list.", "A startup menu listing a USB stick"],
          ])}
          ${more("If that doesn't work", fallbackBoot())}` : `
          <p>We don't know the startup menu key for this PC. This way always works:</p>
          ${fallbackBoot()}`}
        ${tip(`Windows starts as normal? You pressed the key too late. Try again, pressing it the moment the PC turns on. USB stick not listed? Try a USB port on the back of the PC.${p.requirements?.includes("turnOffSecureBoot") ? " A red <em>Secure Boot violation</em> message means Secure Boot is still on (see step 3)." : ""}`)}`;
      } },

    { title: "Try Linux without installing", body: () => `
        <p>Choose <strong>Try ${esc(name)}</strong> when it asks. Nothing is installed, and your files aren't touched.</p>
        <p>Check these things work:</p>
        ${checklist("try", g.checks.map(c => `<strong>${esc(c.title)}.</strong> ${esc(c.test)}`))}
        ${g.expectedInLiveSession.length ? `<p class="secondary">${g.expectedInLiveSession.map(esc).join(" ")}</p>` : ""}
        <p>Done? Shut down from the power menu, remove the stick, and Windows starts as before.</p>
        ${tip("Everything is a bit slow, because it's running from the USB stick. That's normal. Something didn't work? Note it down. GreyHaven's report can help you find out why.")}` },

    { title: "Choose how to install", body: installStep },

    { title: "After installing", body: () => {
        const id = p.distroId;
        const fixes = state.report.components
          .map(c => ({ c, r: c.ratings.find(x => x.distroId === id) }))
          .filter(x => x.r.rating !== "worksOutOfTheBox" && x.r.fix);
        return `
        <p>Welcome to ${esc(name)}! A few last things.</p>
        <h3>1. Install updates</h3>
        <p>Open <strong>${id === "mint" ? "Update Manager" : id === "pop-os" ? "COSMIC Store" : "Software Updater"}</strong> and install everything it offers. Then restart.</p>
        ${fixes.map((x, i) => `
          <h3>${i + 2}. Set up your ${esc(noun(x.c.component.category))}</h3>
          <p>${esc(x.c.displayName)} needs one extra step. Follow the pictures. Don't download drivers from websites: that can break your setup.</p>
          ${pictureSteps(x.r.fix.steps.map(st => [st]))}
          ${x.r.fix.secureBootAdvice ? `<p class="note-box">${esc(x.r.fix.secureBootAdvice)}</p>` : ""}`).join("")}
        <h3>Where to get help</h3>
        <ul>${(HELP[id] ?? []).map(([t, u]) => `<li><a href="${esc(u)}" target="_blank" rel="noopener">${esc(t)}</a></li>`).join("")}</ul>
        <p class="secondary">Tip: when you ask for help, share your GreyHaven report. It lists your parts without any personal details.</p>
        ${tip("Screen stays black after the graphics step? Restart and wait a minute. Still black? Ask in the help forums and share your report. People see this a lot and can help quickly.")}`;
      } },
  ];
}

function fallbackBoot() {
  return pictureSteps([
    [`In Windows, open ${recoverySettings()}.`, `Windows ${isWin11() ? "11" : "10"} Recovery settings`],
    ["Next to Advanced startup, click Restart now.", "The Restart now button"],
    ["On the blue screen, choose Use a device, then your USB stick.", "The blue Use a device menu"],
  ]);
}

// ---- Step 8: install alongside Windows, or replace it -----------------------------------------
function installStep() {
  const p = chosenPath();
  const name = distroName(p.distroId);
  const disk = DISK[state.sample] ?? { free: 100, total: 500 };
  const canShare = disk.free >= LINUX_MIN_GB + WINDOWS_KEEP_GB;
  const give = Math.max(LINUX_MIN_GB, Math.min(100, Math.floor((disk.free - WINDOWS_KEEP_GB - 10) / 10) * 10));
  const pop = p.distroId === "pop-os";
  const choice = walk.install;
  return `
    <p>Happy with how Linux worked? Now choose how to install it.</p>
    <div class="paths" role="radiogroup" aria-label="How to install">
      <label class="option${choice === "alongside" ? " selected" : ""}"><span class="path-label"><input type="radio" name="install" value="alongside" ${choice === "alongside" ? "checked" : ""}>
        <span><span class="path-head strong">Keep Windows too</span>
        <span class="path-line">Each time your PC starts, you pick Windows or ${esc(name)}.</span>
        <span class="path-line ${canShare ? "" : "c-partial strong"}">${canShare
          ? `You have ${disk.free} GB free. Give ${esc(name)} about ${give} GB.`
          : `You only have ${disk.free} GB free. You need at least ${LINUX_MIN_GB + WINDOWS_KEEP_GB} GB.`}</span>
        <span class="secondary small">Free space: simulated in this preview.</span></span></span></label>
      <label class="option${choice === "replace" ? " selected" : ""}"><span class="path-label"><input type="radio" name="install" value="replace" ${choice === "replace" ? "checked" : ""}>
        <span><span class="path-head strong">Replace Windows</span>
        <span class="path-line">${esc(name)} uses the whole drive. Windows and everything on the drive is erased.</span></span></span></label>
    </div>
    <div id="installsteps">${choice ? installSteps(choice, name, pop, canShare, give, disk) : ""}</div>`;
}

function installSteps(choice, name, pop, canShare, give, disk) {
  if (choice === "alongside" && !canShare) return `
    <div class="warn-box"><p class="strong">There isn't enough free space yet.</p>
    <p>Free up at least ${LINUX_MIN_GB + WINDOWS_KEEP_GB - disk.free} GB in Windows (Settings > System > Storage), then scan again. Or choose Replace Windows.</p></div>`;
  if (choice === "alongside" && pop) return `
    <div class="warn-box"><p class="strong">Pop!_OS can't set this up for you.</p>
    <p>You would shrink Windows yourself, then use the installer's Custom option. That's tricky for a first try. Ubuntu and Linux Mint can do it for you. To use one of those, go back and pick Ubuntu.</p></div>
    ${more("I still want Pop!_OS next to Windows", `<p><a href="https://support.system76.com/" target="_blank" rel="noopener">System76's support articles</a> explain how.</p>`)}`;
  if (choice === "replace" && !walk.confirmErase) return `
    <div class="warn-box big-warn">
      <p class="strong">This erases Windows and every file on this drive.</p>
      <p>Your photos, documents, games and programs will be gone. Only continue if your backup is done.</p>
      <label class="check"><input type="checkbox" id="confirmerase"> <span>I understand everything on this drive will be erased, and I've backed up my files.</span></label>
    </div>`;
  const steps = installerSteps(choice, name, give);
  return `
    <h3>Install ${esc(name)}, step by step</h3>
    <p class="secondary small">Draft: we'll check each step against the real installer screens.</p>
    ${pictureSteps(steps)}
    ${tip(choice === "replace"
      ? "Plug in a laptop before you start. If the power goes off while installing, start again from the USB stick."
      : "Plug in a laptop before you start. When the PC restarts, you'll see a menu. Pick Windows or Linux with the arrow keys and press Enter.")}`;
}

function installerSteps(choice, name, give) {
  const alongside = choice === "alongside";
  const startStep = state.report.recommendation.paths.length && chosenPath().distroId === "mint"
    ? ["Double-click Install Linux Mint on the desktop.", "The Install Linux Mint icon on the desktop"]
    : [`Click Install ${name} on the desktop.`, `The Install ${name} button`];
  const disk = alongside
    ? [[`Choose Install ${name} alongside Windows.`, "The installation type screen with 'alongside Windows' chosen"],
       [`Drag the divider to give ${name} about ${give} GB.`, "The divider between Windows and Linux"]]
    : [[`Choose Erase disk and install ${name}.`, "The installation type screen with 'Erase disk' chosen"],
       ["Check it shows the right drive, then continue.", "The drive name and size shown before erasing"]];
  return [
    startStep,
    ["Pick your language and keyboard.", "The language list"],
    ["Connect to Wi-Fi.", "The Wi-Fi list"],
    ["Tick the box to install extra software for graphics and Wi-Fi.", "The third-party software checkbox, ticked"],
    ...disk,
    ["Pick your time zone, then type your name and a password.", "The account screen"],
    ["Click Install, then wait about 15 minutes.", "The install progress bar"],
    ["When it's done, click Restart, then take out the USB stick.", "The Restart now button"],
  ];
}

// ---- Screen ---------------------------------------------------------------------------------
function showWalkthrough(step) {
  rerender = () => showWalkthrough(walk.step);
  walk.step = Math.max(0, Math.min(8, step ?? walk.step));
  const all = steps();
  const s = all[walk.step];
  screen.innerHTML = `
    ${demoBanner()}
    <nav class="walk-top" aria-label="Walkthrough">
      <button class="linkbutton" id="toresults">← Back to results</button>
      <button class="linkbutton" id="allsteps">See all steps on one page</button>
    </nav>
    <p class="stepcount">Step ${walk.step + 1} of ${all.length}</p>
    <div class="steps-bar" role="progressbar" aria-label="Walkthrough progress" aria-valuemin="1" aria-valuemax="${all.length}" aria-valuenow="${walk.step + 1}">
      ${all.map((_, i) => `<span class="${i < walk.step ? "done" : i === walk.step ? "now" : ""}"></span>`).join("")}
    </div>
    <h1>${esc(s.title)}</h1>
    <div class="step-body">${s.body()}</div>
    <div class="walk-nav">
      ${walk.step > 0 ? `<button class="plain" id="prev">← Back</button>` : "<span></span>"}
      ${walk.step < all.length - 1
        ? `<button class="accent big" id="nextstep">Next: ${esc(all[walk.step + 1].title)} →</button>`
        : `<button class="accent big" id="finish">Finish</button>`}
    </div>
    <details class="more"><summary>All steps</summary><div class="more-body"><ol>${all.map((x, i) =>
      `<li>${i === walk.step ? `<strong>${esc(x.title)}</strong>` : `<button class="linkbutton" data-go="${i}">${esc(x.title)}</button>`}</li>`).join("")}</ol></div></details>`;

  $("#toresults").addEventListener("click", () => showResults(true));
  $("#allsteps").addEventListener("click", showAllSteps);
  $("#prev")?.addEventListener("click", () => showWalkthrough(walk.step - 1));
  $("#nextstep")?.addEventListener("click", () => showWalkthrough(walk.step + 1));
  $("#finish")?.addEventListener("click", () => showResults(true));
  screen.querySelectorAll("[data-go]").forEach(b => b.addEventListener("click", () => showWalkthrough(Number(b.dataset.go))));
  screen.querySelectorAll("[data-done]").forEach(c => c.addEventListener("change", () => (walk.done[c.dataset.done] = c.checked)));
  screen.querySelectorAll('input[name="install"]').forEach(i => i.addEventListener("change", () => { walk.install = i.value; walk.confirmErase = false; showWalkthrough(walk.step); $(`input[name="install"][value="${i.value}"]`).focus(); }));
  $("#confirmerase")?.addEventListener("change", e => { if (e.target.checked) { walk.confirmErase = true; showWalkthrough(walk.step); } });
  $("#download")?.addEventListener("click", () => simulate("download", "dlstatus", "Downloading…", "✓ Downloaded and checked. This is the real file.", () => (walk.downloaded = true)));
  $("#write")?.addEventListener("click", () => simulate("write", "wrstatus", "Writing… don't remove the stick.", "✓ Done. The stick is ready.", () => (walk.written = true)));
  const h = $("h1", screen); h.tabIndex = -1; h.focus();
  announce(`Step ${walk.step + 1} of ${all.length}: ${s.title}`);
}

function simulate(buttonId, statusId, during, after, done) {
  const btn = $(`#${buttonId}`), status = $(`#${statusId}`), bar = btn.parentElement.querySelector(".progress-fill span");
  btn.disabled = true;
  status.textContent = during;
  let pct = 0;
  const t = setInterval(() => {
    pct += 10;
    bar.style.width = `${pct}%`;
    if (pct >= 100) { clearInterval(t); done(); status.textContent = after; announce(after); }
  }, 150);
}

// All steps on one page: what the saved report will include, for printing or a phone.
function showAllSteps() {
  rerender = showAllSteps;
  const all = steps();
  screen.innerHTML = `
    ${demoBanner()}
    <nav class="walk-top"><button class="linkbutton" id="back">← Back to the steps</button>
      <button class="linkbutton" onclick="window.print()">Print</button></nav>
    <h1>Your steps to Linux</h1>
    <p>This page is also in your saved report, so you can keep it on your phone while your PC restarts.</p>
    ${all.map((s, i) => `<section class="card printstep"><h2>Step ${i + 1}. ${esc(s.title)}</h2>${s.body()}</section>`).join("")}`;
  screen.querySelectorAll("details").forEach(d => (d.open = true));
  $("#back").addEventListener("click", () => showWalkthrough(walk.step));
  const h = $("h1", screen); h.tabIndex = -1; h.focus();
}
