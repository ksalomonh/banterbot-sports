## Exploration: Landing Page Hero Simplification

### Current State

The anonymous landing page is served by `HomeController.Index()`, which renders `Views/Home/Index.cshtml`. Authenticated users are redirected to `Torneo/Index` before the view is ever rendered.

All views use `_Layout.cshtml` (via `_ViewStart.cshtml`), which contains:

1. **Mobile topbar** (`<header>` — line 16-36): Fixed top, `md:hidden`, logo + auth link
2. **Desktop topbar** (`<nav>` — line 39-87): Fixed top, `hidden md:flex`, logo + nav links + auth
3. **Banter Rail** (`<aside>` — line 90-124): Fixed right, `hidden lg:flex`, always renders for all users
4. **Content wrapper** (`<div>` — line 127-170): Adds `pt-16 pb-24 md:pb-0 md:pt-20 lg:pr-80` to accommodate topbar/rail
5. **Mobile bottom nav** (`<nav>` — line 173-202): Fixed bottom, `md:hidden`

The landing hero (`Home/Index.cshtml`) uses `min-h-[80vh] md:min-h-[70vh]` — NOT full viewport. The stadium image (`stadium-hero.png`) is absolutely positioned with `object-cover`. Both assets exist: `stadium-hero.png` and `logo-icon.png`.

**Key constraint**: Login and Register pages already use `Layout = null` to escape the shared chrome. The current layout has NO mechanism to conditionally suppress any section.

### Affected Areas

- `Views/Shared/_Layout.cshtml` — or new layout file; topbar (lines 16-87), Banter Rail (lines 90-124), content wrapper (lines 127-170), mobile bottom nav (lines 173-202)
- `Views/Home/Index.cshtml` — hero section needs viewport takeover, floating logo overlay; must set a different layout or Layout=null
- `Views/Shared/_TailwindHead.cshtml` — shared, should NOT change (already includes needed styles)
- `wwwroot/images/stadium-hero.png` — background asset, exists ✓
- `wwwroot/images/logo-icon.png` — floating logo asset, exists ✓

### Approaches

1. **New `_LandingLayout.cshtml`** — Create a minimal layout that shares `<head>` via `_TailwindHead.cshtml` but omits topbar, rail, and bottom nav. Landing page sets `Layout = "_LandingLayout"`.
   - Pros: Clean separation, no conditionals in shared layout, easy to add full-viewport hero CSS, matches intent
   - Cons: Slight duplication of the `<html>/<body>` shell (~10 lines)
   - Effort: **Low**

2. **Conditional rendering via ViewData** — Add `@if (ViewData["HideChrome"] == true)` blocks throughout `_Layout.cshtml` to skip topbar/rail/nav.
   - Pros: Single layout file, DRY
   - Cons: Clutters shared layout with conditionals; hard to reason about landing-specific spacing; the content wrapper's padding still needs conditional removal
   - Effort: **Low-Medium**

3. **`Layout = null` (standalone HTML)** — Like Login/Register, write the entire page as standalone HTML.
   - Pros: Maximum control, no layout coupling
   - Cons: Must duplicate `<head>`, Tailwind config, and `@section Scripts` rendering; drift risk
   - Effort: **Medium**

### Recommendation

**Approach 1 — `_LandingLayout.cshtml`**. It's the cleanest pattern: it shares `_TailwindHead.cshtml` (no duplication of theme/config), removes all chrome cleanly, and allows the hero to take the full viewport without fighting the parent wrapper's padding. This also parallels the existing `Layout = null` pattern without duplicating the full HTML scaffold.

The hero should be `min-h-screen` (full viewport), with the stadium image as `absolute inset-0` cover. Subsequent content (features, CTA) scrolls below. The floating logo overlays in the top-left corner using `fixed` or `absolute` positioning with high z-index.

### Risks

- **Mobile bottom nav suppression**: The mobile bottom nav (`<nav>` with `md:hidden`) is rendered by the layout. On the landing page, anonymous users don't need it — but removing it must NOT break other pages. Using a separate layout sidesteps this entirely.
- **Hero scroll behavior**: The hero is full-viewport with content below. Need to confirm the user wants a scroll-down experience (not a single-screen app). The current landing already has feature sections below the hero, so this is consistent.
- **Banter Rail rug-pull**: The rail is currently visible to anonymous users on lg+. Removing it on the landing page is fine since authenticated users are redirected. But the CTA button "Unirse al Banter" currently goes nowhere (`#`) — already a known issue, not introduced by this change.
- **SEO/social meta**: No `<meta>` tags currently; not in scope but worth noting for future.

### Ready for Proposal

**Yes** — the scope is clear, the files are identified, and the approach is decided.