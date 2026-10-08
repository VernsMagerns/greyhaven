# Hosting the phone guide (Cloudflare Pages)

*7 October 2026.* GreyHaven's "Take your guide" step shows a QR code. It opens the public phone
guide at **https://greyhaven-guide.pages.dev/** (`GreyHavenInfo.PhoneGuideSite`), a free
Cloudflare Pages site deployed by `.github/workflows/phone-guide.yml` on every push to `main`.

**Status (7 October 2026):** live. The maintainer set up the account, token and secrets, ran the
workflow, and scanned the QR code on a phone over mobile data. QR codes use the page address
without ".html" (Cloudflare Pages redirects "x.html" to "x"), so there's no redirect.

## Why Cloudflare Pages

- **Free:** static pages are "free and unlimited" on the Free plan; 500 deploys a month
  ([Pages limits](https://developers.cloudflare.com/pages/platform/limits/),
  [pricing](https://developers.cloudflare.com/pages/functions/pricing/)).
- **Works with a private repository:** GitHub Actions builds the site and uploads the files
  ("Direct Upload"). Cloudflare never reads the repository, so it doesn't matter that it's
  private ([Direct Upload with CI](https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration/)).
- No account or app is needed by the people who scan the code.

## What's on it

Six general pages (`greyhaven phone-guide-site --out site`): Ubuntu and Linux Mint × keep or
replace Windows, and Pop!_OS (Intel/AMD and NVIDIA downloads) × replace Windows. They're built
from `data/` only, so they're identical for everyone; tests and the workflow check no part IDs
or maker names get in. The QR code adds a few non-identifying touches after `#`, which the
browser never sends to the website: the PC maker, its startup keys, where Secure Boot is, laptop
or desktop, how much space to give Linux, and which extra steps apply (e.g. `nvidia-gpu`). The
full list and their allowed formats are in `src/GreyHaven.Reports/GuideFragment.cs`. App lists
never go on the site or in the address.

## What only you can do (about 10 minutes)

**1. Create a free Cloudflare account** (skip if you have one)

1. Go to https://dash.cloudflare.com/sign-up and sign up with your email and a password.
2. Confirm your email from the message Cloudflare sends.
3. Turn on two-factor authentication: click the person icon (top right) > **My Profile** >
   **Authentication**.

**2. Create an API token that can only deploy Pages sites**

1. In the Cloudflare dashboard, click the person icon (top right) > **My Profile** > **API Tokens**.
2. Click **Create Token**, then next to **Create Custom Token** click **Get started**.
3. **Token name:** `GreyHaven phone guide deploy`.
4. **Permissions:** choose **Account**, then **Cloudflare Pages**, then **Edit**. Add nothing else.
5. **Account Resources:** **Include** > your account.
6. Leave the rest. Click **Continue to summary**, then **Create Token**.
7. Click **Copy** to copy the token. Cloudflare shows it only once. Don't paste it anywhere
   except step 4 below (not in chat, email or a file).

**3. Copy your account ID**

In the Cloudflare dashboard, press **Ctrl+K**, type `Copy account ID` and pick the result.
(Or open **Workers & Pages**; it's under **Account details**.) The account ID isn't a secret,
but it goes in a secret next to the token.

**4. Add both as GitHub secrets**

1. Open https://github.com/VernsMagerns/greyhaven/settings/secrets/actions.
2. Click **New repository secret**. **Name:** `CLOUDFLARE_API_TOKEN`. **Secret:** paste the
   token. Click **Add secret**.
3. Click **New repository secret** again. **Name:** `CLOUDFLARE_ACCOUNT_ID`. **Secret:** paste
   the account ID. Click **Add secret**.

(Or in a terminal: `gh secret set CLOUDFLARE_API_TOKEN -R VernsMagerns/greyhaven`, then paste at
the prompt; the same for `CLOUDFLARE_ACCOUNT_ID`.)

**5. Run the deploy**

1. Open https://github.com/VernsMagerns/greyhaven/actions/workflows/phone-guide.yml.
2. Click **Run workflow** > **Run workflow**. It takes about 2 minutes.
3. The first run creates the Pages project. The last step prints the guide's address and checks
   it loads. If it warns that the address is different (someone else already has
   `greyhaven-guide`), tell Claude the address shown in Cloudflare (**Workers & Pages** >
   **greyhaven-guide**) so `GreyHavenInfo.PhoneGuideSite` can be updated.

**6. Test from your phone on mobile data**

Turn off Wi-Fi on your phone, then open
https://greyhaven-guide.pages.dev/ubuntu-desktop-keep#v=1&m=MSI&k=F11&f=desktop&g=100&x=nvidia-gpu
(or scan the QR code in the design preview's "Take your guide" step). Check that "PC didn't start
from the USB stick?" says **F11**, step 2 says **about 100 GB**, and step 3 shows the NVIDIA
driver steps.

## Later

- After step 5, every push to `main` redeploys the guide automatically.
- To stop the site: Cloudflare dashboard > **Workers & Pages** > **greyhaven-guide** >
  **Settings** > **Delete project**, and delete the two GitHub secrets.
- A custom domain (e.g. guide.greyhaven.app) can be added later in the project's **Custom
  domains** tab; then update `GreyHavenInfo.PhoneGuideSite`.
