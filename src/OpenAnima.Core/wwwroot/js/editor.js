// Editor canvas event interop — prevents browser defaults that
// Blazor Server can't reliably preventDefault via SignalR round-trip.
window.editorCanvas = {
    _handler: null,

    init: function (element) {
        if (!element) return;

        const handler = function (e) {
            e.preventDefault();
        };

        element.addEventListener('wheel', handler, { passive: false });
        element.addEventListener('contextmenu', handler);

        // Store for cleanup
        element._editorHandler = handler;
    },

    dispose: function (element) {
        if (!element || !element._editorHandler) return;

        element.removeEventListener('wheel', element._editorHandler);
        element.removeEventListener('contextmenu', element._editorHandler);
        delete element._editorHandler;
    }
};
