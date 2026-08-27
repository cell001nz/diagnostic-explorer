export interface JsonToken {
    text: string;
    type?: 'json-key' | 'json-string' | 'json-number' | 'json-literal' | 'json-punctuation';
}

export function tokenizeJson(json: string): JsonToken[] {
    try {
        JSON.parse(json);
    } catch {
        return [{ text: json }];
    }

    const tokenPattern = /("(?:\\["\\/bfnrt]|\\u[0-9a-fA-F]{4}|[^"\\])*")|(-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)|\b(true|false|null)\b|([{}\[\],:])/g;
    const tokens: JsonToken[] = [];
    let lastIndex = 0;
    let match: RegExpExecArray | null;

    while ((match = tokenPattern.exec(json)) !== null) {
        if (match.index > lastIndex) {
            tokens.push({ text: json.slice(lastIndex, match.index) });
        }

        const text = match[0];
        const type = match[1] ? (/^\s*:/.test(json.slice(tokenPattern.lastIndex)) ? 'json-key' : 'json-string') : match[2] ? 'json-number' : match[3] ? 'json-literal' : 'json-punctuation';
        tokens.push({ text, type });
        lastIndex = tokenPattern.lastIndex;
    }

    if (lastIndex < json.length) {
        tokens.push({ text: json.slice(lastIndex) });
    }

    return tokens;
}
