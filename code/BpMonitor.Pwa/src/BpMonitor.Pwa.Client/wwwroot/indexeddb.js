(function () {
    const DB_NAME = 'BpMonitor';
    const READINGS_STORE = 'readings';

    function openDb() {
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, 3);
            req.onupgradeneeded = e => {
                const db = e.target.result;
                if (!db.objectStoreNames.contains(READINGS_STORE)) {
                    db.createObjectStore(READINGS_STORE, { autoIncrement: true });
                }
                if (db.objectStoreNames.contains('handles')) {
                    db.deleteObjectStore('handles');
                }
            };
            req.onsuccess = e => resolve(e.target.result);
            req.onerror = e => reject(e.target.error);
        });
    }

    window.bpMonitor = {
        saveReading: async function (reading) {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(READINGS_STORE, 'readwrite');
                const store = tx.objectStore(READINGS_STORE);
                const req = store.add(reading);
                req.onsuccess = () => resolve();
                req.onerror = e => reject(e.target.error);
            });
        },
        loadReadings: async function () {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(READINGS_STORE, 'readonly');
                const store = tx.objectStore(READINGS_STORE);
                const req = store.getAll();
                req.onsuccess = e => resolve(e.target.result);
                req.onerror = e => reject(e.target.error);
            });
        },
        clearReadings: async function () {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(READINGS_STORE, 'readwrite');
                const store = tx.objectStore(READINGS_STORE);
                const req = store.clear();
                req.onsuccess = () => resolve();
                req.onerror = e => reject(e.target.error);
            });
        },
        exportReadings: async function (json) {
            const blob = new Blob([json], { type: 'application/json' });
            const file = new File([blob], 'readings.json', { type: 'application/json' });
            if (navigator.canShare && navigator.canShare({ files: [file] })) {
                await navigator.share({ files: [file], title: 'BpMonitor readings' });
            } else {
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = 'readings.json';
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            }
        },
        readFileById: async function (id) {
            const input = document.getElementById(id);
            if (!input || !input.files || !input.files[0]) return null;
            return await input.files[0].text();
        },
        saveSettings: function (settings) {
            localStorage.setItem('bpMonitor.webdav', JSON.stringify(settings));
        },
        loadSettings: function () {
            const s = localStorage.getItem('bpMonitor.webdav');
            return s ? JSON.parse(s) : { url: '', username: '', password: '' };
        }
    };
})();
