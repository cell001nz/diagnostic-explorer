import {IFilterableEvent} from './IFilterableEvent';
import {Level} from './Level';
import * as _ from 'lodash-es';

export class FilterCriteria {


    constructor() {
        this.watchEnabled = true;
    }

    watchEnabled = false;

    // @Watch((_this: FilterCriteria) => _this.initFilterFunc())
    info = false

    // @Watch((_this: FilterCriteria) => _this.initFilterFunc())
    notice = false;

    // @Watch((_this: FilterCriteria) => _this.initFilterFunc())
    warn = false;

    // @Watch((_this: FilterCriteria) => _this.initFilterFunc())
    error = false;

    // @Watch((_this: FilterCriteria) => _this.initFilterFunc())
    searchText = '';

    _filterFunc: (evt: IFilterableEvent) => boolean = _ => true;

    get isBlank(): boolean {
        return !this.searchText
            && this.info === this.warn
            && this.info === this.notice
            && this.info === this.error;
    }

    filter(evt: IFilterableEvent): boolean {
        console.log('filterFunc', this._filterFunc);
        return this._filterFunc(evt);
    }

    private createFilterFunc(): ((evt: IFilterableEvent) => boolean) {

        if (this.isBlank)
            return _ => true;

        let info = this.info;
        let notice = this.notice;
        let warn = this.warn;
        let error = this.error;

        let matcher: RegExp | undefined;

        if (!info && !warn && !error && !notice)
            info = notice = warn = error = true;

        try {
            if (this.searchText?.trim())
                matcher = new RegExp(this.searchText, 'i');
        } catch (err) {
            matcher = new RegExp(_.escapeRegExp(this.searchText), 'i');
        }

        return evt => {
            const levelNum = evt.level;
            
            if (!info && levelNum <= Level.Info)
                return false;

            if (!notice && levelNum > Level.Info && levelNum <= Level.Notice)
                return false;

            if (!warn && levelNum > Level.Notice && levelNum <= Level.Warn)
                return false;

            if (!error && levelNum >= Level.Error)
                return false;

            if (evt.user && matcher?.test(evt.user)) return true;
            if (evt.machine && matcher?.test(evt.machine)) return true;
            if (evt.process && matcher?.test(evt.process)) return true;
            if (matcher?.test(evt.message)) return true;

            return matcher?.test(evt.detail) ?? true;
        };
    }

    private initFilterFunc(): void {
        this._filterFunc = this.createFilterFunc();
    }
}
