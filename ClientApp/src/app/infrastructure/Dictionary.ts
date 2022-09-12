/**
 * @author Philipp Lasslesberger
 * @description
 * Provides a c# dictionary like interface to associative arrays
 * https://stackoverflow.com/a/45188213
 */

export interface Dictionary<T> {
  [Key: string]: T;
}
