export class ConfirmDeleteDialogData {
  constructor(init?: Partial<ConfirmDeleteDialogData>) {
    Object.assign(this, init);
  }

  /**
   * Number of items to be deleted
   */
  public itemCount: number = 0;

  /**
   * Optional custom title for the dialog
   */
  public customTitle?: string;

  /**
   * Optional custom message for the dialog
   */
  public customMessage?: string;

  /**
   * Entity type being deleted (e.g., "item", "list", "user")
   */
  public entityType?: string = 'item';
}
