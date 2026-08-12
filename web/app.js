/**
 * CAPTURE - Modern WebView2 Frontend
 * Communicates with C# backend via window.chrome.webview.postMessage
 */

(function() {
    'use strict';

    // ===================================
    // State Management
    // ===================================
    const state = {
        running: false,
        frozen: false,
        gameMode: false,
        fakeCursor: false,
        settings: {
            fpsMin: 8,
            fpsMax: 15,
            quality: 75,
            pixelation: 1,
            region: { x: 0, y: 0, width: 1920, height: 1080 }
        },
        regionSelectorVisible: false,
        regionDragging: false,
        regionDragHandle: null,
        regionDragStart: { x: 0, y: 0 },
        regionStartRect: { x: 0, y: 0, width: 0, height: 0 }
    };

    // ===================================
    // DOM Element References
    // ===================================
    const els = {
        // Quick Bar
        quickBar: document.getElementById('quickBar'),
        statusDot: document.getElementById('statusDot'),
        statusText: document.getElementById('statusText'),
        expandBtn: document.getElementById('expandBtn'),
        freezeBtnQuick: document.getElementById('freezeBtn'),
        settingsBtnQuick: document.getElementById('settingsBtn'),

        // Settings Panel
        settingsPanel: document.getElementById('settingsPanel'),
        closeSettingsBtn: document.getElementById('closeSettingsBtn'),
        startBtn: document.getElementById('startBtn'),
        stopBtn: document.getElementById('stopBtn'),
        freezeBtnMain: document.getElementById('freezeBtnMain'),
        freezeBtnText: document.getElementById('freezeBtnText'),
        cursorBtn: document.getElementById('cursorBtn'),
        gameModeBtn: document.getElementById('gameModeBtn'),

        // Inputs
        fpsMin: document.getElementById('fpsMin'),
        fpsMax: document.getElementById('fpsMax'),
        quality: document.getElementById('quality'),
        qualityValue: document.getElementById('qualityValue'),
        pixelation: document.getElementById('pixelation'),
        pixelationValue: document.getElementById('pixelationValue'),
        regionX: document.getElementById('regionX'),
        regionY: document.getElementById('regionY'),
        regionW: document.getElementById('regionW'),
        regionH: document.getElementById('regionH'),

        // Preset Buttons
        qualityPresets: document.querySelectorAll('.preset-btn[data-quality]'),
        pixelationPresets: document.querySelectorAll('.preset-btn[data-pixel]'),
        regionPresets: document.querySelectorAll('.preset-btn[data-preset]'),

        // Region
        showRegionBtn: document.getElementById('showRegionBtn'),
        hideRegionBtn: document.getElementById('hideRegionBtn'),
        regionSelector: document.getElementById('regionSelector'),
        regionFrame: document.getElementById('regionFrame'),
        regionInfo: document.getElementById('regionInfo'),
        regionHandles: document.querySelectorAll('.region-handle'),

        // Status
        statusBadge: document.getElementById('statusBadge')
    };

    // ===================================
    // WebView2 Communication
    // ===================================
    const messageId = { current: 0 };
    const pendingCallbacks = new Map();

    function sendMessage(type, payload) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(JSON.stringify({ type, payload }));
        }
    }

    function sendMessageWithCallback(type, payload) {
        return new Promise((resolve, reject) => {
            const id = ++messageId.current;
            pendingCallbacks.set(id, { resolve, reject });
            sendMessage(type, { ...payload, _msgId: id });
            // Timeout after 5 seconds
            setTimeout(() => {
                if (pendingCallbacks.has(id)) {
                    pendingCallbacks.delete(id);
                    reject(new Error('Message timeout'));
                }
            }, 5000);
        });
    }

    function handleResponse(message) {
        const { _msgId, type, payload, error } = message;
        const callback = pendingCallbacks.get(_msgId);
        if (callback) {
            pendingCallbacks.delete(_msgId);
            if (error) callback.reject(new Error(error));
            else callback.resolve(payload);
        }
    }

    // Listen for messages from C#
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.addEventListener('message', (e) => {
            try {
                const message = JSON.parse(e.data);
                if (message._msgId) {
                    handleResponse(message);
                } else {
                    handleIncomingMessage(message);
                }
            } catch (err) {
                console.error('Failed to parse message:', err);
            }
        });
    }

    function handleIncomingMessage(message) {
        switch (message.type) {
            case 'stateUpdate':
                updateState(message.payload);
                break;
            case 'settingsUpdate':
                updateSettings(message.payload);
                break;
            case 'regionUpdate':
                updateRegion(message.payload);
                break;
            case 'toast':
                showToast(message.payload.message, message.payload.type || 'info');
                break;
        }
    }

    // ===================================
    // State Updates
    // ===================================
    function updateState(newState) {
        Object.assign(state, newState);
        render();
    }

    function updateSettings(newSettings) {
        Object.assign(state.settings, newSettings);
        syncInputsToState();
        render();
    }

    function updateRegion(region) {
        state.settings.region = region;
        syncInputsToState();
        updateRegionSelector();
    }

    function syncInputsToState() {
        els.fpsMin.value = state.settings.fpsMin;
        els.fpsMax.value = state.settings.fpsMax;
        els.quality.value = state.settings.quality;
        els.qualityValue.textContent = state.settings.quality;
        els.pixelation.value = state.settings.pixelation;
        els.pixelationValue.textContent = state.settings.pixelation === 1 ? '1x' : `${state.settings.pixelation}x`;
        els.regionX.value = state.settings.region.x;
        els.regionY.value = state.settings.region.y;
        els.regionW.value = state.settings.region.width;
        els.regionH.value = state.settings.region.height;

        // Update preset buttons
        document.querySelectorAll('.preset-btn[data-quality]').forEach(btn => {
            btn.classList.toggle('active', parseInt(btn.dataset.quality) === state.settings.quality);
        });
        document.querySelectorAll('.preset-btn[data-pixel]').forEach(btn => {
            btn.classList.toggle('active', parseInt(btn.dataset.pixel) === state.settings.pixelation);
        });
    }

    // ===================================
    // Rendering
    // ===================================
    function render() {
        // Update status indicator
        updateStatusIndicator();

        // Update button states
        els.startBtn.disabled = state.running;
        els.stopBtn.disabled = !state.running;

        els.freezeBtnQuick.textContent = state.frozen ? '▶ Resume' : '⏸ Freeze';
        els.freezeBtnQuick.classList.toggle('active', state.frozen);
        els.freezeBtnMain.classList.toggle('active', state.frozen);
        els.freezeBtnText.textContent = state.frozen ? 'Resume' : 'Freeze';

        els.cursorBtn.classList.toggle('active', state.fakeCursor);
        els.gameModeBtn.classList.toggle('active', state.gameMode);

        // Update status badge
        updateStatusBadge();
    }

    function updateStatusIndicator() {
        let statusClass = 'idle';
        let statusText = 'IDLE';

        if (state.running) {
            if (state.frozen) {
                statusClass = 'frozen';
                statusText = 'FROZEN';
            } else if (state.gameMode) {
                statusClass = 'game';
                statusText = 'GAME MODE';
            } else {
                statusClass = 'live';
                statusText = 'LIVE';
            }
        }

        els.statusDot.className = 'status-dot ' + statusClass;
        els.statusText.textContent = statusText;
    }

    function updateStatusBadge() {
        let statusClass = 'idle';
        let statusText = 'IDLE';

        if (state.running) {
            if (state.frozen) {
                statusClass = 'frozen';
                statusText = 'FROZEN';
            } else if (state.gameMode) {
                statusClass = 'game';
                statusText = 'GAME MODE';
            } else {
                statusClass = 'live';
                statusText = 'LIVE';
            }
        }

        els.statusBadge.className = 'status-badge ' + statusClass;
        els.statusBadge.textContent = statusText;
    }

    // ===================================
    // Event Handlers
    // ===================================
    function initEventListeners() {
        // Quick Bar
        els.expandBtn.addEventListener('click', toggleQuickBar);
        els.freezeBtnQuick.addEventListener('click', toggleFreeze);
        els.settingsBtnQuick.addEventListener('click', toggleSettingsPanel);

        // Settings Panel
        els.closeSettingsBtn.addEventListener('click', () => hideSettingsPanel());

        // Capture Controls
        els.startBtn.addEventListener('click', startCapture);
        els.stopBtn.addEventListener('click', stopCapture);
        els.freezeBtnMain.addEventListener('click', toggleFreeze);
        els.cursorBtn.addEventListener('click', toggleCursor);
        els.gameModeBtn.addEventListener('click', toggleGameMode);

        // FPS Inputs
        els.fpsMin.addEventListener('change', () => updateSetting('fpsMin', parseInt(els.fpsMin.value) || 1));
        els.fpsMax.addEventListener('change', () => updateSetting('fpsMax', parseInt(els.fpsMax.value) || 1));

        // Quality Slider
        els.quality.addEventListener('input', (e) => {
            const val = parseInt(e.target.value);
            els.qualityValue.textContent = val;
            updateSetting('quality', val);
        });
        els.quality.addEventListener('change', () => updateSetting('quality', parseInt(els.quality.value)));

        // Pixelation Slider
        els.pixelation.addEventListener('input', (e) => {
            const val = parseInt(e.target.value);
            els.pixelationValue.textContent = val === 1 ? '1x' : `${val}x`;
        });
        els.pixelation.addEventListener('change', () => updateSetting('pixelation', parseInt(els.pixelation.value)));

        // Quality Presets
        els.qualityPresets.forEach(btn => {
            btn.addEventListener('click', () => {
                const val = parseInt(btn.dataset.quality);
                els.quality.value = val;
                els.qualityValue.textContent = val;
                updateSetting('quality', val);
            });
        });

        // Pixelation Presets
        els.pixelationPresets.forEach(btn => {
            btn.addEventListener('click', () => {
                const val = parseInt(btn.dataset.pixel);
                els.pixelation.value = val;
                els.pixelationValue.textContent = val === 1 ? '1x' : `${val}x`;
                updateSetting('pixelation', val);
            });
        });

        // Region Inputs
        [els.regionX, els.regionY, els.regionW, els.regionH].forEach(input => {
            input.addEventListener('change', syncRegionFromInputs);
        });

        // Region Presets
        els.regionPresets.forEach(btn => {
            btn.addEventListener('click', () => {
                const [w, h] = btn.dataset.preset.split(',').map(Number);
                setRegionPreset(0, 0, w, h);
            });
        });

        // Region Selector
        els.showRegionBtn.addEventListener('click', showRegionSelector);
        els.hideRegionBtn.addEventListener('click', hideRegionSelector);

        // Region Handles
        els.regionHandles.forEach(handle => {
            handle.addEventListener('mousedown', startRegionDrag);
        });

        document.addEventListener('mousemove', onRegionDrag);
        document.addEventListener('mouseup', endRegionDrag);

        // Close region selector on escape
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && state.regionSelectorVisible) {
                hideRegionSelector();
            }
        });

        // Click outside settings panel to close (optional)
        document.addEventListener('click', (e) => {
            if (!state.regionSelectorVisible && !els.settingsPanel.classList.contains('hidden')) {
                const rect = els.settingsPanel.getBoundingClientRect();
                if (e.clientX < rect.left || e.clientX > rect.right || e.clientY < rect.top || e.clientY > rect.bottom) {
                    // Don't auto-close settings panel on outside click
                }
            }
        });
    }

    // ===================================
    // Actions
    // ===================================
    async function startCapture() {
        try {
            await sendMessageWithCallback('startCapture', {});
            showToast('Capture started', 'success');
        } catch (err) {
            showToast('Failed to start capture: ' + err.message, 'error');
        }
    }

    async function stopCapture() {
        try {
            await sendMessageWithCallback('stopCapture', {});
            showToast('Capture stopped', 'info');
        } catch (err) {
            showToast('Failed to stop capture: ' + err.message, 'error');
        }
    }

    async function toggleFreeze() {
        try {
            await sendMessageWithCallback('toggleFreeze', {});
        } catch (err) {
            showToast('Failed to toggle freeze: ' + err.message, 'error');
        }
    }

    async function toggleCursor() {
        try {
            await sendMessageWithCallback('toggleCursor', {});
        } catch (err) {
            showToast('Failed to toggle cursor: ' + err.message, 'error');
        }
    }

    async function toggleGameMode() {
        try {
            await sendMessageWithCallback('toggleGameMode', {});
        } catch (err) {
            showToast('Failed to toggle game mode: ' + err.message, 'error');
        }
    }

    async function updateSetting(key, value) {
        try {
            await sendMessageWithCallback('updateSetting', { key, value });
        } catch (err) {
            showToast('Failed to update setting: ' + err.message, 'error');
        }
    }

    function syncRegionFromInputs() {
        const region = {
            x: parseInt(els.regionX.value) || 0,
            y: parseInt(els.regionY.value) || 0,
            width: parseInt(els.regionW.value) || 64,
            height: parseInt(els.regionH.value) || 64
        };
        sendMessage('updateRegion', region);
    }

    function setRegionPreset(x, y, width, height) {
        els.regionX.value = x;
        els.regionY.value = y;
        els.regionW.value = width;
        els.regionH.value = height;
        syncRegionFromInputs();
        updateRegionSelector();
    }

    // ===================================
    // Quick Bar
    // ===================================
    function toggleQuickBar() {
        const expanded = els.quickBar.classList.toggle('collapsed');
        els.expandBtn.setAttribute('aria-expanded', !expanded);
        els.quickBarExpanded.setAttribute('aria-hidden', expanded);
    }

    // ===================================
    // Settings Panel
    // ===================================
    function toggleSettingsPanel() {
        const hidden = els.settingsPanel.classList.toggle('hidden');
        els.settingsBtnQuick.setAttribute('aria-expanded', !hidden);
    }

    function hideSettingsPanel() {
        els.settingsPanel.classList.add('hidden');
        els.settingsBtnQuick.setAttribute('aria-expanded', 'false');
    }

    // ===================================
    // Region Selector
    // ===================================
    function showRegionSelector() {
        state.regionSelectorVisible = true;
        els.regionSelector.classList.remove('hidden');
        updateRegionSelector();
        sendMessage('showRegionSelector', {});
    }

    function hideRegionSelector() {
        state.regionSelectorVisible = false;
        els.regionSelector.classList.add('hidden');
        sendMessage('hideRegionSelector', {});
    }

    function updateRegionSelector() {
        const r = state.settings.region;
        els.regionFrame.style.left = r.x + 'px';
        els.regionFrame.style.top = r.y + 'px';
        els.regionFrame.style.width = r.width + 'px';
        els.regionFrame.style.height = r.height + 'px';
        els.regionInfo.textContent = `${r.width} x ${r.height}`;
    }

    function startRegionDrag(e) {
        e.preventDefault();
        e.stopPropagation();
        state.regionDragging = true;
        state.regionDragHandle = e.target.dataset.handle;
        state.regionDragStart = { x: e.clientX, y: e.clientY };
        state.regionStartRect = { ...state.settings.region };
        document.body.style.cursor = e.target.style.cursor;
    }

    function onRegionDrag(e) {
        if (!state.regionDragging) return;

        const dx = e.clientX - state.regionDragStart.x;
        const dy = e.clientY - state.regionDragStart.y;
        const handle = state.regionDragHandle;
        const r = { ...state.regionStartRect };

        if (handle === 'move') {
            r.x = Math.max(0, r.x + dx);
            r.y = Math.max(0, r.y + dy);
        } else {
            if (handle.includes('w')) {
                r.x = Math.max(0, r.x + dx);
                r.width = Math.max(64, r.width - dx);
            }
            if (handle.includes('e')) {
                r.width = Math.max(64, r.width + dx);
            }
            if (handle.includes('n')) {
                r.y = Math.max(0, r.y + dy);
                r.height = Math.max(64, r.height - dy);
            }
            if (handle.includes('s')) {
                r.height = Math.max(64, r.height + dy);
            }
        }

        state.settings.region = r;
        syncInputsToState();
        updateRegionSelector();
        sendMessage('updateRegion', r);
    }

    function endRegionDrag() {
        if (state.regionDragging) {
            state.regionDragging = false;
            state.regionDragHandle = null;
            document.body.style.cursor = '';
        }
    }

    // ===================================
    // Toast Notifications
    // ===================================
    function showToast(message, type = 'info') {
        const container = document.getElementById('toastContainer');
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.innerHTML = `
            <div class="toast-icon">
                ${getToastIcon(type)}
            </div>
            <span class="toast-message">${escapeHtml(message)}</span>
            <button class="toast-close" aria-label="Dismiss">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="18" y1="6" x2="6" y2="18"></line>
                    <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
            </button>
        `;

        toast.querySelector('.toast-close').addEventListener('click', () => removeToast(toast));
        container.appendChild(toast);

        // Auto-remove after 4 seconds
        setTimeout(() => removeToast(toast), 4000);
    }

    function removeToast(toast) {
        toast.classList.add('removing');
        toast.addEventListener('animationend', () => toast.remove());
    }

    function getToastIcon(type) {
        const icons = {
            success: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>',
            error: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
            warning: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>',
            info: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
        };
        return icons[type] || icons.info;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // ===================================
    // Initialization
    // ===================================
    function init() {
        initEventListeners();
        syncInputsToState();
        render();

        // Request initial state from backend
        sendMessage('getInitialState', {});

        // Notify C# that WebView is ready
        sendMessage('webviewReady', {});

        console.log('CAPTURE WebView2 frontend initialized');
    }

    // Start when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();