function createAdapter(context) {
  const { DataApi } = require('@unity-services/cloud-save-1.4');
  const { ConfigurationApi, CurrenciesApi, InventoryApi } = require('@unity-services/economy-2.5');
  const axios = require('axios-1.6');
  // Service authority is needed after the player Economy write policy is applied.
  const auth = { ...context, accessToken: context.serviceToken };
  const save = new DataApi(auth);
  const config = new ConfigurationApi(auth);
  const currency = new CurrenciesApi(auth);
  const inventory = new InventoryApi(auth);
  const projectId = context.projectId;
  const hashes = {};
  async function playerArgs(playerId) {
    if (!hashes[playerId]) {
      const response = await config.getPlayerConfiguration({ projectId, playerId });
      hashes[playerId] = response.data.metadata.configAssignmentHash;
    }
    return { projectId, playerId, configAssignmentHash: hashes[playerId] };
  }
  return {
    async requireInventoryDefinition(playerId, itemId) {
      const response = await config.getPlayerConfiguration({ projectId, playerId });
      hashes[playerId] = response.data.metadata.configAssignmentHash;
      if (!(response.data.results || []).some(item => item.id === itemId && item.type === 'INVENTORY_ITEM'))
        throw new Error('SETUP_REQUIRED: LEGENDARY_SWORD Inventory Item을 같은 환경에 Publish하세요.');
    },
    async read(key) {
      const response = await save.getCustomItems(projectId, 'simple_market', [key]);
      const item = (response.data.results || []).find(x => x.key === key);
      return item ? { value: item.value, writeLock: item.writeLock } : null;
    },
    async write(key, value, writeLock) {
      if (!writeLock) throw new Error('SETUP_REQUIRED: state must be created in Dashboard first.');
      await save.setCustomItem(projectId, 'simple_market', { key, value, writeLock });
    },
    async rpc(url, method, params) {
      // Never log axios errors: their request config may contain a private RPC URL.
      try {
        const response = await axios.post(url, { jsonrpc: '2.0', id: 1, method, params }, { timeout: 4500 });
        if (response.data.error || !Object.prototype.hasOwnProperty.call(response.data, 'result')) throw new Error();
        return response.data.result;
      } catch (_) { throw new Error('RPC_UNAVAILABLE: 잠시 후 결제 확인을 다시 누르세요.'); }
    },
    async balance(playerId) {
      const response = await currency.getPlayerCurrencies(await playerArgs(playerId));
      const coin = (response.data.results || []).find(x => x.currencyId === 'COIN');
      if (!coin) throw new Error('SETUP_REQUIRED: COIN 통화를 Publish하고 잔액을 조회하세요.');
      return coin;
    },
    async changeBalance(playerId, amount) {
      const method = amount > 0 ? 'incrementPlayerCurrencyBalance' : 'decrementPlayerCurrencyBalance';
      await currency[method]({ ...await playerArgs(playerId), currencyId: 'COIN', currencyModifyBalanceRequest: { amount: Math.abs(amount) } });
    },
    async getItem(playerId, instanceId) {
      const args = await playerArgs(playerId);
      try {
        const response = await inventory.getPlayerInventory({ ...args, playersInventoryItemIds: [instanceId] });
        return (response.data.results || []).find(x => x.playersInventoryItemId === instanceId);
      } catch (e) {
        if (Number(e.response && e.response.status || e.status) !== 400) throw e;
      }
      // Legacy receipt IDs may be rejected as a query filter. Read pages instead.
      // This is read-only and never interprets an incomplete scan as a missing item.
      let after;
      for (let page = 0; page < 20; page++) {
        const response = await inventory.getPlayerInventory({ ...args, limit: 100, ...(after ? { after } : {}) });
        const items = response.data.results || [];
        const found = items.find(x => x.playersInventoryItemId === instanceId);
        if (found) return found;
        if (!(response.data.links && response.data.links.next)) return undefined;
        const next = items.length && items[items.length - 1].playersInventoryItemId;
        if (!next || next === after) throw new Error('INVENTORY_SCAN_INCOMPLETE');
        after = next;
      }
      throw new Error('INVENTORY_SCAN_INCOMPLETE');
    },
    async removeItem(playerId, item) {
      await inventory.deleteInventoryItem({ ...await playerArgs(playerId), playersInventoryItemId: item.playersInventoryItemId, writeLock: item.writeLock, inventoryDeleteRequest: {} });
    },
    async addItem(playerId, itemId, instanceId, data) {
      await inventory.addInventoryItem({ ...await playerArgs(playerId), addInventoryRequest: { inventoryItemId: itemId, playersInventoryItemId: instanceId, instanceData: data || {} } });
    }
  };
}
