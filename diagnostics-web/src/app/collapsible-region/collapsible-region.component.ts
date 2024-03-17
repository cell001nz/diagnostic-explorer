import {Component, Input, OnInit} from '@angular/core';
import {CollapsibleRegion} from "../Model/CollapsibleRegion";

@Component({
  selector: 'app-collapsible-region',
  templateUrl: './collapsible-region.component.html',
  styleUrls: ['./collapsible-region.component.scss']
})
export class CollapsibleRegionComponent implements OnInit {

  @Input()
  region?: CollapsibleRegion;

  constructor() { }

  ngOnInit(): void
  {
  }

  public getClass(): string
  {
    return 'collapsible-region-level' + this.region?.level;
  }
}
