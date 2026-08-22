import { inject, Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { v4 as uuidv4 } from 'uuid';
import { DiagProcess } from '@domain/DiagProcess';
import { Observable, Subject, firstValueFrom } from 'rxjs';
import { LoadEventData, OperationRequest, OperationResponse, SetPropertyRequest } from '@domain/SetPropertyRequest';
import { DiagnosticResponse, SystemEvent } from '@domain/DiagResponse';
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
    clearEvents$ = new Subject<{ processId: string }>();
    streamEvents$ = new Subject<{ processId: string; events: SystemEvent[] }>();
    loadEvents$ = new Subject<LoadEventData>();
    retroResults$ = new Subject<RetroSearchResult>();
    retroSearchEnd$ = new Subject<number>();
    retroSearchError$ = new Subject<{ searchId: number; error: string; detail: string }>();
    tabId = '';

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
                hub.on('say', (message) => console.log('Hub message', message));
                hub.on('SetProcesses', (processes: DiagProcess[]) => {
                    // console.log('DiagHubService.ReceiveProcess', processes);
                    this.processesArrived$.next(processes);
                });
                hub.on('UpdateProcess', (process: DiagProcess) => {
                    // console.log('DiagHubService.UpdateProcess', process);
                    this.processArrived$.next(process);
                });
                hub.on('ShowDiagnostics', (processId: string, response: DiagnosticResponse) => {
                    // console.log('Diagnostics arrived', processId, response);
                    this.diagsArrived$.next({ processId, response });
                });
                hub.on('SetEvents', (processId: string, events: SystemEvent[]) => {
                     console.log('SetEvents', processId, events);
                     this.clearEvents$.next({ processId});
                    this.streamEvents$.next({ processId, events });
                });
                hub.on('StreamEvents', (processId: string, events: SystemEvent[]) => {
                     console.log('StreamEvents', processId, events);
                    this.streamEvents$.next({ processId, events });
                });
                hub.on('ProcessSearchResults', (result: RetroSearchResult) => {
                    this.retroResults$.next(result);
                });
                hub.on('ProcessSearchEnd', (searchId: number) => {
                    this.retroSearchEnd$.next(searchId);
                });
                hub.on('ProcessSearchError', (searchId: number, error: string, detail: string) => {
                    this.retroSearchError$.next({ searchId, error, detail });
                });
                console.log('Hub connection configured');
                hub.onclose((error) => this.handleConnectionClosed(error));
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
        await hub.invoke('Subscribe', processId);
    }

    async unsubscribeProcess(processId: string) {
        let hub = await this.getHubConnection();
        await hub.invoke('Subscribe', processId);
    }
    
    async setPropertyValue(processId: string, request: SetPropertyRequest): Promise<OperationResponse> {
        let hub = await this.getHubConnection();
        return hub.invoke<OperationResponse>(`SetProperty`, processId, request);
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
