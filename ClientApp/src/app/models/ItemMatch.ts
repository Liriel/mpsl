/**
 * An existing item offered as a "did you mean" suggestion, or listed in a duplicate group.
 */
export interface ItemMatch {
  id: number;
  name: string;
  shortName: string;
  status: string;
  purchaseCount: number;
  lastPurchaseDate: string | null;
}
