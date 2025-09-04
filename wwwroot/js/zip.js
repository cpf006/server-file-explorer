import * as api from './api.js';

export async function downloadZip(paths, progressElement) {
    const response = await api.zipPaths(paths);
    const totalSize = parseInt(response.headers.get('Content-Length')) || 0;
    const reader = response.body.getReader();
    let received = 0;
    const chunks = [];
    while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        chunks.push(value);
        received += value.length;
        if (totalSize && progressElement) {
            progressElement.textContent = `Downloading... ${(received / totalSize * 100).toFixed(1)}%`;
        }
    }
    if (progressElement) {
        progressElement.textContent = '';
    }
    const blob = new Blob(chunks, { type: 'application/zip' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'files.zip';
    link.click();
    URL.revokeObjectURL(url);
}
