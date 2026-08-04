(() => {
  const XPATH =
    "//div[contains(@class,'j-b-basket-item')]//a[contains(@class,'good-info__title')]";

  function fromXPath() {
    const articles = [];
    const seen = new Set();
    const nodes = document.evaluate(
      XPATH,
      document,
      null,
      XPathResult.ORDERED_NODE_SNAPSHOT_TYPE,
      null
    );

    for (let i = 0; i < nodes.snapshotLength; i++) {
      const href = nodes.snapshotItem(i)?.href || "";
      const match = href.match(/\/catalog\/(\d+)\//);
      if (match && !seen.has(match[1])) {
        seen.add(match[1]);
        articles.push(match[1]);
      }
    }

    return articles;
  }

  function fromCatalogLinks() {
    const articles = [];
    const seen = new Set();
    const roots = document.querySelectorAll(
      ".j-b-basket-item, [class*='basket'], [class*='Basket']"
    );
    const scope = roots.length ? roots : [document];

    for (const root of scope) {
      const links = root.querySelectorAll?.('a[href*="/catalog/"]') || [];
      for (const link of links) {
        const match = (link.href || "").match(/\/catalog\/(\d+)\//);
        if (match && !seen.has(match[1])) {
          seen.add(match[1]);
          articles.push(match[1]);
        }
      }
    }

    return articles;
  }

  function extractBasketArticles() {
    const primary = fromXPath();
    if (primary.length) {
      return primary;
    }

    return fromCatalogLinks();
  }

  function articlesToText(articles) {
    return articles.join(" ");
  }

  window.PriceLabBasket = {
    extractBasketArticles,
    articlesToText,
  };
})();
