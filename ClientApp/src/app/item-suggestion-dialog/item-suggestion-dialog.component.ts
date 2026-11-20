import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ItemMatch } from '../models';

export interface ItemSuggestionDialogData {
  /** what the user typed */
  name: string;
  suggestions: ItemMatch[];
}

/**
 * "Meintest du ...?" - closes with the chosen existing name, with null to keep the typed name,
 * or with undefined (backdrop / Escape) to go back to the form without adding anything.
 */
@Component({
  selector: 'app-item-suggestion-dialog',
  templateUrl: './item-suggestion-dialog.component.html',
  styleUrls: ['./item-suggestion-dialog.component.scss'],
  standalone: false
})
export class ItemSuggestionDialogComponent {
  readonly data = inject<ItemSuggestionDialogData>(MAT_DIALOG_DATA);
}
