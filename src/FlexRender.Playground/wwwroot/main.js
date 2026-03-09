import { dotnet } from './_framework/dotnet.js';

// --- .NET WASM initialization ---
const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments('start')
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const api = exports.FlexRender.Playground.PlaygroundApi;

await runMain();

// --- Load Monaco Editor from CDN ---
const MONACO_CDN = 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min';

function loadScript(src) {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = src;
        script.onload = resolve;
        script.onerror = reject;
        document.head.appendChild(script);
    });
}

window.require = { paths: { vs: `${MONACO_CDN}/vs` } };
await loadScript(`${MONACO_CDN}/vs/loader.js`);

const monaco = await new Promise((resolve) => {
    window.require(['vs/editor/editor.main'], () => resolve(window.monaco));
});

// --- Built-in examples ---
const EXAMPLES = {
    'Simple Text': {
        yaml: `canvas:
  width: 400
  height: 150
  fixed: both
  background: "#ffffff"
fonts:
  - name: main
    path: Inter-Regular.ttf
layout:
  - type: text
    content: "Hello, FlexRender!"
    size: 28
    color: "#333333"
    padding: "30"`,
        json: '{}'
    },
    'Flex Layout': {
        yaml: `canvas:
  width: 400
  height: 200
  fixed: both
  background: "#f5f5f5"
fonts:
  - name: main
    path: Inter-Regular.ttf
layout:
  - type: flex
    direction: row
    gap: "10"
    padding: "20"
    children:
      - type: flex
        background: "#4CAF50"
        padding: "20"
        grow: 1
        children:
          - type: text
            content: "Left"
            color: "#ffffff"
            size: 16
      - type: flex
        background: "#2196F3"
        padding: "20"
        grow: 2
        children:
          - type: text
            content: "Right (grow: 2)"
            color: "#ffffff"
            size: 16`,
        json: '{}'
    },
    'Data Binding': {
        yaml: `canvas:
  width: 400
  height: 300
  fixed: both
  background: "#ffffff"
fonts:
  - name: main
    path: Inter-Regular.ttf
layout:
  - type: flex
    padding: "20"
    gap: "8"
    children:
      - type: text
        content: "{{title}}"
        size: 24
        color: "#333"
      - type: text
        content: "By {{author}}"
        size: 14
        color: "#888"
      - type: separator
        color: "#eee"
      - type: each
        array: items
        children:
          - type: text
            content: "\u2022 {{item}}"
            size: 14
            color: "#555"`,
        json: `{
  "title": "Shopping List",
  "author": "FlexRender",
  "items": ["Apples", "Bread", "Milk", "Cheese"]
}`
    }
};

// --- Create Monaco editors ---
const defaultYaml = EXAMPLES['Simple Text'].yaml;
const defaultJson = '{}';

const yamlEditor = monaco.editor.create(document.getElementById('yaml-editor'), {
    value: defaultYaml,
    language: 'yaml',
    theme: 'vs-dark',
    minimap: { enabled: false },
    fontSize: 13,
    tabSize: 2,
    automaticLayout: true,
    scrollBeyondLastLine: false,
});

const jsonEditor = monaco.editor.create(document.getElementById('json-editor'), {
    value: defaultJson,
    language: 'json',
    theme: 'vs-dark',
    minimap: { enabled: false },
    fontSize: 13,
    tabSize: 2,
    automaticLayout: true,
    scrollBeyondLastLine: false,
});

// --- UI elements ---
const statusBar = document.getElementById('status-bar');
const statusText = document.getElementById('status-text');
const previewImg = document.getElementById('preview-img');
const errorsPane = document.getElementById('errors-pane');
const layoutPane = document.getElementById('layout-pane');

// --- Tab switching ---
document.querySelectorAll('.preview-tabs button').forEach(btn => {
    btn.addEventListener('click', () => {
        document.querySelectorAll('.preview-tabs button').forEach(b => b.classList.remove('active'));
        document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
        btn.classList.add('active');
        document.getElementById(`${btn.dataset.tab}-pane`).classList.add('active');
    });
});

// --- Render function ---
let renderTimeout = null;
let lastObjectUrl = null;

function render() {
    const yaml = yamlEditor.getValue();
    const json = jsonEditor.getValue();

    statusBar.classList.remove('error');
    statusText.textContent = 'Rendering...';

    try {
        // Validate
        const errorsJson = api.Validate(yaml);
        const errors = JSON.parse(errorsJson);

        if (errors.length > 0) {
            errorsPane.textContent = errors.map(e =>
                e.line > 0 ? `Line ${e.line}: ${e.message}` : e.message
            ).join('\n');
            statusBar.classList.add('error');
            statusText.textContent = `${errors.length} error(s)`;
            // Switch to errors tab
            document.querySelectorAll('.preview-tabs button').forEach(b => b.classList.remove('active'));
            document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
            document.querySelector('[data-tab="errors"]').classList.add('active');
            document.getElementById('errors-pane').classList.add('active');
            return;
        }

        errorsPane.textContent = '';

        // Render
        const start = performance.now();
        const dataArg = json.trim() === '{}' || json.trim() === '' ? null : json;
        const pngBytes = api.RenderToPng(yaml, dataArg);
        const elapsed = (performance.now() - start).toFixed(0);

        if (pngBytes && pngBytes.length > 0) {
            if (lastObjectUrl) URL.revokeObjectURL(lastObjectUrl);
            const blob = new Blob([pngBytes], { type: 'image/png' });
            lastObjectUrl = URL.createObjectURL(blob);
            previewImg.src = lastObjectUrl;
            statusText.textContent = `Rendered in ${elapsed}ms \u00b7 ${(pngBytes.length / 1024).toFixed(1)} KB`;
        } else {
            statusText.textContent = 'Render returned empty \u2014 check console';
        }
    } catch (e) {
        errorsPane.textContent = e.message || String(e);
        statusBar.classList.add('error');
        statusText.textContent = 'Error';
    }
}

// --- Debounced render ---
function scheduleRender() {
    clearTimeout(renderTimeout);
    renderTimeout = setTimeout(render, 300);
}

yamlEditor.onDidChangeModelContent(scheduleRender);
jsonEditor.onDidChangeModelContent(scheduleRender);

// --- Examples dropdown ---
const examplesSelect = document.getElementById('examples');
for (const name of Object.keys(EXAMPLES)) {
    const option = document.createElement('option');
    option.value = name;
    option.textContent = name;
    examplesSelect.appendChild(option);
}

examplesSelect.addEventListener('change', () => {
    const example = EXAMPLES[examplesSelect.value];
    if (example) {
        yamlEditor.setValue(example.yaml);
        jsonEditor.setValue(example.json);
    }
});

// --- Export PNG ---
document.getElementById('btn-export-png').addEventListener('click', () => {
    const yaml = yamlEditor.getValue();
    const json = jsonEditor.getValue();
    const dataArg = json.trim() === '{}' || json.trim() === '' ? null : json;

    try {
        const pngBytes = api.RenderToPng(yaml, dataArg);
        if (pngBytes && pngBytes.length > 0) {
            const blob = new Blob([pngBytes], { type: 'image/png' });
            const a = document.createElement('a');
            a.href = URL.createObjectURL(blob);
            a.download = 'flexrender-output.png';
            a.click();
            URL.revokeObjectURL(a.href);
        }
    } catch (e) {
        alert('Export failed: ' + (e.message || e));
    }
});

// --- Drag & drop ---
const dropOverlay = document.getElementById('drop-overlay');
let dragCounter = 0;

const FONT_EXT = ['.ttf', '.otf', '.woff2'];
const IMAGE_EXT = ['.png', '.jpg', '.jpeg', '.svg', '.gif', '.webp'];
const CONTENT_EXT = ['.ndc', '.txt'];

document.addEventListener('dragenter', (e) => {
    e.preventDefault();
    dragCounter++;
    dropOverlay.classList.add('visible');
});

document.addEventListener('dragleave', (e) => {
    e.preventDefault();
    dragCounter--;
    if (dragCounter === 0) dropOverlay.classList.remove('visible');
});

document.addEventListener('dragover', (e) => e.preventDefault());

document.addEventListener('drop', async (e) => {
    e.preventDefault();
    dragCounter = 0;
    dropOverlay.classList.remove('visible');

    const loaded = [];
    for (const file of e.dataTransfer.files) {
        const ext = '.' + file.name.split('.').pop().toLowerCase();
        const buffer = new Uint8Array(await file.arrayBuffer());

        if (FONT_EXT.includes(ext)) {
            api.LoadFont(file.name, buffer);
            loaded.push(`font: ${file.name}`);
        } else if (IMAGE_EXT.includes(ext)) {
            api.LoadImage(file.name, buffer);
            loaded.push(`image: ${file.name}`);
        } else if (CONTENT_EXT.includes(ext)) {
            api.LoadContent(file.name, buffer);
            loaded.push(`content: ${file.name}`);
        }
    }

    if (loaded.length > 0) {
        statusText.textContent = `Loaded: ${loaded.join(', ')}`;
        scheduleRender();
    }
});

// --- Show app & initial render ---
document.getElementById('loading').style.display = 'none';
document.getElementById('app').style.display = 'flex';
render();
