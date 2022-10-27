import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ExecOperationsComponent } from './exec-operations.component';

describe('ExecOperationsComponent', () => {
  let component: ExecOperationsComponent;
  let fixture: ComponentFixture<ExecOperationsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ ExecOperationsComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(ExecOperationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
