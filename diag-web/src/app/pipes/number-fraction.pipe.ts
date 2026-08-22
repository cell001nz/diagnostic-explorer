import {inject, Pipe, PipeTransform} from '@angular/core';
import {DecimalPipe} from "@angular/common";

@Pipe({
  name: 'numberFraction',
})
export class NumberFractionPipe implements PipeTransform {

  constructor(private decimalPipe: DecimalPipe) {}

  transform(value: number | string | undefined, digitsInfo: string = '1.0-3'): string | null {
    if (value == null) return null;
    let formatted = this.decimalPipe.transform(value, digitsInfo);
    if (formatted) {
      let index = formatted.indexOf('.');
      if (index != -1)
        formatted = formatted?.substring(index)
    }
    return formatted;
  }
}

  