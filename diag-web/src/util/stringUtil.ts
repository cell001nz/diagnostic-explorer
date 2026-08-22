export function strEqCI(s1: string | null, s2: string | null): boolean {
    if (s1 === null && s2 === null)
        return true;

    if (s1 === undefined && s2 === undefined)
        return true;

    if (s1 === null || s2 === null)
        return false;

    if (s1 === undefined || s2 === undefined)
        return false;

    return s1.localeCompare(s2, undefined, {sensitivity: 'base'}) === 0;
}