(() => {
    const form = document.getElementById('lookup-form');
    const input = document.getElementById('identifier');
    const startButton = document.getElementById('scan-qr');
    const stopButton = document.getElementById('stop-scanner');
    const scanner = document.getElementById('qr-scanner');
    const video = document.getElementById('scanner-video');
    const status = document.getElementById('scanner-status');
    let stream;
    let scanning = false;

    if (!form || !input || !startButton || !scanner || !video) return;

    const stop = () => {
        scanning = false;
        stream?.getTracks().forEach(track => track.stop());
        stream = undefined;
        video.srcObject = null;
        scanner.hidden = true;
        startButton.focus();
    };

    const scanFrame = async detector => {
        if (!scanning) return;
        try {
            const codes = await detector.detect(video);
            const value = codes.find(code => code.rawValue)?.rawValue;
            if (value) {
                input.value = value.trim();
                stop();
                form.requestSubmit();
                return;
            }
        } catch { /* Video is not ready for every frame. */ }
        window.setTimeout(() => scanFrame(detector), 160);
    };

    startButton.addEventListener('click', async () => {
        if (!('BarcodeDetector' in window) || !navigator.mediaDevices?.getUserMedia) {
            scanner.hidden = false;
            status.textContent = 'Bu tarayıcı doğrudan QR okumayı desteklemiyor. Seri numarasını elle girebilirsiniz.';
            return;
        }
        scanner.hidden = false;
        status.textContent = 'Kamera izni bekleniyor…';
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
                audio: false
            });
            video.srcObject = stream;
            await video.play();
            const detector = new BarcodeDetector({ formats: ['qr_code'] });
            scanning = true;
            status.textContent = 'QR kod aranıyor…';
            scanFrame(detector);
        } catch {
            status.textContent = 'Kamera açılamadı. Kamera iznini kontrol edin veya seri numarasını elle girin.';
            stream?.getTracks().forEach(track => track.stop());
        }
    });

    stopButton?.addEventListener('click', stop);
    window.addEventListener('pagehide', () => stream?.getTracks().forEach(track => track.stop()));
})();
