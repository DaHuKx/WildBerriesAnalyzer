const hostInput = document.getElementById("host");
const portInput = document.getElementById("port");
const usernameInput = document.getElementById("username");
const passwordInput = document.getElementById("password");
const statusEl = document.getElementById("status");
const errorEl = document.getElementById("error");
const enableBtn = document.getElementById("enable-btn");
const disableBtn = document.getElementById("disable-btn");
const form = document.getElementById("proxy-form");

function showError(text) {
  if (!text) {
    errorEl.hidden = true;
    errorEl.textContent = "";
    return;
  }

  errorEl.hidden = false;
  errorEl.textContent = text;
}

function setEnabledUi(enabled, host, port) {
  if (enabled) {
    statusEl.textContent = `Прокси включён: ${host}:${port}`;
    statusEl.className = "status on";
    enableBtn.hidden = true;
    disableBtn.hidden = false;
    hostInput.disabled = true;
    portInput.disabled = true;
    usernameInput.disabled = true;
    passwordInput.disabled = true;
  } else {
    statusEl.textContent = "Прокси выключен";
    statusEl.className = "status off";
    enableBtn.hidden = false;
    disableBtn.hidden = true;
    hostInput.disabled = false;
    portInput.disabled = false;
    usernameInput.disabled = false;
    passwordInput.disabled = false;
  }
}

function sendMessage(message) {
  return new Promise((resolve, reject) => {
    chrome.runtime.sendMessage(message, (response) => {
      if (chrome.runtime.lastError) {
        reject(new Error(chrome.runtime.lastError.message));
        return;
      }

      if (!response?.ok) {
        reject(new Error(response?.error || "Unknown error"));
        return;
      }

      resolve(response);
    });
  });
}

async function loadState() {
  const { state } = await sendMessage({ type: "getState" });
  hostInput.value = state.host || "";
  portInput.value = state.port || "";
  usernameInput.value = state.username || "";
  passwordInput.value = state.password || "";
  setEnabledUi(state.enabled, state.host, state.port);
}

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  showError("");

  const host = hostInput.value.trim();
  const port = portInput.value.trim();
  const username = usernameInput.value.trim();
  const password = passwordInput.value;

  if (!host || !port) {
    showError("Укажите IP и порт.");
    return;
  }

  const portNum = Number.parseInt(port, 10);
  if (!Number.isFinite(portNum) || portNum < 1 || portNum > 65535) {
    showError("Порт должен быть от 1 до 65535.");
    return;
  }

  enableBtn.disabled = true;
  try {
    await sendMessage({
      type: "enable",
      host,
      port: String(portNum),
      username,
      password
    });
    setEnabledUi(true, host, String(portNum));
  } catch (error) {
    showError(error.message);
  } finally {
    enableBtn.disabled = false;
  }
});

disableBtn.addEventListener("click", async () => {
  showError("");
  disableBtn.disabled = true;
  try {
    await sendMessage({ type: "disable" });
    setEnabledUi(false);
  } catch (error) {
    showError(error.message);
  } finally {
    disableBtn.disabled = false;
  }
});

loadState().catch((error) => showError(error.message));
