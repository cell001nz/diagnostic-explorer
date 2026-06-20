import { Pipe, PipeTransform } from '@angular/core';
import { Level } from '@model/Level';

@Pipe({
  name: 'levelToString',
  standalone: true
})
export class LevelToStringPipe implements PipeTransform {
  transform(value: number | string): string {
    const numValue = typeof value === 'string' ? parseInt(value, 10) : value;
    if (isNaN(numValue)) {
      return 'Unknown';
    }
    return Level.LevelToString(numValue);
  }
}

