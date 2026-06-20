import { Pipe, PipeTransform } from '@angular/core';
import {getErrorMsg} from "../../util/errorUtil";

@Pipe({
  name: 'errMsg'
})
export class ErrMsgPipe implements PipeTransform {

  transform(value: unknown, ...args: unknown[]): string {
    return getErrorMsg(value);
  }

}
