import {ComponentFixture, TestBed} from '@angular/core/testing';

import {RetroNavComponent} from './retro-nav.component';

describe('RetroNavComponent', () => {
  let component: RetroNavComponent;
  let fixture: ComponentFixture<RetroNavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [RetroNavComponent]
    })
      .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(RetroNavComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
