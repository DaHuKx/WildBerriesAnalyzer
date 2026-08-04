const metaEl = document.getElementById("meta");
const previewEl = document.getElementById("preview");
const copyBtn = document.getElementById("copy");

let currentText = "";

function render(payload) {
  if (!payload || !payload.count) {
    metaEl.textContent = "Артикулы ещё не собраны. Откройте корзину на wildberries.ru.";
    previewEl.textContent = "—";
    copyBtn.disabled = true;
    currentText = "";
    return;
  }

  const when = payload.updatedAt
    ? new Date(payload.updatedAt).toLocaleTimeString("ru-RU")
    : "";

  metaEl.textContent = `Найдено: ${payload.count}${when ? ` · обновлено ${when}` : ""}`;
  currentText = payload.text || (payload.articles || []).join(" ");
  previewEl.textContent = currentText || "—";
  copyBtn.disabled = !currentText;
}

async function load() {
  const data = await chrome.storage.local.get("pricelabBasketArticles");
  render(data.pricelabBasketArticles || null);
}

copyBtn.addEventListener("click", async () => {
  if (!currentText) {
    return;
  }

  try {
    await navigator.clipboard.writeText(currentText);
    metaEl.textContent = `Скопировано ${currentText.split(/\s+/).filter(Boolean).length} арт.`;
  } catch {
    metaEl.textContent = "Не удалось скопировать. Выделите текст вручную.";
  }
});

load();
chrome.storage.onChanged.addListener((changes, area) => {
  if (area === "local" && changes.pricelabBasketArticles) {
    render(changes.pricelabBasketArticles.newValue || null);
  }
});
