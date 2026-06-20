import { TestBed } from '@angular/core/testing';

import { DiagHubService } from './diag-hub.service';

describe('DiagHubService', () => {
  let service: DiagHubService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(DiagHubService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
