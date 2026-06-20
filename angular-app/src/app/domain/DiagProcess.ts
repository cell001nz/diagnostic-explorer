export interface DiagProcess {
    id: string;
    siteId: number;
    instanceId: string;
    name: string;
    userName: string;
    lastOnline: string | Date;
    isOnline: boolean;
    machineName: string;
}