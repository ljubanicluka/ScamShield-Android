# ScamShield Android

ScamShield is an Android-first security-awareness app built with .NET 10 MAUI and C#.

## Project layout

- ScamShieldMobile/ is the active Android application and the project built by GitHub Actions.
- ScamShieldMaui-v8/ is a previous project kept for reference. It is not the current APK target.
- .github/workflows/android-apk.yml builds, validates, signs and publishes a sideload APK.

## Current implementation

- Local onboarding, lowercase username normalization and optional birth year.
- Light/dark themes and Serbian Latin/Cyrillic UI switching.
- QR camera scanning and gallery-image decoding. QR content is displayed for review; links, calls and payments are not triggered automatically.
- Local heuristic checks for suspicious content. This is not a live domain-reputation service and cannot certify a link as safe.
- Read-only public security advisories from official CERT/CIRT sources, with per-source availability, original links and a local cache.
- Community and map screens do not fabricate people, reports, votes or city-level statistics. Shared community data is not connected to a backend yet.
- Family screen is guidance only; it does not monitor devices or forward messages.

## Live advisory sources

| Region | Source | Coverage |
|---|---|---|
| Serbia | https://www.cert.rs/rs/obavestenja.html | Serbia |
| Bosnia and Herzegovina | https://cert.aikt.rs/ | Republika Srpska only, not all of BiH |
| Montenegro | https://www.gov.me/gov-cirt | Government networks / state administration |
| North Macedonia | https://mkd-cirt.mk/ | North Macedonia |

Each source can be unavailable or return no current entries. The app displays the status instead of inventing data. Original source pages remain authoritative.

## Build

The GitHub Actions workflow uses Ubuntu 24.04, .NET 10, Java 17 and the MAUI Android workload.

A push to main that changes ScamShieldMobile/** or the workflow triggers a build. A successful run creates ScamShield.apk and ScamShield.apk.sha256, and publishes them to a GitHub Release.

The workflow currently uses an ephemeral demo signing key for sideload testing. It is not a production signing identity; builds signed with different temporary keys cannot be installed as in-place updates over one another. A persistent signing key must be configured through GitHub Actions Secrets before production distribution. Never commit a keystore or signing password.

## Important limitations

- No account system or shared online community is connected.
- User reports are not sent to a server.
- The URL checker uses local heuristics; there is no live URL-reputation provider integration.
- There is no isolated VM/sandbox for opening risky links.
- English translations, a true geographic scam map, and production moderation are not complete.
- Camera/gallery QR behaviour and all UI flows still require testing on physical Android devices, including the Xiaomi 14C.

## Privacy

The advisory feature makes read-only requests to the listed public sources. It does not upload profile details, scanned QR content, messages, checked URLs or evidence. Do not add third-party URL-checking APIs without an explicit user opt-in, a clear privacy explanation, and confirmation that the provider's terms permit the intended use.

## Next development stage

Before enabling accounts, shared posts, comments, votes or report submission, choose and approve a backend (for example Supabase or Firebase), define data retention and moderation rules, configure access controls, and review the privacy implications. Do not represent local-only functionality as online or live.
