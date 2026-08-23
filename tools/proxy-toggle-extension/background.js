const STORAGE_KEYS = {
  enabled: "proxyEnabled",
  host: "proxyHost",
  port: "proxyPort",
  username: "proxyUsername",
  password: "proxyPassword"
};

let authCredentials = null;

chrome.webRequest.onAuthRequired.addListener(
  (details, callback) => {
    if (!authCredentials?.username) {
      callback({});
      return;
    }

    callback({
      authCredentials: {
        username: authCredentials.username,
        password: authCredentials.password
      }
    });
  },
  { urls: ["<all_urls>"] },
  ["asyncBlocking"]
);

async function loadState() {
  const data = await chrome.storage.local.get(Object.values(STORAGE_KEYS));
  return {
    enabled: Boolean(data[STORAGE_KEYS.enabled]),
    host: data[STORAGE_KEYS.host] || "",
    port: data[STORAGE_KEYS.port] || "",
    username: data[STORAGE_KEYS.username] || "",
    password: data[STORAGE_KEYS.password] || ""
  };
}

function buildProxyConfig(host, port) {
  return {
    mode: "fixed_servers",
    rules: {
      singleProxy: {
        scheme: "http",
        host,
        port: Number.parseInt(port, 10)
      },
      bypassList: ["localhost", "127.0.0.1", "<local>"]
    }
  };
}

async function applyProxy(enabled, host, port, username, password) {
  if (enabled) {
    authCredentials = { username, password };
    await chrome.proxy.settings.set({
      value: buildProxyConfig(host, port),
      scope: "regular"
    });
  } else {
    authCredentials = null;
    await chrome.proxy.settings.clear({ scope: "regular" });
  }
}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  (async () => {
    try {
      if (message.type === "getState") {
        sendResponse({ ok: true, state: await loadState() });
        return;
      }

      if (message.type === "enable") {
        const { host, port, username, password } = message;
        await chrome.storage.local.set({
          [STORAGE_KEYS.enabled]: true,
          [STORAGE_KEYS.host]: host,
          [STORAGE_KEYS.port]: port,
          [STORAGE_KEYS.username]: username,
          [STORAGE_KEYS.password]: password
        });
        await applyProxy(true, host, port, username, password);
        sendResponse({ ok: true, enabled: true });
        return;
      }

      if (message.type === "disable") {
        await chrome.storage.local.set({ [STORAGE_KEYS.enabled]: false });
        await applyProxy(false);
        sendResponse({ ok: true, enabled: false });
        return;
      }

      sendResponse({ ok: false, error: "Unknown message type" });
    } catch (error) {
      sendResponse({ ok: false, error: String(error?.message || error) });
    }
  })();

  return true;
});

chrome.runtime.onStartup.addListener(async () => {
  const state = await loadState();
  if (state.enabled && state.host && state.port) {
    await applyProxy(true, state.host, state.port, state.username, state.password);
  }
});

chrome.runtime.onInstalled.addListener(async () => {
  const state = await loadState();
  if (state.enabled && state.host && state.port) {
    await applyProxy(true, state.host, state.port, state.username, state.password);
  }
});
