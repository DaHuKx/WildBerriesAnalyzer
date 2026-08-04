const STORAGE_KEY = "pricelabBasketArticles";

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "pricelab-basket-updated") {
    chrome.action.setBadgeBackgroundColor({ color: "#0F766E" });
    const count = message.payload?.count || 0;
    chrome.action.setBadgeText({ text: count > 0 ? String(count) : "" });
    sendResponse({ ok: true });
    return true;
  }

  if (message?.type === "pricelab-get-basket") {
    chrome.storage.local.get(STORAGE_KEY).then((data) => {
      sendResponse(data[STORAGE_KEY] || null);
    });
    return true;
  }

  return false;
});

chrome.storage.local.get(STORAGE_KEY).then((data) => {
  const count = data[STORAGE_KEY]?.count || 0;
  chrome.action.setBadgeBackgroundColor({ color: "#0F766E" });
  chrome.action.setBadgeText({ text: count > 0 ? String(count) : "" });
});
