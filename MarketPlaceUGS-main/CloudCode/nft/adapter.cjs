const { DataApi } = require('@unity-services/cloud-save-1.4');
const { ConfigurationApi, InventoryApi } = require('@unity-services/economy-2.5');
const axios = require('axios-1.6');
exports.adapter = context => {
  const auth = { ...context, accessToken: context.serviceToken };
  const save = new DataApi(auth), config = new ConfigurationApi(auth), inventory = new InventoryApi(auth);
  const projectId = context.projectId;
  let configAssignmentHash;
  const args = playerId => ({ projectId, playerId, configAssignmentHash });
  return {
    async read(key) {
      const r = await save.getCustomItems(projectId, 'simple_market', [key]);
      return r.data.results.find(x => x.key === key);
    },
    async write(key, value, writeLock) { await save.setCustomItem(projectId, 'simple_market', { key, value, writeLock }); },
    async requireDefinition(playerId, itemId) {
      const r = await config.getPlayerConfiguration({ projectId, playerId });
      configAssignmentHash = r.data.metadata.configAssignmentHash;
      if (!r.data.results.some(x => x.id === itemId && x.type === 'INVENTORY_ITEM')) throw Error('PUBLISH_MYTHIC_SWORD_NFT');
    },
    async rpc(url, method, params) {
      try {
        const r = await axios.post(url, { jsonrpc: '2.0', id: 1, method, params }, { timeout: 2500 });
        if (r.data.error || !Object.prototype.hasOwnProperty.call(r.data, 'result')) throw Error();
        return r.data.result;
      } catch (_) { throw Error('NFT_RPC_FAILED_RETRY'); }
    },
    async inventory(player) {
      const all = []; let after;
      for (let page = 0; page < 10; page++) {
        const r = await inventory.getPlayerInventory({ ...args(player), limit: 100, ...(after ? { after } : {}) });
        if (!Array.isArray(r.data.results)) throw Error('INVENTORY_RESPONSE_INVALID');
        all.push(...r.data.results);
        if (!r.data.links?.next) return all;
        const next = r.data.results.at(-1)?.playersInventoryItemId;
        if (!next || next === after) throw Error('INVENTORY_SCAN_INCOMPLETE');
        after = next;
      }
      throw Error('INVENTORY_SCAN_INCOMPLETE');
    },
    async addItem(player, itemId, instance, data) {
      await inventory.addInventoryItem({ ...args(player), addInventoryRequest: { inventoryItemId: itemId, playersInventoryItemId: instance, instanceData: data } });
    },
    async removeItem(player, item) {
      await inventory.deleteInventoryItem({ ...args(player), playersInventoryItemId: item.playersInventoryItemId, inventoryDeleteRequest: { writeLock: item.writeLock } });
    }
  };
};
