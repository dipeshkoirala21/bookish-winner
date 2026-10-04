# CI secrets and variables

Everything the GitHub Actions workflows in `.github/workflows/` need, how to create it, and what runs without it. Status: M0, written 2026-10-04 against Unity 6.3 LTS (6000.3.25f1), GameCI unity-builder/unity-test-runner v4, fastlane 2.240.

Add secrets under **Settings → Secrets and variables → Actions → Secrets**, and the one variable under **… → Variables**. Secrets are not available to workflows triggered from forks, so pull requests from forks always take the "no secrets" path below.

## What runs without any secrets

| Workflow | Without secrets |
|---|---|
| `pipeline.yml` | Everything: pytest on fixtures, including the end-to-end and determinism tests. |
| `core.yml` | Everything: golden-file regeneration check and `dotnet test core-tests`. |
| `localization.yml` | Everything: string-table check, font/Devanagari shaping check, TextSpike sync, UXML/USS validation. |
| `nightly-data.yml` | Everything: fetch, coverage report, landmark check, region build, artifacts (14 days). Staging CDN upload is not wired yet. |
| `unity.yml` | `check-secrets` and `compile-check` (all game C# compiled outside Unity, plus the audit of every Unity API we use against Unity 6000.3's reference source). EditMode tests, builds, TestFlight and Play uploads **skip with a notice**. |

So no secret is needed to keep the code honest; the secrets below are for running Unity itself and for shipping to devices.

## Summary

| Name | Kind | Needed for | Required? |
|---|---|---|---|
| `UNITY_EMAIL` | secret | Unity tests and builds | yes, for any Unity job |
| `UNITY_PASSWORD` | secret | Unity tests and builds | yes, for any Unity job |
| `UNITY_LICENSE` | secret | Unity Personal activation | Personal only |
| `UNITY_SERIAL` | secret | Unity Pro/Plus/Enterprise activation | paid tiers only (instead of `UNITY_LICENSE`) |
| `ANDROID_KEYSTORE_BASE64` | secret | signing the AAB with your upload key | for Play uploads |
| `ANDROID_KEYSTORE_PASS` | secret | " | " |
| `ANDROID_KEYALIAS_NAME` | secret | " | " |
| `ANDROID_KEYALIAS_PASS` | secret | " | " |
| `APPLE_TEAM_ID` | secret | iOS signing, TestFlight | for TestFlight |
| `APPSTORE_KEY_ID` | secret | App Store Connect API (TestFlight, match, build numbers) | for TestFlight |
| `APPSTORE_ISSUER_ID` | secret | " | for TestFlight |
| `APPSTORE_P8` | secret | " (contents of the `.p8` key file) | for TestFlight |
| `MATCH_REPOSITORY` | secret | fastlane match certificates repo (`owner/repo`) | recommended (else cloud signing) |
| `MATCH_PASSWORD` | secret | decrypts the match repo | with match |
| `MATCH_DEPLOY_KEY` | secret | SSH private key with read access to the match repo | with match |
| `PLAY_SERVICE_ACCOUNT_JSON` | secret | upload to the Play internal track | optional |
| `GHUMANTE_BUNDLE_ID` | variable | application id on both stores | optional (default `com.ghumante.game`, a placeholder) |

## 1. Unity licence (`UNITY_*`)

GameCI activates Unity inside its Docker image at the start of each job and (for serial-based licences) returns the seat at the end. Which secrets you add depends on the tier ([GameCI activation docs](https://game.ci/docs/github/activation)). Whether the project needs a paid tier is decision P9 / question 2 in ARCHITECTURE.md: Unity Personal is for "individuals and small organizations with less than $200K USD of revenue and funds raised in the last 12 months" ([unity.com](https://unity.com/products/unity-personal)).

**Unity Personal**: there is no serial, and Unity no longer supports the old manual `.alf` → `.ulf` activation for Personal licences, so the licence file has to come from a machine where Unity Hub has activated it:

1. Install Unity Hub on any machine, sign in with the Unity account CI should use (a dedicated account is cleaner than a person's), and activate a **Personal** licence (Hub → Preferences → Licenses → Add).
2. Find the licence file it wrote: Windows `C:\ProgramData\Unity\Unity_lic.ulf`, macOS `/Library/Application Support/Unity/Unity_lic.ulf`, Linux `~/.local/share/unity3d/Unity/Unity_lic.ulf`. GameCI notes that a licence shown in Hub does not guarantee the file exists; if it is missing, return and re-activate.
3. Add secrets: `UNITY_LICENSE` = the full contents of `Unity_lic.ulf` (XML, multi-line is fine), `UNITY_EMAIL`, `UNITY_PASSWORD`.
4. If activation starts failing later (Unity refreshes licence files periodically), repeat steps 1-3 and replace `UNITY_LICENSE`.

**Unity Pro / Plus / Enterprise**: add `UNITY_SERIAL` (the `XX-XXXX-XXXX-XXXX-XXXX-XXXX` key from the Unity dashboard's subscriptions page), `UNITY_EMAIL` and `UNITY_PASSWORD`, and do **not** add `UNITY_LICENSE`. Each running job holds a seat until GameCI returns it at the end; a cancelled job can leave a seat activated, which you release from the Unity dashboard. Organisations with a floating licence server can instead pass `unityLicensingServer` to the GameCI actions.

GameCI logs in non-interactively with the email and password, so use a dedicated CI account whose login does not require an interactive second factor, and keep it out of day-to-day use.

## 2. Android upload keystore (`ANDROID_*`)

Google Play uses **Play App Signing**: Google holds the app signing key, and you sign uploads with an **upload key**. If the upload key is lost you can "request an upload key reset in the Play console", which "will not affect the app signing key" ([Android docs](https://developer.android.com/studio/publish/app-signing)). The key should be "valid for at least 25 years".

Create it once (JDK `keytool`):

```bash
keytool -genkeypair -v -keystore ghumante-upload.keystore -storetype PKCS12 \
  -alias upload -keyalg RSA -keysize 4096 -validity 10000
base64 -w0 ghumante-upload.keystore > ghumante-upload.keystore.b64   # macOS: base64 -i ghumante-upload.keystore
```

Secrets: `ANDROID_KEYSTORE_BASE64` (contents of the `.b64` file), `ANDROID_KEYSTORE_PASS` (store password), `ANDROID_KEYALIAS_NAME` (`upload`), `ANDROID_KEYALIAS_PASS` (key password; with PKCS12 it is the same as the store password). Keep the original keystore in a password manager too: CI is not a backup.

How it flows: `unity.yml` passes the secrets to GameCI only when all four exist; GameCI decodes the keystore into the project as `upload.keystore` and calls `Ghumante.EditorTools.BuildScript.BuildFromCommandLine` with `-androidKeystoreName/-Pass/-androidKeyaliasName/-Pass`. Locally, `BuildScript` reads the same four values from the environment instead. Passwords are cleared from the in-memory player settings after a batch build.

**Without these secrets** the AAB is signed with Unity's debug key and a notice is printed. It installs on test devices through `bundletool build-apks --connected-device` / `install-apks`, but Play Console rejects it.

## 3. App Store Connect API key (`APPSTORE_*`, `APPLE_TEAM_ID`)

Needed to upload to TestFlight, to read the latest TestFlight build number, and by match. Apple has required uploads to be "built with Xcode 26 or later using an SDK for iOS 26" since 2026-04-28 ([Apple](https://developer.apple.com/news/upcoming-requirements/)); the workflow selects Xcode 26 on `macos-latest` (macOS 26 runners).

1. In App Store Connect: **Users and Access → Integrations → App Store Connect API → Team Keys → +**. Name it e.g. "Ghumante CI".
   * Role **App Manager** is enough for TestFlight uploads when signing uses match.
   * Role **Admin** is required for cloud signing (no match): Xcode then creates the distribution certificate itself.
2. Download the `.p8` file (it can be downloaded **once**). Note the **Key ID** (on the key's row) and the **Issuer ID** (top of the page).
3. Secrets: `APPSTORE_KEY_ID`, `APPSTORE_ISSUER_ID`, `APPSTORE_P8` = the full text of the `.p8` (including the `-----BEGIN PRIVATE KEY-----` lines). If you prefer base64, store that and add `APPSTORE_P8_BASE64=true` to the job environment.
4. `APPLE_TEAM_ID`: the 10-character team id from developer.apple.com → Membership.
5. Create the app record in App Store Connect (My Apps → +) with the bundle id you will ship (see `GHUMANTE_BUNDLE_ID`), and register the bundle id under Certificates, Identifiers & Profiles → Identifiers.

## 4. Code signing for iOS: match (recommended) or cloud signing

The Unity export (an Xcode project) is built on Ubuntu by GameCI, then signed and uploaded on macOS by `game/fastlane/Fastfile` (`bundle exec fastlane ios beta`). Two options:

**fastlane match (recommended; deterministic, works with an App Manager key)**

1. Create an **empty private** repository, e.g. `your-org/ghumante-certificates`.
2. Create an SSH key pair: `ssh-keygen -t ed25519 -C ghumante-match -f match_deploy_key -N ""`. Add `match_deploy_key.pub` to the certificates repo as a **deploy key** (Settings → Deploy keys); allow write access only for the first run (step 4), then make it read-only.
3. Secrets: `MATCH_REPOSITORY` = `your-org/ghumante-certificates`, `MATCH_DEPLOY_KEY` = contents of `match_deploy_key` (private key), `MATCH_PASSWORD` = a strong passphrase (encrypts the certificates in the repo; store it in a password manager).
4. Generate the distribution certificate and App Store profile once, from a Mac:
   `cd game && bundle install && MATCH_READONLY=false bundle exec fastlane match appstore --git_url git@github.com:your-org/ghumante-certificates.git --app_identifier <bundle id> --team_id <team id>`
   (or run the CI job once with `MATCH_READONLY=false` added to the testflight job's environment). After that CI only reads (`readonly`), so it never revokes or recreates certificates.

**Cloud signing (no match secrets)**: if `MATCH_REPOSITORY` is not set, the Fastfile switches the project to automatic signing and passes the API key to `xcodebuild -allowProvisioningUpdates`. This needs the API key to have the **Admin** role, and Apple creates a cloud-managed distribution certificate. Simpler to set up, less transparent.

## 5. Google Play service account (`PLAY_SERVICE_ACCOUNT_JSON`, optional)

Lets `unity.yml` (manual run with "Upload the AAB to the Play internal track") push the AAB to the **internal testing** track with `fastlane android internal`.

1. Google Cloud console: create (or pick) a project → IAM & Admin → Service Accounts → create `ghumante-play-upload` → Keys → Add key → JSON. Download it.
2. Enable the **Google Play Android Developer API** for that Cloud project.
3. Play Console → Users and permissions → Invite new users → the service account's email → app permissions for Ghumante: *Release apps to testing tracks* (and *View app information*).
4. Secret: `PLAY_SERVICE_ACCOUNT_JSON` = the JSON file contents.
5. The first AAB must be uploaded manually in Play Console (Play needs one upload to bind the package name to the app). Until the app has passed review, uploads must be drafts; the Fastfile defaults to `release_status: draft` (override with `PLAY_RELEASE_STATUS=completed` later).

Google Play currently requires new apps and updates to target **Android 16 (API 36)** (since 2026-08-31, extension possible to 2026-11-01) ([Android docs](https://developer.android.com/google/play/requirements/target-sdk)); `ProjectSetup` sets target API 36 and minimum API 29.

## 6. Repository variable `GHUMANTE_BUNDLE_ID` (optional)

The application id for both stores, e.g. `np.example.ghumante`. Default: `com.ghumante.game`, a **placeholder** that you should not ship. It reaches Unity as `-ghumanteBundleId` (GameCI does not forward environment variables into its container) and fastlane as `IOS_BUNDLE_ID` / `ANDROID_PACKAGE_NAME`. Changing it after the first store upload creates a different app.

## 7. Later: staging CDN

ARCHITECTURE.md 6.4 has the nightly job upload packs to a staging CDN. That step is not wired yet; when the CDN exists it will need its own credentials (for example an S3-compatible bucket: `STAGING_CDN_ENDPOINT`, `STAGING_CDN_BUCKET`, `STAGING_CDN_ACCESS_KEY_ID`, `STAGING_CDN_SECRET_ACCESS_KEY`), added to `nightly-data.yml` behind the same skip-if-absent pattern.

## Rotating and revoking

* **Unity**: change the password of the CI account and update `UNITY_PASSWORD`; for Pro, return seats from the dashboard if a cancelled job left one activated.
* **Upload keystore**: request an upload key reset in Play Console, then replace the four `ANDROID_*` secrets.
* **App Store Connect key**: revoke it in Users and Access → Integrations and create a new one; update the three `APPSTORE_*` secrets.
* **match**: `fastlane match nuke distribution` (from a Mac, with care) and re-run step 4 of section 4; rotate `MATCH_PASSWORD` with `fastlane match change_password`.
