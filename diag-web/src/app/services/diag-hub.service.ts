import { inject, Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { v4 as uuidv4 } from 'uuid';
import { DiagProcess } from '@domain/DiagProcess';
import { Observable, Subject, firstValueFrom } from 'rxjs';
import { LoadEventData, OperationRequest, OperationResponse, SetPropertyRequest } from '@domain/SetPropertyRequest';
import { DiagnosticResponse, DrillDownRequest, DrillDownResponse, LogStreamEvent, LogStreamInitialization } from '@domain/DiagResponse';
import { RetroQuery, RetroSearchResult } from '@model/RetroQuery';

const TAB_ID_KEY = 'tabIdStorageKey';

@Injectable({
    providedIn: 'root'
})
export class DiagHubService implements OnDestroy {
    // #hubConnection!: signalR.HubConnection;
    #http = inject(HttpClient);
    readonly #negotiateUrl = '/web-hub';
    #hubConnection?: Promise<signalR.HubConnection>;
    processArrived$ = new Subject<DiagProcess>();
    processesArrived$ = new Subject<DiagProcess[]>();
    diagsArrived$ = new Subject<{ processId: string; response: DiagnosticResponse }>();
    logStreamInitialized$ = new Subject<{ processId: string; initialization: LogStreamInitialization }>();
    logStreamEvents$ = new Subject<{ processId: string; events: LogStreamEvent[] }>();
    retroResults$ = new Subject<RetroSearchResult>();
    retroSearchEnd$ = new Subject<number>();
    retroSearchError$ = new Subject<{ searchId: number; error: string; detail: string }>();
    tabId = '';
    #selectedProcessId?: string;

    constructor() {
        this.initTabId();
    }

    async openHubConnection() {
        await this.getHubConnection();
    }

    private async getHubConnection(): Promise<signalR.HubConnection> {
        if (!this.#hubConnection) {
            console.log('INITIALISING HUB CONNECTION');
            this.#hubConnection = new Promise(async (resolve) => {
                let hub = new signalR.HubConnectionBuilder().withUrl('/web-hub').withAutomaticReconnect().build();

                await hub.start();
                const register = (name: string, callback: (...args: any[]) => void): void =>
                    hub.on(name, (...args: any[]) => {
                        console.log(`Server message: ${name}`, ...args);
                        callback(...args);
                    });

                register('say', () => {});
                register('SetProcesses', (processes: DiagProcess[]) => {
                    this.processesArrived$.next(processes);
                });
                register('UpdateProcess', (process: DiagProcess) => {
                    this.processArrived$.next(process);
                });
                register('ShowDiagnostics', (processId: string, response: DiagnosticResponse) => {
                    this.diagsArrived$.next({ processId, response });
                });
                register('InitializeLogStream', (processId: string, initialization: LogStreamInitialization) => {
                    this.logStreamInitialized$.next({ processId, initialization });
                });
                register('StreamLogEvents', (processId: string, events: LogStreamEvent[]) => {
                    this.logStreamEvents$.next({ processId, events });
                });
                register('ProcessSearchResults', (result: RetroSearchResult) => {
                    this.retroResults$.next(result);
                });
                register('ProcessSearchEnd', (searchId: number) => {
                    this.retroSearchEnd$.next(searchId);
                });
                register('ProcessSearchError', (searchId: number, error: string, detail: string) => {
                    this.retroSearchError$.next({ searchId, error, detail });
                });
                console.log('Hub connection configured');
                hub.onreconnected(() => {
                    if (this.#selectedProcessId) void hub.invoke('Subscribe', this.#selectedProcessId);
                });
                hub.onclose((error) => this.handleConnectionClosed(error));
                if (this.#selectedProcessId) await hub.invoke('Subscribe', this.#selectedProcessId);
                resolve(hub);
            });
        }
        return this.#hubConnection;
    }

    private initTabId() {
        const initTabId = (): string => {
            const id = sessionStorage.getItem(TAB_ID_KEY);
            if (id) {
                sessionStorage.removeItem(TAB_ID_KEY);
                return id;
            }
            return uuidv4();
        };

        this.tabId = initTabId();
        // window.addEventListener("beforeunload", () => sessionStorage.setItem(TAB_ID_KEY, this.#tabId));
    }

    ngOnDestroy() {
        sessionStorage.setItem(TAB_ID_KEY, this.tabId);
    }

    async subscribeProcess(processId: string) {
        let hub = await this.getHubConnection();
        this.#selectedProcessId = processId;
        await hub.invoke('Subscribe', processId);
    }

    async unsubscribeProcess(processId: string) {
        let hub = await this.getHubConnection();
        await hub.invoke('Unsubscribe', processId);
        if (this.#selectedProcessId === processId) this.#selectedProcessId = undefined;
    }

    async setPropertyValue(processId: string, request: SetPropertyRequest): Promise<OperationResponse> {
        let hub = await this.getHubConnection();
        return hub.invoke<OperationResponse>(`SetProperty`, processId, request);
    }

    async getDrillDown(processId: string, request: DrillDownRequest): Promise<DrillDownResponse> {
        const hub = await this.getHubConnection();
        return hub.invoke<DrillDownResponse>('GetDrillDown', processId, request);
    }

    async executeOperation(processId: string, request: OperationRequest): Promise<OperationResponse> {
        let hub = await this.getHubConnection();
        return hub.invoke<OperationResponse>(`ExecuteOperation`, processId, request);
    }

    async removeProcess(id: string): Promise<void> {
        let hub = await this.getHubConnection();
        await hub.invoke('RemoveProcess', id);
    }

    async startRetroSearch(query: RetroQuery): Promise<void> {
        let hub = await this.getHubConnection();
        await hub.invoke('StartRetroSearch', query);
    }

    async cancelRetroSearch(searchId: number): Promise<void> {
        let hub = await this.getHubConnection();
        await hub.invoke('CancelRetroSearch', searchId);
    }

    private async handleConnectionClosed(err: Error | undefined) {
        console.log('Hub connection closed:', err);
        this.#hubConnection = undefined;
    }

    async deleteRecords(toDelete: string[]): Promise<number> {
        let hub = await this.getHubConnection();
        return await hub.invoke<number>('RetroDelete', toDelete);
    }
}
