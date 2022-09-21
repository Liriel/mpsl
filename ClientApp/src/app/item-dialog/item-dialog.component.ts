import { Component, Inject, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ShoppingListItem } from '../models/ShoppingListItem';
import { IRepoToken, IRepo } from '../services/IRepo';
import { ItemDialogData } from './ItemDialogData';
import { FormHelper } from '../infrastructure/FormHelper';
import { trigger, state, style, transition, animate } from '@angular/animations';

@Component({
  selector: 'app-item-dialog',
  templateUrl: './item-dialog.component.html',
  styleUrls: ['./item-dialog.component.scss'],
  animations: [
    trigger("flyInOut", [
      state("in", style({ left: 0 })),
      state("out", style({ left: "200%" })),

      transition("* => out", [animate("300ms ease-in")]),
      transition("* => in", [animate("100ms ease-in")])
    ]),
    trigger("flyOutIn", [
      state("in", style({ left: "-150%" })),
      state("out", style({ left: 0 })),

      transition("* => out", [animate("300ms ease-in")]),
      transition("* => in", [animate("100ms ease-in")])
    ]),
    trigger("fadeInOut", [
      state("0", style({ opacity: "1" })),
      transition("* => 1", [
        animate("500ms ease-out", style({ opacity: "0" }))
      ])
    ])
  ]
})
export class ItemDialogComponent {
  public item: ShoppingListItem;
  public flyInOut: string = "in";

  public formGroup: FormGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    hint: new FormControl('', [Validators.maxLength(100)]),
    amount: new FormControl(null, [Validators.min(1)]),
    unitShortName: new FormControl('', [Validators.maxLength(5)]),
  });

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    @Inject(MAT_DIALOG_DATA) private data: ItemDialogData,
    public dialogRef: MatDialogRef<ItemDialogComponent>,
  ) {
    this.repo.GetEntity<ShoppingListItem>("item", this.data.itemId)
    .subscribe(i => {
      this.item = i;
      FormHelper.ReadModel(i, this.formGroup);
    });
  }

  onNoClick(): void {
    this.dialogRef.close();
  }

  onDeleteClick(): void{
    this.repo.Delete("api/item", this.item.id.toString()).subscribe(result =>
      this.dialogRef.close(result)
    );
  }

  onRemoveClick(): void{
    this.repo.Delete("api/shoppinglist", this.item.shoppingListId + "/remove/" + this.item.id).subscribe(result =>
      this.dialogRef.close(result)
    );
  }

  public save(): void {
    FormHelper.UpdateModel(ShoppingListItem, this.item, this.formGroup);
    this.repo.SaveEntity("item", this.item).subscribe(result =>
      this.dialogRef.close(result)
    );
  }
}