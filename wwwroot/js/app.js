import { escapeHtml } from './util.js';
import * as api from './api.js';
import { renderDirectoryList, renderFileList, renderSearchResults } from './ui.js';

const selected = new Set();
let map;

async function load(p) {
    const path = typeof p === 'string'
        ? p
        : (new URLSearchParams(window.location.search).get('path') || '');
    document.getElementById('backBtn').style.display = 'none';
    try {
        const data = await api.listDirectory(path);
        document.getElementById('stats').textContent =
            `Folders: ${data.stats.directoryCount}, Files: ${data.stats.fileCount}, Size: ${data.stats.totalSize} bytes`;

        const list = document.getElementById('listing');
        list.innerHTML = '';
        selected.clear();

        if (path) {
            const parent = path.split('/').slice(0, -1).join('/');
            const li = document.createElement('li');
            const btn = document.createElement('button');
            btn.textContent = '..';
            btn.onclick = () => navigateTo(parent);
            li.appendChild(btn);
            list.appendChild(li);
        }

        renderDirectoryList(list, data.directories, path, navigateTo);
        renderFileList(list, data.files, path);

        document.querySelectorAll('.select').forEach(cb => {
            cb.onchange = () => {
                const p = decodeURIComponent(cb.dataset.path);
                if (cb.checked) selected.add(p); else selected.delete(p);
            };
        });

        const actions = { deletePath, movePath, copyPath, preview };
        document.querySelectorAll('button[data-action]').forEach(btn => {
            const action = actions[btn.dataset.action];
            if (action) {
                btn.onclick = () => action(btn.dataset.path);
            }
        });

        document.getElementById('uploadBtn').onclick = async () => {
            const fileInput = document.getElementById('uploadFile');
            const file = fileInput.files[0];
            if (!file) return;
            try {
                await api.uploadFile(path, file);
                load(path);
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('searchBtn').onclick = async () => {
            const q = document.getElementById('search').value;
            try {
                const d = await api.search(q);
                showSearch(d);
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('createFolderBtn').onclick = async () => {
            const name = prompt('Folder name:');
            if (!name) return;
            const newPath = (path ? path + '/' : '') + name;
            try {
                await api.createFolder(newPath);
                load(path);
            } catch (err) {
                console.error(err);
            }
        };

        document.getElementById('zipBtn').onclick = async () => {
            if (selected.size === 0) return;
            try {
                const res = await api.zipPaths(Array.from(selected));
                const total = parseInt(res.headers.get('Content-Length')) || 0;
                const reader = res.body.getReader();
                let received = 0;
                const chunks = [];
                const progress = document.getElementById('progress');
                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;
                    chunks.push(value);
                    received += value.length;
                    if (total) progress.textContent = `Downloading... ${(received / total * 100).toFixed(1)}%`;
                }
                progress.textContent = '';
                const blob = new Blob(chunks, { type: 'application/zip' });
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = 'files.zip';
                a.click();
                URL.revokeObjectURL(url);
                selected.clear();
                document.querySelectorAll('.select').forEach(cb => cb.checked = false);
            } catch (err) {
                console.error(err);
            }
        };
    } catch (err) {
        console.error(err);
    }
}

function showSearch(data) {
    const list = document.getElementById('listing');
    renderSearchResults(list, data, navigateTo);
    document.getElementById('stats').textContent = 'Search results';
    const back = document.getElementById('backBtn');
    back.style.display = 'inline';
    back.onclick = () => {
        document.getElementById('search').value = '';
        load();
    };
}

async function deletePath(p) {
    const path = decodeURIComponent(p);
    if (!confirm('Delete ' + path + '?')) return;
    try {
        await api.deletePath(path);
        load();
    } catch (err) {
        console.error(err);
    }
}

async function promptAndPost(p, fn, message) {
    const path = decodeURIComponent(p);
    const dest = prompt(message, path);
    if (!dest) return;
    try {
        await fn(path, dest);
        load();
    } catch (err) {
        console.error(err);
    }
}

function movePath(p) {
    promptAndPost(p, api.movePath, 'Move to:');
}

function copyPath(p) {
    promptAndPost(p, api.copyPath, 'Copy to:');
}

async function preview(p) {
    const path = decodeURIComponent(p);
    try {
        const r = await api.preview(path);
        const dlg = document.getElementById('previewDialog');
        const container = document.getElementById('previewContainer');
        const ct = r.headers.get('Content-Type') || '';
        dlg.showModal();
        if (ct.includes('application/geo+json')) {
            const data = await r.json();
            container.innerHTML = '<div id="map" style="width:100%;height:100%;"></div>';
            if (map) {
                map.remove();
            }
            map = L.map('map');
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '© OpenStreetMap contributors'
            }).addTo(map);
            const layer = L.geoJSON(data).addTo(map);
            const bounds = layer.getBounds();
            if (bounds.isValid()) {
                map.fitBounds(bounds);
            }
        } else if (ct.startsWith('image/')) {
            const blob = await r.blob();
            const url = URL.createObjectURL(blob);
            container.innerHTML = `<img src="${url}" style="max-width:100%;max-height:100%;" />`;
        } else {
            const text = await r.text();
            container.innerHTML = `<pre style="white-space:pre-wrap;">${escapeHtml(text)}</pre>`;
        }
    } catch (err) {
        console.error(err);
    }
}

function navigateTo(path) {
    const url = path ? `?path=${encodeURIComponent(path)}` : location.pathname;
    history.pushState({}, '', url);
    load(path);
}

document.getElementById('closePreview').onclick = () => {
    document.getElementById('previewDialog').close();
    const container = document.getElementById('previewContainer');
    container.innerHTML = '';
    if (map) {
        map.remove();
        map = null;
    }
};
window.addEventListener('load', () => load());
window.onpopstate = () => {
    const params = new URLSearchParams(location.search);
    const path = params.get('path') || '';
    load(path);
};
