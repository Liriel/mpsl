/**
 * Represents a shopping list item that is a candidate for de-cluttering.
 */
export interface DeclutterCandidate {
  /**
   * The shopping list item ID.
   */
  id: number;

  /**
   * The shopping list ID this item belongs to.
   */
  shoppingListId: number;

  /**
   * The item name.
   */
  name: string;

  /**
   * The item's short name (avatar).
   */
  shortName: string;

  /**
   * "Product" or "Task".
   */
  kind: string;

  /**
   * Total number of times this item has been purchased.
   */
  purchaseCount: number;

  /**
   * The date this item was last purchased. Null if never purchased.
   */
  lastPurchaseDate: string | null;

  /**
   * Number of days since the item was last purchased. Null if never purchased.
   */
  daysSinceLastPurchase: number | null;
}

/**
 * Filter parameters for identifying de-clutter candidates.
 */
export interface DeclutterFilters {
  /**
   * Minimum months since last purchase (default: 6).
   */
  minMonthsSinceLastPurchase: number;

  /**
   * Maximum purchase count to be considered rarely purchased (default: 2).
   */
  maxPurchaseCount: number;
}
