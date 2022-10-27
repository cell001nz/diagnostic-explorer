import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RealtimeEventsComponent } from './realtime-events.component';

describe('RealtimeEventsComponent', () => {
  let component: RealtimeEventsComponent;
  let fixture: ComponentFixture<RealtimeEventsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ RealtimeEventsComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(RealtimeEventsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
