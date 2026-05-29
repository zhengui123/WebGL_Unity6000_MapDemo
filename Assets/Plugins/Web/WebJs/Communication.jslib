mergeInto(LibraryManager.library, {

//注意unity传进来的字符串，需要用UTF8ToString转换

    GetDataFromHTML: function(message) {
        
        message = UTF8ToString(message);
      
          if (window.parent && window.parent.funStartTestExample) {
                window.parent.funStartTestExample(message);
                console.log("找到了");
                
          } 
          else if (window.outerHtmlMethod) {
            window.funStartTest(message);
            console.log("没找到");
                
          }

        
        console.log("Unity在调用", message);
        window.alert(message);
    },

    //含返回事件的调用
    CallUnityFunction: function(message, callback) {
        message = UTF8ToString(message);
        callback = UTF8ToString(callback);
    
        var response = "Processed " + message;
        // 调用Unity的回调函数
        SendMessage('WebGLCommunication', callback, response);
    }
  
});



