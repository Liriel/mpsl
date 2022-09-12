import { IEntity } from '../models/IEntity';
import { ValidationError } from './ValidationError';

export class EntityOperationResult<T extends IEntity> {
  entity: T;
  validationErrors: ValidationError[];
  success: boolean;

  constructor(model?: Partial<EntityOperationResult<T>>) {
    Object.assign(this, model);
  }
}