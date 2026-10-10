# Going public: step-by-step checklist

Follow these in order. **You** marks steps only you can do (your accounts, your decisions,
or things that can't be undone). **Claude** marks steps I can do once you say go.

Nothing here has been done yet. The repository stays private until step 7.

## Before

- [ ] **1. You: pick a contact address for conduct reports.** It goes into
      `CODE_OF_CONDUCT.md` and becomes public. A separate address (for example a new free
      email account just for GreyHaven) is better than your personal one. Tell me what it is.
      (Security reports don't need one: they use GitHub's private reporting.)
- [x] **2. You, then Claude: switch your commit email to GitHub's no-reply address.**
      Every commit so far shows your personal address, and anyone can read it once the
      repository is public. *Done 10 Oct: every commit now uses
      `280451244+VernsMagerns@users.noreply.github.com`, and test.wav was dropped from history.
      A backup of the old history is in `~/Documents/claude/greyhaven-backup-2026-10-10/`.*
      1. *You:* on GitHub, open **Settings → Emails**. Tick **Keep my email addresses private**
         and **Block command line pushes that expose my email**. Copy the address shown, like
         `12345678+VernsMagerns@users.noreply.github.com`, and send it to me.
      2. *Claude:* rewrite the history so old commits use that address (`git filter-repo` with a
         mailmap), update the repository's git settings and `CLAUDE.md`, and check the result.
         This rewrites every commit, so it needs a force-push, which I'll only do once you
         confirm.
- [x] **3. You: turn on two-factor authentication (MFA) on GitHub.** (Done 10 Oct.) Settings → Password and
      authentication → Enable two-factor authentication. An authenticator app is best. Save the
      recovery codes somewhere safe.
- [ ] **4. Claude: build the installer and release workflow** (milestone 8): `GreyHaven-Setup.exe`,
      the MSI, checksums, attestations, and the GitHub Pages download page. You test install and
      uninstall on Windows 10 and 11 (Session 2).
- [ ] **5. Claude: switch on public links.** Set `GreyHavenInfo.IsRepositoryPublic = true`, put
      the contact address in place, and run `tools/release-check.sh` (it refuses to pass while
      any placeholder is left).
- [x] **6. You: repository settings.** Settings → Branches → a rule for `main` requiring passing
      checks (set up 10 Oct). GitHub Free only enforces it once the repository is public, and
      *private vulnerability reporting* only exists for public repositories, so both are
      checked in step 8.

## Going public

- [ ] **7. You: make the repository public.** Settings → General → Danger Zone → **Change
      repository visibility** → Public. Only you can do this, and the history can't be made
      private again in the eyes of anyone who copied it.
- [ ] **8. You: settings that only exist once the repository is public.**
      - Settings → Pages → Source: **GitHub Actions**. Claude's workflow then publishes the
        download page.
      - Settings → General → **Social preview** → upload `assets/brand/social-preview.png`.
      - Settings → Code security → turn on **Private vulnerability reporting**.
      - Settings → Rules / Branches: check the `main` rule now shows as active.
- [ ] **9. Claude: publish the first pre-release, unsigned** (for example `v0.1.0-preview.1`),
      so SignPath can see a released build. You approve the release notes first.
- [ ] **10. Claude: adjust CI for a public repository.** Public repositories get free Actions
      minutes, so the Windows jobs can run on every pull request again.

## Signing

- [ ] **11. You: create a SignPath account and turn on MFA there.**
- [ ] **12. You: apply to SignPath Foundation** at https://signpath.org/apply, using the answers
      in [signpath-application.md](signpath-application.md). Raise the open questions there.
- [ ] **13. Claude: once approved, add signing to the release workflow** with SignPath's GitHub
      Action. You'll be the approver for each signing request.
- [ ] **14. Claude: activate the code signing policy page** and link it, and the privacy policy,
      from the download page and the README.

If SignPath says no, the fallback is Azure Artifact Signing (about US$9.99 a month, identity
check with your ID); see docs/PLAN.md section 6.
