
export class DiagnosticResponse {
    propertyBags: PropertyBag[] = [];
    events: EventResponse[] = [];
    operationSets: OperationSet[] = [];
    context:  | null = null;
    exceptionMessage?: string;
    exceptionDetail?: string;
}

export class PropertyBag {
    name: string = '';
    category: string = '';
    operationSet: string = '';
    categories: Category[] = [];
}

export class Category {
    name = '';
    operationSet = '';
    properties: Property[] = [];
}

export class Property {
    name = '';
    value = '';
    description = '';
    operationSet = '';
    canSet = false;
}

export class EventResponse {
    name = '';
    category = '';
    events: SystemEvent[] = []
}

export class SystemEvent {
    id = 0;
    date = Date.parse('1 Jan 2000');
    message = '';
    detail = '';
    level = 0;
    sinkName = '';
    sinkCategory = '';
}


export class OperationSet {
    id = '';
    operations: Operation[] = [];
}

export class Operation {
    returnType = ''
    signature = '';
    description = '';
    parameters: OperationParameter[] = [];
}


export class OperationParameter {
    name = '';
    type = '';
}
