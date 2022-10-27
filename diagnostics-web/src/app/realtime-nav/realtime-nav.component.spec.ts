import {ComponentFixture, TestBed} from '@angular/core/testing';

import {RealtimeNavComponent} from './realtime-nav.component';

describe('RealtimeNavComponent', () => {
  let component: RealtimeNavComponent;
  let fixture: ComponentFixture<RealtimeNavComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [RealtimeNavComponent]
    })
      .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(RealtimeNavComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
