function createCloudAdapter(context) {
  const { DataApi } = require('@unity-services/cloud-save-1.4');
  const axios = require('axios-1.6');
  if (!context.serviceToken) throw Error('SERVER_AUTH_REQUIRED');
  const save = new DataApi({ ...context, accessToken: context.serviceToken });
  const project = context.projectId;
  const makeStore = typeof createCloudStore === 'function' ? createCloudStore : require('./cloud-store').createCloudStore;
  const store = makeStore({
    async readLedger() {
      const r = await save.getPrivateCustomItems(project, 'classroom_market', ['state']);
      return r.data.results.find(x => x.key === 'state');
    },
    async writeLedger(value, writeLock) {
      await save.setPrivateCustomItem(project, 'classroom_market', { key: 'state', value, writeLock });
    }
  });
  const catalog = ['SWORD', 'REDPOTION', 'BLUEPOTION', 'LEGENDARY_SWORD', 'MYTHIC_SWORD_NFT'];
  return {
    store,
    async read(key) {
      if (key === 'state') {
        const r = await store.read();
        const legacy = await save.getCustomItems(project, 'simple_market', ['state']);
        const old = legacy.data.results.find(x => x.key === 'state')?.value?.payments || {};
        const payments = Object.fromEntries(Object.entries(old).map(([k, v]) => [k, { ...v, status: 'MIGRATION_REQUIRED' }]));
        // Compatibility view for the existing, tested Sepolia verifier only.
        return { ...r, value: { ...r.value, payments: { ...payments, ...r.value.payments }, version: 1 } };
      }
      const r = await save.getCustomItems(project, 'simple_market', [key]);
      return r.data.results.find(x => x.key === key);
    },
    async write(key, value, writeLock) {
      if (key === 'state') throw Error('USE_ATOMIC_LEDGER');
      if (!writeLock) throw Error('SETUP_REQUIRED');
      await save.setCustomItem(project, 'simple_market', { key, value, writeLock });
    },
    async rpc(url, method, params) {
      try {
        const r = await axios.post(url, { jsonrpc: '2.0', id: 1, method, params }, { timeout: 4500 });
        if (r.data.error || !Object.prototype.hasOwnProperty.call(r.data, 'result')) throw Error();
        return r.data.result;
      } catch (_) { throw Error('RPC_UNAVAILABLE'); }
    },
    async requireInventoryDefinition(playerId, itemId) {
      if (!catalog.includes(itemId)) throw Error('INVALID_ITEM');
    },
    async balance(id) {
      return store.change(s => ({ currencyId: 'COIN', balance: store.player(s, id).balance }));
    },
    async inventory(id) {
      return store.change(s => Object.values(store.player(s, id).items));
    },
    async getItem(id, instance) { return (await this.inventory(id)).find(x => x.playersInventoryItemId === instance); },
    async addItem(id, itemId, instance, data) {
      if (!catalog.includes(itemId)) throw Error('INVALID_ITEM');
      return store.change(s => {
        const p = store.player(s, id), old = p.items[instance];
        if (old) {
          if (old.inventoryItemId !== itemId || JSON.stringify(old.instanceData) !== JSON.stringify(data || {})) throw Error('ITEM_CONFLICT');
          return;
        }
        p.items[instance] = { inventoryItemId: itemId, playersInventoryItemId: instance, instanceData: data || {} };
      });
    },
    async removeItem(id, item) {
      return store.change(s => { delete store.player(s, id).items[item.playersInventoryItemId]; });
    },
    async grantPayment({ key, txHash, playerId, itemId, instanceId, amount }) {
      return store.change(s => {
        const old = s.payments[key];
        if (old) {
          if (old.playerId !== playerId) throw Error('WRONG_PLAYER');
          if (old.status !== 'GRANTED') throw Error('REVIEW_REQUIRED');
        } else {
          const p = store.player(s, playerId);
          if (itemId) p.items[instanceId] = { inventoryItemId: itemId, playersInventoryItemId: instanceId, instanceData: { source: 'SEPOLIA_SHOP', paymentTx: txHash } };
          else store.credit(p, amount);
          s.payments[key] = { playerId, txHash, itemId, instanceId, amount, status: 'GRANTED', grantedAt: Date.now() };
        }
        return itemId ? { status: 'GRANTED', txHash, itemId, instanceId, quantity: 1 } : { status: 'GRANTED', txHash, goldAmount: amount };
      });
    }
  };
}
if (typeof module !== 'undefined') module.exports = { createCloudAdapter };
