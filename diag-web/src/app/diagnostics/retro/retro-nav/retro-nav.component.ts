import {Component, inject} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {Select} from 'primeng/select';
import {InputText} from 'primeng/inputtext';
import {DatePicker} from 'primeng/datepicker';
import {ProgressBar} from 'primeng/progressbar';
import {ButtonDirective} from 'primeng/button';
import {RetroModel} from '@model/RetroModel';

@Component({
  selector: 'app-retro-nav',
  imports: [
    FormsModule,
    Select,
    InputText,
    DatePicker,
    ProgressBar,
    ButtonDirective,
  ],
  templateUrl: './retro-nav.component.html',
  styleUrl: './retro-nav.component.scss',
})
export class RetroNavComponent {

  readonly model = inject(RetroModel);

  readonly maxRecordsOptions = [
    {label: '1,000', value: 1000},
    {label: '5,000', value: 5000},
    {label: '10,000', value: 10000},
    {label: '20,000', value: 20000},
  ];

  readonly minLevelOptions = [
    {label: 'All', value: 0},
    {label: 'Debug', value: 1},
    {label: 'Info', value: 2},
    {label: 'Warn', value: 3},
    {label: 'Error', value: 4},
    {label: 'Critical', value: 5},
  ];

  readonly times = Array.from(Array(24).keys()).map(h => ({label: `${('0' + h).slice(-2)}:00`, value: h}));

  readonly timeZoneOptions = [
    {label: 'Local', value: 'local'},
    {label: 'UTC', value: 'utc'},
  ];

  readonly hoursOptions = [
    {label: '1 Hour', value: 1},
    {label: '2 Hours', value: 2},
    {label: '4 Hours', value: 4},
    {label: '8 Hours', value: 8},
    {label: '12 Hours', value: 12},
    {label: '1 Day', value: 24},
    {label: '2 Days', value: 2 * 24},
    {label: '5 Days', value: 5 * 24},
    {label: '7 Days', value: 7 * 24},
    {label: '14 Days', value: 14 * 24},
    {label: '30 Days', value: 30 * 24},
  ];
}
