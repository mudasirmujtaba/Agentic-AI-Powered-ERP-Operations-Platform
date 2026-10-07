# OpsPilot — working notes for Claude

OpsPilot is an operations platform (sales, supply chain, inventory, purchasing, invoicing, service, finance):
.NET 10 API (`backend/`), Python AI service (`ai-service/`), Angular 21 frontend (`frontend/`). See README.md for
architecture, setup and tests.

Commit messages carry no AI attribution (no `Co-Authored-By` trailers).

## Design system

The users are operations, finance and sales staff who work in OpsPilot all day. The UI is dense, calm and
neutral-led: one accent colour, hairline borders, no decoration. Every frontend change follows these rules.

### Where it lives

| File | Contents |
|---|---|
| `frontend/src/styles/_tokens.scss` | All design tokens as CSS custom properties on `:root`. The single source of truth. |
| `frontend/src/styles/main.scss` | Angular Material theme mapped onto the tokens; base element styles; buttons; form fields; overlays. |
| `frontend/src/styles.css` | Tailwind, with its theme mapped onto the tokens; shared classes `.panel`, `.panel-header`, `.panel-title`, `.panel-body`, `.data-table`, `.kpi*`, `.details`. |
| `frontend/src/app/shared/components/icon/icon.ts` | `<app-icon>` and the registry of Lucide icons the app uses. |
| `frontend/src/app/shared/components/logo/logo.ts` | `<app-logo>`; the mark also lives in `frontend/src/assets/brand/logomark.svg`. |

### Tokens (use them; never hardcode)

- **Neutrals:** `--gray-0` … `--gray-900` (0, 25, 50, 100–900). **Ink** for dark surfaces: `--ink-950/900/800`.
- **Primary (the only accent):** `--primary-50/100/500/600/700`. Solid actions use `--primary-600`.
- **Semantic (status only, never decoration):** `--success`, `--warning`, `--danger`, `--info`, each with a
  `-bg` partner (e.g. `--danger-bg`).
- **Aliases (prefer these):** `--text-primary`, `--text-secondary`, `--text-muted`, `--border`, `--border-strong`,
  `--surface`, `--surface-subtle`, `--surface-sunken`, `--surface-hover`, `--surface-selected`, `--focus-ring`.
- **Type:** Inter Variable (`@fontsource-variable/inter`, bundled) with `cv11`, `ss01`. Scale (size/line height):
  12/16, 13/18, **14/20 (body)**, 16/24, 18/26, 20/28, 24/32, 30/38 → `--text-xs` … `--text-3xl` with
  `--leading-*`. Weights 400, 500, 600 only. `--tracking-tight` (-0.01em) on headings 20px and up.
- **Numbers** in tables, amounts and KPIs use `font-variant-numeric: tabular-nums` (`.numeric`, `.tabular-nums`;
  automatic in `td` and `.kpi-value`).
- **Spacing:** 4px grid only: `--space-1/2/3/4/5/6/8/10/12/16` = 4, 8, 12, 16, 20, 24, 32, 40, 48, 64px. Tailwind
  spacing steps map 1:1 (`p-4` = 16px); avoid half steps (`py-2.5`, `gap-1.5`).
- **Radius:** `--radius-sm` 4px (badges, checkboxes), `--radius-md` 6px (inputs, buttons, cards), `--radius-lg`
  8px (modals, popovers, menus). Nothing larger, anywhere.
- **Elevation:** 1px borders, not shadows. The only shadow is `--shadow-overlay`, for floating layers (menus,
  select panels, tooltips, dialogs).
- **Controls:** `--control-sm/md/lg` = 32/36/40px; inputs `--input-height` 40px.

In Tailwind, use the semantic colour utilities: `text-fg`, `text-fg-secondary`, `text-muted`, `border-line`,
`border-line-strong`, `bg-surface`, `bg-surface-subtle`, `bg-surface-sunken`, `bg-brand-*`, `text-danger`,
`bg-danger-bg` (likewise success/warning/info). The raw palettes (`slate`, `red`, `amber`…) are re-mapped to tokens
only so legacy markup stays on-system; don't write new code with them.

### Component rules

- **Buttons** (Material directives): primary = `mat-flat-button`; secondary = `mat-stroked-button` (white, 1px
  border); ghost = `mat-button`; danger = `mat-flat-button class="btn-danger"`. Default height 36px;
  `btn-sm` 32px, `btn-lg` 40px. One primary action per view. Disabled buttons keep their variant at 50% opacity.
- **Form fields:** `mat-form-field` defaults are set in `app.config.ts` (outline, label always shown, dynamic
  subscript, no required marker). Labels render **above** the 40px box (14px/500). Don't add asterisks; label
  optional fields with "(optional)". Focus shows the primary border plus a 3px `--focus-ring`.
- **Focus:** every interactive element has a visible `:focus-visible` ring. Never remove outlines without a
  replacement.
- **Icons:** Lucide only, through `<app-icon name="…">`. 16px in dense UI (buttons, tables, inputs), `[size]="20"`
  for navigation and standalone icons; stroke 1.75; colour inherits. Register new icons in `icon.ts`. No
  `mat-icon`, no icon fonts, no emoji.
- **Status:** `<app-status-badge [label] [tone]>` with tones success/warning/danger/info/neutral. Status is always
  text plus colour, never colour alone.
- **Surfaces:** content sits on `.panel` (white, 1px `--border`, 6px radius) on the sunken page background.
  Tables use `mat-table` or `.data-table`; KPIs use `.kpi`.
- **Brand:** `<app-logo variant="light|dark" [showWordmark]="true" [size]="24">`. Use `variant="dark"` on ink
  surfaces. Don't recreate the mark by hand.
- **Component styles** reference tokens (`var(--…)`) only: no hex, rgb or named colours, no magic radii or
  shadows.

### Banned patterns (remove where found, never introduce)

- Gradients on backgrounds, buttons or text; glassmorphism (`backdrop-filter` blur); glow effects.
- Border radius above 8px (`rounded-xl`, `rounded-full` on surfaces, `50%` avatars); heavy or coloured shadows.
- Emoji in the UI; decorative icon-in-coloured-circle tiles; "sparkle" AI iconography.
- Centred marketing-style layouts inside the app (the sign-in page is outside the app shell).
- More than one accent colour for non-status purposes.
- Hardcoded colours, spacing or radii in component styles.

### Verifying UI changes

Run `npx ng build` (no errors or warnings) and `npx ng test --watch=false`, then look at the affected pages in a
browser at 1440px and 900px widths.
