function createCloudMarket(api, context) {
  const legacyPayments = createMarket(api, context);
  const { store } = api, pid = context.playerId;
  const own = (o, k) => Object.prototype.hasOwnProperty.call(o, k);
  const fail = code => { throw Error(code); };
  const catalog = ['SWORD', 'REDPOTION', 'BLUEPOTION'];
  const tradable = [...catalog, 'LEGENDARY_SWORD'];
  const id = x => typeof x === 'string' && /^[A-Za-z0-9_-]{1,100}$/.test(x) && !['__proto__', 'constructor', 'prototype'].includes(x);
  const currency = p => { if (p.currency_id && p.currency_id !== 'COIN') fail('INVALID_CURRENCY'); };
  function listing(s, key) { if (!id(key) || !own(s.listings, key)) fail('LISTING_NOT_FOUND'); return s.listings[key]; }
  function request(params, kind, fn) {
    if (!id(params.request_id)) fail('INVALID_REQUEST_ID');
    const key = pid + ':' + params.request_id;
    const signature = JSON.stringify([kind, params.players_inventory_item_id, params.price, params.listing_id]);
    return store.change(s => {
      if (own(s.operations, key)) {
        if (s.operations[key].signature !== signature) fail('REQUEST_ID_REUSED');
        return s.operations[key].result;
      }
      const result = fn(s, store.player(s, pid));
      s.operations[key] = { signature, result };
      return result;
    });
  }
  return {
    Sword_GetQuote: legacyPayments.Sword_GetQuote,
    Sword_Claim: legacyPayments.Sword_Claim,
    Gold_GetQuote: legacyPayments.Gold_GetQuote,
    Gold_Claim: legacyPayments.Gold_Claim,
    async Mkt_GetPlayer() {
      return store.change(s => { const p = store.player(s, pid); return { balance: p.balance, items: Object.values(p.items) }; });
    },
    async Mkt_GetActiveListings(params) {
      const s = (await store.read()).value;
      let rows = Object.values(s.listings).filter(x => x.status === 'ACTIVE');
      rows.sort(params.sort === 'PRICE_ASC' ? (a,b) => a.price-b.price : params.sort === 'PRICE_DESC' ? (a,b) => b.price-a.price : (a,b) => b.createdAt-a.createdAt);
      const limit = Number.isInteger(params.limit) ? Math.max(1, Math.min(100, params.limit)) : 30;
      // Never expose wallets, receipts, private inventories or the whole ledger.
      return { revision: s.revision, listings: rows.slice(0, limit).map(({ listingId, status, sellerPlayerId, inventoryItemId, currencyId, price, createdAt }) => ({ listingId, status, sellerPlayerId, inventoryItemId, currencyId, price, createdAt })) };
    },
    async Mkt_CreateListing(params) {
      currency(params);
      if (!id(params.players_inventory_item_id) || !Number.isSafeInteger(params.price) || params.price < 1 || params.price > 1000000) fail('INVALID_LISTING');
      return request(params, 'create', (s, p) => {
        const item = p.items[params.players_inventory_item_id];
        if (!item || !tradable.includes(item.inventoryItemId)) fail('ITEM_NOT_TRADABLE');
        const listingId = 'l_' + (s.revision + 1);
        s.listings[listingId] = { listingId, status: 'ACTIVE', sellerPlayerId: pid, inventoryItemId: item.inventoryItemId, item, currencyId: 'COIN', price: params.price, createdAt: Date.now() };
        delete p.items[params.players_inventory_item_id];
        return { listingId };
      });
    },
    async Mkt_BuyListing(params) {
      return request(params, 'buy', (s, p) => {
        const l = listing(s, params.listing_id);
        if (!tradable.includes(l.inventoryItemId)) fail('ITEM_NOT_TRADABLE');
        if (l.sellerPlayerId === pid) fail('CANNOT_BUY_OWN');
        if (l.status !== 'ACTIVE') fail('LISTING_NOT_ACTIVE');
        store.credit(p, -l.price);
        p.items[l.item.playersInventoryItemId] = l.item;
        l.status = 'SOLD'; l.buyerPlayerId = pid; l.earningsStatus = 'AVAILABLE';
        return { ok: true, newPlayersInventoryItemId: l.item.playersInventoryItemId };
      });
    },
    async Mkt_CancelListing(params) {
      return request(params, 'cancel', (s, p) => {
        const l = listing(s, params.listing_id);
        if (l.sellerPlayerId !== pid) fail('SELLER_ONLY');
        if (l.status !== 'ACTIVE') fail('LISTING_NOT_ACTIVE');
        p.items[l.item.playersInventoryItemId] = l.item;
        l.status = 'CANCELLED';
        return { ok: true, returnedPlayersInventoryItemId: l.item.playersInventoryItemId };
      });
    },
    async Mkt_ClaimEarnings(params) {
      currency(params);
      return request(params, 'claim', (s, p) => {
        let claimed = 0;
        for (const l of Object.values(s.listings)) if (l.sellerPlayerId === pid && l.status === 'SOLD' && l.earningsStatus === 'AVAILABLE') {
          claimed += l.price; l.earningsStatus = 'CLAIMED';
        }
        store.credit(p, claimed);
        return { ok: true, claimed };
      });
    },
    async Mkt_Gacha(params) {
      const itemId = catalog[Math.floor(Math.random() * catalog.length)];
      return request(params, 'gacha', (s, p) => {
        store.credit(p, -100);
        const instance = 'i_' + (s.revision + 1);
        p.items[instance] = { inventoryItemId: itemId, playersInventoryItemId: instance, instanceData: {} };
        return { ok: true, inventoryItemId: itemId };
      });
    },
    async Mkt_GrantDemo(params) {
      if (!['coin', 'item'].includes(params.kind)) fail('INVALID_DEMO_KIND');
      const itemId = catalog[Math.floor(Math.random() * catalog.length)];
      return request(params, 'demo_' + params.kind, (s, p) => {
        if (s.demoEnabled !== true) fail('DEMO_DISABLED');
        if ((p.demoGrants || 0) >= 100) fail('DEMO_LIMIT');
        p.demoGrants = (p.demoGrants || 0) + 1;
        if (params.kind === 'coin') store.credit(p, 100);
        else {
          const instance = 'i_' + (s.revision + 1);
          p.items[instance] = { inventoryItemId: itemId, playersInventoryItemId: instance, instanceData: {} };
        }
        return { ok: true, inventoryItemId: params.kind === 'item' ? itemId : null };
      });
    }
  };
}
if (typeof module !== 'undefined') module.exports = { createCloudMarket };
