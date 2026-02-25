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
    preventEnterNewline: function(textareaId) {
        // Called from Blazor to prevent default Enter behavior
        const textarea = document.getElementById(textareaId);
        if (!textarea) return;
        // Handled via event in Blazor side
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
