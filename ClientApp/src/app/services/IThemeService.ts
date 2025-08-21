import { InjectionToken } from "@angular/core";
import { Observable } from "rxjs";

export type ThemeMode = 'light' | 'dark' | 'auto';

export let IThemeServiceToken = new InjectionToken<IThemeService>("IThemeService");

export interface IThemeService {
  currentTheme$: Observable<'light' | 'dark'>;
  themeMode$: Observable<ThemeMode>;
  setTheme(mode: ThemeMode): void;
  toggleTheme(): void;
}