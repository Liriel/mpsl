import { Component, Inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SelectionModel } from '@angular/cdk/collections';
import { Observable } from 'rxjs';
import { IRepo, IRepoToken } from '../services/IRepo';
import { IAppUiService, IAppUiServiceToken } from '../services';
import { DeclutterCandidate, ItemMatch, ShoppingList } from '../models';
import { ConfirmDeleteDialogComponent } from '../shared/confirm-delete-dialog/confirm-delete-dialog.component';
import { ConfirmDeleteDialogData } from '../shared/confirm-delete-dialog/confirm-delete-dialog.model';

/** One suggested set of duplicates, with the user's choice of survivor and exclusions. */
export interface MergeGroup {
  items: ItemMatch[];
  targetId: number;
  /** ids the user took out of the merge */
  excluded: Set<number>;
  isMerging: boolean;
}

@Component({
  selector: 'app-declutter',
  templateUrl: './declutter.component.html',
  styleUrls: ['./declutter.component.scss', '../shared-page.scss'],
  standalone: false
})
export class DeclutterComponent implements OnInit {
  public candidates: DeclutterCandidate[] = [];
  public isLoading = false;
  public shoppingListId: number;
  public shoppingListName: string = '';
  public isMobile: Observable<boolean>;

  // Merge duplicates
  public mergeGroups: MergeGroup[] = [];
  public isLoadingGroups = false;
  private groupsLoaded = false;

  // Filter settings
  public minMonthsSinceLastPurchase = 6;
  public maxPurchaseCount = 2;

  // Table configuration
  public displayedColumns: string[] = ['select', 'shortName', 'name', 'purchaseCount', 'lastPurchaseDate', 'daysSinceLastPurchase'];
  public selection = new SelectionModel<DeclutterCandidate>(true, []);

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    @Inject(IAppUiServiceToken) private appUiService: IAppUiService,
    private route: ActivatedRoute,
    private router: Router,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {
    this.isMobile = this.appUiService.IsMobile;
  }

  ngOnInit(): void {
    this.shoppingListId = parseInt(this.route.snapshot.paramMap.get('id') || '0', 10);

    // Load shopping list name
    this.repo.GetEntity<ShoppingList>('ShoppingList', this.shoppingListId).subscribe({
      next: (shoppingList) => {
        this.shoppingListName = shoppingList.name;
      },
      error: (error) => {
        console.error('Error loading shopping list:', error);
        this.shoppingListName = 'Shopping List';
      }
    });
    
    this.loadCandidates();
  }

  loadCandidates(): void {
    this.isLoading = true;
    this.repo.GetDeclutterCandidates(
      this.shoppingListId,
      this.minMonthsSinceLastPurchase,
      this.maxPurchaseCount
    ).subscribe({
      next: (candidates) => {
        this.candidates = candidates;
        this.selection.clear();
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading declutter candidates:', error);
        this.snackBar.open('Error loading candidates', 'Close', { duration: 3000 });
        this.isLoading = false;
      }
    });
  }

  applyFilters(): void {
    this.loadCandidates();
  }

  /** Whether the number of selected elements matches the total number of rows. */
  isAllSelected(): boolean {
    const numSelected = this.selection.selected.length;
    const numRows = this.candidates.length;
    return numSelected === numRows;
  }

  /** Selects all rows if they are not all selected; otherwise clear selection. */
  toggleAllRows(): void {
    if (this.isAllSelected()) {
      this.selection.clear();
      return;
    }

    this.selection.select(...this.candidates);
  }

  selectAll(): void {
    this.selection.select(...this.candidates);
  }

  selectNone(): void {
    this.selection.clear();
  }

  deleteSelected(): void {
    if (this.selection.selected.length === 0) {
      this.snackBar.open('No items selected', 'Close', { duration: 2000 });
      return;
    }

    const itemIds = this.selection.selected.map(c => c.id);
    
    const dialogRef = this.dialog.open(ConfirmDeleteDialogComponent, {
      data: new ConfirmDeleteDialogData({
        itemCount: itemIds.length,
        entityType: 'item'
      })
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result === true) {
        this.isLoading = true;
        this.repo.BulkDeleteItems(this.shoppingListId, itemIds).subscribe({
          next: (result) => {
            this.snackBar.open(`${result.deletedCount} item(s) deleted`, 'Close', { duration: 3000 });
            this.loadCandidates();
          },
          error: (error) => {
            console.error('Error deleting items:', error);
            this.snackBar.open('Error deleting items', 'Close', { duration: 3000 });
            this.isLoading = false;
          }
        });
      }
    });
  }

  /** Loaded on first expand: the matcher compares every item pair, so it is not free. */
  loadMergeGroups(): void {
    if (this.groupsLoaded || this.isLoadingGroups) {
      return;
    }

    this.isLoadingGroups = true;
    this.repo.GetDuplicateGroups(this.shoppingListId).subscribe({
      next: groups => {
        this.mergeGroups = groups.map(items => ({
          items: items,
          // the matcher sends the best-established item first
          targetId: items.reduce((best, i) => i.purchaseCount > best.purchaseCount ? i : best, items[0]).id,
          excluded: new Set<number>(),
          isMerging: false
        }));
        this.groupsLoaded = true;
        this.isLoadingGroups = false;
      },
      error: () => this.isLoadingGroups = false
    });
  }

  sourcesOf(group: MergeGroup): ItemMatch[] {
    return group.items.filter(i => i.id !== group.targetId);
  }

  mergeCount(group: MergeGroup): number {
    return this.sourcesOf(group).filter(i => !group.excluded.has(i.id)).length;
  }

  selectTarget(group: MergeGroup, id: number): void {
    group.targetId = id;
    group.excluded.delete(id);
  }

  toggleSource(group: MergeGroup, id: number): void {
    if (group.excluded.has(id)) {
      group.excluded.delete(id);
    } else {
      group.excluded.add(id);
    }
  }

  targetName(group: MergeGroup): string {
    return group.items.find(i => i.id === group.targetId)?.name ?? '';
  }

  merge(group: MergeGroup): void {
    const sourceIds = this.sourcesOf(group).filter(i => !group.excluded.has(i.id)).map(i => i.id);
    if (sourceIds.length === 0) {
      return;
    }

    group.isMerging = true;
    this.repo.MergeItems(this.shoppingListId, group.targetId, sourceIds).subscribe({
      next: () => {
        this.snackBar.open(`Merged ${sourceIds.length} item(s) into ${this.targetName(group)}`, 'Close', { duration: 3000 });
        this.mergeGroups = this.mergeGroups.filter(g => g !== group);
        // the merged-away items may have been declutter candidates too
        this.loadCandidates();
      },
      // the interceptor reports 5xx/network; a 400 from the server has nothing useful for the
      // user to fix here beyond reloading the suggestions
      error: () => {
        group.isMerging = false;
        this.snackBar.open('Could not merge these items', 'Close', { duration: 3000 });
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/list', this.shoppingListId]);
  }

  formatDate(date: string | null): string {
    if (!date) return 'Never';
    const d = new Date(date);
    return d.toLocaleDateString();
  }

  formatDaysAgo(days: number | null): string {
    if (days === null) return 'Never';
    if (days === 0) return 'Today';
    if (days === 1) return '1 day ago';
    if (days < 30) return `${days} days ago`;
    if (days < 60) return '~1 month ago';
    if (days < 365) return `~${Math.floor(days / 30)} months ago`;
    return `~${Math.floor(days / 365)} year(s) ago`;
  }
}
