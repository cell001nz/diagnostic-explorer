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
    jsonHover?: boolean;
    excludeEventViews?: boolean;
}

export interface DrillDownResponse {
    diagnostics: DiagnosticResponse;
    displayedCount: number;
    totalCount?: number;
    isTruncated: boolean;
    errorMessage?: string;
    errorDetail?: string;
    eventViews: DrillDownEventViewDefinition[];
    json?: string;
}

export interface DrillDownEventMatcher {
    loggerName: string;
    matchMode: number;
    minLevel?: number;
    maxLevel?: number;
}

export interface DrillDownEventViewDefinition {
    id: string;
    category: string;
    name: string;
    matchers: DrillDownEventMatcher[];
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
    valueKind?: number | string;
    description: string;
    operationSet: string;
    canSet: boolean;
    canDrillDown: boolean;
    drillDownIconOnly: boolean;
    drillDownText?: string;
    canJsonHover?: boolean;
    canExpandedHover?: boolean;
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

export interface LogStreamEvent {
    streamId: string;
    sequence: number;
    timestampUtc: string | Date;
    loggerCategory: string;
    level: number;
    message: string;
    detail: string;
    eventId: number;
    eventName?: string;
}

export interface LogStreamRouteValue {
    source: number | string;
    value?: string;
}

export interface LogStreamRouteDestination {
    category: LogStreamRouteValue;
    name: LogStreamRouteValue;
}

export interface LogStreamRoute {
    order: number;
    loggerName: string;
    loggerNameMatchMode: number | string;
    minLevel?: number;
    maxLevel?: number;
    stopProcessing: boolean;
    destinations: LogStreamRouteDestination[];
}

export interface LogStreamRoutingConfiguration {
    matchMode: number | string;
    routes: LogStreamRoute[];
}

export interface LogStreamInitialization {
    streamId: string;
    routing: LogStreamRoutingConfiguration;
    replayEvents: LogStreamEvent[];
    highWatermark: number;
    maxEvents?: number;
    maxAgeMinutes?: number;
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
