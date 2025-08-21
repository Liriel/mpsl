import { Component, Inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';
import { IThemeService, IThemeServiceToken, ThemeMode } from '../services/IThemeService';

@Component({
  selector: 'app-theme-toggle',
  templateUrl: './theme-toggle.component.html',
  styleUrls: ['./theme-toggle.component.scss']
})
export class ThemeToggleComponent {
  currentTheme$: Observable<'light' | 'dark'>;
  themeMode$: Observable<ThemeMode>;

  constructor(
    @Inject(IThemeServiceToken) private themeService: IThemeService
  ) {
    this.currentTheme$ = this.themeService.currentTheme$;
    this.themeMode$ = this.themeService.themeMode$;
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  setTheme(mode: ThemeMode): void {
    this.themeService.setTheme(mode);
  }

  getThemeIcon(mode: ThemeMode): string {
    switch (mode) {
      case 'light':
        return 'light_mode';
      case 'dark':
        return 'dark_mode';
      case 'auto':
      default:
        return 'settings_brightness';
    }
  }

  getThemeTooltip(mode: ThemeMode): string {
    switch (mode) {
      case 'light':
        return 'Switch to Dark Mode';
      case 'dark':
        return 'Switch to Auto Mode';
      case 'auto':
      default:
        return 'Switch to Light Mode';
    }
  }
}
