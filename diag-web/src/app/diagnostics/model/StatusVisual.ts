import type { Property, PropertyStatus } from '@domain/DiagResponse';

const namedStatusCodes: Record<string, number> = {
    Active: 1,
    Inactive: 2,
    Pending: 3,
    Success: 4,
    Warning: 5,
    Error: 6,
    Alert: 7,
    Danger: 8,
    Running: 9,
    Stopped: 10,
    Disabled: 11,
    Paused: 12
};

const statusVisuals: Record<number, { icon: string; tone: string }> = {
    1: { icon: 'bi-play-circle-fill', tone: 'positive' },
    2: { icon: 'bi-pause-circle-fill', tone: 'muted' },
    3: { icon: 'bi-hourglass-split', tone: 'info' },
    4: { icon: 'bi-check-circle-fill', tone: 'positive' },
    5: { icon: 'bi-exclamation-triangle-fill', tone: 'warning' },
    6: { icon: 'bi-x-circle-fill', tone: 'danger' },
    7: { icon: 'bi-exclamation-circle-fill', tone: 'warning' },
    8: { icon: 'bi-exclamation-octagon-fill', tone: 'danger' },
    9: { icon: 'bi-play-circle-fill', tone: 'positive' },
    10: { icon: 'bi-stop-circle-fill', tone: 'muted' },
    11: { icon: 'bi-power', tone: 'muted' },
    12: { icon: 'bi-pause-circle-fill', tone: 'muted' }
};

export type StatusIconSize = 'small' | 'medium' | 'large';

const namedStatusIconSizes: Record<string, StatusIconSize> = {
    Small: 'small',
    Medium: 'medium',
    Large: 'large'
};

export function getStatusIconClass(status: PropertyStatus): string {
    const statusCode = typeof status.status === 'number' ? status.status : (namedStatusCodes[status.status] ?? 0);
    const visual = statusVisuals[statusCode] ?? { icon: 'bi-circle-fill', tone: 'muted' };
    return `property-status-icon property-status-${visual.tone} bi ${visual.icon}`;
}

export function getStatusIconSize(size: Property['statusIconSize']): StatusIconSize {
    if (typeof size === 'number') return (['small', 'medium', 'large'] as const)[size] ?? 'small';

    return namedStatusIconSizes[size ?? ''] ?? 'small';
}