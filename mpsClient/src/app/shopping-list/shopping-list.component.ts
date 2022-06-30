import { Component, Inject, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { map, Observable, of, startWith } from 'rxjs';
import { FormHelper } from '../infrastructure/FormHelper';
import { ShoppingListItem } from '../models/ShoppingListItem';
import { IRepo, IRepoToken } from '../services/IRepo';

@Component({
  selector: 'app-shopping-list',
  templateUrl: './shopping-list.component.html',
  styleUrls: ['./shopping-list.component.scss']
})
export class ShoppingListComponent implements OnInit {

  public items: Observable<ShoppingListItem[]>;

  public formGroup: FormGroup = new FormGroup({
    name: new FormControl(''),
    amount: new FormControl(1),
    unit: new FormControl('')
  });
  options: string[] = ['One', 'Two', 'Three'];
  filteredOptions: Observable<string[]>;


  constructor(
    @Inject(IRepoToken) private repo: IRepo
  ){
    this.items = this.repo.GetShoppingListItems("asdf");
  }

  ngOnInit(): void {
    this.filteredOptions = this.formGroup.get("name").valueChanges.pipe(
      startWith(''),
      map(value => this.filter(value || '')),
    );
  }

  public async save(): Promise<void>{
    let item = new ShoppingListItem({ avatar: "", hint: "", name: "", amount: 0, unit: ""});
    FormHelper.UpdateModel(ShoppingListItem, item, this.formGroup);
    console.log(item);

    let result = await this.repo.AddOrUpdateItem(item).toPromise();
    this.formGroup.reset();
  }

  private filter(value: string): string[] {
    const filterValue = value.toLowerCase();

    return this.options.filter(option => option.toLowerCase().includes(filterValue));
  }

}
