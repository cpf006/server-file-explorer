export async function apiFetch(url, options) {
    try {
        const res = await fetch(url, options);
        if (!res.ok) {
            const text = await res.text();
            throw new Error(text || res.statusText);
        }
        return res;
    } catch (err) {
        if (typeof alert === 'function') {
            alert('Error: ' + err.message);
        }
        throw err;
    }
}

export function escapeHtml(str) {
    return str.replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}

