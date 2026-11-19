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
