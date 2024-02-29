import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CollapsibleRegionComponent } from './collapsible-region.component';

describe('CollapsibleRegionComponent', () => {
  let component: CollapsibleRegionComponent;
  let fixture: ComponentFixture<CollapsibleRegionComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ CollapsibleRegionComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(CollapsibleRegionComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
