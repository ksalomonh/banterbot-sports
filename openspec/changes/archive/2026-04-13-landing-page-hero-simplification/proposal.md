# Proposal: Landing Page Hero Simplification

## Intent

The anonymous landing page (`/`) is cluttered by the shared topbar, Banter Rail, and bottom nav — chrome built for authenticated players. These elements consume viewport space, distract from the hero image, and misrepresent the product to new visitors. Goal: strip the landing page to a full-viewport hero with only a floating logo, while keeping all chrome intact everywhere else.

## Scope

### In Scope
- Remove topbar (mobile + desktop) on the landing page only
- Remove the Banter Rail on the landing page only
- Remove the mobile bottom nav on the landing page only
- Make the hero image cover the full viewport (`min-h-screen`)
- Add floating `logo-icon.png` overlay in the top-left corner
- Mobile-responsive adjustments for the new layout

### Out of Scope
- No changes to `_Layout.cshtml` or any shared layout
- No changes to logged-in views or auth flows
- Banter Rail and chat features remain fully functional elsewhere
- No SEO/meta tags work
- CTA button destination (`#`) is a known pre-existing issue — not touched

## Capabilities

### New Capabilities
- None

### Modified Capabilities
- None

> Pure UI/layout change. No spec-level behavioral requirements change.

## Approach

Create `Views/Shared/_LandingLayout.cshtml` — a minimal layout that includes `_TailwindHead.cshtml` (shared `<head>`) but omits all chrome. `Views/Home/Index.cshtml` sets `Layout = "_LandingLayout"` at the top. The hero section is updated to `min-h-screen` with `absolute inset-0 object-cover`, and the floating logo uses `fixed top-4 left-4 z-50`.

This mirrors the existing `Layout = null` pattern (used by Login/Register) but avoids duplicating the `<head>` scaffold.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Views/Shared/_LandingLayout.cshtml` | New | Minimal layout: `<html>` shell + `_TailwindHead` + `@RenderBody()` — no chrome |
| `Views/Home/Index.cshtml` | Modified | Set `Layout = "_LandingLayout"`, update hero to full-viewport, add floating logo |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Authenticated users accidentally hit this layout | Low | `HomeController.Index()` redirects auth users before rendering |
| Mobile bottom nav missing on landing for anonymous | Low | Expected — anonymous users don't use nav; landing just shows hero + CTA |

## Rollback Plan

Delete `_LandingLayout.cshtml` and remove the `@{ Layout = "_LandingLayout"; }` directive from `Home/Index.cshtml`. Two-line revert, no migrations, no data impact.

## Dependencies

- None. Both image assets (`stadium-hero.png`, `logo-icon.png`) already exist in `wwwroot/images/`.

## Success Criteria

- [ ] Anonymous `/` renders with no topbar, no rail, no bottom nav
- [ ] Hero image fills full viewport height on desktop and mobile
- [ ] Logo floats over the hero in the top-left corner
- [ ] All other pages (authenticated and anonymous) are visually unchanged
- [ ] No regressions on Login/Register pages
