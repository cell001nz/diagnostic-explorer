import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DatePipe, LowerCasePipe } from '@angular/common';
import { LevelToStringPipe } from '@app/pipes/level-to-string.pipe';
import { EventModel } from '@model/EventModel';
import { JsonToken, tokenizeJson } from '@app/diagnostics/json-tokenizer';

interface JsonRange {
    start: number;
    end: number;
}

@Component({
    selector: 'app-event-detail-panel',
    imports: [DatePipe, LowerCasePipe, LevelToStringPipe],
    templateUrl: './event-detail-panel.component.html',
    styleUrl: './event-detail-panel.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class EventDetailPanelComponent {
    event = input.required<EventModel>();
    closed = output<void>();

    detailSegments = computed(() => this.formatDetail(this.event().displayText));

    onDetailKeyDown(event: KeyboardEvent, detail: HTMLElement): void {
        if (event.key.toLowerCase() !== 'a' || (!event.ctrlKey && !event.metaKey)) {
            return;
        }

        const selection = window.getSelection();
        if (!selection) {
            return;
        }

        const range = document.createRange();
        range.selectNodeContents(detail);
        selection.removeAllRanges();
        selection.addRange(range);
        event.preventDefault();
    }

    private formatDetail(detail: string): DetailSegment[] {
        const segments: DetailSegment[] = [];
        let position = 0;
        let jsonRange: JsonRange | undefined;

        while ((jsonRange = this.findJsonRange(detail, position)) !== undefined) {
            if (jsonRange.start > position) {
                segments.push({ text: detail.slice(position, jsonRange.start) });
            }

            segments.push({ jsonTokens: tokenizeJson(detail.slice(jsonRange.start, jsonRange.end)) });
            position = jsonRange.end;
        }

        if (position < detail.length || segments.length === 0) {
            segments.push({ text: detail.slice(position) });
        }

        return segments;
    }

    private findJsonRange(text: string, searchStart: number): JsonRange | undefined {
        for (let start = searchStart; start < text.length; start++) {
            if (text[start] !== '{' && text[start] !== '[') {
                continue;
            }

            const end = this.findJsonEnd(text, start);
            if (end !== undefined) {
                const candidate = text.slice(start, end);
                try {
                    JSON.parse(candidate);
                    return { start, end };
                } catch {
                    // Continue looking for the next JSON object or array.
                }
            }
        }

        return undefined;
    }

    private findJsonEnd(text: string, start: number): number | undefined {
        const expectedClosers: string[] = [];
        let inString = false;
        let escaped = false;

        for (let index = start; index < text.length; index++) {
            const character = text[index];
            if (inString) {
                if (escaped) {
                    escaped = false;
                } else if (character === '\\') {
                    escaped = true;
                } else if (character === '"') {
                    inString = false;
                }
                continue;
            }

            if (character === '"') {
                inString = true;
            } else if (character === '{') {
                expectedClosers.push('}');
            } else if (character === '[') {
                expectedClosers.push(']');
            } else if (character === '}' || character === ']') {
                if (expectedClosers.pop() !== character) {
                    return undefined;
                }
                if (expectedClosers.length === 0) {
                    return index + 1;
                }
            }
        }

        return undefined;
    }
}

interface DetailSegment {
    text?: string;
    jsonTokens?: JsonToken[];
}
