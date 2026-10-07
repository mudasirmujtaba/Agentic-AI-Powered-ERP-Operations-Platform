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
  8px (modals, popovers, menus). **Containers and controls never exceed 8px.**
  - **Exception, `--radius-full` (9999px), only for things that are conventionally circular:** avatars, status
    dots, radio buttons, switch tracks and thumbs, spinners and progress rings. Nothing else is round.
- **Elevation:** 1px borders, not shadows. The only shadow is `--shadow-overlay`, for floating layers (menus,
  select panels, tooltips, dialogs).
- **Controls:** `--control-sm/md/lg` = 32/36/40px; inputs `--input-height` 40px; filter controls `--filter-height` 32px.
- **Avatars:** `--avatar-sm/md/lg` = 24/32/40px.

In Tailwind, use the semantic colour utilities: `text-fg`, `text-fg-secondary`, `text-muted`, `border-line`,
`border-line-strong`, `bg-surface`, `bg-surface-subtle`, `bg-surface-sunken`, `bg-brand-*`, `text-danger`,
`bg-danger-bg` (likewise success/warning/info). The raw palettes (`slate`, `red`, `amber`…) are re-mapped to tokens
only so legacy markup stays on-system; don't write new code with them.

### Component rules

- **Buttons** (Material directives): primary = `mat-flat-button`; secondary = `mat-stroked-button` (white, 1px
  border); ghost = `mat-button`; danger = `mat-flat-button class="btn-danger"`. Default height 36px;
  `btn-sm` 32px, `btn-lg` 40px. One primary action per view. Disabled buttons keep their variant at 50% opacity.
- **Form fields (real forms):** `mat-form-field` defaults are set in `app.config.ts` (outline, label always shown,
  dynamic subscript, no required marker). Labels render **above** the 40px box (14px/500). No asterisks: required
  fields stay unmarked, and every field whose control has no `Validators.required` gets
  `<mat-label>Notes <span class="optional">(optional)</span></mat-label>` (muted, weight 400). Focus shows the
  primary border plus a 3px `--focus-ring`. Stacked fields inside a `<form>` get 16px between them.
- **Filter bars (list pages, not forms):** filters above a table use the compact pattern, never labelled form
  fields:
  ```html
  <app-filter-bar [active]="list.hasFilters(['status'])" (clear)="list.resetFilters({ status: undefined })">
    <app-filter-search placeholder="Search orders…" [value]="list.query().search" (valueChange)="list.onSearch($event)" />
    <app-filter-select label="Status" [options]="statusOptions" [value]="list.query()['status']"
                       (valueChange)="list.setFilter({ status: $event })" />
    <mat-slide-toggle …>Late orders only</mat-slide-toggle>
  </app-filter-bar>
  ```
  One row, 8px gaps, wraps when narrow. Search is 280px wide and 32px tall, with a leading search icon and a
  "Search orders…" placeholder. Selects are 32px with no visible label: the trigger reads "Status: All" / "Status: Open",
  and the label doubles as the `aria-label`. A ghost "Clear filters" button (x icon) appears whenever a filter differs
  from its default. Components live in `shared/components/filter-bar/filter-bar.ts`; `hasFilters`/`resetFilters` are
  on `createPagedList`.
- **Switches** (`mat-slide-toggle`, styled globally): 32x18px track, 14px thumb inset 2px, fully rounded; off =
  `--gray-300` track, white thumb; on = `--primary-600` track; no ripple or thumb icon; disabled at 50% opacity;
  focus ring around the track; 150ms ease.
- **Avatars:** circular `.avatar` (32px; `.avatar-sm` 24px, `.avatar-lg` 40px). Without a photo, show initials
  (12px/600) on `--gray-100` with `--gray-700` text. Never a random colour per user.
- **Status dots:** `.status-dot` (6px) or `.status-dot-lg` (8px), circular, coloured by `currentColor`.
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
- Border radius above 8px on containers or controls (`rounded-xl`, `rounded-2xl`, `rounded-full` on a card, button or
  input). Circular items listed under Radius are the only exception. Heavy or coloured shadows.
- Emoji in the UI; decorative icon-in-coloured-circle tiles; "sparkle" AI iconography.
- Centred marketing-style layouts inside the app (the sign-in page is outside the app shell).
- More than one accent colour for non-status purposes.
- Hardcoded colours, spacing or radii in component styles.

### Migrate as you touch

Legacy markup still uses raw Tailwind palette classes (`text-slate-500`, `bg-red-50`, `text-amber-800`…) and off-grid
or arbitrary sizes (`text-[11px]`, `text-[13px]`, `py-2.5`, `gap-1.5`, `!h-[18px]`). They render on-system because
the palettes are mapped to tokens, but they are not the vocabulary. **Whenever a component is touched during a page
redesign, migrate its classes in the same change:** palette classes to the semantic utilities (`text-muted`,
`text-fg-secondary`, `bg-surface-sunken`, `bg-danger-bg`, `text-danger`…), and off-grid or arbitrary sizes to the
type scale and the 4px grid. Don't run a global sweep; convert component by component as pages are redesigned.

### Verifying UI changes

Run `npx ng build` (no errors or warnings) and `npx ng test --watch=false`, then look at the affected pages in a
browser at 1440px and 900px widths.
