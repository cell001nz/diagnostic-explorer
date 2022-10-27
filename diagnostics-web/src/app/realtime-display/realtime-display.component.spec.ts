import {ComponentFixture, TestBed} from '@angular/core/testing';

import {RealtimeDisplayComponent} from './realtime-display.component';

describe('RealtimeDisplayComponent', () => {
  let component: RealtimeDisplayComponent;
  let fixture: ComponentFixture<RealtimeDisplayComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [RealtimeDisplayComponent]
    })
      .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(RealtimeDisplayComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
