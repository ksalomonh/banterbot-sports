# Design: Landing Page Hero Simplification

## Technical Approach

Create a dedicated `_LandingLayout.cshtml` that reuses `_TailwindHead.cshtml` for styling but renders zero chrome (no topbar, no Banter Rail, no bottom nav). `Index.cshtml` opts into this layout via `@{ Layout = "_LandingLayout"; }`, overriding `_ViewStart.cshtml`. The hero section becomes full-viewport with a floating logo overlay.

## Architecture Decisions

| Decision | Choice | Alternatives Rejected | Rationale |
|----------|--------|-----------------------|-----------|
| Chrome removal strategy | Dedicated `_LandingLayout` | (a) Conditional `@if` blocks inside `_Layout.cshtml`; (b) `Layout = null` like Login/Register | (a) pollutes the shared layout with landing-specific logic; (b) duplicates the entire `<head>` scaffold. A dedicated layout keeps isolation while reusing `_TailwindHead`. |
| Logo positioning | `fixed top-4 left-4 z-50` over the hero image | `absolute` within hero section | `fixed` keeps logo visible during scroll on pages with below-fold content (features section, final CTA). |
| Hero image sizing | `min-h-screen` + `absolute inset-0 object-cover` | `background-image` CSS | `<img>` tag with `object-cover` already exists in the codebase and is easier to manage with `onerror` fallback. |

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `BanterBotSports.Web/Views/Shared/_LandingLayout.cshtml` | **Create** | Minimal HTML shell: `<!DOCTYPE html>`, `<html>`, `<head>` via `_TailwindHead` partial, `<body>` with only `@RenderBody()` + optional `Scripts` section. No nav, no rail, no bottom bar. |
| `BanterBotSports.Web/Views/Home/Index.cshtml` | **Modify** | Add `Layout = "_LandingLayout"` directive. Update hero `<section>` to `min-h-screen`. Add floating logo `<img>` with `fixed top-4 left-4 z-50`. Remove outer `<main>` padding that assumed shared chrome spacing (`pt-4 pb-24`). |

No controller changes — `HomeController.Index()` already redirects authenticated users before the view renders.

## Mobile Considerations

- The `_Layout.cshtml` bottom nav is **not rendered** by `_LandingLayout` — intentional, since the landing page is a single hero + features scroll, not a navigable app shell.
- Hero `min-h-screen` works correctly on mobile viewports; existing `text-5xl md:text-7xl lg:text-8xl` responsive font sizing stays unchanged.
- CTA buttons already use `flex-col sm:flex-row` stacking — no changes needed.
- Floating logo `fixed top-4 left-4` stays visible during scroll on both mobile and desktop without overlapping CTAs (CTAs are centered, logo is top-left).

## Safety & Rollback

**Why this is safe:**
1. `_LandingLayout` is a NEW file — zero risk to existing pages.
2. Only `Index.cshtml` references it; all other views still resolve to `_Layout` via `_ViewStart.cshtml`.
3. `HomeController` redirects authenticated users → they never see this layout.
4. Login/Register pages use `Layout = null` — completely independent, unaffected.

**Rollback:** Delete `_LandingLayout.cshtml` + remove `Layout = "_LandingLayout"` from `Index.cshtml`. Two-file, zero-migration revert.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Visual | Hero fills viewport, no chrome visible, logo floats | Manual browser check (desktop + mobile viewport) |
| Regression | Other pages unchanged | Navigate to `/Torneo`, `/Account/Login`, `/Account/Register` — verify chrome intact |

## Migration / Rollout

No migration required. Pure additive UI change.

## Open Questions

None — all inputs are resolved.
