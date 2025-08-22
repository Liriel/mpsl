import { Component, Inject, OnInit } from '@angular/core';
import { FormControl, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { MatBottomSheetRef } from '@angular/material/bottom-sheet';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ShoppingList } from '../models/ShoppingList';
import { IRepo, IRepoToken } from '../services';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';

@Component({
    selector: 'app-create-list-drawer',
    templateUrl: './create-list-drawer.component.html',
    styleUrls: ['./create-list-drawer.component.scss'],
    standalone: false
})
export class CreateListDrawerComponent implements OnInit {
  createForm: FormGroup;
  isLoading = false;
  existingLists: ShoppingList[] = [];

  constructor(
    private bottomSheetRef: MatBottomSheetRef<CreateListDrawerComponent>,
    @Inject(IRepoToken) private repo: IRepo
  ) {
    this.createForm = new FormGroup({
      name: new FormControl('', [
        Validators.required,
        Validators.minLength(1),
        this.uniqueNameValidator.bind(this)
      ])
    });
  }

  ngOnInit() {
    this.loadExistingLists();
  }

  private loadExistingLists() {
    this.repo.GetEntities<ShoppingList>("ShoppingList").subscribe(
      result => {
        this.existingLists = result.results || [];
      }
    );
  }

  private uniqueNameValidator(control: AbstractControl): ValidationErrors | null {
    if (!control.value || !this.existingLists) {
      return null;
    }
    
    const nameExists = this.existingLists.some(
      list => list.name?.toLowerCase() === control.value.toLowerCase()
    );
    
    return nameExists ? { uniqueName: true } : null;
  }

  onSubmit() {
    if (this.createForm.valid && !this.isLoading) {
      this.isLoading = true;
      const newList = new ShoppingList();
      newList.name = this.createForm.get('name')?.value.trim();
      
      this.repo.SaveEntity<ShoppingList>("ShoppingList", newList).subscribe({
        next: (result) => {
          this.bottomSheetRef.dismiss(result.entity);
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Error creating shopping list:', error);
          this.isLoading = false;
        }
      });
    }
  }

  onCancel() {
    this.bottomSheetRef.dismiss();
  }

  get nameControl() {
    return this.createForm.get('name');
  }

  getErrorMessage() {
    if (this.nameControl?.hasError('required')) {
      return 'List name is required';
    }
    if (this.nameControl?.hasError('uniqueName')) {
      return 'A list with this name already exists';
    }
    return '';
  }
}
