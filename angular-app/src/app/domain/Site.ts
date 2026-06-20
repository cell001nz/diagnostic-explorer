import {Secret} from "./Secret";

export interface Site {    
    id: number,
    name: string,
    code: string,
    secrets: Secret[];
}

