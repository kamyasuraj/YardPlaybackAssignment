// Tells the page that playback finished: window gets a 'yard:finished' event whose detail is the
// payload JSON string, {"ticket":..,"left":[{"id","lane","load"},..]}. The page parses it; nothing is evaluated.
mergeInto(LibraryManager.library, {
  YardPage_Finished: function (payloadPtr) {
    var payload = UTF8ToString(payloadPtr);
    window.dispatchEvent(new CustomEvent('yard:finished', { detail: payload }));
  }
});
