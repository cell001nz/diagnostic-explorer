import {ComponentFixture, TestBed} from '@angular/core/testing';

import {RetroDisplayComponent} from './retro-display.component';

describe('RetroDisplayComponent', () => {
  let component: RetroDisplayComponent;
  let fixture: ComponentFixture<RetroDisplayComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [RetroDisplayComponent]
    })
      .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(RetroDisplayComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
