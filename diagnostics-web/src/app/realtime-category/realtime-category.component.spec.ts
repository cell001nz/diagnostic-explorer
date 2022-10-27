import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RealtimeCategoryComponent } from './realtime-category.component';

describe('RealtimeCategoryComponent', () => {
  let component: RealtimeCategoryComponent;
  let fixture: ComponentFixture<RealtimeCategoryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ RealtimeCategoryComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(RealtimeCategoryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
