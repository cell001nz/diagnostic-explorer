export interface DiagnosticResponse {
    processId: number;
    propertyBags: PropertyBag[];
    events: EventResponse[];
    operationSets: OperationSet[];
    context?: string;
    exceptionMessage?: string;
    exceptionDetail?: string;
    date: Date | string;
    serverDate: Date | string;
}

export interface DrillDownRequest {
    objectPaths: string[];
}

export interface DrillDownResponse {
    diagnostics: DiagnosticResponse;
    displayedCount: number;
    totalCount?: number;
    isTruncated: boolean;
    errorMessage?: string;
    errorDetail?: string;
}

export interface PropertyBag {
    name: string;
    category: string;
    operationSet: string;
    canDrillDown: boolean;
    categories: SubBag[];
}

export interface SubBag {
    name: string;
    operationSet: string;
    canDrillDown: boolean;
    properties: Property[];
}

export interface Property {
    name: string;
    value: string | null;
    description: string;
    operationSet: string;
    canSet: boolean;
    canDrillDown: boolean;
    drillDownIconOnly: boolean;
    alerts: PropertyAlert[];
}

export interface PropertyAlert {
    severity: number | 'None' | 'Warning' | 'Error';
    message: string;
    category: string;
}

export interface EventResponse {
    name: string;
    category: string;
    events: SystemEvent[];
}

export interface SystemEvent {
    id: number;
    sinkSeq: number;
    date: string | Date;
    message: string;
    detail: string;
    level: number;
    sinkName: string;
    sinkCategory: string;
}

export interface OperationSet {
    id: string;
    operations: Operation[];
}

export interface Operation {
    returnType: string;
    signature: string;
    description: string;
    parameters: OperationParameter[];
}

export interface OperationParameter {
    name: string;
    type: string;
}
