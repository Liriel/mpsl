export class ModelBase<T>{
    constructor(model?: Partial<T>) {
      Object.assign(this, model);
    }

  }
  