import { escapeHtml } from './util.js';
import * as api from './api.js';

let mapInstance = null;

export async function showPreview(path) {
    try {
        const response = await api.preview(path);
        const dialog = document.getElementById('previewDialog');
        const container = document.getElementById('previewContainer');
        const contentType = response.headers.get('Content-Type') || '';
        dialog.showModal();
        if (contentType.includes('application/geo+json')) {
            const data = await response.json();
            container.innerHTML = '<div id="map" style="width:100%;height:100%;"></div>';
            if (mapInstance) {
                mapInstance.remove();
            }
            mapInstance = L.map('map');
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(mapInstance);
            const layer = L.geoJSON(data).addTo(mapInstance);
            const bounds = layer.getBounds();
            if (bounds.isValid()) {
                mapInstance.fitBounds(bounds);
            }
        } else if (contentType.startsWith('image/')) {
            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            container.innerHTML = `<img src="${url}" style="max-width:100%;max-height:100%;" />`;
        } else {
            const text = await response.text();
            container.innerHTML = `<pre style="white-space:pre-wrap;">${escapeHtml(text)}</pre>`;
        }
    } catch (err) {
        console.error(err);
    }
}

export function closePreview() {
    document.getElementById('previewDialog').close();
    const container = document.getElementById('previewContainer');
    container.innerHTML = '';
    if (mapInstance) {
        mapInstance.remove();
        mapInstance = null;
    }
}
