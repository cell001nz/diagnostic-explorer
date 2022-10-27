import { Injectable } from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {AppConfig} from '../Model/AppConfig';
import {environment} from '../../environments/environment';
import {config} from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class AppConfigService {

  private appConfig?: AppConfig;

  constructor(private http: HttpClient) { }

  public async initialise(): Promise<void> {
    this.appConfig = await this.http.get<AppConfig>('assets/config.json').toPromise();
  }


  public get apiBaseUrl(): string
  {
    if (!this.appConfig)
      throw Error('Config file not found');

    return this.appConfig.apiBaseUrl;
  }
}
