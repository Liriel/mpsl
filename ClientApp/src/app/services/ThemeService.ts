import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, combineLatest } from 'rxjs';
import { map, distinctUntilChanged } from 'rxjs/operators';
import { IThemeService, ThemeMode } from './IThemeService';

@Injectable()
export class ThemeService implements IThemeService {
  private readonly THEME_STORAGE_KEY = 'mps-theme-mode';
  
  private themeModeSubject = new BehaviorSubject<ThemeMode>('auto');
  private systemThemeSubject = new BehaviorSubject<'light' | 'dark'>('light');

  public readonly themeMode$ = this.themeModeSubject.asObservable();
  public readonly currentTheme$: Observable<'light' | 'dark'>;

  constructor() {
    // Initialize system theme detection
    this.initSystemThemeDetection();
    
    // Load saved theme preference
    this.loadThemePreference();

    // Combine user preference with system theme
    this.currentTheme$ = combineLatest([
      this.themeModeSubject,
      this.systemThemeSubject
    ]).pipe(
      map(([mode, systemTheme]) => {
        if (mode === 'auto') {
          return systemTheme;
        }
        return mode;
      }),
      distinctUntilChanged()
    );

    // Apply theme changes to document
    this.currentTheme$.subscribe(theme => {
      this.applyTheme(theme);
    });
  }

  setTheme(mode: ThemeMode): void {
    this.themeModeSubject.next(mode);
    this.saveThemePreference(mode);
  }

  toggleTheme(): void {
    const currentMode = this.themeModeSubject.value;
    let newMode: ThemeMode;
    
    switch (currentMode) {
      case 'light':
        newMode = 'dark';
        break;
      case 'dark':
        newMode = 'auto';
        break;
      case 'auto':
      default:
        newMode = 'light';
        break;
    }
    
    this.setTheme(newMode);
  }

  private initSystemThemeDetection(): void {
    // Check initial system preference
    if (window.matchMedia) {
      const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      this.systemThemeSubject.next(mediaQuery.matches ? 'dark' : 'light');
      
      // Listen for system theme changes
      mediaQuery.addEventListener('change', (e) => {
        this.systemThemeSubject.next(e.matches ? 'dark' : 'light');
      });
    }
  }

  private loadThemePreference(): void {
    try {
      const savedTheme = localStorage.getItem(this.THEME_STORAGE_KEY);
      if (savedTheme && this.isValidThemeMode(savedTheme)) {
        this.themeModeSubject.next(savedTheme as ThemeMode);
      }
    } catch (error) {
      console.warn('Failed to load theme preference from localStorage:', error);
    }
  }

  private saveThemePreference(mode: ThemeMode): void {
    try {
      localStorage.setItem(this.THEME_STORAGE_KEY, mode);
    } catch (error) {
      console.warn('Failed to save theme preference to localStorage:', error);
    }
  }

  private applyTheme(theme: 'light' | 'dark'): void {
    const body = document.body;
    
    // Remove existing theme classes
    body.classList.remove('light-theme', 'dark-theme');
    
    // Add new theme class
    body.classList.add(`${theme}-theme`);
    
    // Update meta theme-color for mobile browsers
    const metaThemeColor = document.querySelector('meta[name="theme-color"]');
    if (metaThemeColor) {
      metaThemeColor.setAttribute('content', theme === 'dark' ? '#303030' : '#ffffff');
    }
  }

  private isValidThemeMode(value: string): boolean {
    return ['light', 'dark', 'auto'].includes(value);
  }
}