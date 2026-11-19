# Claude task — ScamShield Android hardening

You are working on the Kotlin Android app in `ScamShieldApk/` on branch `scamshield-apk-ci`. Do not edit the older .NET MAUI project on `main`.

## Main goal

Make existing features reliable before adding more screens. The highest priority is QR scanning from the live camera and from gallery images. Fix broken interactions and error handling; keep the app local-first and privacy-preserving. Do not implement a VPN.

## Read first

1. `AUDIT_REPORT.md`
2. `TEST_CHECKLIST.md`
3. `CLAUDE_HANDOFF.md`
4. `app/src/main/java/rs/scamshield/app/MainActivity.kt`
5. `app/src/main/java/rs/scamshield/app/QrScannerActivity.kt`
6. `web/scamshield-app.html`

## Requirements

- Keep QR decoding on-device with bundled ML Kit. Camera scanning must use CameraX, handle runtime permission grant/denial, release every ImageProxy on success/failure/exception, support cancellation and repeated scans, and return decoded text to the app without opening it.
- Gallery QR decoding must use Android's document picker and handle content URIs, EXIF rotation, JPEG, PNG and supported HEIC formats. Surface meaningful errors for cancellation, unsupported/corrupt files and images without a QR. Do not upload user images.
- QR text may be a URL, Wi-Fi configuration, phone, SMS, email, payment URI or plain text. Display and analyze it safely. Never automatically open a QR destination, place calls, send messages, join Wi-Fi or initiate payments.
- Check every visible UI action. The project uses delegated `data-a` handlers; add automated checks so every rendered action has a matching implementation. Make errors visible instead of silently swallowing them.
- Keep external site navigation outside the restricted WebView. Keep local JS/CSS assets bundled; do not add remote script execution.
- Improve local URL parsing and risk explanations, but clearly label heuristic results. Do not claim a URL, number, email, file or device is safe based on a heuristic alone.
- Preserve Serbian Latin/Cyrillic and English, light/dark themes, accessibility, and device compatibility.
- Keep official CERT/CIRT integrations read-only. Verify endpoint and parser behavior against the publisher's current official source before calling a source live.
- Do not fabricate community profiles, reports, independent counts, locations, ratings, scan results or threat-intelligence feeds.
- A shared community, city-level map, live reputation checks, antivirus engine, call blocking and VPN require real services/platform APIs and privacy/security designs. Keep these features clearly labelled as unavailable until genuinely implemented.

## Build and verification

- Use Java 17, Gradle 8.13, Android Gradle Plugin 8.11.1, Kotlin 2.2.21 and API 36 as currently configured, unless a concrete compatibility failure is demonstrated.
- Run the CI workflow and inspect the full build log, not only the workflow badge.
- Run `node scripts/check-web.js app/src/main/assets/index.html`.
- Validate APK ZIP integrity and signature and publish the SHA-256 checksum.
- Add Android instrumentation tests where practical.
- Do not claim camera or gallery functionality is physically tested unless run on an actual Android device. Record device model, Android version, image format and exact test result.
- Update the audit, handoff and checklist with verified results only.

## Expected delivery

A clean source ZIP for Claude, a working APK if the build succeeds, checksums for both files, and a concise list of fixed defects and remaining device tests. Keep all signing secrets out of the repository.
