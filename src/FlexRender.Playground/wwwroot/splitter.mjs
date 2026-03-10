// Draggable splitter logic for resizable panels.
// Reads data-split attribute from .splitter elements to identify panel pairs.

const STORAGE_KEY = 'flexrender-panel-sizes';

/** @type {Record<string, number>} */
let savedSizes = {};

try {
    savedSizes = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
} catch { /* ignore */ }

function saveSizes() {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(savedSizes));
}

/**
 * Initialize all splitters in the document.
 * Call once after DOM is ready.
 *
 * @param {Record<string, {before: HTMLElement, after: HTMLElement, container: HTMLElement, direction: 'h'|'v'}>} config
 */
export function initSplitters(config) {
    for (const [name, { before, after, container, direction }] of Object.entries(config)) {
        const splitter = document.querySelector(`.splitter[data-split="${name}"]`);
        if (!splitter) continue;

        // Restore saved size
        if (savedSizes[name] !== undefined) {
            applySavedSize(before, after, container, direction, savedSizes[name]);
        }

        let startPos = 0;
        let startSize = 0;

        function onMouseDown(e) {
            e.preventDefault();
            startPos = direction === 'v' ? e.clientX : e.clientY;
            startSize = direction === 'v' ? before.getBoundingClientRect().width : before.getBoundingClientRect().height;
            splitter.classList.add('active');
            document.body.style.cursor = direction === 'v' ? 'col-resize' : 'row-resize';
            document.body.style.userSelect = 'none';

            document.addEventListener('mousemove', onMouseMove);
            document.addEventListener('mouseup', onMouseUp);
        }

        function onMouseMove(e) {
            const currentPos = direction === 'v' ? e.clientX : e.clientY;
            const delta = currentPos - startPos;
            const containerSize = direction === 'v'
                ? container.getBoundingClientRect().width
                : container.getBoundingClientRect().height;

            const newSize = Math.max(50, Math.min(containerSize - 50, startSize + delta));
            const pct = (newSize / containerSize) * 100;

            before.style.flex = `0 0 ${pct}%`;
            after.style.flex = '1 1 0%';
            savedSizes[name] = pct;
        }

        function onMouseUp() {
            splitter.classList.remove('active');
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
            saveSizes();
            window.dispatchEvent(new Event('resize'));
        }

        splitter.addEventListener('mousedown', onMouseDown);
    }
}

function applySavedSize(before, after, container, direction, pct) {
    before.style.flex = `0 0 ${pct}%`;
    after.style.flex = '1 1 0%';
}

/**
 * Set up collapsible sections. Clicking .collapsible labels toggles the parent .editor-section.
 */
export function initCollapsible() {
    document.querySelectorAll('.editor-label.collapsible').forEach(label => {
        label.addEventListener('click', (e) => {
            if (e.target.closest('.files-toolbar')) return;
            const section = label.closest('.editor-section');
            if (section) {
                section.classList.toggle('collapsed');
                window.dispatchEvent(new Event('resize'));
            }
        });
    });
}
