import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RetroMainComponent } from './retro-main.component';

describe('RetroMainComponent', () => {
  let component: RetroMainComponent;
  let fixture: ComponentFixture<RetroMainComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RetroMainComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RetroMainComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
