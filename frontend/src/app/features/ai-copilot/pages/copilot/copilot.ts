import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnInit, inject, input, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';

import { describeApiError } from '../../../../core/http/api-error';
import { AssistantMessage } from '../../components/assistant-message/assistant-message';
import { AiApi } from '../../data/ai.api';
import { AiConversationSummary, AiMessage } from '../../data/ai.models';
import { Icon } from '../../../../shared/components/icon/icon';
import { Logo } from '../../../../shared/components/logo/logo';

const SUGGESTIONS = [
  'Which products are running low?',
  'Why is SO-10044 delayed?',
  'Are we going to run out of X200?',
  'Prepare a purchase recommendation for products at risk of stocking out.',
  'Which customers spent the most in the last 90 days?',
  'What is the approval policy for purchases above $10,000?',
];

@Component({
  selector: 'app-copilot',
  imports: [Logo, Icon, DatePipe, FormsModule, RouterLink, MatButtonModule, MatProgressBarModule, AssistantMessage],
  templateUrl: './copilot.html',
  styleUrl: './copilot.css',
})
export class Copilot implements OnInit {
  /** Bound from the optional `:conversationId` route param. */
  readonly conversationId = input<string>();

  private readonly api = inject(AiApi);
  private readonly router = inject(Router);
  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');

  protected readonly suggestions = SUGGESTIONS;
  protected readonly conversations = signal<AiConversationSummary[]>([]);
  protected readonly messages = signal<AiMessage[]>([]);
  protected readonly activeId = signal<string | null>(null);
  protected readonly thinking = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected draft = '';

  ngOnInit(): void {
    this.refreshConversations();
    const id = this.conversationId();
    if (id) this.open(id);
  }

  protected open(id: string): void {
    this.activeId.set(id);
    this.loading.set(true);
    this.error.set(null);
    this.api.conversation(id).subscribe({
      next: (conversation) => {
        this.messages.set(conversation.messages);
        this.loading.set(false);
        this.scrollToEnd();
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(describeApiError(err));
      },
    });
  }

  protected newConversation(): void {
    this.activeId.set(null);
    this.messages.set([]);
    this.error.set(null);
    this.router.navigate(['/ai-copilot']);
  }

  protected ask(text?: string): void {
    const message = (text ?? this.draft).trim();
    if (!message || this.thinking()) return;

    this.draft = '';
    this.error.set(null);
    const pending: AiMessage = { id: 'pending', role: 'User', content: message, metadata: null, createdAtUtc: new Date().toISOString(), action: null };
    this.messages.update((m) => [...m, pending]);
    this.thinking.set(true);
    this.scrollToEnd();

    this.api.chat(message, this.activeId()).subscribe({
      next: (response) => {
        this.thinking.set(false);
        this.messages.update((m) => [...m.filter((x) => x.id !== 'pending'), ...response.messages]);
        if (this.activeId() !== response.conversationId) {
          this.activeId.set(response.conversationId);
          this.router.navigate(['/ai-copilot', response.conversationId], { replaceUrl: true });
          this.refreshConversations();
        }
        this.scrollToEnd();
      },
      error: (err) => {
        this.thinking.set(false);
        this.messages.update((m) => m.filter((x) => x.id !== 'pending'));
        this.draft = message;
        this.error.set(describeApiError(err));
      },
    });
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.ask();
    }
  }

  /** After a decision the ERP appends the resumed agent's follow-up; reload to show it. */
  protected onActionDecided(): void {
    const id = this.activeId();
    if (id) this.open(id);
  }

  private refreshConversations(): void {
    this.api.conversations().subscribe({ next: (list) => this.conversations.set(list), error: () => {} });
  }

  private scrollToEnd(): void {
    setTimeout(() => {
      const element = this.scroller()?.nativeElement;
      if (element) element.scrollTop = element.scrollHeight;
    });
  }
}
