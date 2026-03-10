import { dotnet } from './_framework/dotnet.js';

// --- .NET WASM initialization ---
const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withApplicationArguments('start')
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const api = exports.FlexRender.Playground.PlaygroundApi;

await runMain();

// --- VFS & Splitter modules ---
import * as vfs from './vfs.mjs';
import { initSplitters, initCollapsible } from './splitter.mjs';

// --- Monaco Editor via modern-monaco from CDN ---
const { init } = await import('https://esm.sh/modern-monaco');
const monaco = await init({
    langs: ['yaml', 'json'],
    themes: ['one-dark-pro'],
});

// --- Custom YAML autocomplete (schema-driven, no workers) ---
try {
    const { registerYamlAutocomplete } = await import('./yaml-autocomplete.mjs');
    const schemaResponse = await fetch('schemas/flexrender-template.json');
    const flexrenderSchema = await schemaResponse.json();
    registerYamlAutocomplete(monaco, flexrenderSchema);
    console.log('YAML autocomplete registered with FlexRender schema');
} catch (e) {
    console.warn('YAML autocomplete setup failed:', e.message);
}

// --- Restore VFS from IndexedDB and sync to WASM ---
const restoredCount = await vfs.restore();
if (restoredCount > 0) {
    for (const entry of vfs.allEntries()) {
        api.LoadResource(entry.path, entry.data);
    }
    console.log(`Restored ${restoredCount} files from IndexedDB`);
}

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

const yamlModelUri = monaco.Uri.parse('file:///template.yaml');
const yamlModel = monaco.editor.createModel(defaultYaml, 'yaml', yamlModelUri);

const yamlEditor = monaco.editor.create(document.getElementById('yaml-editor'), {
    model: yamlModel,
    theme: 'one-dark-pro',
    minimap: { enabled: false },
    fontSize: 13,
    tabSize: 2,
    automaticLayout: true,
    scrollBeyondLastLine: false,
    quickSuggestions: {
        other: true,
        comments: false,
        strings: true,
    },
});

const jsonEditor = monaco.editor.create(document.getElementById('json-editor'), {
    value: defaultJson,
    language: 'json',
    theme: 'one-dark-pro',
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

// --- Debug overlay toggle ---
const previewTabs = document.querySelector('.preview-tabs');
const zoomSelect = document.getElementById('zoom-select');

const overlayToggle = document.createElement('label');
overlayToggle.id = 'overlay-toggle';
overlayToggle.innerHTML = '<input type="checkbox" id="overlay-checkbox" /> Debug overlay';
previewTabs.insertBefore(overlayToggle, zoomSelect);

const overlayCheckbox = document.getElementById('overlay-checkbox');
let debugMode = false;

overlayCheckbox.addEventListener('change', () => {
    debugMode = overlayCheckbox.checked;
    scheduleRender();
});

// --- Layout tree builder (with data attributes for highlight) ---
let canvasWidth = 0;
let canvasHeight = 0;

function buildLayoutTree(node, depth) {
    if (!node || !node.type) return '';
    const dims = `${node.w}\u00d7${node.h} @ (${node.x}, ${node.y})`;
    const hasChildren = node.children && node.children.length > 0;
    const openAttr = depth < 2 ? ' open' : '';
    const dataAttrs = `data-x="${node.x}" data-y="${node.y}" data-w="${node.w}" data-h="${node.h}"`;

    const props = [];
    if (node.content) props.push(`"${node.content}"`);
    if (node.font) props.push(`font=${node.font}`);
    if (node.fontFamily) props.push(`family=${node.fontFamily}`);
    if (node.size) props.push(`size=${node.size}`);
    if (node.color) props.push(`color=${node.color}`);
    if (node.fontWeight) props.push(`weight=${node.fontWeight}`);
    if (node.fontStyle) props.push(`style=${node.fontStyle}`);
    if (node.direction && node.type === 'Flex') props.push(node.direction);
    if (node.align) props.push(`align=${node.align}`);
    if (node.justify) props.push(`justify=${node.justify}`);
    if (node.fontSize) props.push(`fontSize=${node.fontSize}px`);
    if (node.textLines) props.push(`lines=${node.textLines}`);

    const propsStr = props.length > 0 ? ` <span class="node-props">[${props.join(', ')}]</span>` : '';

    if (hasChildren) {
        const childrenHtml = node.children.map(c => buildLayoutTree(c, depth + 1)).join('');
        return `<details${openAttr}><summary ${dataAttrs}><span class="node-type" data-type="${node.type}">${node.type}</span> <span class="node-dims">${dims}</span>${propsStr}</summary>${childrenHtml}</details>`;
    }
    return `<div class="layout-leaf" ${dataAttrs}><span class="node-type" data-type="${node.type}">${node.type}</span> <span class="node-dims">${dims}</span>${propsStr}</div>`;
}

// --- File tree rendering ---
const filesTree = document.getElementById('files-tree');

function renderFileTree() {
    const tree = vfs.buildTree();
    filesTree.innerHTML = tree.length === 0 ? '' : renderTreeNodes(tree);
}

function renderTreeNodes(nodes) {
    return nodes.map(node => {
        if (node.isDir) {
            return `<div class="ft-dir open" data-path="${escHtml(node.path)}">
                <div class="ft-node" draggable="true" data-path="${escHtml(node.path)}" data-dir="true">
                    <span class="ft-arrow">&#x25B6;</span>
                    <span class="ft-icon">&#x1F4C1;</span>
                    <span class="ft-name">${escHtml(node.name)}</span>
                </div>
                <div class="ft-children">${renderTreeNodes(node.children)}</div>
            </div>`;
        }
        const icon = node.type === 'font' ? '&#x1F524;' : node.type === 'image' ? '&#x1F5BC;' : '&#x1F4C4;';
        const file = vfs.getFile(node.path);
        const size = file ? formatFileSize(file.data.length) : '';
        return `<div class="ft-file" data-path="${escHtml(node.path)}">
            <div class="ft-node" draggable="true" data-path="${escHtml(node.path)}">
                <span class="ft-icon">${icon}</span>
                <span class="ft-name">${escHtml(node.name)}</span>
                <span class="ft-size">${size}</span>
            </div>
        </div>`;
    }).join('');
}

function escHtml(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function formatFileSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
}

// Toggle directory open/closed + click to copy path
filesTree.addEventListener('click', (e) => {
    const dir = e.target.closest('.ft-dir');
    const node = e.target.closest('.ft-node');
    if (dir && node && node.dataset.dir) {
        dir.classList.toggle('open');
        return;
    }
    if (node && !node.dataset.dir) {
        navigator.clipboard.writeText(node.dataset.path).then(() => {
            statusText.textContent = `Copied: ${node.dataset.path}`;
        });
    }
});

// Re-render tree when VFS changes
vfs.subscribe(() => renderFileTree());
renderFileTree();

// --- New folder button ---
document.getElementById('btn-add-folder').addEventListener('click', (e) => {
    e.stopPropagation();
    const name = prompt('Folder name:');
    if (!name || !name.trim()) return;
    vfs.addFile(name.trim() + '/.gitkeep', new Uint8Array(0), 'other');
});

// --- Context menu ---
let ctxMenu = null;

function showContextMenu(x, y, items) {
    hideContextMenu();
    ctxMenu = document.createElement('div');
    ctxMenu.className = 'ctx-menu';
    ctxMenu.style.left = x + 'px';
    ctxMenu.style.top = y + 'px';

    for (const item of items) {
        if (item === '---') {
            const sep = document.createElement('div');
            sep.className = 'ctx-menu-sep';
            ctxMenu.appendChild(sep);
            continue;
        }
        const el = document.createElement('div');
        el.className = 'ctx-menu-item';
        el.innerHTML = `<span>${item.label}</span>${item.shortcut ? `<span class="ctx-menu-shortcut">${item.shortcut}</span>` : ''}`;
        el.addEventListener('click', () => { hideContextMenu(); item.action(); });
        ctxMenu.appendChild(el);
    }

    document.body.appendChild(ctxMenu);
    const rect = ctxMenu.getBoundingClientRect();
    if (rect.right > window.innerWidth) ctxMenu.style.left = (window.innerWidth - rect.width - 4) + 'px';
    if (rect.bottom > window.innerHeight) ctxMenu.style.top = (window.innerHeight - rect.height - 4) + 'px';
}

function hideContextMenu() {
    if (ctxMenu) { ctxMenu.remove(); ctxMenu = null; }
}

document.addEventListener('click', hideContextMenu);
document.addEventListener('contextmenu', (e) => {
    if (!e.target.closest('.files-tree')) hideContextMenu();
});

filesTree.addEventListener('contextmenu', (e) => {
    e.preventDefault();
    const node = e.target.closest('.ft-node');
    if (!node) return;

    const path = node.dataset.path;
    const isDir = !!node.dataset.dir;
    const items = [];

    items.push({ label: 'Copy path', action: () => navigator.clipboard.writeText(path) });
    items.push({
        label: 'Rename',
        action: () => startRename(node, path, isDir),
    });

    if (isDir) {
        items.push({
            label: 'New folder inside',
            action: () => {
                const name = prompt('Folder name:');
                if (name?.trim()) vfs.addFile(path + '/' + name.trim() + '/.gitkeep', new Uint8Array(0), 'other');
            },
        });
    } else {
        items.push({
            label: 'Duplicate',
            action: () => {
                const entry = vfs.getFile(path);
                if (!entry) return;
                const parts = path.split('/');
                const filename = parts.pop();
                const ext = filename.includes('.') ? '.' + filename.split('.').pop() : '';
                const base = ext ? filename.slice(0, -ext.length) : filename;
                const newName = base + '-copy' + ext;
                const newPath = [...parts, newName].join('/');
                vfs.addFile(newPath, entry.data, entry.type);
            },
        });
    }

    items.push('---');
    items.push({
        label: 'Delete',
        action: () => {
            if (isDir) {
                for (const f of vfs.listFiles()) {
                    if (f.startsWith(path + '/')) vfs.removeFile(f);
                }
            } else {
                vfs.removeFile(path);
            }
        },
    });

    showContextMenu(e.clientX, e.clientY, items);
});

function startRename(node, path, isDir) {
    const nameSpan = node.querySelector('.ft-name');
    const oldName = nameSpan.textContent;
    const input = document.createElement('input');
    input.className = 'ft-name-input';
    input.value = oldName;
    nameSpan.replaceWith(input);
    input.focus();
    input.select();

    function commit() {
        const newName = input.value.trim();
        input.replaceWith(nameSpan);
        if (!newName || newName === oldName) return;

        if (isDir) {
            const prefix = path + '/';
            const parts = path.split('/');
            parts[parts.length - 1] = newName;
            const newPrefix = parts.join('/') + '/';
            for (const f of vfs.listFiles()) {
                if (f.startsWith(prefix)) {
                    vfs.renameFile(f, newPrefix + f.slice(prefix.length));
                }
            }
        } else {
            const parts = path.split('/');
            parts[parts.length - 1] = newName;
            vfs.renameFile(path, parts.join('/'));
        }
    }

    input.addEventListener('blur', commit);
    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') { e.preventDefault(); input.blur(); }
        if (e.key === 'Escape') { input.value = oldName; input.blur(); }
    });
}

// --- Internal drag & drop (move files between folders) ---
filesTree.addEventListener('dragstart', (e) => {
    const node = e.target.closest('.ft-node');
    if (!node) return;
    e.dataTransfer.setData('text/x-vfs-path', node.dataset.path);
    e.dataTransfer.effectAllowed = 'move';
});

filesTree.addEventListener('dragover', (e) => {
    if (!e.dataTransfer.types.includes('text/x-vfs-path')) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    filesTree.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));
    const dir = e.target.closest('.ft-dir');
    if (dir) dir.querySelector(':scope > .ft-node')?.classList.add('drag-over');
});

filesTree.addEventListener('dragleave', (e) => {
    const node = e.target.closest('.ft-node');
    if (node) node.classList.remove('drag-over');
});

filesTree.addEventListener('drop', (e) => {
    filesTree.querySelectorAll('.drag-over').forEach(el => el.classList.remove('drag-over'));
    const sourcePath = e.dataTransfer.getData('text/x-vfs-path');
    if (!sourcePath) return;
    e.preventDefault();

    const targetDir = e.target.closest('.ft-dir');
    const targetPath = targetDir ? targetDir.dataset.path : '';
    if (sourcePath === targetPath) return;

    const fileName = sourcePath.split('/').pop();
    const newPath = targetPath ? targetPath + '/' + fileName : fileName;
    if (sourcePath === newPath) return;

    const isDir = vfs.listFiles().some(f => f.startsWith(sourcePath + '/'));
    if (isDir) {
        const prefix = sourcePath + '/';
        const newPrefix = newPath + '/';
        for (const f of vfs.listFiles()) {
            if (f.startsWith(prefix)) vfs.renameFile(f, newPrefix + f.slice(prefix.length));
        }
    } else {
        vfs.renameFile(sourcePath, newPath);
    }
});

// --- Tab switching (preview / errors only) ---
function switchToTab(tabName) {
    document.querySelectorAll('.preview-tabs button').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-pane').forEach(p => p.classList.remove('active'));
    document.querySelector(`[data-tab="${tabName}"]`).classList.add('active');
    document.getElementById(`${tabName}-pane`).classList.add('active');
}

document.querySelectorAll('.preview-tabs button').forEach(btn => {
    btn.addEventListener('click', () => switchToTab(btn.dataset.tab));
});

// --- Layout inspector toggle ---
const layoutSection = document.getElementById('layout-section');
document.getElementById('layout-header').addEventListener('click', () => {
    layoutSection.classList.toggle('collapsed');
});

// --- Canvas highlight overlay for layout tree hover ---
const highlightCanvas = document.getElementById('highlight-canvas');
const highlightCtx = highlightCanvas.getContext('2d');
const previewImageWrap = document.getElementById('preview-image-wrap');

function syncCanvasSize() {
    // Use the image's CSS (layout) size before any transform scaling.
    // Since canvas is inside the same scaled wrapper, we match the un-scaled img size.
    const w = previewImg.offsetWidth;
    const h = previewImg.offsetHeight;
    if (w === 0) return;
    highlightCanvas.width = w * devicePixelRatio;
    highlightCanvas.height = h * devicePixelRatio;
    highlightCanvas.style.width = w + 'px';
    highlightCanvas.style.height = h + 'px';
    highlightCtx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
}

function showHighlight(x, y, w, h) {
    if (!previewImg.naturalWidth || canvasWidth === 0) return;
    syncCanvasSize();
    const imgW = previewImg.offsetWidth;
    const imgH = previewImg.offsetHeight;
    if (imgW === 0) return;

    const scaleX = imgW / canvasWidth;
    const scaleY = imgH / canvasHeight;
    const px = x * scaleX;
    const py = y * scaleY;
    const pw = w * scaleX;
    const ph = h * scaleY;

    highlightCtx.clearRect(0, 0, imgW, imgH);

    // Fill with semi-transparent color
    highlightCtx.fillStyle = 'rgba(255, 90, 50, 0.12)';
    highlightCtx.fillRect(px, py, pw, ph);

    // Thick border like debug overlay
    highlightCtx.strokeStyle = 'rgba(255, 90, 50, 0.8)';
    highlightCtx.lineWidth = 2;
    highlightCtx.strokeRect(px, py, pw, ph);

    // Dimension label
    highlightCtx.font = '10px system-ui, sans-serif';
    highlightCtx.fillStyle = 'rgba(255, 90, 50, 0.9)';
    const label = `${Math.round(w)}\u00d7${Math.round(h)}`;
    const textY = py > 14 ? py - 3 : py + ph + 12;
    highlightCtx.fillText(label, px + 2, textY);
}

function hideHighlight() {
    const imgW = previewImg.offsetWidth;
    const imgH = previewImg.offsetHeight;
    highlightCtx.clearRect(0, 0, imgW, imgH);
}

layoutPane.addEventListener('mouseover', (e) => {
    const target = e.target.closest('summary[data-x], .layout-leaf[data-x]');
    if (!target) return;
    showHighlight(
        parseFloat(target.dataset.x),
        parseFloat(target.dataset.y),
        parseFloat(target.dataset.w),
        parseFloat(target.dataset.h)
    );
});

layoutPane.addEventListener('mouseout', (e) => {
    const target = e.target.closest('summary[data-x], .layout-leaf[data-x]');
    if (target) hideHighlight();
});

// --- Preview zoom & scroll ---
let zoomLevel = 1;
const previewContent = document.querySelector('.preview-content');

function applyZoom(level) {
    zoomLevel = level;
    if (level === 1) {
        previewImageWrap.style.transform = '';
    } else {
        previewImageWrap.style.transform = `scale(${level})`;
        previewImageWrap.style.transformOrigin = 'top left';
    }
    // Update dropdown to nearest preset (or clear custom)
    const nearest = [...zoomSelect.options].find(o => o.value !== 'fit' && Math.abs(parseFloat(o.value) - level) < 0.05);
    zoomSelect.value = nearest ? nearest.value : '';
}

zoomSelect.addEventListener('change', () => {
    const val = zoomSelect.value;
    if (val === 'fit') {
        applyZoom(1);
        previewImageWrap.style.transform = '';
        zoomSelect.value = 'fit';
        return;
    }
    applyZoom(parseFloat(val));
});

previewContent.addEventListener('wheel', (e) => {
    if (!e.ctrlKey && !e.metaKey) return;
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.1 : 0.1;
    applyZoom(Math.max(0.25, Math.min(5, zoomLevel + delta)));
}, { passive: false });

// Double-click to reset zoom
previewContent.addEventListener('dblclick', () => {
    applyZoom(1);
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
        const errorsJson = api.Validate(yaml);
        const errors = JSON.parse(errorsJson);

        if (errors.length > 0) {
            errorsPane.textContent = errors.map(e =>
                e.line > 0 ? `Line ${e.line}: ${e.message}` : e.message
            ).join('\n');
            statusBar.classList.add('error');
            statusText.textContent = `${errors.length} error(s)`;
            switchToTab('errors');
            return;
        }

        errorsPane.textContent = '';

        const start = performance.now();
        const dataArg = json.trim() === '{}' || json.trim() === '' ? null : json;
        const pngBytes = debugMode
            ? api.RenderDebugPng(yaml, dataArg)
            : api.RenderToPng(yaml, dataArg);
        const elapsed = (performance.now() - start).toFixed(0);

        if (pngBytes && pngBytes.length > 0) {
            if (lastObjectUrl) URL.revokeObjectURL(lastObjectUrl);
            const blob = new Blob([pngBytes], { type: 'image/png' });
            lastObjectUrl = URL.createObjectURL(blob);
            previewImg.src = lastObjectUrl;
            const modeLabel = debugMode ? ' [debug]' : '';
            statusText.textContent = `Rendered in ${elapsed}ms \u00b7 ${(pngBytes.length / 1024).toFixed(1)} KB${modeLabel}`;

            try {
                const layoutJson = api.GetLayout(yaml, dataArg);
                const layoutData = JSON.parse(layoutJson);
                canvasWidth = layoutData.w || 0;
                canvasHeight = layoutData.h || 0;
                layoutPane.innerHTML = '<div class="layout-tree">' + buildLayoutTree(layoutData, 0) + '</div>';
            } catch (layoutErr) {
                console.warn('Layout computation failed:', layoutErr);
                layoutPane.innerHTML = '<div class="layout-error">Layout unavailable</div>';
            }

            switchToTab('preview');
        } else {
            statusText.textContent = 'Render returned empty \u2014 check console';
        }
    } catch (e) {
        errorsPane.textContent = e.message || String(e);
        statusBar.classList.add('error');
        statusText.textContent = 'Error';
        switchToTab('errors');
    }
}

// --- Debounced render ---
function scheduleRender() {
    clearTimeout(renderTimeout);
    renderTimeout = setTimeout(render, 300);
}

yamlEditor.onDidChangeModelContent(scheduleRender);
jsonEditor.onDidChangeModelContent(scheduleRender);

// Sync VFS changes to WASM MemoryResourceLoader
vfs.subscribe((event, path) => {
    if (event === 'add') {
        const entry = vfs.getFile(path);
        if (entry && entry.data.length > 0) api.LoadResource(entry.path, entry.data);
    } else if (event === 'remove') {
        api.RemoveResource(path);
    }
    scheduleRender();
});

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

// --- Drag & drop from OS ---
const dropOverlay = document.getElementById('drop-overlay');
let dragCounter = 0;

document.addEventListener('dragenter', (e) => {
    e.preventDefault();
    if (e.dataTransfer.types.includes('text/x-vfs-path')) return;
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
    if (e.dataTransfer.getData('text/x-vfs-path')) return;
    e.preventDefault();
    dragCounter = 0;
    dropOverlay.classList.remove('visible');

    const items = e.dataTransfer.items;
    const fileEntries = [];

    if (items) {
        for (const item of items) {
            const entry = item.webkitGetAsEntry?.();
            if (entry) {
                await collectEntries(entry, '', fileEntries);
            }
        }
    }

    if (fileEntries.length === 0) {
        for (const file of e.dataTransfer.files) {
            const buffer = new Uint8Array(await file.arrayBuffer());
            fileEntries.push({ path: file.name, data: buffer });
        }
    }

    for (const { path, data } of fileEntries) {
        const type = vfs.detectType(path);
        await vfs.addFile(path, data, type);
    }

    if (fileEntries.length > 0) {
        statusText.textContent = `Added ${fileEntries.length} file(s) to VFS`;
        scheduleRender();
    }
});

async function collectEntries(entry, prefix, results) {
    if (entry.isFile) {
        const file = await new Promise((resolve) => entry.file(resolve));
        const data = new Uint8Array(await file.arrayBuffer());
        results.push({ path: prefix + entry.name, data });
    } else if (entry.isDirectory) {
        const reader = entry.createReader();
        const entries = await new Promise((resolve) => reader.readEntries(resolve));
        for (const child of entries) {
            await collectEntries(child, prefix + entry.name + '/', results);
        }
    }
}

// --- Init resizable panels ---
const editorPanel = document.getElementById('editor-panel');
const yamlSection = document.getElementById('yaml-section');
const jsonSection = document.getElementById('json-section');
const filesSection = document.getElementById('files-section');
const previewPanel = document.querySelector('.preview-panel');

initSplitters({
    'yaml-json': { before: yamlSection, after: jsonSection, container: editorPanel, direction: 'h' },
    'json-files': { before: jsonSection, after: filesSection, container: editorPanel, direction: 'h' },
    'editor-preview': { before: editorPanel, after: previewPanel, container: document.querySelector('.main-content'), direction: 'v' },
    'preview-layout': { before: previewContent, after: layoutSection, container: previewPanel, direction: 'h' },
});

initCollapsible();

// Ensure embedded font appears in VFS
if (!vfs.exists('Inter-Regular.ttf')) {
    await vfs.addFile('Inter-Regular.ttf', new TextEncoder().encode('(embedded)'), 'font');
}

// --- Show app & initial render ---
document.getElementById('loading').style.display = 'none';
document.getElementById('app').style.display = 'flex';
render();
