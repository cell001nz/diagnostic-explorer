import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DiagnosticsNavComponent } from './diagnostics-nav.component';

describe('DiagnosticsNavComponent', () => {
  let component: DiagnosticsNavComponent;
  let fixture: ComponentFixture<DiagnosticsNavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DiagnosticsNavComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DiagnosticsNavComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
