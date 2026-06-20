import {Component, inject, signal} from '@angular/core';
import {DatePipe, LowerCasePipe} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {InputText} from 'primeng/inputtext';
import {Select} from 'primeng/select';
import {LevelToStringPipe} from '@pipes/level-to-string.pipe';
import {RetroModel} from '@model/RetroModel';
import {DiagnosticMsg} from '@model/DiagnosticMsg';

@Component({
  selector: 'app-retro-list',
  imports: [
    DatePipe,
    LowerCasePipe,
    FormsModule,
    InputText,
    Select,
    LevelToStringPipe,
  ],
  templateUrl: './retro-list.component.html',
  styleUrl: './retro-list.component.scss',
})
export class RetroListComponent {

  readonly model = inject(RetroModel);

  filtersVisible = signal(false);
  detailTab = signal<'detail' | 'trace'>('detail');

  readonly filterLevelOptions = [
    {label: 'All', value: 0},
    {label: 'Debug', value: 1},
    {label: 'Info', value: 2},
    {label: 'Warn', value: 3},
    {label: 'Error', value: 4},
    {label: 'Critical', value: 5},
  ];

  toggleFilters(): void {
    this.filtersVisible.update(v => !v);
  }

  private mouseDown = false;

  onRowMouseDown(item: DiagnosticMsg): void {
    this.mouseDown = true;
    this.model.setCurrentEvent(item);
  }

  onRowMouseOver(item: DiagnosticMsg): void {
    if (this.mouseDown)
      this.model.setCurrentEvent(item);
  }

  onMouseUp(): void {
    this.mouseDown = false;
  }
}
