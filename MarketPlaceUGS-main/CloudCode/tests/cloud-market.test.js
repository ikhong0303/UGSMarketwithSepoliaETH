const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const clone = x => JSON.parse(JSON.stringify(x));
function fixture() {
  const db = { state: JSON.parse(fs.readFileSync(path.join(__dirname, '../classroom-state.json'))), lock: 1, failBefore: false, failAfter: false, legacy: {} };
  const context = { playerId: 'seller', projectId: 'project', environmentId: 'env', serviceToken: 'server' };
  const scope = { Buffer, module: { exports: {} }, require(name) {
    if (name.includes('economy')) throw Error('ECONOMY_MUST_NOT_BE_USED');
    if (name.includes('cloud-save')) return { DataApi: class {
      constructor(auth) { assert.equal(auth.accessToken, 'server'); }
      async getPrivateCustomItems(project, custom, keys) { assert.equal(custom, 'classroom_market'); return { data: { results: [{ key: 'state', value: clone(db.state), writeLock: String(db.lock) }] } }; }
      async setPrivateCustomItem(project, custom, { value, writeLock }) {
        assert(writeLock); assert.equal(custom, 'classroom_market');
        if (writeLock !== String(db.lock)) throw { status: 409 };
        if (db.failBefore) { db.failBefore = false; throw Error('network'); }
        db.state = clone(value); db.lock++;
        if (db.failAfter) { db.failAfter = false; throw Error('lost-response'); }
      }
      async getCustomItems(project, custom, keys) { return { data: { results: keys[0] === 'state' ? [{ key: 'state', value: { payments: db.legacy } }] : [] } }; }
    } };
    return {};
  } };
  vm.createContext(scope);
  for (const name of ['core.js','cloud-store.js','cloud-adapter.js','cloud-market.js']) vm.runInContext(fs.readFileSync(path.join(__dirname,'../src',name),'utf8'), scope);
  const api = scope.createCloudAdapter(context);
  const user = id => scope.createCloudMarket(api, { ...context, playerId: id });
  return { db, api, user };
}
async function listing(f, price = 100) {
  const seller = f.user('seller');
  await seller.Mkt_GrantDemo({ kind: 'item', request_id: 'seed' });
  const item = (await seller.Mkt_GetPlayer()).items[0];
  const result = await seller.Mkt_CreateListing({ players_inventory_item_id: item.playersInventoryItemId, price, request_id: 'list' });
  return { listing_id: result.listingId, request_id: 'buy' };
}
test('new player gets 1000 exactly once under concurrent login; no Economy calls', async () => {
  const f = fixture(); await Promise.all(Array.from({length:12}, () => f.user('a').Mkt_GetPlayer()));
  assert.equal(f.db.state.players.a.balance,1000); assert.equal(f.db.state.revision,1);
});
test('concurrent buyers: only one debit, one owner, one seller receivable', async () => {
  const f=fixture(), args=await listing(f);
  const result=await Promise.allSettled(['a','b','c'].map(id=>f.user(id).Mkt_BuyListing(args)));
  assert.equal(result.filter(x=>x.status==='fulfilled').length,1);
  assert.equal(Object.values(f.db.state.players).reduce((n,p)=>n+Object.keys(p.items).length,0),1);
  const l=f.db.state.listings[args.listing_id]; assert.equal(l.status,'SOLD');
  assert.equal(f.db.state.players[l.buyerPlayerId].balance,900);
  assert.equal((await f.user('seller').Mkt_ClaimEarnings({request_id:'claim'})).claimed,100);
  assert.equal(f.db.state.players.seller.balance,1100);
});
test('lost purchase response is replayable after commit without duplicate debit', async()=>{
  const f=fixture(),args=await listing(f);f.db.failAfter=true;
  await assert.rejects(f.user('buyer').Mkt_BuyListing(args),/lost-response/);
  await f.user('buyer').Mkt_BuyListing(args);
  assert.equal(f.db.state.players.buyer.balance,900);assert.equal(Object.keys(f.db.state.players.buyer.items).length,1);
});
test('failure before commit leaves listing and balances untouched', async()=>{
  const f=fixture(),args=await listing(f);await f.user('buyer').Mkt_GetPlayer();f.db.failBefore=true;
  await assert.rejects(f.user('buyer').Mkt_BuyListing(args));
  assert.equal(f.db.state.listings[args.listing_id].status,'ACTIVE');assert.equal(f.db.state.players.buyer.balance,1000);
  await f.user('buyer').Mkt_BuyListing(args);assert.equal(f.db.state.players.buyer.balance,900);
});
test('concurrent purchases of different listings cannot overspend',async()=>{
 const f=fixture(),first=await listing(f,700), seller=f.user('seller');
 await seller.Mkt_GrantDemo({kind:'item',request_id:'seed2'});
 const item=(await seller.Mkt_GetPlayer()).items[0];
 const second=await seller.Mkt_CreateListing({players_inventory_item_id:item.playersInventoryItemId,price:700,request_id:'list2'});
 const results=await Promise.allSettled([f.user('buyer').Mkt_BuyListing(first),f.user('buyer').Mkt_BuyListing({listing_id:second.listingId,request_id:'buy2'})]);
 assert.equal(results.filter(x=>x.status==='fulfilled').length,1);assert.equal(f.db.state.players.buyer.balance,300);
});
test('buy versus cancel leaves exactly one item owner',async()=>{
 const f=fixture(),args=await listing(f);
 const results=await Promise.allSettled([f.user('buyer').Mkt_BuyListing(args),f.user('seller').Mkt_CancelListing({...args,request_id:'cancel'})]);
 assert.equal(results.filter(x=>x.status==='fulfilled').length,1);
 assert.equal(Object.values(f.db.state.players).reduce((n,p)=>n+Object.keys(p.items).length,0),1);
});
test('cancel can be replayed and returned item can be listed again',async()=>{
 const f=fixture(),args=await listing(f),seller=f.user('seller');
 const r=await seller.Mkt_CancelListing({...args,request_id:'cancel'});await seller.Mkt_CancelListing({...args,request_id:'cancel'});
 await seller.Mkt_CreateListing({players_inventory_item_id:r.returnedPlayersInventoryItemId,price:200,request_id:'relist'});
 assert.equal((await seller.Mkt_GetActiveListings({})).listings.length,1);
});
test('repeated settlement and gacha are idempotent; request IDs bind parameters',async()=>{
 const f=fixture(),args=await listing(f);await f.user('buyer').Mkt_BuyListing(args);
 const seller=f.user('seller');await seller.Mkt_ClaimEarnings({request_id:'claim'});await seller.Mkt_ClaimEarnings({request_id:'claim'});
 assert.equal(f.db.state.players.seller.balance,1100);
 await seller.Mkt_Gacha({request_id:'gacha'});await seller.Mkt_Gacha({request_id:'gacha'});assert.equal(f.db.state.players.seller.balance,1000);
 await assert.rejects(seller.Mkt_GrantDemo({kind:'coin',request_id:'gacha'}),/REQUEST_ID_REUSED/);
});
test('client cannot mint paid items or trade NFT; demo grants can be disabled',async()=>{
 const f=fixture();f.db.state.demoEnabled=false;
 await assert.rejects(f.user('seller').Mkt_GrantDemo({kind:'coin',request_id:'x'}),/DEMO_DISABLED/);
 await f.api.addItem('seller','MYTHIC_SWORD_NFT','nft',{tokenId:'1'});
 await assert.rejects(f.user('seller').Mkt_CreateListing({players_inventory_item_id:'nft',price:1,request_id:'list'}),/ITEM_NOT_TRADABLE/);
 assert.equal((await f.user('seller').Mkt_GetPlayer()).items.length,1);
});
test('public list excludes private account data, metadata and receipts',async()=>{
 const f=fixture();await listing(f);const value=JSON.stringify(await f.user('viewer').Mkt_GetActiveListings({}));
 for(const key of ['playersInventoryItemId','payments','instanceData','balance','operations'])assert(!value.includes(key));
});
test('payment award and receipt commit atomically and recover a lost response',async()=>{
 const f=fixture();const p={key:'tx',txHash:'tx',playerId:'seller',amount:10000};f.db.failAfter=true;
 await assert.rejects(f.api.grantPayment(p));await f.api.grantPayment(p);
 assert.equal(f.db.state.players.seller.balance,11000);assert.equal(f.db.state.payments.tx.status,'GRANTED');
 await assert.rejects(f.api.grantPayment({...p,playerId:'other'}),/WRONG_PLAYER/);
});
test('existing Economy receipt is marked for migration rather than awarded again',async()=>{
 const f=fixture();f.db.legacy.tx={playerId:'seller',status:'GRANTED',amount:10000};
 assert.equal((await f.api.read('state')).value.payments.tx.status,'MIGRATION_REQUIRED');
 assert.equal(Object.keys(f.db.state.players).length,0);
});
test('missing ledger never silently resets data; invalid IDs and prices rejected',async()=>{
 const f=fixture();await assert.rejects(f.user('seller').Mkt_CreateListing({players_inventory_item_id:'__proto__',price:1,request_id:'bad'}));
 await assert.rejects(f.user('seller').Mkt_CreateListing({players_inventory_item_id:'x',price:NaN,request_id:'bad'}));
 f.db.state.version=1;await assert.rejects(f.user('seller').Mkt_GetPlayer(),/SETUP_REQUIRED/);
});

for (const product of ['GOLD', 'LEGENDARY-SWORD']) test(product + ' verified claim uses atomic v2 storage and cannot replay after spending/selling', async () => {
  const f = fixture(), originalRead = f.api.read.bind(f.api);
  const txHash = '0x' + 'a'.repeat(64), blockHash = '0x' + 'b'.repeat(64);
  const receiver = '0x' + '2'.repeat(40), sender = '0x' + '1'.repeat(40);
  f.api.read = async key => key === 'config' ? { value: { receiverAddress: receiver, rpcUrl: 'https://rpc.example', priceWei: '100000000000000', goldAmount: 10000, confirmations: 3 } } : originalRead(key);
  const tx = { hash: txHash, from: sender, to: receiver, value: '0x5af3107a4000', blockHash, input: '0x' + Buffer.from('UGS-' + product + '-v1|project|env|seller').toString('hex') };
  f.api.rpc = async (_, method) => ({ eth_chainId: '0xaa36a7', eth_getCode: '0x', eth_getTransactionByHash: tx, eth_getTransactionReceipt: { transactionHash: txHash, from: sender, to: receiver, status: '0x1', blockNumber: '0x64', blockHash }, eth_blockNumber: '0x66', eth_getBlockByNumber: { hash: blockHash } })[method];
  const endpoint = product === 'GOLD' ? 'Gold_Claim' : 'Sword_Claim';
  const results = await Promise.all(Array.from({ length: 6 }, () => f.user('seller')[endpoint]({ tx_hash: txHash })));
  assert(results.every(x => x.status === 'GRANTED'));
  assert.equal(Object.keys(f.db.state.payments).length, 1);
  const p = f.db.state.players.seller;
  assert.equal(p.balance, product === 'GOLD' ? 11000 : 1000);
  assert.equal(Object.keys(p.items).length, product === 'GOLD' ? 0 : 1);
  p.balance = 0; p.items = {}; // Simulate assets spent after a successful grant.
  await f.user('seller')[endpoint]({ tx_hash: txHash });
  assert.equal(f.db.state.players.seller.balance, 0); assert.equal(Object.keys(f.db.state.players.seller.items).length, 0);
});
