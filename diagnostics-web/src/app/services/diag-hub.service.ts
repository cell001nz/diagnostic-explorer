import { Injectable } from '@angular/core';
import {HubConnection} from '@microsoft/signalr';
import * as signalR from '@microsoft/signalr';
import {Observable, ReplaySubject} from 'rxjs';
import {OperationResponse, SetPropertyRequest} from '../Model/SetPropertyRequest';
import {DiagnosticResponse} from '../Model/DiagResponse';
import {map} from 'rxjs/operators';
import {plainToClass, plainToInstance} from 'class-transformer';
import {ExecOperationRequest} from '../Model/ExecOperationRequest';
import {DiagProcess} from '../Model/DiagProcess';
import {RetroQuery} from '../Model/RetroQuery';
import {DiagnosticMsg} from '../Model/DiagnosticMsg';
import {AppConfigService} from './app-config.service';

@Injectable({
  providedIn: 'root'
})
export class DiagHubService {

  public connection?: HubConnection;
  public connectionReady = new ReplaySubject<HubConnection>(1);
  public connectionStarted = new ReplaySubject<HubConnection>(1);


  constructor(private appConfig: AppConfigService) {

  }

  public async connect(): Promise<void>
  {
    while (!this.connection) {
      try {

        const connection = new signalR.HubConnectionBuilder()
          .withUrl(this.appConfig.apiBaseUrl)
          .build();

        this.connectionReady.next(connection);
        await connection.start();
        this.connectionStarted.next(connection);
        connection.onreconnecting(err => console.log('Hub reconnecting', err))
        connection.onreconnected(connectionId => console.log('Hub reconnected', connectionId))
        connection.onclose(err => this.handleConnectionClosed(err));
        this.connection = connection;
      }
      catch (err)
      {
        console.log(err);
        await new Promise(resolve => setTimeout(resolve, 1000));
      }
    }
  }

  async setPropertyValue(request: SetPropertyRequest): Promise<OperationResponse> {

    let response =  this.connection!.invoke<OperationResponse>(`SetProperty`, request);
    return plainToInstance(OperationResponse, response);
  }

  async executeOperation(request: ExecOperationRequest): Promise<OperationResponse> {
    let response = this.connection!.invoke<OperationResponse>(`ExecuteOperation`, request);
    return plainToInstance(OperationResponse, response);
  }

  async removeProcess(id: string): Promise<void> {
    await this.connection!.invoke('RemoveProcess', id);
  }

  async startRetroSearch(query: RetroQuery): Promise<void> {
    await this.connection!.invoke('StartRetroSearch', query);
  }

  async cancelRetroSearch(searchId: number): Promise<void> {
    await this.connection!.invoke('CancelRetroSearch', searchId);
  }

  private async handleConnectionClosed(err: Error | undefined) {
    this.connection = undefined;
    await this.connect();
  }

  async deleteRecords(toDelete: string[]): Promise<number> {
      return await this.connection!.invoke<number>('RetroDelete', toDelete);
  }
}
