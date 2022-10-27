export interface IFilterableEvent {
    level: number;
    machine: string;
    user: string;
    process: string;
    message: string;
    detail: string;
}
