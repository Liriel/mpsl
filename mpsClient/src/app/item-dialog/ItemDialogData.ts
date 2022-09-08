import { ShoppingListItem } from '../models/ShoppingListItem';


export class ItemDialogData {
  constructor(model?: Partial<ItemDialogData>) {
    Object.assign(this, model);
  }

  public itemId: number;
}
