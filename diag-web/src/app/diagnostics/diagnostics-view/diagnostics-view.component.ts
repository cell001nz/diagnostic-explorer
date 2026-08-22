import {ChangeDetectionStrategy, Component, computed, inject, input, OnDestroy, signal} from '@angular/core';
import {
  concat,
  distinctUntilChanged,
  filter, finalize, map, of, pairwise, repeat, retry, startWith,
  tap,
  timer
} from "rxjs";
import {rxResource, takeUntilDestroyed, toObservable} from "@angular/core/rxjs-interop";
import {DecimalPipe, JsonPipe} from "@angular/common";
import {ActivatedRoute, Router} from "@angular/router";
import {ErrMsgPipe} from "@pipes/err-msg.pipe";
import {DiagHubService} from "@services/diag-hub.service";
import {DiagnosticModelFactory} from "@model/DiagnosticModelFactory";
import {ProcessModel} from "@model/ProcessModel";
import {Tab, TabList, TabPanel, TabPanels, Tabs} from "primeng/tabs";
import {CategoryViewComponent} from "@app/diagnostics/category-view/category-view.component";
import {DiagProcess} from "@domain/DiagProcess";
import {EventModel} from "@model/EventModel";
import {EventDetailPanelComponent} from "@app/diagnostics/event-detail-panel/event-detail-panel.component";
import { RealtimeModel } from '@model/RealtimeModel';

const REFRESH_INTERVAL = 5_000;

@Component({
  selector: 'app-diagnostics-view',
  imports: [
    Tabs,
    TabPanel,
    TabList,
    Tab,
    TabPanels,
    CategoryViewComponent,
    EventDetailPanelComponent,
  ],
  templateUrl: './diagnostics-view.component.html',
  styleUrl: './diagnostics-view.component.scss',
  providers: [DiagnosticModelFactory, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DiagnosticsViewComponent implements OnDestroy {
  route = inject(ActivatedRoute);
  #router = inject(Router);
  processId = input.required<string>();
  #hubService = inject(DiagHubService);
  realtime = inject(RealtimeModel)
  nextRefresh = signal<number | undefined>(undefined);
  process = computed(() => this.realtime.allProcesses().find(p => p.id === this.processId()));
  processModel = signal<ProcessModel>(new ProcessModel());

  #desiredCat = this.route.snapshot.queryParamMap.get('cat') ?? this.realtime.selectedCategory();
  #catRestored = false;

  constructor() {
    // effect(() => console.log('ProcessId changed ', this.processId(), typeof this.processId()));
    // effect(() => console.log('Process changed ', this.process()));

    toObservable(this.processId)
        .pipe(takeUntilDestroyed())
        .subscribe(id => this.realtime.selectedProcessId.set(id));

    toObservable(this.process)
        .pipe(
            filter(p => !!p),
            takeUntilDestroyed(),
            distinctUntilChanged((a, b) => a.id === b.id),
            startWith(undefined),
            pairwise(),
            tap(x => this.processModel().clear()),
            )
        .subscribe(async ([prev, curr]) => {
          await this.tryUnsubscribe(prev)
          await this.trySubscribe(curr)
        })
        // .subscribe(processId => this.#appContext.processId.set(processId));
    
    this.#hubService.clearEvents$
      .pipe(filter(d => d.processId === this.processId()), takeUntilDestroyed())
      .subscribe(d => this.processModel().clearEvents());
    
    this.#hubService.streamEvents$
      .pipe(filter(d => d.processId === this.processId()), takeUntilDestroyed())
      .subscribe(d => this.processModel().streamEvents(d.events));
    
    this.#hubService.loadEvents$
      .pipe(filter(d => d.processId === this.processId()), takeUntilDestroyed())
      .subscribe(d => this.processModel().loadEvents(d));
    
    this.#hubService.diagsArrived$
        .pipe(filter(d => d.processId === this.processId()), takeUntilDestroyed())
        .subscribe(d => {
          this.processModel().update(d.response);
          this.#syncCategory();
        });
    
    timer(0, 1_000)
        .pipe(takeUntilDestroyed())
        .subscribe(() => {
          for (const cat of this.processModel().categories())
            cat.checkEventSeverityLevels();
        });
    
  /*  timer(0, 100)
        .pipe(takeUntilDestroyed())
        .subscribe(() => {
      this.nextRefresh.set(this.#appContext.diagnosticsLoading() 
          ? undefined
          : (REFRESH_INTERVAL - (Date.now() - this.#appContext.diagnosticsUpdated())) / 1000)
    });*/
  }
  
  selectedEvent = signal<EventModel | null>(null);

  onCategoryChange(cat: string | number | undefined): void {
    if (cat == null)
      return;
    const name = String(cat);
    this.processModel().activeCatName.set(name);
    this.#writeCategory(name);
  }

  // Restore the category from the URL once categories have loaded, then keep the URL in sync.
  #syncCategory(): void {
    const model = this.processModel();
    if (!this.#catRestored && this.#desiredCat && model.categories().some(c => c.name() === this.#desiredCat)) {
      model.activeCatName.set(this.#desiredCat);
      this.#catRestored = true;
    }
    const cat = model.activeCatName();
    if (cat)
      this.#writeCategory(cat);
  }

  #writeCategory(cat: string): void {
    this.realtime.selectedCategory.set(cat);
    void this.#router.navigate([], {
      relativeTo: this.route,
      queryParams: {cat},
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  onEventSelected(event: EventModel): void {
    // Deselect previous
    const prev = this.selectedEvent();
    if (prev) prev.isSelected = false;
    // Select new (toggle off if same row clicked again)
    if (prev === event) {
      this.selectedEvent.set(null);
    } else {
      event.isSelected = true;
      this.selectedEvent.set(event);
    }
  }

  closeDetail(): void {
    const prev = this.selectedEvent();
    if (prev) prev.isSelected = false;
    this.selectedEvent.set(null);
  }

  detailHeight = signal(200);

  #resizeStartY = 0;
  #resizeStartHeight = 0;

  #onResizeMove = (e: MouseEvent) => {
    const delta = this.#resizeStartY - e.clientY;
    const next = Math.min(Math.max(this.#resizeStartHeight + delta, 80), window.innerHeight - 160);
    this.detailHeight.set(next);
  };

  #onResizeEnd = () => {
    document.removeEventListener('mousemove', this.#onResizeMove);
    document.removeEventListener('mouseup', this.#onResizeEnd);
    document.body.style.userSelect = '';
    document.body.style.cursor = '';
  };

  startResize(event: MouseEvent): void {
    event.preventDefault();
    this.#resizeStartY = event.clientY;
    this.#resizeStartHeight = this.detailHeight();
    document.body.style.userSelect = 'none';
    document.body.style.cursor = 'row-resize';
    document.addEventListener('mousemove', this.#onResizeMove);
    document.addEventListener('mouseup', this.#onResizeEnd);
  }
  
  
  private async tryUnsubscribe(process: DiagProcess | undefined) {
    try {
      if (process)
        await this.#hubService.unsubscribeProcess(process.id);
    } catch (err) {
      console.log(err);
    }
  }
  
  private async trySubscribe(process: DiagProcess | undefined) {
        try {
      if (process)
        await this.#hubService.subscribeProcess(process.id);
        this.console.log('Subscribed to ', process?.id)
    } catch (err) {
      console.log(err);
    }
  }
  
  async ngOnDestroy() {
    await this.tryUnsubscribe(this.process());
  }
  
  expandCollapse() {
    this.processModel().activeCat()?.expandCollapse();
  }

  protected readonly console = console;
  protected readonly String = String;
}
