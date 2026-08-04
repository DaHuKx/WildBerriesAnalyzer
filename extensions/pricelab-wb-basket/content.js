(() => {
  const PANEL_ID = "pricelab-wb-panel";
  const STORAGE_KEY = "pricelabBasketArticles";

  let articles = [];
  let scrolling = false;
  let lastSignature = "";
  let panel;
  let countEl;
  let metaEl;
  let previewEl;
  let copyBtn;
  let collapsed = false;

  function isBasketPage() {
    return /\/lk\/basket/i.test(location.pathname);
  }

  function ensurePanel() {
    if (panel || !isBasketPage()) {
      return;
    }

    panel = document.createElement("div");
    panel.id = PANEL_ID;
    panel.innerHTML = `
      <div class="pl-row">
        <div>
          <p class="pl-brand">PRICELAB</p>
          <p class="pl-title">Артикулы корзины</p>
        </div>
        <button type="button" class="pl-icon-btn" data-action="toggle" title="Свернуть">−</button>
      </div>
      <div class="pl-details">
        <p class="pl-meta" data-role="meta">Сканирование…</p>
        <p class="pl-meta" data-role="count">Найдено: 0</p>
        <div class="pl-preview" data-role="preview" hidden></div>
        <div class="pl-actions">
          <button type="button" class="pl-primary" data-action="copy" disabled>Скопировать</button>
          <button type="button" class="pl-secondary" data-action="rescan">Обновить</button>
          <button type="button" class="pl-secondary" data-action="scroll">Прокрутить вниз</button>
        </div>
      </div>
    `;

    document.documentElement.appendChild(panel);
    metaEl = panel.querySelector('[data-role="meta"]');
    countEl = panel.querySelector('[data-role="count"]');
    previewEl = panel.querySelector('[data-role="preview"]');
    copyBtn = panel.querySelector('[data-action="copy"]');

    panel.addEventListener("click", async (event) => {
      const action = event.target?.getAttribute?.("data-action");
      if (!action) {
        return;
      }

      if (action === "toggle") {
        collapsed = !collapsed;
        panel.dataset.collapsed = String(collapsed);
        event.target.textContent = collapsed ? "+" : "−";
        event.target.title = collapsed ? "Развернуть" : "Свернуть";
        return;
      }

      if (action === "copy") {
        await copyArticles();
        return;
      }

      if (action === "rescan") {
        refresh(true);
        return;
      }

      if (action === "scroll") {
        await autoScrollBasket();
        refresh(true);
      }
    });
  }

  function updatePanel(statusText) {
    if (!panel) {
      return;
    }

    const text = window.PriceLabBasket.articlesToText(articles);
    countEl.textContent = `Найдено: ${articles.length}`;
    metaEl.textContent = statusText;
    copyBtn.disabled = articles.length === 0;

    if (articles.length) {
      previewEl.hidden = false;
      previewEl.textContent = text;
    } else {
      previewEl.hidden = true;
      previewEl.textContent = "";
    }
  }

  function persist(statusText) {
    const payload = {
      articles,
      text: window.PriceLabBasket.articlesToText(articles),
      count: articles.length,
      updatedAt: Date.now(),
      url: location.href,
      statusText,
    };

    chrome.storage.local.set({ [STORAGE_KEY]: payload });
    chrome.runtime.sendMessage({ type: "pricelab-basket-updated", payload }).catch(() => {});
  }

  function refresh(forceStatus) {
    if (!isBasketPage()) {
      return;
    }

    ensurePanel();
    articles = window.PriceLabBasket.extractBasketArticles();
    const signature = articles.join(",");
    const changed = signature !== lastSignature;
    lastSignature = signature;

    const statusText =
      articles.length === 0
        ? "Артикулы не найдены. Прокрутите корзину до конца и нажмите «Обновить»."
        : forceStatus || changed
          ? "Список готов — можно копировать в PriceLab или бота."
          : "Список актуален.";

    updatePanel(statusText);
    if (changed || forceStatus) {
      persist(statusText);
    }
  }

  async function copyArticles() {
    if (!articles.length) {
      return;
    }

    const text = window.PriceLabBasket.articlesToText(articles);

    try {
      await navigator.clipboard.writeText(text);
      updatePanel(`Скопировано ${articles.length} арт. Вставьте в PriceLab или бота.`);
      persist(`Скопировано ${articles.length} арт.`);
    } catch {
      window.prompt(`Скопируйте артикулы (${articles.length}):`, text);
    }
  }

  async function autoScrollBasket() {
    if (scrolling) {
      return;
    }

    scrolling = true;
    updatePanel("Прокрутка корзины для подгрузки товаров…");

    try {
      let stableRounds = 0;
      let lastHeight = 0;

      for (let i = 0; i < 40 && stableRounds < 3; i++) {
        window.scrollTo(0, document.documentElement.scrollHeight);
        await wait(450);
        refresh(false);

        const height = document.documentElement.scrollHeight;
        if (height <= lastHeight) {
          stableRounds += 1;
        } else {
          stableRounds = 0;
          lastHeight = height;
        }
      }
    } finally {
      scrolling = false;
    }
  }

  function wait(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  function startObservers() {
    const observer = new MutationObserver(() => {
      window.clearTimeout(startObservers._timer);
      startObservers._timer = window.setTimeout(() => refresh(false), 400);
    });

    observer.observe(document.documentElement, {
      childList: true,
      subtree: true,
    });
  }

  async function boot() {
    if (!isBasketPage()) {
      return;
    }

    ensurePanel();
    refresh(true);
    startObservers();

    // Подгружаем lazy-list один раз при открытии корзины.
    await wait(800);
    await autoScrollBasket();
    refresh(true);
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => {
      boot();
    });
  } else {
    boot();
  }
})();
