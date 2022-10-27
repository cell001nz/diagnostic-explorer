import {Component} from '@angular/core';
import {AppModel} from './Model/AppModel';
import {RealtimeModel} from './Model/RealtimeModel';
import {RetroModel} from './Model/RetroModel';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss'],
  providers: [AppModel, RealtimeModel, RetroModel]

})
export class AppComponent {
  title = 'diagnostics-web';


  constructor(readonly appModel: AppModel) {

    appModel.start();
  }
}
