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
      const response = await inventory.getPlayerInventory({ ...await playerArgs(playerId), playersInventoryItemIds: [instanceId] });
      return (response.data.results || []).find(x => x.playersInventoryItemId === instanceId);
    },
    async removeItem(playerId, item) {
      await inventory.deleteInventoryItem({ ...await playerArgs(playerId), playersInventoryItemId: item.playersInventoryItemId, writeLock: item.writeLock, inventoryDeleteRequest: {} });
    },
    async addItem(playerId, itemId, instanceId, data) {
      await inventory.addInventoryItem({ ...await playerArgs(playerId), addInventoryRequest: { inventoryItemId: itemId, playersInventoryItemId: instanceId, instanceData: data || {} } });
    }
  };
}
