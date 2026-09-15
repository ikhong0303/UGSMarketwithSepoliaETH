// Shared, dependency-injected business logic. No client-supplied player IDs or amounts.
function createMarket(api, context) {
  const playerId = context.playerId;
  if (!playerId || !context.projectId || !context.environmentId) throw new Error('LOGIN_REQUIRED');
  const currencyId = 'COIN';
  const catalog = ['SWORD', 'REDPOTION', 'BLUEPOTION'];
  const maxRecords = 500; // Small test project ledger. Never delete paid receipt records.
  const own = (obj, key) => Object.prototype.hasOwnProperty.call(obj, key);
  const copy = x => JSON.parse(JSON.stringify(x));
  const error = message => { throw new Error(message); };
  const validId = value => typeof value === 'string' && /^[A-Za-z0-9_-]{1,100}$/.test(value);
  function checkCurrency(params) {
    if (params.currency_id && params.currency_id !== currencyId) error('INVALID_CURRENCY');
  }
  function capacity(state) {
    if (Object.keys(state.payments).length + Object.keys(state.operations).length + Object.keys(state.listings).length >= maxRecords) error('LEDGER_FULL: 관리자 확인이 필요합니다.');
  }
  async function readState() {
    const item = await api.read('state');
    if (!item || !item.writeLock || item.value.version !== 1 || !item.value.payments || !item.value.listings || !item.value.operations) error('SETUP_REQUIRED: Cloud Save simple_market/state 초기 설정을 확인하세요.');
    return item;
  }
  async function change(mutator) {
    for (let attempt = 0; attempt < 5; attempt++) {
      const record = await readState();
      const state = copy(record.value);
      const result = mutator(state);
      if (JSON.stringify(state) === JSON.stringify(record.value)) return result;
      try {
        await api.write('state', state, record.writeLock);
        return result;
      } catch (e) {
        if ((e.response && e.response.status === 409) || e.status === 409) continue;
        throw e; // Unknown write result: never start a financial side effect.
      }
    }
    error('BUSY: 다른 요청 처리 중입니다. 다시 눌러주세요.');
  }
  function listing(state, id) {
    if (!validId(id) || !own(state.listings, id)) error('LISTING_NOT_FOUND');
    return state.listings[id];
  }
  async function configuration() {
    const record = await api.read('config');
    const cfg = record && record.value;
    if (!cfg || !/^0x[0-9a-fA-F]{40}$/.test(cfg.receiverAddress) || /^0x0{40}$/i.test(cfg.receiverAddress) || !/^https:\/\//.test(cfg.rpcUrl)) error('SETUP_REQUIRED: 수신 지갑과 RPC URL을 설정하세요.');
    // A single fixed product keeps old receipts unambiguous. New pricing needs a new product version.
    if (cfg.priceWei !== '100000000000000' || cfg.goldAmount !== 10000 || !Number.isInteger(cfg.confirmations) || cfg.confirmations < 3 || cfg.confirmations > 64) error('INVALID_CONFIG: 기본 상품은 0.0001 ETH / 10000 COIN, confirmations 3 이상입니다.');
    return cfg;
  }
  function memo() {
    const text = `UGS-GOLD-v1|${context.projectId}|${context.environmentId}|${playerId}`;
    if (!/^[\x20-\x7e]+$/.test(text)) error('INVALID_CONTEXT');
    return '0x' + Array.from(text).map(c => c.charCodeAt(0).toString(16).padStart(2, '0')).join('');
  }
  function paymentStatus(p) {
    if (p.playerId !== playerId) error('WRONG_PLAYER');
    return { status: p.status === 'GRANTED' ? 'GRANTED' : 'REVIEW_REQUIRED', txHash: p.txHash, goldAmount: p.amount };
  }
  async function verifyPayment(cfg, txHash) {
    const rpc = (method, params) => api.rpc(cfg.rpcUrl, method, params);
    const [chain, tx, receipt] = await Promise.all([
      rpc('eth_chainId', []), rpc('eth_getTransactionByHash', [txHash]), rpc('eth_getTransactionReceipt', [txHash])
    ]);
    if (BigInt(chain) !== 11155111n) error('WRONG_CHAIN');
    if (!tx) return 'RPC_TX_NOT_FOUND';
    if (!receipt || !receipt.blockNumber) return 'RPC_RECEIPT_NOT_FOUND';
    const lower = value => typeof value === 'string' ? value.toLowerCase() : '';
    const receiver = cfg.receiverAddress.toLowerCase();
    if (lower(tx.hash) !== txHash || lower(receipt.transactionHash) !== txHash || lower(tx.to) !== receiver || lower(receipt.to) !== receiver) error('INVALID_RECEIVER');
    if (!/^0x[0-9a-fA-F]{40}$/.test(tx.from) || lower(tx.from) === receiver || lower(receipt.from) !== lower(tx.from)) error('INVALID_SENDER');
    if (tx.chainId && BigInt(tx.chainId) !== 11155111n) error('WRONG_CHAIN');
    if (BigInt(tx.value) !== BigInt(cfg.priceWei)) error('INVALID_AMOUNT');
    if (lower(tx.input) !== memo()) error('WRONG_PLAYER: 해당 게임 계정으로 만든 결제가 아닙니다.');
    if (lower(tx.blockHash) !== lower(receipt.blockHash)) error('REORG_PENDING');
    const [head, block, code] = await Promise.all([
      rpc('eth_blockNumber', []), rpc('eth_getBlockByNumber', [receipt.blockNumber, false]), rpc('eth_getCode', [cfg.receiverAddress, receipt.blockNumber])
    ]);
    // This version accepts a plain receiving wallet, not a forwarding/refunding contract.
    if (code !== '0x') error('RECEIVER_MUST_BE_EOA');
    if (!block || lower(block.hash) !== lower(receipt.blockHash)) return 'RPC_BLOCK_MISMATCH';
    if (BigInt(receipt.status) !== 1n) error('TRANSACTION_FAILED');
    const count = BigInt(head) - BigInt(receipt.blockNumber) + 1n;
    return count >= BigInt(cfg.confirmations) ? null : `CONFIRMATIONS:${count}/${cfg.confirmations}`;
  }
  async function guardedEffect(effect) {
    try { await effect(); }
    catch (_) { error('REVIEW_REQUIRED: 처리 결과 확인이 필요합니다. 같은 작업을 다시 지급하지 않습니다.'); }
  }
  return {
    async Gold_GetQuote() {
      const cfg = await configuration();
      capacity((await readState()).value);
      const [chain, code] = await Promise.all([
        api.rpc(cfg.rpcUrl, 'eth_chainId', []), api.rpc(cfg.rpcUrl, 'eth_getCode', [cfg.receiverAddress, 'latest'])
      ]);
      if (BigInt(chain) !== 11155111n) error('WRONG_CHAIN');
      if (code !== '0x') error('RECEIVER_MUST_BE_EOA');
      return { chainId: '0xaa36a7', to: cfg.receiverAddress, value: '0x' + BigInt(cfg.priceWei).toString(16), data: memo(), goldAmount: 10000, priceEth: '0.0001', confirmations: cfg.confirmations };
    },
    async Gold_Claim(params) {
      if (typeof params.tx_hash !== 'string' || !/^0x[0-9a-fA-F]{64}$/.test(params.tx_hash)) error('INVALID_TX_HASH');
      const txHash = params.tx_hash.toLowerCase();
      let state = (await readState()).value;
      if (own(state.payments, txHash)) return paymentStatus(state.payments[txHash]);
      const cfg = await configuration();
      const pendingReason = await verifyPayment(cfg, txHash);
      if (pendingReason) return { status: 'PENDING', txHash, goldAmount: 10000, pendingReason };
      // Validate Economy before reserving. A missing currency does not strand a receipt.
      await api.balance(playerId);
      const reserved = await change(s => {
        if (own(s.payments, txHash)) return false;
        capacity(s);
        s.payments[txHash] = { txHash, playerId, amount: 10000, status: 'GRANTING', createdAt: Date.now() };
        return true;
      });
      if (!reserved) return paymentStatus((await readState()).value.payments[txHash]);
      // Only the invocation that successfully reserved can call increment, exactly once.
      // A crash or an uncertain Economy response stays GRANTING for operator reconciliation.
      // Cloud Save + Economy are not one atomic transaction; do not claim automatic exactly-once recovery.
      await guardedEffect(async () => {
        await api.changeBalance(playerId, 10000);
        await change(s => { s.payments[txHash].status = 'GRANTED'; s.payments[txHash].grantedAt = Date.now(); });
      });
      return { status: 'GRANTED', txHash, goldAmount: 10000 };
    },
    async Mkt_GetActiveListings(params) {
      let entries = Object.values((await readState()).value.listings).filter(x => x.status === 'ACTIVE');
      if (params.sort === 'PRICE_ASC') entries.sort((a, b) => a.price - b.price);
      else if (params.sort === 'PRICE_DESC') entries.sort((a, b) => b.price - a.price);
      else entries.sort((a, b) => b.createdAt - a.createdAt);
      const limit = Number.isInteger(params.limit) ? Math.max(1, Math.min(50, params.limit)) : 30;
      return { listings: entries.slice(0, limit) };
    },
    async Mkt_CreateListing(params) {
      checkCurrency(params);
      const instanceId = params.players_inventory_item_id;
      if (!validId(instanceId) || !Number.isSafeInteger(params.price) || params.price < 1 || params.price > 1000000) error('INVALID_LISTING');
      const id = 'lst_' + Date.now().toString(36) + '_' + Math.random().toString(36).slice(2, 14);
      const item = await api.getItem(playerId, instanceId);
      if (!item || !catalog.includes(item.inventoryItemId)) error('ITEM_NOT_FOUND');
      await change(s => {
        if (own(s.listings, id) || Object.values(s.listings).some(l => l.sourceInstanceId === instanceId && l.sellerPlayerId === playerId)) error('ALREADY_LISTED');
        capacity(s);
        s.listings[id] = { listingId: id, sourceInstanceId: instanceId, status: 'CREATING', sellerPlayerId: playerId, inventoryItemId: item.inventoryItemId, instanceData: item.instanceData || {}, currencyId, price: params.price, createdAt: Date.now() };
      });
      await guardedEffect(async () => {
        await api.removeItem(playerId, item);
        await change(s => { s.listings[id].status = 'ACTIVE'; });
      });
      return { listingId: id };
    },
    async Mkt_BuyListing(params) {
      const balance = await api.balance(playerId);
      const item = await change(s => {
        const l = listing(s, params.listing_id);
        if (l.status !== 'ACTIVE') error('LISTING_NOT_ACTIVE');
        if (l.sellerPlayerId === playerId) error('CANNOT_BUY_OWN');
        if (balance.balance < l.price) error('INSUFFICIENT_GOLD');
        l.status = 'BUYING'; l.buyerPlayerId = playerId;
        return copy(l);
      });
      const instanceId = 'buy_' + item.listingId;
      await guardedEffect(async () => {
        await api.changeBalance(playerId, -item.price);
        await api.addItem(playerId, item.inventoryItemId, instanceId, item.instanceData);
        await change(s => { const l = s.listings[item.listingId]; l.status = 'SOLD'; l.soldAt = Date.now(); l.earningsStatus = 'AVAILABLE'; });
      });
      return { ok: true, newPlayersInventoryItemId: instanceId };
    },
    async Mkt_CancelListing(params) {
      const item = await change(s => {
        const l = listing(s, params.listing_id);
        if (l.sellerPlayerId !== playerId) error('SELLER_ONLY');
        if (l.status !== 'ACTIVE') error('LISTING_NOT_ACTIVE');
        l.status = 'CANCELLING'; return copy(l);
      });
      const instanceId = 'ret_' + item.listingId;
      await guardedEffect(async () => {
        await api.addItem(playerId, item.inventoryItemId, instanceId, item.instanceData);
        await change(s => { s.listings[item.listingId].status = 'CANCELLED'; });
      });
      return { ok: true, returnedPlayersInventoryItemId: instanceId };
    },
    async Mkt_ClaimEarnings(params) {
      checkCurrency(params);
      await api.balance(playerId);
      const ids = await change(s => {
        const rows = Object.values(s.listings).filter(l => l.sellerPlayerId === playerId && l.status === 'SOLD' && l.earningsStatus === 'AVAILABLE');
        rows.forEach(l => { l.earningsStatus = 'CLAIMING'; });
        return rows.map(l => ({ id: l.listingId, price: l.price }));
      });
      const total = ids.reduce((sum, l) => sum + l.price, 0);
      if (!total) return { ok: true, claimed: 0 };
      await guardedEffect(async () => {
        await api.changeBalance(playerId, total);
        await change(s => { ids.forEach(l => { s.listings[l.id].earningsStatus = 'CLAIMED'; }); });
      });
      return { ok: true, claimed: total };
    },
    async Mkt_Gacha(params) {
      if (typeof params.request_id !== 'string' || !/^[a-f0-9]{32}$/.test(params.request_id)) error('INVALID_REQUEST_ID');
      const id = 'g_' + playerId + '_' + params.request_id;
      const previous = (await readState()).value.operations[id];
      if (previous) {
        if (previous.status !== 'DONE') error('REVIEW_REQUIRED');
        return { ok: true, inventoryItemId: previous.itemId };
      }
      if ((await api.balance(playerId)).balance < 100) error('INSUFFICIENT_GOLD');
      const itemId = catalog[Math.floor(Math.random() * catalog.length)];
      await change(s => {
        if (own(s.operations, id)) error('ALREADY_REQUESTED');
        capacity(s);
        s.operations[id] = { playerId, itemId, status: 'GRANTING', createdAt: Date.now() };
      });
      await guardedEffect(async () => {
        await api.changeBalance(playerId, -100);
        await api.addItem(playerId, itemId, id, {});
        await change(s => { s.operations[id].status = 'DONE'; });
      });
      return { ok: true, inventoryItemId: itemId };
    }
  };
}

if (typeof module !== 'undefined') module.exports = { createMarket };
