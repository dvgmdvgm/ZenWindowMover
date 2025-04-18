(function() {
    'use strict';

    const movableElements = new WeakSet();
    let ws = null;
    const MIN_HEADER_HEIGHT = 30;
    const TOP_AREA_HEIGHT = 50;
    let SEND_INTERVAL = 4;
    let isDragging = false;
    let lastDoubleClickTime = 0;
    let activeElement = null;
	

    const selectionUtils = {
        disable: () => {
            document.body.style.userSelect = 'none';
            document.body.style.webkitUserSelect = 'none';
            document.body.style.msUserSelect = 'none';
        },
        enable: () => {
            document.body.style.userSelect = '';
            document.body.style.webkitUserSelect = '';
            document.body.style.msUserSelect = '';
        }
    };

    function isInteractiveCursor(target) {
        if (!target) return false;
        const cursorStyle = getComputedStyle(target).cursor;
        return ['pointer', 'text', 'grab', 'grabbing', 'move', 'cell'].includes(cursorStyle);
    }

    function getCurrentDomain() {
        return window.location.hostname.replace(/^www\./, '');
    }

    function notifyElementStatus() {
        if (!ws) return;
        ws.send(movableElements.size > 0 ? "elementFound" : "elementNotFound");
    }

    function findHeader() {
        const header = document.querySelector('header, [role="banner"], .header, .page-header, #masthead-container');
        return header?.offsetHeight >= MIN_HEADER_HEIGHT ? header : null;
    }

    function waitForHeader() {
        const observer = new MutationObserver(() => {
            const header = findHeader();
            if (header && !movableElements.has(header)) {
                movableElements.add(header);
                setupMovableElement(header);
                notifyElementStatus();
            }
        });

        observer.observe(document.body, { childList: true, subtree: true });

        const header = findHeader();
        if (header) {
            movableElements.add(header);
            setupMovableElement(header);
        } else {
            setupTopArea();
        }
        notifyElementStatus();
    }

    function setupMovableElement(element) {
        console.log('Movable element found:', element);
        activeElement = element;

        if (getComputedStyle(element).pointerEvents === 'none') {
            element.style.pointerEvents = 'auto';
        }

        let isDragging = false;
        let prevX, prevY;
        let lastSendTime = 0;

        const resizeObserver = new ResizeObserver(([entry]) => {
            ws?.send(`elementWidth:${entry.contentRect.width}`);
        });
        resizeObserver.observe(element);

        const handleMouseDown = (e) => {
            if (e.button !== 0 || isInteractiveCursor(e.target)) return;
            if (Date.now() - lastDoubleClickTime < 500) return;
            
            e.stopPropagation();
            isDragging = true;
            ws?.send("dragStart");
            prevX = e.screenX;
            prevY = e.screenY;
            selectionUtils.disable();

            const rect = element.getBoundingClientRect();
            const percentage = Math.round(((e.clientX - rect.left) / rect.width) * 100);
            ws?.send(`cursorPercentage:${percentage}`);
        };

        const handleMouseMove = (e) => {
            if (!isDragging) return;
            const now = Date.now();
            if (now - lastSendTime >= SEND_INTERVAL) {
                const deltaX = e.screenX - prevX;
                const deltaY = e.screenY - prevY;
                prevX = e.screenX;
                prevY = e.screenY;
                ws?.send(`${deltaX},${deltaY}`);
                lastSendTime = now;
            }
        };

        const handleMouseUp = () => {
            if (isDragging) {
                isDragging = false;
                selectionUtils.enable();
                ws?.send("dragEnd");
            }
        };

        const handleDoubleClick = (e) => {
            e.stopPropagation();
            const now = Date.now();
            if (now - lastDoubleClickTime > 300) {
                lastDoubleClickTime = now;
                ws?.send("doubleClick");
                console.log('Double Clicked');
                
                // Reset dragging state if needed
                if (isDragging) {
                    isDragging = false;
                    selectionUtils.enable();
                    ws?.send("dragEnd");
                }
            }
        };

        element.addEventListener('mousedown', handleMouseDown);
        document.addEventListener('mousemove', handleMouseMove);
        document.addEventListener('mouseup', handleMouseUp);
        element.addEventListener('dblclick', handleDoubleClick, true);

        ws?.send(`elementWidth:${element.offsetWidth}`);
    }

    function setupTopArea() {
        console.log('No movable element found, setting up top area for dragging.');
        activeElement = null;

        let isDragging = false;
        let prevX, prevY;
        let lastSendTime = 0;

        const handleMouseDown = (e) => {
            if (e.button !== 0 || isInteractiveCursor(e.target) || e.clientY > TOP_AREA_HEIGHT) return;
            if (Date.now() - lastDoubleClickTime < 500) return;
            
            isDragging = true;
            ws?.send("dragStart");
            prevX = e.screenX;
            prevY = e.screenY;
            selectionUtils.disable();

            const percentage = Math.round((e.clientX / window.innerWidth) * 100);
            ws?.send(`cursorPercentage:${percentage}`);
        };

        const handleMouseMove = (e) => {
            if (!isDragging) return;
            const now = Date.now();
            if (now - lastSendTime >= SEND_INTERVAL) {
                const deltaX = e.screenX - prevX;
                const deltaY = e.screenY - prevY;
                prevX = e.screenX;
                prevY = e.screenY;
                ws?.send(`${deltaX},${deltaY}`);
                lastSendTime = now;
            }
        };

        const handleMouseUp = () => {
            if (isDragging) {
                isDragging = false;
                selectionUtils.enable();
                ws?.send("dragEnd");
            }
        };

        const handleDoubleClick = (e) => {
            if (e.clientY > TOP_AREA_HEIGHT) return;
            e.stopPropagation();
            const now = Date.now();
            if (now - lastDoubleClickTime > 300) {
                lastDoubleClickTime = now;
                ws?.send("doubleClick");
                console.log('Double Clicked');
                
                if (isDragging) {
                    isDragging = false;
                    selectionUtils.enable();
                    ws?.send("dragEnd");
                }
            }
        };

        document.addEventListener('mousedown', handleMouseDown);
        document.addEventListener('mousemove', handleMouseMove);
        document.addEventListener('mouseup', handleMouseUp);
        document.addEventListener('dblclick', handleDoubleClick, true);

        ws?.send(`elementWidth:${window.innerWidth}`);
    }

    function initWebSocket() {
        function connect() {
            ws = new WebSocket('ws://127.0.0.1:8080/mover');

            ws.onopen = () => {
                console.log('WebSocket connection established.');
                ws.send(`getMovableElement:${getCurrentDomain()}`);
            };

            ws.onmessage = (event) => {
                if (event.data.startsWith("movableElement:")) {
                    const classNames = event.data.substring(15).split('\n').filter(Boolean);
                    let foundElements = false;

                    classNames.forEach(className => {
                        document.querySelectorAll(`.${className.trim()}`).forEach(element => {
                            if (!movableElements.has(element)) {
                                movableElements.add(element);
                                setupMovableElement(element);
                                foundElements = true;
                            }
                        });
                    });

                    if (!foundElements) {
                        waitForHeader();
                    }
                    notifyElementStatus();
                }
            };

            ws.onerror = (error) => {
                console.error('WebSocket error:', error);
            };

            ws.onclose = () => {
                console.log('WebSocket disconnected, attempting to reconnect...');
                setTimeout(connect, 3000);
            };
        }

        connect();
    }

    // Initialize
    initWebSocket();
})();