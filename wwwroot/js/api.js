import { apiFetch } from './util.js';

export const apiBase = '/api/files';
export const previewApi = '/api/preview';

export async function listDirectory(path = '') {
    const res = await apiFetch(`${apiBase}?path=${encodeURIComponent(path)}`);
    return res.json();
}

export async function search(query) {
    const res = await apiFetch(`${apiBase}/search?query=${encodeURIComponent(query)}`);
    return res.json();
}

export async function uploadFile(path, file) {
    const form = new FormData();
    form.append('file', file);
    await apiFetch(`${apiBase}/upload?path=${encodeURIComponent(path)}`, {
        method: 'POST',
        body: form
    });
}

export async function createFolder(path) {
    await apiFetch(`${apiBase}/mkdir?path=${encodeURIComponent(path)}`, {
        method: 'POST'
    });
}

export async function zipPaths(paths) {
    return apiFetch(`${apiBase}/zip`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ paths })
    });
}

export async function deletePath(path) {
    await apiFetch(`${apiBase}?path=${encodeURIComponent(path)}`, { method: 'DELETE' });
}

export async function movePath(from, to) {
    await apiFetch(`${apiBase}/move`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ from, to })
    });
}

export async function copyPath(from, to) {
    await apiFetch(`${apiBase}/copy`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ from, to })
    });
}

export async function preview(path) {
    return apiFetch(`${previewApi}?path=${encodeURIComponent(path)}`);
}
