export class DataResult<T>
{
    public results: T[];
    public totalRecordCount: number;

    constructor(model?: Partial<DataResult<T>>) {
        Object.assign(this, model);
    }
}