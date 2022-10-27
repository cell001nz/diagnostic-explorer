import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SetPropertyDialogComponent } from './set-property-dialog.component';

describe('SetPropertyDialogComponent', () => {
  let component: SetPropertyDialogComponent;
  let fixture: ComponentFixture<SetPropertyDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ SetPropertyDialogComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(SetPropertyDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
