import { filter } from 'rxjs';
import {computed, inject, Injectable, signal} from "@angular/core";
import {ObservableDisposable} from "@model/ObservableDisposable";
import {DiagProcess} from "@domain/DiagProcess";
import { DiagHubService } from "@services/diag-hub.service";


@Injectable({providedIn: 'root'})
export class RealtimeModel {

    allProcesses = signal<DiagProcess[]>([]);
    filteredProcesses = computed(() => this.allProcesses());
    selectedProcessId = signal<string | null>(null);
    selectedCategory = signal<string | null>(null);
    #hubService = inject(DiagHubService);

    constructor() {
        this.#hubService.openHubConnection();
        this.#hubService.processesArrived$.subscribe(p => {
            console.log('got processes: ', p)
            this.allProcesses.set(p);
        });
        this.#hubService.processArrived$.subscribe((p: DiagProcess) => {
            console.log('got process: ', p)
            
            let found = this.allProcesses().find(x => x.id === p.id);
            if (found)
                this.allProcesses.update(procs => procs.map(x => x.id === p.id ? p : x));
            else
                this.allProcesses.update(procs => [...procs, p]);

        });
    }    
}

