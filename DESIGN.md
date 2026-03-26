# Design System Strategy: High-Octane Stadium

## 1. Overview & Creative North Star
**Creative North Star: The Neon Colosseum**

This design system moves away from the static, table-heavy interfaces of traditional sports management and shifts toward a high-energy, immersive digital arena. We are building a "Digital Colosseum" where the "Banter" is as important as the betting. 

To break the "template" look visible in early iterations, we utilize **intentional asymmetry** and **tonal depth**. The interface should feel like a premium broadcast overlay—dynamic, layered, and high-contrast. We achieve this by using large typography scales to anchor the eye, overlapping card elements to create a sense of physical space, and "glow" states that mimic the neon lights of a night-time stadium.

---

## 2. Colors: Midnight & Electricity

Our palette is designed for deep-focus dark mode, optimized for high-energy interaction.

*   **Primary (`#69daff`):** Electric Blue. Used for primary CTAs and active states.
*   **Secondary (`#2ff801`):** Neon Green. The color of "Go," profit, and "Live" status.
*   **Tertiary (`#ff7166`):** Actionable heat. Used for "Locked" or high-urgency alerts.
*   **Neutral/Surface (`#090e1c`):** The "Midnight Stadium" base.

### The "No-Line" Rule
Prohibit 1px solid borders for sectioning content. Boundaries must be defined solely through background color shifts. For example, a card (`surface-container-low`) should sit on the main `background` without a stroke. Separation is achieved through value contrast, not outlines.

### Surface Hierarchy & Nesting
Treat the UI as a series of physical layers.
*   **Background (`#090e1c`):** The floor of the stadium.
*   **Surface Container Low (`#0d1323`):** Large structural sections.
*   **Surface Container High (`#181f33`):** Interactive cards and list items.
*   **Surface Bright (`#242b43`):** Active or hovered states.

### The "Glass & Gradient" Rule
Floating overlays (modals, dropdowns, navigation bars) must use **Glassmorphism**. Apply `surface-container-highest` at 60% opacity with a `20px` backdrop blur. For main CTAs, use a linear gradient transitioning from `primary` (`#69daff`) to `primary_container` (`#00cffc`) at a 135-degree angle to provide visual "soul."

---

## 3. Typography: Editorial Authority

We use **Inter** for its modern, neutral legibility, but we apply it with aggressive scale to create an editorial feel.

*   **Display (`3.5rem` / `display-lg`):** Reserved for big scores or major "Banter" headlines. Tracking: -2%.
*   **Headline (`2rem` / `headline-lg`):** Used for section titles (e.g., "Your Tournaments"). Bold weight.
*   **Body (`1rem` / `body-lg`):** High readability for chat logs and betting descriptions.
*   **Labels (`0.75rem` / `label-md`):** All-caps for status badges (LIVE, PENDING) with 5% letter spacing.

The contrast between the oversized `Display` text and the functional `Body` text creates a professional, magazine-style hierarchy that feels curated, not generated.

---

## 4. Elevation & Depth: Tonal Layering

Traditional drop shadows are forbidden. We use "Ambient Glows" and Tonal Layering.

*   **The Layering Principle:** Place a `surface-container-highest` card inside a `surface-container-low` section to create natural lift.
*   **Ambient Shadows:** For floating elements, use a shadow with a 40px blur, 0% spread, and 8% opacity. The shadow color should be tinted with `primary` (`#69daff`) to mimic the blue light of the stadium screens.
*   **The "Ghost Border" Fallback:** If a divider is essential for accessibility, use the `outline_variant` (`#434759`) at **15% opacity**. It should be barely felt, never seen as a hard line.
*   **Subtle Glows:** For "Live" cards, apply a 2px outer glow using the `secondary` (`#2ff801`) color at 30% opacity to draw the user’s eye to active competition.

---

## 5. Components

### Buttons
*   **Primary:** Gradient (`primary` to `primary_container`), `xl` roundedness (0.75rem), white text.
*   **Secondary:** Ghost style. No background, `outline` token at 20% opacity, `primary` colored text.
*   **States:** On hover, increase the gradient intensity and add a subtle `primary` glow.

### Cards & Lists
*   **Layout:** Forbid divider lines. Use `8` (2rem) spacing from the scale to separate list items.
*   **Glass Cards:** Use for overlays. `surface-container-highest` at 70% opacity + backdrop blur.
*   **Badges:** 
    *   **Live:** `secondary` background, `on_secondary` text.
    *   **Pending:** `inverse_on_surface` background, `on_surface` text.
    *   **Locked:** `tertiary_container` background, `on_tertiary_container` text.

### Input Fields
*   **Style:** Minimalist. `surface-container-highest` background. No border.
*   **Focus:** Transition to a 1px `primary` ghost border (20% opacity) and a subtle `primary` inner glow.

### Exclusive Component: The "Banter Rail"
A vertical, glassmorphic social feed that sits asymmetrically on the right side of the screen, allowing users to trash-talk in real-time without leaving their tournament dashboard.

---

## 6. Do's and Don'ts

### Do
*   **Do** use asymmetrical layouts. A 3-column grid where the center column is wider and slightly offset feels more dynamic than a centered container.
*   **Do** use `secondary` (`#2ff801`) sparingly. It is a high-energy laser; use it only for the most important "Live" triggers.
*   **Do** lean into `xl` roundedness for cards to soften the dark, aggressive color palette.

### Don't
*   **Don't** use 100% white text. Use `on_surface` (`#e1e4fa`) to reduce eye strain against the midnight background.
*   **Don't** use standard 1px borders. If you feel you need one, try a background color shift first.
*   **Don't** cram data. Use the spacing scale (`12` or `16`) to let the "Stadium" breathe.