import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { DiagnosticResponse, SystemEvent } from '@domain/DiagResponse';
import { DiagProcess } from '@domain/DiagProcess';
import { LoadEventData, OperationRequest, OperationResponse, SetPropertyRequest } from '@domain/SetPropertyRequest';
import { selfHostTransport } from './transport';

declare const $: any;

const LOCAL_PROCESS_ID = 'self';
const RECONNECT_INTERVAL = 3_000;

interface SelfHostProcessInfo {
    id: string;
    name: string;
    machineName: string;
    userName: string;
}

interface SelfHostOperationResponse {
    isSuccess: boolean;
    result?: string;
    errorMessage?: string;
    errorDetail?: string;
}

interface SelfHostConnection {
    start(): Promise<void>;
    getProcessInfo(): Promise<SelfHostProcessInfo>;
    subscribe(processId: string): Promise<void>;
    unsubscribe(processId: string): Promise<void>;
    setProperty(path: string, value: string): Promise<SelfHostOperationResponse>;
    executeOperation(path: string, operation: string, args: string[]): Promise<SelfHostOperationResponse>;
    stop(): Promise<void>;
}

@Injectable()
export class SelfHostDiagHubService {
    readonly connectionState = signal<'connecting' | 'connected' | 'disconnected'>('connecting');
    readonly error = signal('');
    readonly process = signal<DiagProcess | undefined>(undefined);
    readonly diagsArrived$ = new Subject<{ processId: string; response: DiagnosticResponse }>();
    readonly clearEvents$ = new Subject<{ processId: string }>();
    readonly streamEvents$ = new Subject<{ processId: string; events: SystemEvent[] }>();
    readonly loadEvents$ = new Subject<LoadEventData>();
    #connection?: Promise<SelfHostConnection>;
    #hubProxyLoad?: Promise<void>;
    #reconnectTimer?: ReturnType<typeof setTimeout>;
    #stopRequested = false;

    async openHubConnection(): Promise<void> {
        this.#stopRequested = false;
        await this.getConnection();
    }

    async subscribeProcess(processId: string): Promise<void> {
        this.error.set('');
        await (await this.getConnection()).subscribe(processId);
    }

    async unsubscribeProcess(processId: string): Promise<void> {
        await (await this.getConnection()).unsubscribe(processId);
    }

    async setPropertyValue(processId: string, request: SetPropertyRequest): Promise<OperationResponse> {
        const response = await (await this.getConnection()).setProperty(request.path, request.value);
        return this.toOperationResponse(response);
    }

    async executeOperation(processId: string, request: OperationRequest): Promise<OperationResponse> {
        const response = await (await this.getConnection()).executeOperation(request.path, request.operation, request.arguments);
        return this.toOperationResponse(response);
    }

    async stop(): Promise<void> {
        this.#stopRequested = true;
        if (this.#reconnectTimer != null) {
            clearTimeout(this.#reconnectTimer);
            this.#reconnectTimer = undefined;
        }

        const connection = this.#connection;
        this.#connection = undefined;
        this.process.set(undefined);
        this.connectionState.set('disconnected');
        if (connection) {
            try {
                await (await connection).stop();
            } catch {}
        }
    }

    private getConnection(): Promise<SelfHostConnection> {
        if (!this.#connection) {
            this.connectionState.set('connecting');
            this.error.set('');
            this.#connection = (async () => {
                const connection = selfHostTransport === 'signalr2' ? await this.createSignalR2Connection() : this.createCoreConnection();
                await connection.start();
                this.process.set(this.toDiagProcess(await connection.getProcessInfo()));
                this.connectionState.set('connected');
                return connection;
            })().catch((error) => {
                this.#connection = undefined;
                this.process.set(undefined);
                this.connectionState.set('disconnected');
                this.error.set(this.errorMessage(error));
                this.scheduleReconnect();
                throw error;
            });
        }

        return this.#connection;
    }

    private createCoreConnection(): SelfHostConnection {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(new URL('hub', document.baseURI).toString())
            .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => RECONNECT_INTERVAL })
            .build();
        this.registerCallbacks((name, callback) => connection.on(name, callback));
        connection.onreconnecting(() => this.connectionState.set('connecting'));
        connection.onreconnected(() => {
            this.connectionState.set('connected');
            this.error.set('');
            void this.subscribeProcess(LOCAL_PROCESS_ID);
        });
        connection.onclose((error) => this.handleDisconnected(error));

        return {
            start: () => connection.start(),
            getProcessInfo: () => connection.invoke<SelfHostProcessInfo>('GetProcessInfo'),
            subscribe: (processId) => connection.invoke('Subscribe', processId),
            unsubscribe: (processId) => connection.invoke('Unsubscribe', processId),
            setProperty: (path, value) => connection.invoke<SelfHostOperationResponse>('SetProperty', LOCAL_PROCESS_ID, { path, value }),
            executeOperation: (path, operation, args) => connection.invoke<SelfHostOperationResponse>('ExecuteOperation', LOCAL_PROCESS_ID, { path, operation, arguments: args }),
            stop: () => connection.stop()
        };
    }

    private async createSignalR2Connection(): Promise<SelfHostConnection> {
        if (!$.connection?.selfHostWebHub) {
            this.#hubProxyLoad ??= this.loadScript(new URL('hub/hubs', document.baseURI).toString());
            await this.#hubProxyLoad;
        }
        if (!$.connection?.selfHostWebHub) throw new Error('The local SignalR 2 hub proxy could not be loaded.');

        const hub = $.connection.selfHostWebHub;
        const connection = $.connection.hub;
        connection.url = new URL('hub', document.baseURI).toString();
        this.registerCallbacks((name, callback) => (hub.client[name] = callback));
        connection.reconnecting(() => this.connectionState.set('connecting'));
        connection.reconnected(() => {
            this.connectionState.set('connected');
            this.error.set('');
            void this.subscribeProcess(LOCAL_PROCESS_ID);
        });
        connection.disconnected(() => this.handleDisconnected());

        return {
            start: () => this.toPromise<void>(connection.start()),
            getProcessInfo: () => this.toPromise<SelfHostProcessInfo>(hub.server.getProcessInfo()),
            subscribe: (processId) => this.toPromise<void>(hub.server.subscribe(processId)),
            unsubscribe: (processId) => this.toPromise<void>(hub.server.unsubscribe(processId)),
            setProperty: (path, value) => this.toPromise<SelfHostOperationResponse>(hub.server.setProperty(LOCAL_PROCESS_ID, { path, value })),
            executeOperation: (path, operation, args) => this.toPromise<SelfHostOperationResponse>(hub.server.executeOperation(LOCAL_PROCESS_ID, { path, operation, arguments: args })),
            stop: () => this.toPromise<void>(connection.stop())
        };
    }

    private registerCallbacks(register: (name: string, callback: (...args: any[]) => void) => void): void {
        register('ShowDiagnostics', (processId: string, response: DiagnosticResponse) => {
            this.diagsArrived$.next({
                processId,
                response: { ...response, serverDate: response.serverDate ?? new Date().toISOString() }
            });
        });
        register('ShowDiagnosticsError', (_processId: string, message: string) => this.error.set(message));
        register('SetEvents', (processId: string, events: SystemEvent[]) => {
            this.clearEvents$.next({ processId });
            this.loadEvents$.next({ requestId: '', clientId: '', processId, events });
        });
        register('StreamEvents', (processId: string, events: SystemEvent[]) => {
            this.streamEvents$.next({ processId, events });
        });
    }

    private handleDisconnected(error?: Error): void {
        this.#connection = undefined;
        this.process.set(undefined);
        this.connectionState.set('disconnected');
        if (error) this.error.set(this.errorMessage(error));
        this.scheduleReconnect();
    }

    private scheduleReconnect(): void {
        if (this.#stopRequested || this.#reconnectTimer != null) return;

        this.#reconnectTimer = setTimeout(() => {
            this.#reconnectTimer = undefined;
            void this.tryReconnect();
        }, RECONNECT_INTERVAL);
    }

    private async tryReconnect(): Promise<void> {
        if (this.#stopRequested || this.#connection) return;

        try {
            const connection = await this.getConnection();
            if (!this.#stopRequested) await connection.subscribe(LOCAL_PROCESS_ID);
        } catch {}
    }

    private toDiagProcess(process: SelfHostProcessInfo): DiagProcess {
        return {
            id: process.id,
            siteId: 0,
            instanceId: process.id,
            name: process.name,
            userName: process.userName,
            lastOnline: new Date(),
            isOnline: true,
            machineName: process.machineName
        };
    }

    private toOperationResponse(response: SelfHostOperationResponse): OperationResponse {
        return Object.assign(new OperationResponse(), {
            isSuccess: response.isSuccess,
            result: response.result ?? '',
            message: response.errorMessage ?? '',
            detail: response.errorDetail ?? ''
        });
    }

    private toPromise<T>(deferred: any): Promise<T> {
        return new Promise<T>((resolve, reject) => deferred.done(resolve).fail(reject));
    }

    private loadScript(url: string): Promise<void> {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = url;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error('The SignalR 2 browser client could not be loaded.'));
            document.head.appendChild(script);
        });
    }

    private errorMessage(error: unknown): string {
        return error instanceof Error ? error.message : 'The diagnostics connection could not be established.';
    }
}
