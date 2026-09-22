// Classroom ledger: one private value, one compare-and-swap per transaction.
// Keep receipts permanently. Provision state in Dashboard; never create it in a race.
function createCloudStore(storage) {
  const clone = x => JSON.parse(JSON.stringify(x));
  const own = (o, k) => Object.prototype.hasOwnProperty.call(o, k);
  async function read() {
    const row = await storage.readLedger();
    if (!row?.writeLock || row.value?.version !== 2 || !Number.isSafeInteger(row.value.revision) || row.value.revision < 0 || !row.value.players || !row.value.listings || !row.value.operations || !row.value.payments)
      throw Error('SETUP_REQUIRED: classroom_market / Private / state v2');
    return row;
  }
  function player(s, id) {
    if (!/^[A-Za-z0-9_-]{1,100}$/.test(id) || ['__proto__', 'constructor', 'prototype'].includes(id)) throw Error('INVALID_PLAYER');
    if (!own(s.players, id)) {
      if (Object.keys(s.players).length >= 200) throw Error('CLASSROOM_FULL');
      s.players[id] = { balance: 1000, items: {} };
    }
    return s.players[id];
  }
  function credit(p, amount) {
    if (!Number.isSafeInteger(amount) || !Number.isSafeInteger(p.balance + amount)) throw Error('INVALID_AMOUNT');
    if (p.balance + amount < 0) throw Error('INSUFFICIENT_GOLD');
    p.balance += amount;
  }
  async function change(fn) {
    for (let attempt = 0; attempt < 8; attempt++) {
      const row = await read(), state = clone(row.value);
      const result = fn(state);
      if (JSON.stringify(state) === JSON.stringify(row.value)) return result;
      state.revision = (state.revision || 0) + 1;
      if (Buffer.byteLength(JSON.stringify(state), 'utf8') > 4000000) throw Error('LEDGER_FULL');
      try { await storage.writeLedger(state, row.writeLock); return result; }
      catch (e) {
        if (Number(e.response?.status || e.status) !== 409) throw e;
      }
    }
    throw Error('BUSY: retry the same request');
  }
  return { read, change, player, credit };
}
if (typeof module !== 'undefined') module.exports = { createCloudStore };
