import { Component, Inject, ChangeDetectorRef } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { BehaviorSubject } from 'rxjs';
import { ShoppingList } from '../models/ShoppingList';
import { IRepoToken, IRepo } from '../services/IRepo';

@Component({
  selector: 'app-shopping-list-dialog',
  templateUrl: './shopping-list-dialog.component.html',
  styleUrls: ['./shopping-list-dialog.component.scss']
})
export class ShoppingListDialogComponent {
  public isLoading: BehaviorSubject<boolean> = new BehaviorSubject(false);
  public nameExistsError: string | null = null;

  public formGroup: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(50)])
  });

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    public dialogRef: MatDialogRef<ShoppingListDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: any,
    private cdr: ChangeDetectorRef
  ) {}

  onNoClick(): void {
    this.dialogRef.close();
  }

  public save(): void {
    if (!this.formGroup.valid) return;

    const name = this.formGroup.get('name')?.value;
    
    // First check if name already exists
    this.isLoading.next(true);
    this.nameExistsError = null;
    this.cdr.markForCheck(); // Trigger change detection when clearing error

    this.repo.Get<any>(`api/shoppinglist?name=${encodeURIComponent(name)}`).subscribe({
      next: (result) => {
        // Check if any shopping list with this name already exists
        if (result && result.results && result.results.length > 0) {
          this.nameExistsError = 'A shopping list with this name already exists';
          this.cdr.markForCheck(); // Trigger change detection for OnPush components
          this.isLoading.next(false);
          return;
        }

        // Name is unique, create the shopping list
        const shoppingList = new ShoppingList({ name: name });
        this.repo.Post('api/shoppinglist', '', shoppingList).subscribe({
          next: (createResult) => {
            this.isLoading.next(false);
            this.dialogRef.close(createResult);
          },
          error: (error) => {
            this.isLoading.next(false);
            console.error('Error creating shopping list:', error);
          }
        });
      },
      error: (error) => {
        this.isLoading.next(false);
        console.error('Error checking name uniqueness:', error);
      }
    });
  }
}