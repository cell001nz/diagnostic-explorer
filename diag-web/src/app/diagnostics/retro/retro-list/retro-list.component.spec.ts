import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RetroListComponent } from './retro-list.component';

describe('RetroListComponent', () => {
  let component: RetroListComponent;
  let fixture: ComponentFixture<RetroListComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RetroListComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RetroListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
