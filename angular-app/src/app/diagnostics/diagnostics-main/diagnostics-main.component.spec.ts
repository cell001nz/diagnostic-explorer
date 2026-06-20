import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DiagnosticsMainComponent } from './diagnostics-main.component';

describe('DiagnosticsMainComponent', () => {
  let component: DiagnosticsMainComponent;
  let fixture: ComponentFixture<DiagnosticsMainComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DiagnosticsMainComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(DiagnosticsMainComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
