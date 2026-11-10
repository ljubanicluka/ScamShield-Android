# ScamShield APK — source audit and changes

This project is the Kotlin Android WebView wrapper (`rs.scamshield.app`) around `web/scamshield-app.html`. It is separate from the .NET MAUI `ScamShieldMobile` project and is not the build-27 APK.

## Changes in this revision

- Added a native, read-only public advisory service for Serbia National CERT, CERT RS (Republika Srpska only), Montenegro Government CIRT and North Macedonia MKD-CIRT.
- Added `INTERNET` permission specifically for those native advisory GET requests. The WebView itself blocks remote subresources; external links are opened outside the app.
- Parses RSS and Atom feeds with Android's XML pull parser. The Serbia National CERT page is parsed as a public HTML listing. HTML descriptions are converted to plain text and excerpts are limited to 260 characters.
- Adds audience labels (`For ordinary users`, `For administrators`, `Both`) using keyword heuristics and sorts ordinary-user items first. These labels are guidance and can be wrong.
- Deduplicates advisories by normalized title across sources.
- Stores the last successful item list and update time per source. A failed source retains its cached items and reports its current availability and last successful update time.
- Fetches sources concurrently with connection/read timeouts so one slow source does not block all others sequentially.
- Adds an in-app live advisory screen with refresh, per-source status, audience labels, excerpt, date and original article link.
- Clarifies privacy text: user searches, messages, QR payloads, local reports and evidence are not uploaded by the advisory service. Advisory hosts receive ordinary network metadata such as the device IP address.
- Corrects the privacy wording around automatic masking: it is limited and users must remove names, addresses, codes and other personal data themselves.
- Keeps Community as local-only notes; no fake online users, posts, votes or shared reports are introduced.
- Keeps the map free of fabricated city statistics.
- Restricts WebView subresources to the local asset origin, allows only safe external link schemes, and permits camera access only for video capture requested by the trusted local asset origin.
- Stops the QR camera when leaving the QR screen and when the Android activity pauses/destroys.
- Signing workflow validates that all four signing secrets are present together, validates the keystore alias, assigns a run-based version code and verifies the APK signature before release.

## Important limitations

- This ZIP is source code, not a built or device-tested APK.
- Android Gradle build and physical-device tests were not run in this environment because no Android SDK/Gradle installation was available.
- The Serbia CERT parser depends on the current public HTML markup and may need adjustment if the site changes.
- Audience classification is keyword-based, not a professional threat-intelligence taxonomy.
- Live data is limited to public advisories. The URL/message checker still uses local heuristics and labelled DEMO examples; it is not a live URL reputation service.
- Community notes and evidence remain local to the device. There is no shared backend, account system or online reporting.
- The signing secrets must be configured in GitHub Actions. Without them, CI creates a test/debug-signed build that cannot update a differently signed installation.

## Validation performed

- Inline JavaScript syntax checked with Node.js.
- Android manifest parsed as XML.
- GitHub Actions workflow parsed as YAML.
- ZIP integrity checked after packaging.
- Static review of permissions, WebView origin checks, signing-secret handling and source endpoints.

These checks do not replace a full Android build or physical-device test.
