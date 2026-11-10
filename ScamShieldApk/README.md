# ScamShield APK
A local-first Kotlin Android application that hosts the ScamShield interface in a WebView and downloads public CERT/CIRT advisories through a native read-only service. Built by GitHub Actions.

1. Create a new GitHub repository and upload everything in this folder (keep the `.github` folder).
2. Open Actions > "Build ScamShield APK" > Run workflow (it also runs on every push to `main`).
3. When it is green, open Releases and download `ScamShield.apk` (or the `ScamShield-APK` artifact).
4. On the phone: allow "install unknown apps" for your browser or file manager, then open the APK.

Notes: the APK has CAMERA and INTERNET permissions. Camera access is requested only for QR scanning. Internet access is used by the native read-only advisory service to download public notices from Serbia National CERT, CERT RS (Republika Srpska only), Montenegro Government CIRT and North Macedonia MKD-CIRT. The service does not transmit checked URLs, messages, QR payloads, local reports or evidence. Advisory providers will receive normal network metadata such as the device IP address. Community notes and reports remain local-only. Official advisory titles, short excerpts, dates and source links are shown in the app; full articles remain on their original websites. Items are heuristically labelled for ordinary users, administrators or both. This classification can be imperfect.
To change the app, edit `web/scamshield-app.html` and push; the workflow rebuilds the APK.

## Optional: one permanent signing key (so updates install over the old app)
1. On your PC: `keytool -genkeypair -v -keystore release.keystore -alias scamshield -keyalg RSA -keysize 2048 -validity 10000`
2. Back up `release.keystore` and its passwords somewhere safe. NEVER commit them. If you lose the key you must uninstall and reinstall.
3. Encode it: in PowerShell run `[Convert]::ToBase64String([IO.File]::ReadAllBytes("release.keystore"))` and copy the output.
4. GitHub repo > Settings > Secrets and variables > Actions > add: `ANDROID_KEYSTORE_BASE64` (the output), `ANDROID_STORE_PASSWORD`, `ANDROID_KEY_ALIAS` (scamshield), `ANDROID_KEY_PASSWORD`.
5. Run the workflow again. The first APK signed with the new key still needs the old differently-signed build uninstalled once; after that, updates signed with the same permanent key can install over it. Configure all four secrets together. The workflow now fails if only some signing secrets are supplied.


The workflow assigns an increasing `versionCode` from the GitHub Actions run number, validates both the keystore password and key password, and verifies the final APK signature before publishing. Do not lose the permanent keystore: Android updates require the same signing certificate. Keep a secure offline backup.
