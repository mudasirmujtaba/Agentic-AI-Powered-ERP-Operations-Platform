import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import {
  Activity,
  ArrowLeft,
  ArrowLeftRight,
  ArrowUpRight,
  BadgeCheck,
  Banknote,
  Bell,
  ChartColumn,
  ChartLine,
  Check,
  ChevronDown,
  ChevronRight,
  CircleAlert,
  CircleCheck,
  ClipboardCheck,
  Clock,
  Download,
  ExternalLink,
  Factory,
  Headset,
  History,
  Hourglass,
  Info,
  LayoutDashboard,
  LogOut,
  LucideAngularModule,
  LucideIconData,
  MapPin,
  MessagesSquare,
  Package,
  Play,
  Plus,
  Receipt,
  ReceiptText,
  RefreshCw,
  Search,
  Send,
  Shapes,
  ShoppingCart,
  SlidersHorizontal,
  Trash2,
  TriangleAlert,
  Truck,
  Users,
  Warehouse,
  X,
} from 'lucide-angular';

/**
 * The application's icon set (Lucide), by name. Register an icon here before using it; importing only these keeps
 * the bundle small and the vocabulary consistent.
 */
const ICONS = {
  activity: Activity,
  'arrow-left': ArrowLeft,
  'arrow-left-right': ArrowLeftRight,
  'arrow-up-right': ArrowUpRight,
  'badge-check': BadgeCheck,
  banknote: Banknote,
  bell: Bell,
  'chart-column': ChartColumn,
  'chart-line': ChartLine,
  check: Check,
  'chevron-down': ChevronDown,
  'chevron-right': ChevronRight,
  'circle-alert': CircleAlert,
  'circle-check': CircleCheck,
  'clipboard-check': ClipboardCheck,
  clock: Clock,
  download: Download,
  'external-link': ExternalLink,
  factory: Factory,
  headset: Headset,
  history: History,
  hourglass: Hourglass,
  info: Info,
  'layout-dashboard': LayoutDashboard,
  'log-out': LogOut,
  'map-pin': MapPin,
  'messages-square': MessagesSquare,
  package: Package,
  play: Play,
  plus: Plus,
  receipt: Receipt,
  'receipt-text': ReceiptText,
  'refresh-cw': RefreshCw,
  search: Search,
  send: Send,
  shapes: Shapes,
  'shopping-cart': ShoppingCart,
  'sliders-horizontal': SlidersHorizontal,
  'trash-2': Trash2,
  'triangle-alert': TriangleAlert,
  truck: Truck,
  users: Users,
  warehouse: Warehouse,
  x: X,
} satisfies Record<string, LucideIconData>;

export type IconName = keyof typeof ICONS;

/**
 * Icon: `<app-icon name="plus" />`. 16px by default (dense UI: buttons, tables, inputs); pass `[size]="20"` for
 * navigation and standalone icons. Stroke 1.75; colour follows the surrounding text. Decorative by default
 * (aria-hidden); pass `label` when the icon carries meaning on its own.
 */
@Component({
  selector: 'app-icon',
  imports: [LucideAngularModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'app-icon',
    '[attr.aria-hidden]': 'label() ? null : "true"',
    '[attr.role]': 'label() ? "img" : null',
    '[attr.aria-label]': 'label() || null',
  },
  template: `<lucide-icon [img]="icon()" [size]="size()" [strokeWidth]="1.75" color="currentColor" />`,
  styles: `
    :host { display: inline-flex; flex-shrink: 0; align-items: center; justify-content: center; line-height: 0; color: inherit; }
    :host ::ng-deep svg { display: block; }
  `,
})
export class Icon {
  readonly name = input.required<IconName>();
  readonly size = input(16);
  readonly label = input<string>();

  protected readonly icon = computed(() => ICONS[this.name()]);
}
