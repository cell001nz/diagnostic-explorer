import { inject, Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { v4 as uuidv4 } from 'uuid';
import { DiagProcess } from '@domain/DiagProcess';
import { Observable, Subject, firstValueFrom } from 'rxjs';
import { LoadEventData, OperationRequest, OperationResponse, SetPropertyRequest } from '@domain/SetPropertyRequest';
import { DiagnosticResponse, SystemEvent } from '@domain/DiagResponse';
import { RetroQuery } from '@model/RetroQuery';

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
                    console.log('DiagHubService.ReceiveProcess', processes);
                    this.processesArrived$.next(processes);
                });
                hub.on('UpdateProcess', (process: DiagProcess) => {
                    console.log('DiagHubService.UpdateProcess', process);
                    this.processArrived$.next(process);
                });
                hub.on('ShowDiagnostics', (processId: string, response: DiagnosticResponse) => {
                    console.log('Diagnostics arrived', processId, response);
                    this.diagsArrived$.next({ processId, response });
                });
                hub.on('ClearEvents', (processId: string) => {
                     console.log('ClearEvents', processId);
                    this.clearEvents$.next({ processId });
                });
                hub.on('LoadEvents', (data: LoadEventData) => {
                    //  console.log('StreamEvents', processId, events);
                    this.loadEvents$.next(data);
                });
                hub.on('StreamEvents', (processId: string, events: SystemEvent[]) => {
                     console.log('StreamEvents', processId, events);
                    this.streamEvents$.next({ processId, events });
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
    
    async setPropertyValue(request: SetPropertyRequest): Promise<OperationResponse> {
        let hub = await this.getHubConnection();
        return hub.invoke<OperationResponse>(`SetProperty`, request);
    }

    async executeOperation(request: OperationRequest): Promise<OperationResponse> {
        let hub = await this.getHubConnection();
        return hub.invoke<OperationResponse>(`ExecuteOperation`, request);
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
