window.chatHelpers = {
    shouldAutoScroll: function(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return false;
        const threshold = 100;
        return (container.scrollHeight - container.scrollTop - container.clientHeight) < threshold;
    },
    scrollToBottom: function(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;
        container.scrollTo({ top: container.scrollHeight, behavior: 'auto' });
    },
    autoExpand: function(textareaId) {
        const textarea = document.getElementById(textareaId);
        if (!textarea) return;
        textarea.style.height = 'auto';
        textarea.style.height = Math.min(textarea.scrollHeight, 200) + 'px';
    },
    resetTextarea: function(textareaId) {
        const textarea = document.getElementById(textareaId);
        if (!textarea) return;
        textarea.style.height = 'auto';
        textarea.value = '';
    },
    setupEnterHandler: function(textareaId, dotNetRef) {
        const textarea = document.getElementById(textareaId);
        if (!textarea) return;
        // Remove any previous listener
        if (textarea._enterHandler) {
            textarea.removeEventListener('keydown', textarea._enterHandler);
        }
        textarea._enterHandler = function(e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                dotNetRef.invokeMethodAsync('SendFromJs');
            }
        };
        textarea.addEventListener('keydown', textarea._enterHandler);
    },
    getTextareaValue: function(textareaId) {
        const textarea = document.getElementById(textareaId);
        return textarea ? textarea.value : '';
    },
    setTextareaValue: function(textareaId, value) {
        const textarea = document.getElementById(textareaId);
        if (textarea) textarea.value = value;
    },
    copyToClipboard: async function(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (err) {
            console.error('Copy failed:', err);
            return false;
        }
    }
};
