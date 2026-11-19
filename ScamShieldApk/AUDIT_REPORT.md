# ScamShield Android — code audit

Audit scope: the Kotlin WebView Android project in `ScamShieldApk/` on branch `scamshield-apk-ci`. This is a source-level review. A prior CI build succeeded, but the previous APK was not physically tested on the user's phone.

## Defects found and changes made

### 1. QR camera depended on WebView camera support
**Finding:** live scanning used `navigator.mediaDevices.getUserMedia()` inside WebView, with Android `PermissionRequest` bridging. This is more fragile across WebView/device implementations than a native camera screen and had a custom polling/timeout loop.

**Change:** added `QrScannerActivity` using CameraX preview and ML Kit QR-only frame decoding. Camera permission is requested by the native activity. The scanner supports flashlight where the device has one, stops when the activity closes, and returns the decoded text to the existing checker. It does not open the QR destination.

### 2. Gallery QR decoding relied on `createImageBitmap`
**Finding:** the JavaScript image path called `createImageBitmap(file)` and returned null on any exception. Some Android WebView/image combinations do not support that path reliably.

**Change:** Android gallery selection now uses `ACTION_OPEN_DOCUMENT` and the bundled ML Kit model to decode the selected content URI locally. A browser fallback remains and now tries `Image` + `canvas` if `createImageBitmap` fails.

### 3. External links used new-window targets while WebView multiple windows were disabled
**Finding:** the HTML used `target="_blank"` while the wrapper had `setSupportMultipleWindows(false)` and no `onCreateWindow` handler. That combination can make links appear clickable but fail to open consistently.

**Change:** removed the new-window targets. Same-window external navigation reaches the WebView client, which launches the external Android browser or relevant app.

### 4. SHA-256 page implied a demo reputation comparison
**Finding:** the old code contained a placeholder all-zero hash in a DEMO list. That was not a real malware hash feed and could produce a misleading security message.

**Change:** the page now computes only the SHA-256 fingerprint locally, offers copying it, and clearly says no reputation lookup is connected. It does not claim the file is safe.

### 5. Antivirus/VPN section overstated actual capabilities
**Finding:** the old page showed recommendations (including a VPN product) but did not scan installed apps, scan the whole device, or establish a VPN tunnel.

**Change:** renamed it to “Device security”. It contains practical instructions, a Play Protect help link, and a working Android app-settings shortcut. It explicitly says it is not an antivirus scanner and does not run a VPN.

### 6. Static app had no action-wiring check
**Finding:** a large single-page interface uses delegated `data-a` click handlers. A button could be rendered without a corresponding handler and the Gradle build would not detect that.

**Change:** added `scripts/check-web.js` to check inline JavaScript syntax, required action branches, native QR bridge hooks, and external-link target policy. CI runs it on the bundled HTML.

## Remaining limitations (not bugs that can be honestly fixed with UI-only code)

- **No backend:** reports, evidence, and community notes are local to one device. Other people cannot see them and they do not increase a shared report count.
- **No real reputation service:** phone, email, message, and URL checks use local heuristics and visibly labelled DEMO examples. They are not a live anti-phishing or malware database.
- **No geographic dataset:** the scam map intentionally does not invent city statistics or pins.
- **No antivirus engine:** the app does not inspect installed apps or scan the Android filesystem.
- **No VPN:** no `VpnService`, tunnel provider, server infrastructure, or VPN connection exists.
- **Password checker is a simple local heuristic:** it is not a breach check, password audit service, or formal entropy estimator.
- **Local storage is not an encrypted vault:** app data stays on-device under Android app storage, but the local HTML state is not end-to-end encrypted. Avoid entering card numbers, PINs, passwords, or unnecessary personal data in notes.
- **CERT/CIRT integration is read-only and parser-dependent:** feeds and the Serbia HTML listing can change. Their current availability and completeness must be rechecked.
- **No physical-device test yet:** build success only confirms compilation and packaging. Camera behavior, file-provider interactions, rendering, and system settings must be checked on actual devices.

## Follow-up priorities

1. Physical-device QA of native camera and gallery QR paths.
2. Add automated Android instrumentation tests for scanner start/stop, permission denial, scanner result, and image decode.
3. Verify official advisory endpoints and parsing against their current publisher pages.
4. Improve URL normalization, IDN display, redirect warnings, and false-positive controls without auto-opening links.
5. Define a privacy and moderation design before creating a shared reporting backend.
6. Only then consider anonymized aggregate map data and opt-in notifications.


## Follow-up build investigation — 2026-11-19

The latest CI run before this update was build #13 and failed during Kotlin compilation (not during Gradle setup, Java setup, SDK installation, or the web asset checks). The compiler reported:

- `MainActivity.kt` and `QrScannerActivity.kt` imported `Barcode` from `com.google.mlkit.vision.barcode`. The model's `Barcode` type is in `com.google.mlkit.vision.barcode.common`.
- `QrScannerActivity.onRequestPermissionsResult` used a Kotlin array type that did not match the Android callback signature.

Fixes have been committed on `scamshield-apk-ci`:

- Corrected the ML Kit `Barcode` import in both Kotlin files.
- Replaced the legacy camera permission callback with `ActivityResultContracts.RequestPermission`.
- Camera denial now returns to the QR page with an explicit message, where the user can still choose an image.
- Added exception handling around per-frame ML Kit input creation/processing so a failed camera frame is closed rather than left outstanding.
- Expanded the web smoke check so it derives actions from rendered `data-a` attributes and requires a matching delegated handler. It also checks that the Android asset uses local `jsQR.js`, not a remote CDN URL.

A green CI build after these commits is still required. No source-level audit can verify autofocus, camera hardware, Android document-provider behavior, HEIC decoding, or device-specific WebView rendering. The user must test the resulting APK on a physical phone before these are marked as working.

## Functional status by area

- **QR camera:** native CameraX preview and bundled on-device ML Kit QR decoding are implemented. Permission allow/deny and repeat-scan behavior require physical-device QA.
- **QR image:** Android document picker and ML Kit local image decoding are implemented. JPEG/PNG/HEIC, rotated photos, large screenshots and provider-specific URIs require device QA.
- **URL/number/message checks:** local heuristics and labelled DEMO examples only. There is no live reputation service and a clean result is not proof of safety.
- **Official notices:** native read-only CERT/CIRT fetching with a local cache. Endpoint availability, parser accuracy and date handling need periodic validation.
- **Reports/community:** private notes stored locally on one phone. There is no public feed, cross-device sync, verified independent report count or moderation backend.
- **Map:** intentionally empty until a verified, privacy-preserving city dataset exists.
- **Device security:** educational guidance and Android settings shortcut only. No antivirus engine, installed-app scan, call blocking or VPN is present.
- **Password check and SHA-256:** local-only utilities. Neither is a breach lookup or malware-reputation verdict.

## Next implementation priorities

1. Complete the post-fix CI build and inspect the produced APK and Claude source ZIP checksums.
2. Run the QR test matrix in `TEST_CHECKLIST.md` on the target Xiaomi/Redmi phone and at least one emulator.
3. Add Android instrumentation tests for camera permission, successful scan, cancellation, analyzer lifecycle and gallery decoding.
4. Add robust URL normalization and clear handling of internationalized domains, user-info fields, redirects, shortened links and non-HTTP QR payloads. Never auto-open scanned content.
5. Validate CERT/CIRT feeds against official source pages; make stale/cache/unavailable states explicit.
6. Design an opt-in backend only if cross-device community is needed. It needs abuse controls, reporting thresholds, consent, retention limits and moderation before it can power public reports or a map.
7. Keep VPN, antivirus and call-blocking labels out unless real supported platform services and infrastructure are implemented.
