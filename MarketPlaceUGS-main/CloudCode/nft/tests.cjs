const { test } = require('node:test');
const assert = require('node:assert/strict');
const { Wallet, verifyMessage, id } = require('../../NFTWorkshop/node_modules/ethers');
const { createNft } = require('./core.cjs');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');
const owner = new Wallet('0x' + '11'.repeat(32));
const other = new Wallet('0x' + '22'.repeat(32));
const contract = '0x' + '33'.repeat(20);
const word = n => '0x' + BigInt(n).toString(16).padStart(64, '0');
function setup() {
  const db = { nft_config: { contractAddress: contract, rpcUrl: 'https://example.test' },
    nft_state: { version: 1, sequence: 0, bindings: {}, wallets: {}, challenges: {}, leases: {} } };
  let version = 1;
  const f = { db, tokens: ['1'], items: [], rpcFails: false, scanFails: false, adds: 0, removes: 0 };
  f.api = {
    read: async key => ({ value: structuredClone(db[key]), writeLock: String(version) }),
    write: async (key, value, lock) => { if (lock !== String(version)) throw Object.assign(Error('conflict'), { status: 409 }); db[key] = structuredClone(value); version++; },
    requireDefinition: async () => {},
    rpc: async (_url, method, params) => {
      if (f.rpcFails) throw Error('RPC_FAILED');
      if (method === 'eth_chainId') return '0xaa36a7';
      if (method === 'eth_blockNumber') return '0x100';
      if (method === 'eth_getBlockByNumber') return { hash: '0xblock' };
      const data = params[0].data;
      assert.equal(params[1], '0xfe');
      if (data.startsWith('0x70a08231')) return word(f.tokens.length);
      if (data.startsWith('0x2f745c59')) return word(f.tokens[Number(BigInt('0x' + data.slice(-64)))]);
      if (data.startsWith('0x6352211e')) return '0x' + owner.address.slice(2).toLowerCase().padStart(64, '0');
      throw Error('unexpected RPC');
    },
    inventory: async () => { if (f.scanFails) throw Error('SCAN_FAILED'); return structuredClone(f.items); },
    addItem: async (_p, inventoryItemId, playersInventoryItemId, instanceData) => {
      assert(!f.items.some(x => x.playersInventoryItemId === playersInventoryItemId));
      f.items.push({ inventoryItemId, playersInventoryItemId, instanceData }); f.adds++;
      if (f.timeoutAfterAdd) { f.timeoutAfterAdd = false; throw Error('RESPONSE_LOST'); }
    },
    removeItem: async (_p, item) => { f.items = f.items.filter(x => x.playersInventoryItemId !== item.playersInventoryItemId); f.removes++; }
  };
  f.server = (playerId = 'p1') => createNft(f.api, { playerId, projectId: 'project', environmentId: 'production' }, { verifyMessage, id });
  f.bind = async () => {
    const c = await f.server().Nft_GetChallenge({ wallet_address: owner.address });
    return f.server().Nft_BindWallet({ signature: await owner.signMessage(c.message) });
  };
  return f;
}
test('signature binds once; challenge cannot replay or bind a second player', async () => {
  const f = setup();
  const c = await f.server().Nft_GetChallenge({ wallet_address: owner.address });
  const signature = await owner.signMessage(c.message);
  await assert.rejects(f.server().Nft_BindWallet({ signature: await other.signMessage(c.message) }), /WRONG_SIGNER/);
  await f.server().Nft_BindWallet({ signature });
  await assert.rejects(f.server().Nft_BindWallet({ signature }), /CHALLENGE_EXPIRED/);
  await assert.rejects(f.server('p2').Nft_GetChallenge({ wallet_address: owner.address }), /WALLET_ALREADY_LINKED/);
  await assert.rejects(f.server().Nft_GetChallenge({ wallet_address: other.address }), /PLAYER_ALREADY_LINKED/);
});
test('expired or superseded challenge fails', async () => {
  const f = setup();
  const c = await f.server().Nft_GetChallenge({ wallet_address: owner.address });
  await f.server().Nft_GetChallenge({ wallet_address: owner.address });
  await assert.rejects(f.server().Nft_BindWallet({ signature: await owner.signMessage(c.message) }));
  f.db.nft_state.challenges.p1.expires = 1;
  await assert.rejects(f.server().Nft_BindWallet({ signature: await owner.signMessage(c.message) }), /CHALLENGE_EXPIRED/);
});
test('unlinked player gets no inventory; repeated sync grants exactly once', async () => {
  const f = setup();
  assert.equal((await f.server().Nft_SyncInventory()).status, 'NOT_LINKED');
  await f.bind();
  assert.equal((await f.server().Nft_SyncInventory()).added, 1);
  assert.equal((await f.server().Nft_SyncInventory()).added, 0);
  assert.equal(f.adds, 1);
  assert.equal(f.items[0].instanceData.tokenId, '1');
});
test('transfer away removes only NFT mirror, preserves legendary sword', async () => {
  const f = setup(); await f.bind(); await f.server().Nft_SyncInventory();
  f.items.push({ inventoryItemId: 'LEGENDARY_SWORD', playersInventoryItemId: 'ordinary' });
  f.tokens = [];
  assert.equal((await f.server().Nft_SyncInventory()).removed, 1);
  assert.equal(f.items.length, 1); assert.equal(f.items[0].inventoryItemId, 'LEGENDARY_SWORD');
});
test('RPC failure or incomplete inventory scan never removes an item', async () => {
  const f = setup(); await f.bind(); await f.server().Nft_SyncInventory();
  f.tokens = []; f.rpcFails = true;
  await assert.rejects(f.server().Nft_SyncInventory(), /RPC_FAILED/);
  f.rpcFails = false; f.scanFails = true;
  await assert.rejects(f.server().Nft_SyncInventory(), /SCAN_FAILED/);
  assert.equal(f.removes, 0); assert.equal(f.items.length, 1);
});
test('unknown response after grant recovers without adding duplicate', async () => {
  const f = setup(); await f.bind(); f.timeoutAfterAdd = true;
  await assert.rejects(f.server().Nft_SyncInventory(), /RESPONSE_LOST/);
  assert.equal((await f.server().Nft_SyncInventory()).added, 0); assert.equal(f.adds, 1);
});
test('active lease blocks concurrent reconciliation', async () => {
  const f = setup(); await f.bind();
  f.db.nft_state.leases.p1 = { id: 100, until: Date.now() + 60000 };
  await assert.rejects(f.server().Nft_SyncInventory(), /NFT_BUSY_RETRY/);
  assert.equal(f.adds, 0);
});
test('ownership scan above limit keeps existing inventory unchanged', async () => {
  const f = setup(); await f.bind(); await f.server().Nft_SyncInventory();
  f.tokens = Array.from({ length: 21 }, (_, i) => String(i + 1));
  await assert.rejects(f.server().Nft_SyncInventory(), /NFT_HOLDING_LIMIT_20/);
  assert.equal(f.items.length, 1); assert.equal(f.removes, 0);
});
test('marketplace rejects NFT listing and purchase before any mutation', async () => {
  for (const name of ['Mkt_CreateListing', 'Mkt_BuyListing']) {
    const noWrite = () => { assert.fail('NFT market mutation must not run'); };
    const item = { inventoryItemId: 'MYTHIC_SWORD_NFT', status: 'ACTIVE', sellerPlayerId: 'other' };
    const sandbox = { module: { exports: {} }, require(name) {
      if (name.includes('cloud-save')) return { DataApi: class {
        async getCustomItems() { return { data: { results: [{ value: item }] } }; }
        setCustomItem = noWrite;
      } };
      return { ConfigurationApi: class { async getPlayerConfiguration() { return { data: { metadata: { configAssignmentHash: 'hash' } } }; } },
        InventoryApi: class {
          async getPlayerInventory() { return { data: { results: [item] } }; }
          deleteInventoryItem = noWrite; addInventoryItem = noWrite;
        }, CurrenciesApi: class { decrementPlayerCurrencyBalance = noWrite; } };
    } };
    vm.runInNewContext(readFileSync(__dirname + '/../../js/' + name + '.txt', 'utf8'), sandbox);
    await assert.rejects(sandbox.module.exports({ params: { listing_id: 'listing', players_inventory_item_id: 'instance', price: 1, currency_id: 'COIN' },
      context: { projectId: 'project', playerId: 'p1' } }), /NFT_TRANSFER_IN_WALLET_ONLY/);
  }
});
test('compiled Cloud Code verifies real personal_sign without native modules/browser globals', async () => {
  const f = setup(); const c = await f.server().Nft_GetChallenge({ wallet_address: owner.address });
  const sandbox = { module: { exports: {} }, require(name) {
    if (name === '@unity-services/cloud-save-1.4') return { DataApi: class {
      async getCustomItems(_p, _id, keys) { return { data: { results: [{ key: keys[0], ...await f.api.read(keys[0]) }] } }; }
      async setCustomItem(_p, _id, row) { await f.api.write(row.key, row.value, row.writeLock); }
    } };
    if (name === '@unity-services/economy-2.5') return { ConfigurationApi: class {}, InventoryApi: class {} };
    if (name === 'axios-1.6') return {};
    throw Error('Unexpected runtime dependency: ' + name);
  } };
  vm.runInNewContext(readFileSync(__dirname + '/deploy/Nft_BindWallet.js', 'utf8'), sandbox);
  const r = await sandbox.module.exports({ context: { playerId: 'p1', projectId: 'project', environmentId: 'production' }, params: { signature: await owner.signMessage(c.message) } });
  assert.equal(r.status, 'LINKED');
});
