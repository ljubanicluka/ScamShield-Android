# ScamShield - continuation prompt and current project state

Continue work in the GitHub repository ljubanicluka/ScamShield-Android.

## Primary goal

Develop ScamShield into a reliable, polished Android anti-scam awareness app for the MTS app-contest project. Treat the supplied Claude HTML file (ScamShield (1).html) as a design and feature reference, not as production data or code to copy blindly. Its hard-coded DIR, POSTS, CITY, CT and quiz arrays are examples; its community, reports and map are localStorage-only and explicitly DEMO. Do not present these as real users, real reports, live statistics or confirmed threat intelligence.

The active Android project is ScamShieldMobile (.NET 10 MAUI, C# UI). ScamShieldMaui-v8 is a previous reference project and is not the workflow's current build target. Inspect the current branch before editing; do not assume that old files or earlier assistant summaries reflect the current head.

## Current source and build state

- Active app: ScamShieldMobile/ScamShieldMobile.csproj, target net10.0-android.
- UI: ScamShieldMobile/MainPage.cs.
- Public advisory ingestion: ScamShieldMobile/LiveFeedService.cs.
- Startup uses Application.CreateWindow in ScamShieldMobile/App.cs.
- Scanner registration is in ScamShieldMobile/MauiProgram.cs.
- GitHub Actions workflow: .github/workflows/android-apk.yml.
- Workflow runner: Ubuntu 24.04; .NET 10; Java 17; setup actions v5; produces APK + SHA-256; publishes a GitHub Release.
- Signing is currently ephemeral demo signing for sideload testing only. It is not a production identity. Future builds may have a different signature; do not promise in-place updates unless the same persistent key is used.
- The previous stable app APK was build #18. A later build series added live official advisories and removed fabricated demo content. Check the latest Actions run and release assets before claiming an APK exists.
- The repository's GitHub API metadata has reported its visibility as PUBLIC. Verify its current visibility and warn the owner if they intend the source to be private. Do not expose secrets. No secrets should ever be committed.

## Implemented in the current native app

1. Onboarding username is normalized to lowercase while typing, on save, and on the home greeting.
2. Optional birth-year input is explicitly labelled and validated; it is stored locally and not displayed publicly.
3. Light/dark theme refresh rebuilds the root layout rather than reusing native controls already attached to the old root.
4. Serbian Latin/Cyrillic switching is persisted. Cyrillic conversion covers the rendered UI. Keep raw decoded QR payloads and URLs unchanged; never transliterate a URL.
5. QR scanning has camera permission checks, rear camera selection, start/stop, torch and scan-result handling. Gallery import copies the selected image into a seekable memory stream before decoding with ZXing options (rotation/try-harder).
6. QR contents are displayed for review; do not automatically open links, start calls, join Wi-Fi networks or initiate payment actions.
7. The previous local seeded community examples and the fake CommunityPost class were removed. Community now explicitly says the shared server is not connected; do not reintroduce synthetic people, posts or vote counts.
8. The map no longer shows invented city-level report numbers. It shows actual source coverage/status only and explains the limitation.
9. Home shows actual public advisory counts and the number of sources currently available.
10. A live “Upozorenja” screen fetches public official advisories and shows source health, article title, source/coverage, dates when available, and the original link.
11. LiveFeedService makes read-only GET requests and caches received advisory items locally. It does not upload profiles, scanned QR payloads, checked URLs, messages or evidence.
12. The URL/message checker is a local heuristic only. It is NOT a live URL reputation lookup and cannot certify that a link is safe. The UI must state this honestly.
13. Family guidance remains a consent-based static explanation, not device monitoring, account linking or message forwarding.
14. There is no actual virtual machine / isolated browser sandbox.

## Official regional sources in the current feed service

- Serbia — National CERT advisory listing: https://www.cert.rs/rs/obavestenja.html. This is a paginated HTML listing, not a documented RSS feed. Its article links use the /rs/obavestenje/... path.
- Republika Srpska — CERT RS feed: https://cert.aikt.rs/feed/. This source covers Republika Srpska only, not all Bosnia and Herzegovina.
- Montenegro — Government CIRT Atom feed: https://rss.gov.me/atom/me/gov-cirt; source page: https://www.gov.me/gov-cirt. Coverage is the state administration/government network, not the whole country's private sector.
- North Macedonia — MKD-CIRT feed: https://mkd-cirt.mk/feed/; official news page: https://mkd-cirt.mk/category/vesti/.

A source may timeout, be unavailable, have an empty feed, or change its markup. Show its status accurately and allow the official page to be opened. Do not fill missing sources with sample posts. In Bosnia and Herzegovina, clearly mark CERT RS as Republika Srpska coverage; do not label it a countrywide BiH source. There is no single operational state-level CERT/CSIRT currently identified for BiH; retain that caveat and re-verify it when making a current claim.

## Required product principles

- Serbian is primary. Serbian Latin/Cyrillic switching should work across controls, data labels and alerts. English is still not fully translated; do not say otherwise.
- Light theme: white/blue. Dark theme: dark/black/yellow.
- Never claim “safe”. Use careful verdicts such as “no obvious signs found” and remind users this is not a guarantee.
- Never imply MTS has integrated, endorsed or deployed ScamShield.
- No fake engagement, fake user profiles, fake counts, fake city statistics or fabricated security advisories. If sample data is ever used for UI testing, label it DEMO in the UI.
- Privacy-first: do not send user content to third parties without clear disclosure and an explicit opt-in.
- Links from QR codes must never auto-open. Do not auto-call, send messages, join networks or pay.
- Family functions must be opt-in, visible and consent-based. No covert monitoring.
- Avoid asking for unnecessary sensitive onboarding information. Do not ask for sex, exact date of birth, address or contact lists.
- Maintain accessibility: large text, readable contrast, keyboard/screen-reader semantics and layouts that fit narrow Android screens.

## Live data versus online backend

Current live data means public read-only CERT/CIRT advisories only. The app does not yet have:
- accounts or authentication;
- a shared community feed;
- cross-device posts/comments/upvotes/downvotes;
- online user reports;
- a live URL/domain reputation database;
- a true isolated VM/sandbox;
- real geographic scam statistics.

Do not silently create a Supabase/Firebase project, database or external service. The user previously wanted approval before creating online infrastructure. Before implementation, ask the user to approve a backend provider and explain:
- what user data would leave the phone;
- what data is stored and for how long;
- who can see posts/reports;
- moderation and abuse-report processes;
- minimum data retention and deletion;
- database access rules and abuse prevention.

If a third-party URL reputation service is proposed, explain that the checked URL may be sent to that provider. Do not integrate it without explicit opt-in and checking the provider's API terms, privacy policy, quota and license.

## Next steps

1. Check current main commit, the latest full Actions logs, and whether the latest release actually contains ScamShield.apk and ScamShield.apk.sha256.
2. Fix every compiler error in the complete log before pushing; search the whole project for the same class of error.
3. Inspect LiveFeedService.cs for parser correctness and source failures. Prefer per-source status, cache timestamps and safe timeouts. Never turn a failed feed into fake sample data.
4. Test all builds and APK validation in CI, then publish the APK and provide the direct release link and SHA-256.
5. Add device test instructions for Xiaomi 14C: onboarding; lowercase username; birth year; navigation/back; theme; script persistence; camera permission; QR camera scan; screenshot/gallery import; QR payload display without auto-open; offline/cache behaviour; alerts source status; no fake posts/counts; profile persistence.
6. Record actual device tests separately from CI compile/package tests. A green build does not prove runtime camera behaviour.
7. Continue porting relevant HTML reference screens only after the live/real-data foundation works: emergency help checklists, evidence locker, learning quiz, official contact verification, accessibility. Do not silently label local-only screens as online.
8. Keep README.md and this prompt file updated after every significant change.

## Current backend question

Do not connect a backend without asking. The immediate release can provide genuinely live public advisory feeds without accounts or user-data upload. Shared community posts, online reports and votes require the user's explicit approval of a backend and privacy model first.
