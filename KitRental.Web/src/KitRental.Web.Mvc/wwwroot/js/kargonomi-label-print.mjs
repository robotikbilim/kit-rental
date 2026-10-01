import { getDocument, GlobalWorkerOptions } from '../lib/pdfjs/6.3.289/build/pdf.min.mjs';

const assetUrl = (path) => new URL(`../lib/pdfjs/6.3.289/${path}`, import.meta.url).href;
GlobalWorkerOptions.workerSrc = assetUrl('build/pdf.worker.min.mjs');

const labelMm = 100;
const marginMm = 3;
const renderDpi = 600;
const labelPixels = Math.round(labelMm / 25.4 * renderDpi);
const marginPixels = Math.ceil(marginMm / 25.4 * renderDpi);

// Measure only the outside whitespace. Internal spacing, text, logos and barcode
// geometry remain exactly as supplied by Kargonomi, with one uniform scale.
async function getContentBounds(page) {
    const original = page.getViewport({ scale: 1 });
    const scale = Math.min(2, 1600 / Math.max(original.width, original.height));
    const viewport = page.getViewport({ scale });
    const canvas = document.createElement('canvas');
    canvas.width = Math.ceil(viewport.width);
    canvas.height = Math.ceil(viewport.height);
    const context = canvas.getContext('2d', { willReadFrequently: true });
    try {
        await page.render({ canvasContext: context, viewport, intent: 'print', background: '#ffffff' }).promise;
        const { data } = context.getImageData(0, 0, canvas.width, canvas.height);
        let left = canvas.width;
        let top = canvas.height;
        let right = -1;
        let bottom = -1;
        for (let y = 0; y < canvas.height; y++) {
            for (let x = 0; x < canvas.width; x++) {
                const index = (y * canvas.width + x) * 4;
                if (data[index] < 250 || data[index + 1] < 250 || data[index + 2] < 250) {
                    left = Math.min(left, x);
                    top = Math.min(top, y);
                    right = Math.max(right, x);
                    bottom = Math.max(bottom, y);
                }
            }
        }
        if (right < left) throw new Error('Kargonomi boş bir etiket döndürdü. Lütfen tekrar deneyin.');

        // Include antialiased edges and a small measurement tolerance.
        left = Math.max(0, left - 2);
        top = Math.max(0, top - 2);
        right = Math.min(canvas.width, right + 3);
        bottom = Math.min(canvas.height, bottom + 3);
        return { x: left / scale, y: top / scale, width: (right - left) / scale, height: (bottom - top) / scale };
    } finally {
        canvas.width = canvas.height = 0;
    }
}

async function renderLabel(page, ownerDocument) {
    const bounds = await getContentBounds(page);
    const available = labelPixels - marginPixels * 2;
    const scale = Math.min(available / bounds.width, available / bounds.height);
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = labelPixels;
    try {
        // Render the PDF again at print resolution; never enlarge the measuring
        // bitmap or capture the browser's PDF viewer (which includes its UI).
        await page.render({
            canvasContext: canvas.getContext('2d'),
            viewport: page.getViewport({ scale }),
            transform: [1, 0, 0, 1,
                (labelPixels - bounds.width * scale) / 2 - bounds.x * scale,
                (labelPixels - bounds.height * scale) / 2 - bounds.y * scale],
            intent: 'print',
            background: '#ffffff'
        }).promise;
        const image = ownerDocument.createElement('img');
        image.alt = `Kargonomi kargo etiketi ${page.pageNumber}`;
        image.src = canvas.toDataURL('image/png');
        await image.decode();
        return image;
    } finally {
        canvas.width = canvas.height = 0;
        page.cleanup();
    }
}

export async function printKargonomiLabel(pdfBlob, printWindow) {
    if (printWindow.closed) return;
    const printDocument = printWindow.document;
    printDocument.open();
    printDocument.write(`<!doctype html><html lang="tr"><head><meta charset="utf-8">
        <title>Kargonomi Barkodu · 100 × 100 mm</title><style>
        @page { size: 100mm 100mm; margin: 0; }
        * { box-sizing: border-box; }
        html, body { margin: 0; padding: 0; background: white; }
        .label { width: 100mm; height: 100mm; margin: 0; padding: 0;
            break-inside: avoid; break-after: page; overflow: hidden; }
        .label:last-child { break-after: auto; }
        .label img { display: block; width: 100mm; height: 100mm; }
        @media screen { body { background: #e5e7eb; } .label { margin: 16px auto; } }
        @media print { body { print-color-adjust: exact; -webkit-print-color-adjust: exact; } }
        </style></head><body><p role="status">Kargo etiketi hazırlanıyor…</p></body></html>`);
    printDocument.close();

    const loadingTask = getDocument({
        data: new Uint8Array(await pdfBlob.arrayBuffer()),
        cMapUrl: assetUrl('cmaps/'),
        cMapPacked: true,
        standardFontDataUrl: assetUrl('standard_fonts/'),
        wasmUrl: assetUrl('wasm/'),
        iccUrl: assetUrl('iccs/'),
        isEvalSupported: false,
        stopAtErrors: true
    });
    loadingTask.onPassword = () => {
        // No password dialog is appropriate for a provider shipping label.
        loadingTask.destroy();
    };
    try {
        const pdf = await loadingTask.promise;
        const labels = printDocument.createDocumentFragment();
        for (let pageNumber = 1; pageNumber <= pdf.numPages; pageNumber++) {
            if (printWindow.closed) return;
            const page = await pdf.getPage(pageNumber);
            const label = printDocument.createElement('section');
            label.className = 'label';
            label.append(await renderLabel(page, printDocument));
            labels.append(label);
        }
        if (printWindow.closed) return;
        printDocument.body.replaceChildren(labels);
        // All PNGs have decoded before opening the single print dialog.
        printWindow.focus();
        printWindow.print();
    } finally {
        await loadingTask.destroy();
    }
}
