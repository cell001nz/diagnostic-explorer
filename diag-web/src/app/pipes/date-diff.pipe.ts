import { Pipe, PipeTransform } from '@angular/core';

@Pipe({name: 'dateDiff'})
export class DateDiffPipe implements PipeTransform {
  transform(startDate: string | Date | undefined, endDate: string | Date = new Date()): string {
    if (!startDate)
      return '';
    
    const start = new Date(startDate);
    const end = new Date(endDate);
    let diffMs = Math.abs(end.getTime() - start.getTime());

    // Calculate total seconds difference
    const totalSeconds = Math.floor(diffMs / 1000);
    
    // Calculate days, hours, minutes, seconds
    const days = Math.floor(totalSeconds / (24 * 3600));
    const hours = Math.floor((totalSeconds % (24 * 3600)) / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    // Helper to pad numbers to 2 digits
    const pad = (n: number) => n.toString().padStart(2, '0');

    if (days > 0) {
      // Format: d.hh:mm:ss (day without leading zero, hours padded to 2 digits)
      return `${days}.${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
    } else {
      // Format: hh:mm:ss (no day part)
      return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
    }
  }
}