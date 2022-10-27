import {DiagnosticMsg} from './DiagnosticMsg';

export class RetroQuery {
    searchId = 0;
    maxRecords = 1000;
    minLevel = 0;
    startDate?: string;
    endDate?: string;
    machine?: string;
    process?: string;
    user?: string;
    message?: string;
}

export class RetroSearchResult
{
  searchId = 0;
  info = '';
  results: DiagnosticMsg[] = [];
}
