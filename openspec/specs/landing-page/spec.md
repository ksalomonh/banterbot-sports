# Landing Page Spec

## Requirements

### Requirement: Landing Page Layout and Chrome

The anonymous landing page (`/`) MUST render as a full-viewport hero experience without standard application chrome.

#### Scenario: Anonymous user visits landing page

- GIVEN the user is not authenticated
- WHEN they navigate to `/`
- THEN the page MUST NOT display the topbar
- AND the page MUST NOT display the Banter Rail / chat rail
- AND the page MUST NOT display the mobile bottom nav
- AND the hero background image MUST fill the entire viewport height (`min-h-screen`) on both desktop and mobile
- AND the logo MUST be visible as a floating overlay in the top-left corner

#### Scenario: Existing authenticated redirection

- GIVEN the user is authenticated
- WHEN they navigate to `/`
- THEN they MUST be redirected according to existing behavior
- AND they MUST NOT see the anonymous landing page layout

#### Scenario: Application chrome remains on other pages

- GIVEN any user (anonymous or authenticated)
- WHEN they navigate to any page other than `/`
- THEN the standard application chrome (topbar, bottom nav, Banter Rail) MUST render as expected