// Custom YAML autocomplete provider for FlexRender templates.
// Uses the JSON schema directly — no workers, no monaco-yaml dependency.

/**
 * @param {typeof import('monaco-editor')} monaco
 * @param {object} schema - The FlexRender JSON schema
 * @param {object} [options] - Optional configuration
 * @param {() => Array<{path: string, type: string}>} [options.getVfsFiles] - Callback returning VFS files
 */
export function registerYamlAutocomplete(monaco, schema, options = {}) {
    const defs = schema.definitions || {};
    const rootProps = schema.properties || {};

    // Collect element types from the enum
    const elementTypes = rootProps.layout?.items?.$ref
        ? resolveRef(defs, schema, '#/definitions/element')?.properties?.type?.enum || []
        : [];

    // Build a map: elementType -> merged properties (element-specific + flexItemProperties)
    const elementPropsMap = {};
    for (const elType of elementTypes) {
        elementPropsMap[elType] = getElementProperties(schema, defs, elType);
    }

    // --- Hover provider: show property docs on mouse hover ---
    monaco.languages.registerHoverProvider('yaml', {
        provideHover(model, position) {
            const line = model.getLineContent(position.lineNumber);
            const word = model.getWordAtPosition(position);
            if (!word) return null;

            const textUntil = model.getValueInRange({
                startLineNumber: 1, startColumn: 1,
                endLineNumber: position.lineNumber, endColumn: position.column,
            });

            const key = word.word;

            // Check if this word is a YAML key (followed by colon)
            const afterWord = line.substring(word.endColumn - 1).trimStart();
            const isKey = afterWord.startsWith(':');

            // Check if this word is a value after "type:"
            const typeValMatch = line.match(/^\s*-?\s*type:\s*(\w+)/);
            if (typeValMatch && typeValMatch[1] === key) {
                const desc = getElementDescription(defs, key);
                if (desc) {
                    return {
                        range: new monaco.Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn),
                        contents: [
                            { value: `**${key}** element` },
                            { value: desc },
                        ],
                    };
                }
            }

            if (!isKey) return null;

            // Find property definition based on context
            const propDef = findPropertyDef(key, textUntil, schema, defs, elementPropsMap);
            if (!propDef) return null;

            const contents = [{ value: `**${key}**` }];
            if (propDef.description) contents.push({ value: propDef.description });

            const typeInfo = [];
            if (propDef.type) typeInfo.push(`Type: \`${Array.isArray(propDef.type) ? propDef.type.join(' | ') : propDef.type}\``);
            if (propDef.enum) typeInfo.push(`Values: ${propDef.enum.map(v => '`' + v + '`').join(', ')}`);
            if (propDef.minimum !== undefined) typeInfo.push(`Min: \`${propDef.minimum}\``);
            if (propDef.maximum !== undefined) typeInfo.push(`Max: \`${propDef.maximum}\``);
            if (propDef.default !== undefined) typeInfo.push(`Default: \`${propDef.default}\``);
            if (typeInfo.length) contents.push({ value: typeInfo.join(' · ') });

            return {
                range: new monaco.Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn),
                contents,
            };
        }
    });

    // --- Completion provider ---
    monaco.languages.registerCompletionItemProvider('yaml', {
        triggerCharacters: ['\n', ' ', ':'],
        provideCompletionItems(model, position) {
            const textUntilPosition = model.getValueInRange({
                startLineNumber: 1,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column,
            });

            const currentLine = model.getLineContent(position.lineNumber);
            const indent = currentLine.match(/^(\s*)/)[1].length;
            const word = model.getWordUntilPosition(position);

            const range = {
                startLineNumber: position.lineNumber,
                startColumn: word.startColumn,
                endLineNumber: position.lineNumber,
                endColumn: word.endColumn,
            };

            // After a colon with a space — suggest values
            // Handle both "key: " and "- key: " (YAML list item)
            const colonMatch = currentLine.match(/^\s*(?:-\s+)?(\S+)\s*:\s*/);
            if (colonMatch && position.column > currentLine.indexOf(':') + 2) {
                return suggestValues(monaco, colonMatch[1], textUntilPosition, schema, defs, elementPropsMap, range, options);
            }

            // Determine context from indentation
            const context = detectContext(textUntilPosition, indent);

            switch (context.type) {
                case 'root':
                    return makeSuggestions(monaco, rootProps, range, 'root');
                case 'canvas':
                    return makeSuggestions(monaco, rootProps.canvas?.properties || {}, range, 'canvas');
                case 'font-item':
                    return makeSuggestions(monaco, defs.flexItemProperties ? rootProps.fonts?.items?.properties || {} : {}, range, 'font');
                case 'element': {
                    const elType = context.elementType;
                    const props = elType && elementPropsMap[elType]
                        ? elementPropsMap[elType]
                        : { type: { description: 'The element type', type: 'string' } };
                    return makeSuggestions(monaco, props, range, 'element');
                }
                case 'template':
                    return makeSuggestions(monaco, rootProps.template?.properties || {}, range, 'template');
                case 'content-options': {
                    const formatDef = context.format
                        ? defs[context.format + 'Options']
                        : null;
                    const props = formatDef?.properties || {};
                    return makeSuggestions(monaco, props, range, 'content-options');
                }
                case 'content-charset-item': {
                    const props = defs.charsetStyle?.properties || {};
                    return makeSuggestions(monaco, props, range, 'content-charset-item');
                }
                default:
                    return { suggestions: [] };
            }
        }
    });
}

function detectContext(text, currentIndent) {
    const lines = text.split('\n');

    // Walk backwards to find context
    for (let i = lines.length - 2; i >= 0; i--) {
        const line = lines[i];
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('#')) continue;

        const lineIndent = line.match(/^(\s*)/)[1].length;

        // If this line is less indented, it's a parent context
        if (lineIndent < currentIndent) {
            if (trimmed === 'canvas:') return { type: 'canvas' };
            if (trimmed === 'template:') return { type: 'template' };

            // Check for charset designator (single word key, e.g. "I:")
            const isDesignatorKey = trimmed.match(/^\w+:$/);
            if (isDesignatorKey) {
                for (let k = i - 1; k >= 0; k--) {
                    const prev = lines[k];
                    const prevTrimmed = prev.trim();
                    if (!prevTrimmed || prevTrimmed.startsWith('#')) continue;
                    const prevIndent = prev.match(/^(\s*)/)[1].length;
                    if (prevIndent < lineIndent) {
                        if (prevTrimmed === 'charsets:') {
                            return { type: 'content-charset-item' };
                        }
                        break;
                    }
                }
            }

            if (trimmed === 'options:') {
                const parentInfo = findContentParent(lines, i, lineIndent);
                if (parentInfo) {
                    return { type: 'content-options', format: parentInfo.format };
                }
            }
            if (trimmed === 'fonts:' || trimmed === '- name:' || trimmed.startsWith('- name:')) {
                return { type: 'font-item' };
            }
            if (trimmed === 'layout:' || trimmed === 'children:' || trimmed === 'then:' || trimmed === 'else:') {
                return { type: 'element', elementType: null };
            }

            // Check if we're inside an element (look for type: xxx at same or parent indent)
            const typeMatch = trimmed.match(/^-?\s*type:\s*(\w+)/);
            if (typeMatch) {
                return { type: 'element', elementType: typeMatch[1] };
            }

            // Check if it's an array item marker
            if (trimmed.startsWith('- ')) {
                // Look further back for the array parent
                continue;
            }

            // Look for element type in sibling lines at same indent level
            if (lineIndent === currentIndent || lineIndent === currentIndent - 2) {
                // scan siblings
                for (let j = i; j >= 0; j--) {
                    const sibLine = lines[j].trim();
                    const sibIndent = lines[j].match(/^(\s*)/)[1].length;
                    if (sibIndent < lineIndent) break;
                    const sibType = sibLine.match(/^-?\s*type:\s*(\w+)/);
                    if (sibType) {
                        return { type: 'element', elementType: sibType[1] };
                    }
                }
            }
        }
    }

    // Top-level (indent 0)
    if (currentIndent === 0) return { type: 'root' };

    return { type: 'unknown' };
}

function findContentParent(lines, fromIndex, optionsIndent) {
    let format = null;
    for (let i = fromIndex - 1; i >= 0; i--) {
        const line = lines[i];
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('#')) continue;
        const indent = line.match(/^(\s*)/)[1].length;
        if (indent >= optionsIndent) {
            const formatMatch = trimmed.match(/^format:\s*(\w+)/);
            if (formatMatch) format = formatMatch[1];
            continue;
        }
        const typeMatch = trimmed.match(/^-?\s*type:\s*(\w+)/);
        if (typeMatch) {
            if (typeMatch[1] === 'content') return { format };
            return null;
        }
        if (indent < optionsIndent) {
            const formatMatch = trimmed.match(/^-?\s*format:\s*(\w+)/);
            if (formatMatch) format = formatMatch[1];
        }
        if (indent === 0) break;
    }
    return null;
}

function suggestValues(monaco, key, textUntilPosition, schema, defs, elementPropsMap, range, options) {
    const suggestions = [];

    // "type" key — suggest element types
    if (key === 'type' || key === '- type') {
        const types = schema.definitions?.element?.properties?.type?.enum || [];
        for (const t of types) {
            suggestions.push({
                label: t,
                kind: monaco.languages.CompletionItemKind.EnumMember,
                insertText: t,
                range,
                detail: getElementDescription(defs, t),
            });
        }
        return { suggestions };
    }

    // Find the property definition to get enum values
    const propDef = findPropertyDef(key, textUntilPosition, schema, defs, elementPropsMap);
    if (propDef?.enum) {
        for (const v of propDef.enum) {
            suggestions.push({
                label: String(v),
                kind: monaco.languages.CompletionItemKind.EnumMember,
                insertText: String(v),
                range,
                detail: propDef.description || '',
            });
        }
    }

    // Boolean suggestions
    if (propDef?.type === 'boolean') {
        for (const v of ['true', 'false']) {
            suggestions.push({
                label: v,
                kind: monaco.languages.CompletionItemKind.Value,
                insertText: v,
                range,
            });
        }
    }

    // VFS file path suggestions for path/src/content keys
    const cleanKey = key.replace(/^-\s*/, '');
    if (options?.getVfsFiles && isFilePathKey(cleanKey)) {
        const expectedType = getExpectedFileType(cleanKey, textUntilPosition);
        const vfsFiles = options.getVfsFiles();
        for (const file of vfsFiles) {
            const isMatch = file.type === expectedType;
            const insertText = file.path.includes(' ') ? `"${file.path}"` : file.path;
            suggestions.push({
                label: file.path,
                kind: monaco.languages.CompletionItemKind.File,
                insertText,
                range,
                detail: file.type,
                sortText: isMatch ? '0-' + file.path : '1-' + file.path,
            });
        }
    }

    return { suggestions };
}

/** Determines whether the given key should trigger VFS file suggestions. */
function isFilePathKey(key) {
    return key === 'path' || key === 'src' || key === 'content';
}

/** Returns the expected VFS file type based on key and YAML context. */
function getExpectedFileType(key, textUntilPosition) {
    if (key === 'path') {
        // In fonts context, suggest font files
        const lines = textUntilPosition.split('\n');
        for (let i = lines.length - 1; i >= 0; i--) {
            const trimmed = lines[i].trim();
            if (trimmed === 'fonts:' || trimmed.startsWith('- name:')) return 'font';
        }
        return 'font';
    }
    if (key === 'src') {
        // In image element context, suggest images
        return 'image';
    }
    if (key === 'content') {
        // Only suggest files if inside a content-type element
        const lines = textUntilPosition.split('\n');
        for (let i = lines.length - 1; i >= 0; i--) {
            const typeMatch = lines[i].trim().match(/^-?\s*type:\s*(\w+)/);
            if (typeMatch) {
                if (typeMatch[1] === 'content') return 'content';
                // For non-content elements, content is typically a text string, not a file
                return null;
            }
        }
        return null;
    }
    return null;
}

function findPropertyDef(key, textUntilPosition, schema, defs, elementPropsMap) {
    const cleanKey = key.replace(/^-\s*/, '');

    // Check root properties
    if (schema.properties?.[cleanKey]) return schema.properties[cleanKey];

    // Check canvas
    if (schema.properties?.canvas?.properties?.[cleanKey]) return schema.properties.canvas.properties[cleanKey];

    // Check element type from context
    const lines = textUntilPosition.split('\n');
    for (let i = lines.length - 1; i >= 0; i--) {
        const typeMatch = lines[i].trim().match(/^-?\s*type:\s*(\w+)/);
        if (typeMatch) {
            const props = elementPropsMap[typeMatch[1]];
            if (props?.[cleanKey]) return props[cleanKey];
            break;
        }
    }

    // Check if inside content options or charset item
    const optionsContext = detectContentOptionsContext(lines);
    if (optionsContext === 'charset-item') {
        const charsetDef = defs.charsetStyle;
        if (charsetDef?.properties?.[cleanKey]) return charsetDef.properties[cleanKey];
    } else if (optionsContext) {
        const optDef = defs[optionsContext + 'Options'];
        if (optDef?.properties?.[cleanKey]) return optDef.properties[cleanKey];
    }

    // Check flex item properties as fallback
    if (defs.flexItemProperties?.properties?.[cleanKey]) {
        return defs.flexItemProperties.properties[cleanKey];
    }

    return null;
}

function detectContentOptionsContext(lines) {
    for (let i = lines.length - 2; i >= 0; i--) {
        const line = lines[i];
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('#')) continue;
        const indent = line.match(/^(\s*)/)[1].length;

        // Check for charset designator pattern
        if (trimmed.match(/^\w+:$/)) {
            for (let k = i - 1; k >= 0; k--) {
                const prev = lines[k];
                const prevTrimmed = prev.trim();
                if (!prevTrimmed || prevTrimmed.startsWith('#')) continue;
                const prevIndent = prev.match(/^(\s*)/)[1].length;
                if (prevIndent < indent && prevTrimmed === 'charsets:') {
                    return 'charset-item';
                }
                if (prevIndent < indent) break;
            }
        }

        if (trimmed === 'options:') {
            const parentInfo = findContentParent(lines, i, indent);
            return parentInfo?.format || null;
        }

        if (indent === 0) break;
    }
    return null;
}

function makeSuggestions(monaco, properties, range, context) {
    const suggestions = [];
    for (const [key, def] of Object.entries(properties)) {
        // Skip kebab-case aliases if camelCase exists
        if (key.includes('-') && properties[key.replace(/-([a-z])/g, (_, c) => c.toUpperCase())]) {
            continue;
        }

        const kind = def.enum
            ? monaco.languages.CompletionItemKind.Enum
            : def.type === 'object' || def.type === 'array'
                ? monaco.languages.CompletionItemKind.Module
                : monaco.languages.CompletionItemKind.Property;

        let insertText = key + ': ';
        if (def.type === 'array' && key !== 'enum') {
            insertText = key + ':\n  - ';
        } else if (def.type === 'object') {
            insertText = key + ':\n  ';
        }

        suggestions.push({
            label: key,
            kind,
            insertText,
            range,
            detail: formatType(def),
            documentation: def.description || '',
            sortText: getSortOrder(key, context),
        });
    }
    return { suggestions };
}

function formatType(def) {
    if (def.enum) return `enum: ${def.enum.join(' | ')}`;
    if (Array.isArray(def.type)) return def.type.join(' | ');
    return def.type || '';
}

function getSortOrder(key, context) {
    // Prioritize commonly used properties
    const priority = {
        root: { canvas: '0', fonts: '1', layout: '2', template: '3' },
        canvas: { width: '0', height: '1', background: '2', fixed: '3' },
        element: { type: '0', content: '1', children: '1', src: '1', data: '1', direction: '2', size: '2', color: '2', font: '3', padding: '4' },
        font: { name: '0', path: '1', fallback: '2' },
        'content-options': { input_encoding: '0', columns: '1', font_family: '2', char_width_ratio: '3', charsets: '4' },
        'content-charset-item': { font: '0', font_family: '1', font_style: '2', font_size: '3', color: '4', encoding: '5', uppercase: '6' },
    };
    return priority[context]?.[key] || '5';
}

function getElementDescription(defs, type) {
    const defName = type + 'Element';
    return defs[defName]?.description || '';
}

function getElementProperties(schema, defs, elType) {
    const defName = elType + 'Element';
    const elDef = defs[defName];
    if (!elDef) return {};

    // Merge flex item properties + element-specific properties
    const merged = {};

    // Add flex item properties first (from allOf -> $ref)
    if (elDef.allOf) {
        for (const entry of elDef.allOf) {
            if (entry.$ref) {
                const resolved = resolveRef(defs, schema, entry.$ref);
                if (resolved?.properties) {
                    Object.assign(merged, resolved.properties);
                }
            }
        }
    }

    // Add element-specific properties
    if (elDef.properties) {
        Object.assign(merged, elDef.properties);
    }

    // Remove 'type' const — we already suggest it separately
    delete merged.type;

    return merged;
}

function resolveRef(defs, _schema, ref) {
    const path = ref.replace('#/definitions/', '');
    return defs[path] || null;
}
