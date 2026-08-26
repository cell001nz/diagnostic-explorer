import { SystemEvent } from '@domain/DiagResponse';

export class SetPropertyRequest {
    objectPaths?: string[];
    path = '';
    value = '';
}

export class OperationResponse {
    isSuccess = false;
    result = '';
    message = '';
    detail = '';
}

export interface LoadEventData {
    requestId: string;
    processId: string;
    clientId: string;
    events: SystemEvent[];
}

export class OperationRequest {
    objectPaths?: string[];
    path = '';
    operation = '';
    arguments: string[] = [];
}
