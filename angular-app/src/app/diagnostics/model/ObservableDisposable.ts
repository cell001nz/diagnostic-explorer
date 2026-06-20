import {Observable} from "rxjs";

export interface ObservableDisposable {
    disposed$: Observable<true>;
}