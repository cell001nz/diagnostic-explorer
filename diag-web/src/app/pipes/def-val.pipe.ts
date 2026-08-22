import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'defVal'
})
export class DefValPipe implements PipeTransform {

  transform(value: unknown, ...args: unknown[]): unknown {
    return value ?? args[0];
  }
}
