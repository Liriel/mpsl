import { Component, Inject, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ShoppingListItem } from '../models/ShoppingListItem';
import { IRepoToken, IRepo } from '../services/IRepo';
import { ItemDialogData } from './ItemDialogData';
import { FormHelper } from '../infrastructure/FormHelper';

@Component({
  selector: 'app-item-dialog',
  templateUrl: './item-dialog.component.html',
  styleUrls: ['./item-dialog.component.scss']
})
export class ItemDialogComponent {
  public item: ShoppingListItem;

  public formGroup: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    hint: new FormControl('', [Validators.maxLength(250)]),
    amount: new FormControl(null, [Validators.min(1)]),
    unit: new FormControl('', [Validators.maxLength(5)]),
  });

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    @Inject(MAT_DIALOG_DATA) private data: ItemDialogData,
    public dialogRef: MatDialogRef<ItemDialogComponent>,
  ) {
    this.repo.GetShoppingListItemById(this.data.itemId)
    .subscribe(i => {
      this.item = i;
      FormHelper.ReadModel(i, this.formGroup);
    });
  }

  onNoClick(): void {
    this.dialogRef.close();
  }

  public save(): void {
    FormHelper.UpdateModel(ShoppingListItem, this.item, this.formGroup);
    this.dialogRef.close(this.item);
  }
}