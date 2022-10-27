export function strEqCI(s1: string | null, s2: string | null): boolean
{
  if (s1 === null && s2 === null)
    return true;

  if (s1 === undefined && s2 === undefined)
    return true;

  if (s1 === null || s2 === null)
    return false;

  if (s1 === undefined || s2 === undefined)
    return false;

  return s1.localeCompare(s2, undefined, { sensitivity: 'base'}) === 0;
}


export function getErrorMessage(err: any): string
{
  if (typeof(err) === 'string')
    return err;

  return (err.error?.exceptionMessage ?? err.error?.message ?? err.message ?? '').toString();
}

export function today(): Date
{
  let now: Date = new Date();
  now.setHours(0, 0, 0, 0);
  return now;
}

