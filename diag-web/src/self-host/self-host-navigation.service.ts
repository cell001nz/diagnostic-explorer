import { Injectable, signal } from '@angular/core';

export interface SelfHostDrillDownState {
    category: string;
    objectPaths: readonly string[];
    breadcrumbs: readonly string[];
}

interface SelfHostNavigationState {
    category?: string;
    drillDowns: readonly SelfHostDrillDownState[];
}

@Injectable()
export class SelfHostNavigationService {
    readonly state = signal<SelfHostNavigationState>(this.readLocation());
    #started = false;

    initialize(): void {
        if (this.#started) return;

        window.addEventListener('popstate', this.onPopState);
        this.#started = true;
    }

    destroy(): void {
        if (!this.#started) return;

        window.removeEventListener('popstate', this.onPopState);
        this.#started = false;
    }

    selectCategory(category: string): void {
        const current = this.state();
        if (current.category === category && current.drillDowns.length === 0) return;

        this.push({ category, drillDowns: [] });
    }

    replaceCategory(category: string, clearDrillDowns = false): void {
        const current = this.state();
        const drillDowns = clearDrillDowns ? [] : current.drillDowns;
        if (current.category === category && this.sameDrillDowns(current.drillDowns, drillDowns)) return;

        this.replace({ category, drillDowns });
    }

    drillDownOpened(category: string, objectPaths: readonly string[], breadcrumbs: readonly string[]): void {
        const current = this.state();
        const drillDown: SelfHostDrillDownState = {
            category,
            objectPaths: [...objectPaths],
            breadcrumbs: [...breadcrumbs]
        };

        if (current.drillDowns.some((entry) => this.sameDrillDown(entry, drillDown))) return;

        const parentPaths = objectPaths.slice(0, -1);
        const parentIndex = current.drillDowns.findIndex((entry) => this.samePaths(entry.objectPaths, parentPaths));
        const drillDowns = parentPaths.length === 0 ? [] : parentIndex < 0 ? current.drillDowns : current.drillDowns.slice(0, parentIndex + 1);

        this.push({ category: current.category, drillDowns: [...drillDowns, drillDown] });
    }

    drillDownClosed(objectPaths: readonly string[]): void {
        const current = this.state();
        const index = current.drillDowns.findIndex((entry) => this.samePaths(entry.objectPaths, objectPaths));
        if (index < 0) return;

        if (index === current.drillDowns.length - 1) {
            window.history.back();
            return;
        }

        this.replace({ category: current.category, drillDowns: current.drillDowns.slice(0, index) });
    }

    nextDrillDown(category: string, parentPaths: readonly string[], breadcrumbs: readonly string[]): SelfHostDrillDownState | undefined {
        return this.state().drillDowns.find((entry) => entry.category === category && this.samePaths(entry.objectPaths.slice(0, -1), parentPaths) && this.samePaths(entry.breadcrumbs.slice(0, -1), breadcrumbs));
    }

    includesDrillDown(objectPaths: readonly string[]): boolean {
        return this.state().drillDowns.some((entry) => this.samePaths(entry.objectPaths, objectPaths));
    }

    private push(state: SelfHostNavigationState): void {
        this.state.set(state);
        window.history.pushState(null, '', this.toUrl(state));
    }

    private replace(state: SelfHostNavigationState): void {
        this.state.set(state);
        window.history.replaceState(null, '', this.toUrl(state));
    }

    private readLocation(): SelfHostNavigationState {
        const url = new URL(window.location.href);
        const category = url.searchParams.get('cat') ?? undefined;
        const rawDrillDowns = url.searchParams.get('drilldowns');
        if (!rawDrillDowns) return { category, drillDowns: [] };

        try {
            const parsed = JSON.parse(rawDrillDowns) as unknown;
            if (!Array.isArray(parsed)) return { category, drillDowns: [] };

            const drillDowns = parsed.flatMap((entry): SelfHostDrillDownState[] => {
                if (!entry || typeof entry !== 'object') return [];

                const { category: entryCategory, objectPaths, breadcrumbs } = entry as Record<string, unknown>;
                if (typeof entryCategory !== 'string' || !Array.isArray(objectPaths) || !objectPaths.every((path) => typeof path === 'string') || !Array.isArray(breadcrumbs) || !breadcrumbs.every((breadcrumb) => typeof breadcrumb === 'string')) {
                    return [];
                }

                return [{ category: entryCategory, objectPaths, breadcrumbs }];
            });

            return { category, drillDowns };
        } catch {
            return { category, drillDowns: [] };
        }
    }

    private toUrl(state: SelfHostNavigationState): URL {
        const url = new URL(window.location.href);
        if (state.category) {
            url.searchParams.set('cat', state.category);
        } else {
            url.searchParams.delete('cat');
        }

        if (state.drillDowns.length) {
            url.searchParams.set('drilldowns', JSON.stringify(state.drillDowns));
        } else {
            url.searchParams.delete('drilldowns');
        }

        return url;
    }

    private sameDrillDown(left: SelfHostDrillDownState, right: SelfHostDrillDownState): boolean {
        return left.category === right.category && this.samePaths(left.objectPaths, right.objectPaths) && this.samePaths(left.breadcrumbs, right.breadcrumbs);
    }

    private sameDrillDowns(left: readonly SelfHostDrillDownState[], right: readonly SelfHostDrillDownState[]): boolean {
        return left.length === right.length && left.every((entry, index) => this.sameDrillDown(entry, right[index]));
    }

    private samePaths(left: readonly string[], right: readonly string[]): boolean {
        return left.length === right.length && left.every((value, index) => value === right[index]);
    }

    private readonly onPopState = (): void => this.state.set(this.readLocation());
}
