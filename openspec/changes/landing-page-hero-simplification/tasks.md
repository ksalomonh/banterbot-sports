# Tasks: Landing Page Hero Simplification

## Phase 1: Create Dedicated Landing Layout

- [x] 1.1 Create `BanterBotSports.Web/Views/Shared/_LandingLayout.cshtml` — minimal HTML shell that includes `_TailwindHead.cshtml` partial for `<head>`, renders `@RenderBody()`, and defines an optional `Scripts` section. No topbar, no Banter Rail, no bottom nav.

## Phase 2: Update Landing Page Hero

- [x] 2.1 In `BanterBotSports.Web/Views/Home/Index.cshtml`, add `@{ Layout = "_LandingLayout"; }` at the top to opt out of the shared layout.
- [x] 2.2 Update the hero `<section>` to use `min-h-screen` so the background image fills the full viewport. Ensure `<img>` uses `absolute inset-0 object-cover`.
- [x] 2.3 Add floating logo `<img>` with classes `fixed top-4 left-4 z-50` inside the hero so it overlays the background on both desktop and mobile.
- [x] 2.4 Remove outer `<main>` padding classes (`pt-4 pb-24 md:pb-12`) that assumed shared chrome spacing.

## Phase 3: Verify Isolation

- [ ] 3.1 Manually verify `/` (anonymous) — no topbar, no Banter Rail, no bottom nav; hero fills viewport; logo floats top-left. Check desktop and mobile viewport widths.
- [ ] 3.2 Manually verify `/Account/Login` and `/Account/Register` — chrome still renders as `Layout = null` (unaffected).
- [ ] 3.3 Manually verify one authenticated page (e.g. `/Torneo`) — standard chrome (topbar, bottom nav, Banter Rail) intact via `_ViewStart.cshtml` default.
