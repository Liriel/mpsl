import { Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { FormControl, FormGroup, Validators } from '@angular/forms';

@Component({
  selector: 'app-create-shopping-list-dialog',
  templateUrl: './create-shopping-list-dialog.component.html',
})
export class CreateShoppingListDialogComponent {
  formGroup = new FormGroup({
    name: new FormControl('', [Validators.required, Validators.maxLength(50)])
  });
  existingNames: string[];

  constructor(
    public dialogRef: MatDialogRef<CreateShoppingListDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { existingNames: string[] }
  ) {
    this.existingNames = data.existingNames;
  }

  onNoClick(): void {
    this.dialogRef.close();
  }

  onCreate(): void {
    const name = this.formGroup.value.name?.trim();
    if (!name || this.existingNames.map(n => n.toLowerCase()).includes(name.toLowerCase())) {
      this.formGroup.controls['name'].setErrors({ notUnique: true });
      return;
    }
    this.dialogRef.close(name);
  }
}
