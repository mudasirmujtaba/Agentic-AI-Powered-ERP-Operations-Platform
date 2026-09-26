import { Component, input } from '@angular/core';

@Component({
  selector: 'app-feature-placeholder',
  templateUrl: './feature-placeholder.html',
  styleUrl: './feature-placeholder.css',
})
export class FeaturePlaceholder {
  readonly title = input.required<string>();
}
