export function getErrorMsg(value: any) {
    if (!value) return '';

    console.log('ERROR', value);
    if (typeof (value) == 'string') return value;

    if (typeof (value) !== 'object')
        return String(value)

    if ('message' in value)
        return String(value.message);

    return String(value);
}