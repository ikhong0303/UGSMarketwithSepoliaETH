const { test } = require('node:test');
const assert = require('node:assert/strict');
const { createMarket } = require('../src/core');
const clone = x => JSON.parse(JSON.stringify(x));
const hash = '0x' + 'a'.repeat(64);
const blockHash = '0x' + 'b'.repeat(64);
const buyer = '0x' + '1'.repeat(40);
const receiver = '0x' + '2'.repeat(40);
const context = { playerId: 'player1', projectId: 'project1', environmentId: 'environment1' };
function fixture() {
  const state = { version: 1, payments: {}, listings: {}, operations: {} };
  const config = { receiverAddress: receiver, rpcUrl: 'https://rpc.example', priceWei: '100000000000000', goldAmount: 10000, confirmations: 3 };
  const db = { state, version: 1, balances: { player1: 1000, player2: 1000 }, items: {}, increments: 0, decrements: 0 };
  const input = '0x' + Buffer.from('UGS-GOLD-v1|project1|environment1|player1').toString('hex');
  const tx = { hash, to: receiver, from: buyer, value: '0x5af3107a4000', input, blockHash, chainId: '0xaa36a7' };
  const receipt = { transactionHash: hash, status: '0x1', to: receiver, from: buyer, blockHash, blockNumber: '0x64' };
  const rpcValues = { eth_chainId: '0xaa36a7', eth_getTransactionByHash: tx, eth_getTransactionReceipt: receipt, eth_blockNumber: '0x66', eth_getBlockByNumber: { hash: blockHash }, eth_getCode: '0x' };
  const api = {
    async requireInventoryDefinition(id, itemId) {
      assert.equal(itemId, 'LEGENDARY_SWORD');
      if (db.missingSword) throw new Error('SETUP_REQUIRED');
    },
    async read(key) { return { value: clone(key === 'state' ? db.state : config), writeLock: String(db.version) }; },
    async write(key, next, lock) { if (String(db.version) !== lock) throw Object.assign(new Error('conflict'), { status: 409 }); db.state = clone(next); db.version++; },
    async rpc(url, method) { return clone(rpcValues[method]); },
    async balance(id) { return { balance: db.balances[id], currencyId: 'COIN' }; },
    async changeBalance(id, amount) {
      if (db.balances[id] + amount < 0) throw new Error('insufficient');
      if (amount > 0) db.increments++; else db.decrements++;
      db.balances[id] += amount;
    },
    async getItem(id, instance) { return db.items[id + ':' + instance]; },
    async removeItem(id, item) {
      const key = id + ':' + item.playersInventoryItemId;
      if (!db.items[key]) throw new Error('gone'); delete db.items[key];
    },
    async addItem(id, itemId, instanceId, data) {
      const key = id + ':' + instanceId;
      if (db.items[key]) throw new Error('duplicate');
      db.items[key] = { inventoryItemId: itemId, playersInventoryItemId: instanceId, instanceData: data };
    }
  };
  return { db, config, tx, receipt, rpcValues, api, market: createMarket(api, context) };
}
test('quote binds product to project, environment and authenticated player', async () => {
  const f = fixture(); const q = await f.market.Gold_GetQuote();
  assert.equal(q.value, f.tx.value); assert.equal(q.data, f.tx.input); assert.equal(q.goldAmount, 10000);
});
test('valid receipt grants 10000 once, including concurrent and replayed claims', async () => {
  const f = fixture();
  const results = await Promise.all(Array.from({ length: 12 }, () => f.market.Gold_Claim({ tx_hash: hash })));
  assert(results.some(r => r.status === 'GRANTED'));
  assert.equal(f.db.balances.player1, 11000); assert.equal(f.db.increments, 1);
  assert.equal((await f.market.Gold_Claim({ tx_hash: hash })).status, 'GRANTED');
  assert.equal(f.db.increments, 1);
});
for (const [label, mutate] of Object.entries({
  'wrong chain': f => { f.rpcValues.eth_chainId = '0x1'; },
  'wrong recipient': f => { f.tx.to = buyer; },
  'wrong amount': f => { f.tx.value = '0x1'; },
  'failed transaction': f => { f.receipt.status = '0x0'; },
  'wrong account memo': f => { f.tx.input += '00'; },
  'payer is receiver': f => { f.tx.from = receiver; f.receipt.from = receiver; },
  'forwarding contract': f => { f.rpcValues.eth_getCode = '0x1234'; },
  'inconsistent transaction hash': f => { f.receipt.transactionHash = '0x' + 'c'.repeat(64); }
})) test('rejects ' + label + ' without awarding', async () => {
  const f = fixture(); mutate(f);
  await assert.rejects(f.market.Gold_Claim({ tx_hash: hash }));
  assert.equal(f.db.increments, 0); assert.equal(Object.keys(f.db.state.payments).length, 0);
});
test('another player cannot claim either before or after original grant', async () => {
  const f = fixture(); const other = createMarket(f.api, { ...context, playerId: 'player2' });
  await assert.rejects(other.Gold_Claim({ tx_hash: hash }), /WRONG_PLAYER/);
  await f.market.Gold_Claim({ tx_hash: hash });
  await assert.rejects(other.Gold_Claim({ tx_hash: hash }), /WRONG_PLAYER/);
});
test('unmined, insufficient confirmations and orphaned block remain pending', async () => {
  for (const mutate of [f => { f.rpcValues.eth_getTransactionReceipt = null; }, f => { f.rpcValues.eth_blockNumber = '0x65'; }, f => { f.rpcValues.eth_getBlockByNumber.hash = '0xother'; }]) {
    const f = fixture(); mutate(f);
    assert.equal((await f.market.Gold_Claim({ tx_hash: hash })).status, 'PENDING'); assert.equal(f.db.increments, 0);
  }
});
test('missing initialized ledger never silently disables concurrency protection', async () => {
  const f = fixture(); f.api.read = async () => null;
  await assert.rejects(f.market.Gold_Claim({ tx_hash: hash }), /SETUP_REQUIRED/);
});
test('lost Economy response does not double-credit on retry', async () => {
  const f = fixture(); const change = f.api.changeBalance;
  f.api.changeBalance = async (...args) => { await change(...args); throw new Error('timeout after commit'); };
  await assert.rejects(f.market.Gold_Claim({ tx_hash: hash }), /REVIEW_REQUIRED/);
  assert.equal((await f.market.Gold_Claim({ tx_hash: hash })).status, 'REVIEW_REQUIRED');
  assert.equal(f.db.balances.player1, 11000); assert.equal(f.db.increments, 1);
});
test('lost reservation response does not start a credit', async () => {
  const f = fixture(); const write = f.api.write;
  f.api.write = async (...args) => { await write(...args); throw new Error('lost reservation response'); };
  await assert.rejects(f.market.Gold_Claim({ tx_hash: hash }));
  assert.equal(f.db.increments, 0);
  assert.equal((await f.market.Gold_Claim({ tx_hash: hash })).status, 'REVIEW_REQUIRED');
});
test('full ledger rejects quote before the user pays', async () => {
  const f = fixture(); for (let i = 0; i < 500; i++) f.db.state.operations[i] = {};
  await assert.rejects(f.market.Gold_GetQuote(), /LEDGER_FULL/);
});
async function seedListing(f) {
  f.db.items['player1:original'] = { inventoryItemId: 'SWORD', playersInventoryItemId: 'original', instanceData: {} };
  return (await f.market.Mkt_CreateListing({ players_inventory_item_id: 'original', price: 120 })).listingId;
}
test('gacha spends 100, deduplicates request and grants one catalog item', async () => {
  const f = fixture(); const params = { request_id: 'a'.repeat(32) };
  await f.market.Mkt_Gacha(params); await f.market.Mkt_Gacha(params);
  assert.equal(f.db.balances.player1, 900); assert.equal(Object.keys(f.db.items).length, 1);
});
test('concurrent buy and cancel cannot both transfer the same listing', async () => {
  const f = fixture(); const id = await seedListing(f);
  const other = createMarket(f.api, { ...context, playerId: 'player2' });
  const results = await Promise.allSettled([other.Mkt_BuyListing({ listing_id: id }), f.market.Mkt_CancelListing({ listing_id: id })]);
  assert.equal(results.filter(x => x.status === 'fulfilled').length, 1);
  assert.equal(Object.keys(f.db.items).length, 1);
});
test('buy, seller proceeds and concurrent proceeds claims preserve balances', async () => {
  const f = fixture(); const id = await seedListing(f);
  await createMarket(f.api, { ...context, playerId: 'player2' }).Mkt_BuyListing({ listing_id: id });
  await Promise.all([f.market.Mkt_ClaimEarnings({}), f.market.Mkt_ClaimEarnings({})]);
  assert.equal(f.db.balances.player1, 1120); assert.equal(f.db.balances.player2, 880); assert.equal(f.db.increments, 1);
});
test('rejects own purchase, fractional price and injected currency', async () => {
  const f = fixture(); const id = await seedListing(f);
  await assert.rejects(f.market.Mkt_BuyListing({ listing_id: id }), /CANNOT_BUY_OWN/);
  await assert.rejects(f.market.Mkt_CreateListing({ players_inventory_item_id: 'x', price: 1.5 }), /INVALID_LISTING/);
  await assert.rejects(f.market.Mkt_ClaimEarnings({ currency_id: 'GEM' }), /INVALID_CURRENCY/);
});
test('price sorting is applied before limiting', async () => {
  const f = fixture();
  f.db.state.listings = { a: { status: 'ACTIVE', price: 200 }, b: { status: 'ACTIVE', price: 10 } };
  assert.equal((await f.market.Mkt_GetActiveListings({ limit: 1, sort: 'PRICE_ASC' })).listings[0].price, 10);
});

test('pending diagnostics distinguish missing transaction, receipt, block and confirmations without credit', async () => {
  for (const [method, value, reason] of [
    ['eth_getTransactionByHash', null, 'RPC_TX_NOT_FOUND'],
    ['eth_getTransactionReceipt', null, 'RPC_RECEIPT_NOT_FOUND'],
    ['eth_getBlockByNumber', null, 'RPC_BLOCK_MISMATCH'],
    ['eth_blockNumber', '0x64', 'CONFIRMATIONS:1/3']
  ]) {
    const f = fixture(); f.rpcValues[method] = value;
    const result = await f.market.Gold_Claim({ tx_hash: hash });
    assert.equal(result.status, 'PENDING');
    assert.equal(result.pendingReason, reason);
    assert.equal(f.db.increments, 0);
  }
});

async function swordFixture() {
  const f = fixture();
  const quote = await f.market.Sword_GetQuote();
  f.tx.input = quote.data;
  return f;
}
test('sword quote validates configuration and binds a different product memo', async () => {
  const f = fixture(); const q = await f.market.Sword_GetQuote();
  assert.equal(q.itemId, 'LEGENDARY_SWORD'); assert.equal(q.quantity, 1);
  assert.notEqual(q.data, f.tx.input); assert.equal(q.value, f.tx.value);
  f.db.missingSword = true;
  await assert.rejects(f.market.Sword_GetQuote(), /SETUP_REQUIRED/);
});
test('sword grants one inventory item without changing gold, under concurrent replay', async () => {
  const f = await swordFixture();
  const results = await Promise.all(Array.from({length: 8}, () => f.market.Sword_Claim({tx_hash: hash})));
  assert(results.some(r => r.status === 'GRANTED'));
  const r = await f.market.Sword_Claim({tx_hash: hash});
  assert.equal(r.itemId, 'LEGENDARY_SWORD'); assert.equal(r.quantity, 1);
  assert.equal(Object.keys(f.db.items).length, 1);
  assert.equal(f.db.balances.player1, 1000); assert.equal(f.db.increments, 0);
  assert.equal(f.db.items['player1:' + r.instanceId].instanceData.paymentTx, hash);
});
test('gold receipts cannot purchase swords and sword receipts cannot purchase gold', async () => {
  const gold = fixture();
  await gold.market.Gold_Claim({tx_hash: hash});
  await assert.rejects(gold.market.Sword_Claim({tx_hash: hash}), /WRONG_PLAYER/);
  const sword = await swordFixture();
  await sword.market.Sword_Claim({tx_hash: hash});
  await assert.rejects(sword.market.Gold_Claim({tx_hash: hash}), /WRONG_PLAYER/);
  assert.equal(sword.db.increments, 0);
});
test('sword ownership is bound to authenticated player before and after grant', async () => {
  const f = await swordFixture(); const other = createMarket(f.api, {...context, playerId:'player2'});
  await assert.rejects(other.Sword_Claim({tx_hash: hash}), /WRONG_PLAYER/);
  await f.market.Sword_Claim({tx_hash: hash});
  await assert.rejects(other.Sword_Claim({tx_hash: hash}), /WRONG_PLAYER/);
});
test('pending or missing definition cannot reserve or grant a sword', async () => {
  const f = await swordFixture(); f.rpcValues.eth_getTransactionReceipt = null;
  assert.equal((await f.market.Sword_Claim({tx_hash:hash})).pendingReason, 'RPC_RECEIPT_NOT_FOUND');
  assert.deepEqual(f.db.state.payments, {});
  f.rpcValues.eth_getTransactionReceipt = f.receipt; f.db.missingSword = true;
  await assert.rejects(f.market.Sword_Claim({tx_hash:hash}), /SETUP_REQUIRED/);
  assert.deepEqual(f.db.state.payments, {});
});
test('lost sword award response is not retried, even if inventory was later sold', async () => {
  const f = await swordFixture(); const add = f.api.addItem;
  let calls = 0;
  f.api.addItem = async (...args) => { calls++; await add(...args); throw new Error('lost response'); };
  await assert.rejects(f.market.Sword_Claim({tx_hash:hash}), /REVIEW_REQUIRED/);
  f.db.items = {};
  assert.equal((await f.market.Sword_Claim({tx_hash:hash})).status, 'REVIEW_REQUIRED');
  assert.equal(calls, 1);
});
test('granted sword receipt does not re-create a sold sword', async () => {
  const f = await swordFixture(); await f.market.Sword_Claim({tx_hash:hash}); f.db.items = {};
  assert.equal((await f.market.Sword_Claim({tx_hash:hash})).status, 'GRANTED');
  assert.deepEqual(f.db.items, {});
});
test('sword rejects bad amount, receiver, chain and failed receipt without inventory mutation', async () => {
  for (const mutate of [f=>f.tx.value='0x1', f=>f.tx.to=buyer, f=>f.rpcValues.eth_chainId='0x1', f=>f.receipt.status='0x0']) {
    const f = await swordFixture(); mutate(f);
    await assert.rejects(f.market.Sword_Claim({tx_hash:hash}));
    assert.deepEqual(f.db.items, {}); assert.deepEqual(f.db.state.payments, {});
  }
});

test('sword review diagnoses existing item without re-award or state transition', async () => {
 const f = await swordFixture(); const add = f.api.addItem; let calls = 0;
 f.api.addItem = async (...a) => { calls++; await add(...a); throw Object.assign(new Error('secret'), {response:{status:503,data:{code:123}}}); };
 await assert.rejects(f.market.Sword_Claim({tx_hash:hash}), /INVENTORY_ADD:HTTP_503:CODE_123/);
 const result = await f.market.Sword_Claim({tx_hash:hash});
 assert.equal(result.status, 'REVIEW_REQUIRED');
 assert.match(result.pendingReason, /INVENTORY_MATCH_FOUND/);
 assert.equal(calls, 1);
 f.db.items = {};
 assert.match((await f.market.Sword_Claim({tx_hash:hash})).pendingReason, /INVENTORY_INSTANCE_NOT_FOUND/);
 assert.equal(calls, 1);
});

test('review exposes lookup service validation details without request secrets', async () => {
 const f = await swordFixture();
 f.api.addItem = async () => { throw new Error('unknown'); };
 await assert.rejects(f.market.Sword_Claim({tx_hash:hash}), /REVIEW_REQUIRED/);
 f.api.getItem = async () => { throw {response:{status:400,data:{code:123,title:'Validation',detail:'Invalid instance ID',details:[{message:'ID length exceeded'}]}},config:{headers:{Authorization:'secret'}}}; };
 const r = await f.market.Sword_Claim({tx_hash:hash});
 assert.match(r.pendingReason, /HTTP_400:CODE_123:Validation:Invalid instance ID:ID length exceeded/);
 assert(!r.pendingReason.includes('secret'));
 assert.equal(r.status,'REVIEW_REQUIRED');
});

async function approvedRecoveryFixture() {
 const f = fixture();
 const txHash='0x67a8736d20780e50c421f2a282b62b26e9b11eb7d4e6f440a1cb4fdc72fbbe0d';
 const playerId='py7ENhQ4WIRk8eeYVZQm3PoWP1Wq';
 f.tx.hash=txHash;f.receipt.transactionHash=txHash;
 f.tx.input='0x'+Buffer.from('UGS-LEGENDARY-SWORD-v1|project1|environment1|'+playerId).toString('hex');
 f.db.state.payments['sword_'+txHash]={playerId,txHash,status:'GRANTING',itemId:'LEGENDARY_SWORD',quantity:1,instanceId:'sword_'+txHash.slice(2)};
 f.market=createMarket(f.api,{...context,playerId});
 return {...f,txHash,playerId};
}
test('approved recovery awards once under concurrency and preserves other receipts',async()=>{
 const f=await approvedRecoveryFixture();f.db.state.payments.gold={status:'GRANTED'};
 const results=await Promise.all(Array.from({length:8},()=>f.market.Sword_Claim({tx_hash:f.txHash})));
 assert(results.some(r=>r.status==='GRANTED'));assert.equal(Object.keys(f.db.items).length,1);
 assert.deepEqual(f.db.state.payments.gold,{status:'GRANTED'});
 f.db.items={};assert.equal((await f.market.Sword_Claim({tx_hash:f.txHash})).status,'GRANTED');assert.deepEqual(f.db.items,{});
});
test('uncertain recovery never retries the inventory write',async()=>{
 const f=await approvedRecoveryFixture();let calls=0;
 f.api.addItem=async()=>{calls++;throw new Error('lost');};
 await assert.rejects(f.market.Sword_Claim({tx_hash:f.txHash}),/REVIEW_REQUIRED/);
 assert.equal((await f.market.Sword_Claim({tx_hash:f.txHash})).status,'REVIEW_REQUIRED');assert.equal(calls,1);
});
test('recovery requires valid payment and successful absence lookup',async()=>{
 const f=await approvedRecoveryFixture();f.tx.value='0x1';
 await assert.rejects(f.market.Sword_Claim({tx_hash:f.txHash}),/INVALID_AMOUNT/);assert.deepEqual(f.db.items,{});
 f.tx.value='0x5af3107a4000';f.api.getItem=async()=>{throw new Error('unavailable');};
 await assert.rejects(f.market.Sword_Claim({tx_hash:f.txHash}));assert.equal(f.db.state.payments['sword_'+f.txHash].recoveryStartedAt,undefined);
});
