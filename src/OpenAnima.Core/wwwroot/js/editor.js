// Editor canvas event interop — prevents browser defaults that
// Blazor Server can't reliably preventDefault via SignalR round-trip.
window.editorCanvas = {
    _handler: null,

    init: function (element) {
        if (!element) return;

        const handler = function (e) {
            e.preventDefault();
        };

        const dragoverHandler = function (e) {
            e.preventDefault();
        };

        element.addEventListener('wheel', handler, { passive: false });
        element.addEventListener('contextmenu', handler);
        element.addEventListener('dragover', dragoverHandler);

        // Store for cleanup
        element._editorHandler = handler;
        element._dragoverHandler = dragoverHandler;
    },

    dispose: function (element) {
        if (!element) return;

        if (element._editorHandler) {
            element.removeEventListener('wheel', element._editorHandler);
            element.removeEventListener('contextmenu', element._editorHandler);
            delete element._editorHandler;
        }

        if (element._dragoverHandler) {
            element.removeEventListener('dragover', element._dragoverHandler);
            delete element._dragoverHandler;
        }
    }
};
