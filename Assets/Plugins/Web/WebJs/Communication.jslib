mergeInto(LibraryManager.library, {

    // 注意：unity 传进来的字符串需要用 UTF8ToString 转换

    GetDataFromHTML: function(message) {
        message = UTF8ToString(message);
        console.log('[Unity WebGL] GetDataFromHTML:', message);
    },

    CallUnityFunction: function(message, callback) {
        message = UTF8ToString(message);
        callback = UTF8ToString(callback);

        var response = 'Processed ' + message;
        SendMessage('WebGLCommunication', callback, response);
    },

    // 初始化 iframe 跨域 postMessage 桥（父页面 → Unity）
    InitIframePostMessageBridge: function() {
        if (window.__unityIframeBridgeInitialized) {
            return;
        }
        window.__unityIframeBridgeInitialized = true;
        window.__unityPendingParentMessages = window.__unityPendingParentMessages || [];

        window.addEventListener('message', function(event) {
            var data = event.data;
            if (!data || data.source !== 'webgl-unity-parent' || typeof data.method !== 'string') {
                return;
            }

            var arg = data.arg || '';
            var instance = window.unityInstance;
            if (instance && typeof instance.SendMessage === 'function') {
                instance.SendMessage('WebGLAPI', data.method, arg);
                return;
            }

            window.__unityPendingParentMessages.push({ method: data.method, arg: arg });
        });
    },

    // Unity 加载完成后冲刷排队消息
    FlushIframePendingMessages: function() {
        var queue = window.__unityPendingParentMessages || [];
        window.__unityPendingParentMessages = [];

        var instance = window.unityInstance;
        if (!instance || typeof instance.SendMessage !== 'function') {
            window.__unityPendingParentMessages = queue;
            return;
        }

        for (var i = 0; i < queue.length; i++) {
            var item = queue[i];
            instance.SendMessage('WebGLAPI', item.method, item.arg);
        }
    },

    // Unity → 父页面（跨域 iframe：仅 postMessage，不直接调用 parent 函数）
    CallHTMLHandler: function(methodName, message) {
        methodName = UTF8ToString(methodName);
        message = UTF8ToString(message);

        if (!window.parent || window.parent === window) {
            console.warn('[Unity WebGL] 非 iframe 环境，无法 postMessage 到父页面:', methodName, message);
            return;
        }

        window.parent.postMessage({
            source: 'unity-webgl',
            method: methodName,
            message: message
        }, '*');
    }

});
