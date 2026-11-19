# ScamShield — test checklist

## CI / static checks
- [ ] Build the Android release variant with Java 17 and API 36.
- [ ] Run `node scripts/check-web.js app/src/main/assets/index.html`.
- [ ] Verify APK ZIP integrity and APK signature.
- [ ] Verify the source bundle and SHA-256 file are included in the GitHub release.
- [ ] Confirm the APK and source bundle checksums match the CI logs.

## QR camera — physical Android device required
- [ ] First launch asks for camera permission only after pressing “Scan QR with camera”.
- [ ] Granting permission opens the native rear-camera preview.
- [ ] A normal QR code is detected and its text appears in the ScamShield checker.
- [ ] QR containing an HTTPS URL is analyzed but never auto-opened.
- [ ] Wi-Fi, phone, SMS, email, and plain-text QR payloads are displayed as text and not executed.
- [ ] Flashlight toggles on devices with flash hardware.
- [ ] Cancel closes the camera and returns to the app.
- [ ] Denying permission shows a usable message and the gallery option still works.
- [ ] Scanning, cancelling, and scanning again does not leave the camera active or crash.

## QR gallery — physical Android device required
- [ ] JPEG image with QR decodes.
- [ ] PNG image with QR decodes.
- [ ] HEIC image with QR decodes on supported devices.
- [ ] Rotated image is decoded correctly.
- [ ] High-resolution and compressed screenshots are tested.
- [ ] Image without a QR shows an understandable error.
- [ ] Cancelling the picker does not crash or show a false failure.
- [ ] QR content stays local and is not sent to the advisory sources.

## Other interactions
- [ ] Complete onboarding and open every home tile.
- [ ] Toggle English/Serbian and Latin/Cyrillic.
- [ ] Toggle light/dark theme and large text.
- [ ] Check local URL heuristics and verify wording does not claim “safe”.
- [ ] Create, view, and delete a local report note.
- [ ] Tick help and family checklists and confirm they persist.
- [ ] Generate and copy the family warning message.
- [ ] Save and delete evidence notes.
- [ ] Calculate SHA-256 from a selected file and copy the result.
- [ ] Open external official links in the Android browser.
- [ ] Open ScamShield's Android app settings shortcut.
- [ ] Refresh CERT/CIRT advisories and inspect online/cached/unavailable states.
- [ ] Delete all local app data only after the two-step confirmation.

## Device matrix
Test at minimum: one Xiaomi/Redmi device (the user's intended target), one Android emulator, and one other Android manufacturer/version if available. Record Android version, WebView version, device model, image format, and outcome. Do not mark a test complete based only on a successful Gradle build.
