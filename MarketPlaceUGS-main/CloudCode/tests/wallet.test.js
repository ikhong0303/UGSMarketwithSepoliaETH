const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../../Assets/Plugins/WebGL/SimpleMarketWallet.jslib'), 'utf8');
const address = '0x' + '1'.repeat(40), to = '0x' + '2'.repeat(40), hash = '0x' + 'a'.repeat(64);
function browser() {
  const storage = new Map(), calls = [], replies = new Map(), events = {};
  const provider = {
    isMetaMask: true,
    async request({ method, params }) {
      calls.push({ method, params });
      return { eth_requestAccounts: [address], eth_accounts: [address], eth_chainId: '0xaa36a7', eth_getBalance: '0xde0b6b3a7640000', eth_sendTransaction: hash }[method];
    },
    on(event, fn) { events[event] = fn; }
  };
  const window = { ethereum: provider, addEventListener() {}, dispatchEvent() {} };
  const library = {};
  const sandbox = { window, TextEncoder, LibraryManager: { library }, mergeInto: Object.assign, UTF8ToString: x => x, Event: class {}, setTimeout: fn => setImmediate(fn),
    localStorage: { getItem: k => storage.get(k) || null, setItem: (k, v) => storage.set(k, v), removeItem: k => storage.delete(k) },
    SendMessage(target, method, json) { const result = JSON.parse(json); if (replies.has(result.id)) replies.get(result.id)(result); }
  };
  vm.runInNewContext(source, sandbox);
  let id = 0;
  async function request(args) {
    const key = String(++id);
    return new Promise(resolve => { replies.set(key, resolve); library.SimpleMarketWalletRequest('SimpleMarketApp', JSON.stringify({ id: key, storageKey: 'account1', ...args })); });
  }
  return { request, calls, storage, provider, events };
}
test('NFT binding signs the exact UTF-8 challenge without sending a transaction', async () => {
  const b = browser(), original = b.provider.request;
  const message = 'NFT 연결\nPlayer: student1';
  b.provider.request = async args => {
    if (args.method !== 'personal_sign') return original(args);
    assert.equal(args.params[0], '0x' + Buffer.from(message, 'utf8').toString('hex'));
    assert.equal(args.params[1], address);
    return 'signed-challenge';
  };
  assert.equal((await b.request({ action: 'nftSign', from: address, data: message })).signature, 'signed-challenge');
  assert((await b.request({ action: 'nftSign', from: to, data: message })).error);
  assert.equal(b.calls.filter(x => x.method === 'eth_sendTransaction').length, 0);
});
test('MetaMask connection reports Sepolia account and formatted balance', async () => {
  const b = browser(); const result = await b.request({ action: 'connect' });
  assert.equal(result.address, address); assert.equal(result.balanceEth, '1.000000');
  assert.equal(b.calls.filter(x => x.method === 'eth_requestAccounts').length, 1);
});
test('send persists receipt before result, and blocks another send', async () => {
  const b = browser(); const args = { action: 'send', from: address, to, value: '0x5af3107a4000', data: '0x1234' };
  assert.equal((await b.request(args)).txHash, hash);
  assert.equal((await b.request({ action: 'pending' })).txHash, hash);
  assert((await b.request(args)).error);
  assert.equal(b.calls.filter(x => x.method === 'eth_sendTransaction').length, 1);
});
test('user rejection clears pending, uncertain network failure retains pending', async () => {
  for (const code of [4001, -32603]) {
    const b = browser(); const orig = b.provider.request;
    b.provider.request = async args => { if (args.method === 'eth_sendTransaction') throw Object.assign(new Error('failed'), { code }); return orig(args); };
    const result = await b.request({ action: 'send', from: address, to, value: '0x5af3107a4000', data: '0x1234' });
    assert(result.error);
    assert.equal((await b.request({ action: 'pending' })).status, code === 4001 ? 'EMPTY' : 'PENDING');
  }
});
test('account changes, mainnet and wrong price cannot submit', async () => {
  for (const mode of ['account', 'chain', 'price']) {
    const b = browser();
    const orig = b.provider.request;
    if (mode === 'chain') b.provider.request = args => args.method === 'eth_chainId' ? Promise.resolve('0x1') : orig(args);
    const result = await b.request({ action: 'send', from: mode === 'account' ? to : address, to, value: mode === 'price' ? '0x1' : '0x5af3107a4000', data: '0x1234' });
    assert(result.error); assert.equal(b.calls.filter(x => x.method === 'eth_sendTransaction').length, 0);
  }
});
test('recovery storage is scoped by game account and clear must match receipt', async () => {
  const b = browser();
  await b.request({ action: 'remember', txHash: hash });
  assert.equal((await b.request({ action: 'pending', storageKey: 'account2' })).status, 'EMPTY');
  await b.request({ action: 'clear', txHash: '0x' + 'b'.repeat(64) });
  assert.equal((await b.request({ action: 'pending' })).txHash, hash);
  await b.request({ action: 'clear', txHash: hash });
  assert.equal((await b.request({ action: 'pending' })).status, 'EMPTY');
});
test('NFT redeem sends zero ETH with redeem calldata and does not resend a pending coupon', async () => {
  const b = browser();
  const args = { action: 'nftRedeem', storageKey: 'coupon1', from: address, to, value: '0x0', data: '0xeda1122c' + 'a'.repeat(64) };
  assert.equal((await b.request(args)).txHash, hash);
  assert.equal((await b.request(args)).txHash, hash);
  const sends = b.calls.filter(x => x.method === 'eth_sendTransaction');
  assert.equal(sends.length, 1);
  assert.equal(sends[0].params[0].value, '0x0');
  assert.equal(sends[0].params[0].data, args.data);
});
test('NFT redeem rejects nonzero payment and unrelated contract calls', async () => {
  for (const override of [{ value: '0x1' }, { data: '0x1234' }]) {
    const b = browser();
    assert((await b.request({ action: 'nftRedeem', from: address, to, value: '0x0', data: '0xeda1122c' + 'a'.repeat(64), ...override })).error);
    assert.equal(b.calls.filter(x => x.method === 'eth_sendTransaction').length, 0);
  }
});
test('explicit local forget does not send or refund a transaction', async () => {
  const b = browser();
  await b.request({ action: 'remember', txHash: hash });
  await b.request({ action: 'forget' });
  assert.equal((await b.request({ action: 'pending' })).status, 'EMPTY');
  assert.equal(b.calls.length, 0);
});
