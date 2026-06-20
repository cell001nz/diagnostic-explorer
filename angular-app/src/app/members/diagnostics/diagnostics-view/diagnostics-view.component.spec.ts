import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DiagnosticsViewComponent } from './diagnostics-view.component';

describe('DiagnosticsViewComponent', () => {
  let component: DiagnosticsViewComponent;
  let fixture: ComponentFixture<DiagnosticsViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DiagnosticsViewComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DiagnosticsViewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
