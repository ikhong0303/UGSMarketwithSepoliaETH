// Server-owned wallet bindings. One wallet per UGS player, one player per wallet.
const ITEM = 'MYTHIC_SWORD_NFT';
function createNft(api, context, crypto) {
  const player = context.playerId;
  if (!player || !context.projectId) throw Error('LOGIN_REQUIRED');
  const address = x => { if (!/^0x[0-9a-fA-F]{40}$/.test(x || '') || /^0x0{40}$/.test(x)) throw Error('INVALID_WALLET'); return x.toLowerCase(); };
  async function config() {
    const c = (await api.read('nft_config'))?.value;
    if (!c || !/^https:\/\//.test(c.rpcUrl || '')) throw Error('NFT_CONFIG_REQUIRED');
    return { ...c, contractAddress: address(c.contractAddress) };
  }
  async function change(fn) {
    for (let i = 0; i < 5; i++) {
      const row = await api.read('nft_state');
      if (!row?.writeLock || row.value.version !== 1) throw Error('NFT_STATE_REQUIRED');
      const state = row.value;
      if (!state.bindings || !state.wallets || !state.challenges) throw Error('NFT_STATE_INVALID');
      for (const [key, value] of Object.entries(state.challenges)) if (value.expires < Date.now()) delete state.challenges[key];
      const result = fn(state);
      // Classroom-sized ledger; do not silently exceed a Cloud Save value limit.
      if (JSON.stringify(state).length > 80000) throw Error('NFT_STATE_CAPACITY');
      try { await api.write('nft_state', state, row.writeLock); return result; }
      catch (e) { if (Number(e.response?.status || e.status) !== 409) throw e; }
    }
    throw Error('NFT_BUSY_RETRY');
  }
  return {
    async Nft_GetChallenge(params) {
      const wallet = address(params.wallet_address), c = await config();
      return change(s => {
        if (s.bindings[player]) {
          if (s.bindings[player] !== wallet) throw Error('PLAYER_ALREADY_LINKED');
          return { status: 'LINKED', wallet };
        }
        if (s.wallets[wallet] && s.wallets[wallet] !== player) throw Error('WALLET_ALREADY_LINKED');
        s.sequence = (s.sequence || 0) + 1;
        const expires = Date.now() + 300000;
        // A persisted, never-reused sequence is the replay nonce; no client-provided message.
        const message = ['Mythic Sword NFT - Link wallet', 'No payment or token approval.',
          'Project: ' + context.projectId, 'Environment: ' + context.environmentId,
          'Player: ' + player, 'Wallet: ' + wallet, 'Chain: 11155111',
          'Contract: ' + c.contractAddress, 'Nonce: ' + s.sequence, 'Expires: ' + expires].join('\n');
        s.challenges[player] = { wallet, message, expires, contract: c.contractAddress };
        return { status: 'SIGN_REQUIRED', wallet, message };
      });
    },
    async Nft_BindWallet(params) {
      const c = await config();
      return change(s => {
        const challenge = s.challenges[player];
        if (!challenge || challenge.expires < Date.now() || challenge.contract !== c.contractAddress) throw Error('CHALLENGE_EXPIRED');
        let recovered;
        try { recovered = crypto.verifyMessage(challenge.message, params.signature).toLowerCase(); }
        catch (_) { throw Error('INVALID_SIGNATURE'); }
        if (recovered !== challenge.wallet) throw Error('WRONG_SIGNER');
        if (s.bindings[player] && s.bindings[player] !== recovered) throw Error('PLAYER_ALREADY_LINKED');
        if (s.wallets[recovered] && s.wallets[recovered] !== player) throw Error('WALLET_ALREADY_LINKED');
        s.bindings[player] = recovered; s.wallets[recovered] = player;
        delete s.challenges[player];
        return { status: 'LINKED', wallet: recovered };
      });
    },
    async Nft_SyncInventory() {
      const started = Date.now(), c = await config();
      const lease = await change(s => {
        if (!s.bindings[player]) return null;
        s.leases ||= {};
        if (s.leases[player]?.until > Date.now()) throw Error('NFT_BUSY_RETRY');
        s.sequence = (s.sequence || 0) + 1;
        s.leases[player] = { id: s.sequence, until: Date.now() + 60000 };
        return { id: s.sequence, wallet: s.bindings[player] };
      });
      if (!lease) return { status: 'NOT_LINKED', count: 0 };
      try {
        await api.requireDefinition(player, ITEM);
        const rpc = (method, params) => api.rpc(c.rpcUrl, method, params);
        if (BigInt(await rpc('eth_chainId', [])) !== 11155111n) throw Error('WRONG_CHAIN');
        // Same recent block for all ownership reads. Three confirmations, including mined block.
        const head = BigInt(await rpc('eth_blockNumber', []));
        if (head < 2n) throw Error('CHAIN_NOT_READY');
        const block = '0x' + (head - 2n).toString(16);
        const snapshot = await rpc('eth_getBlockByNumber', [block, false]);
        if (!snapshot?.hash) throw Error('INVALID_BLOCK');
        const call = async data => {
          const result = await rpc('eth_call', [{ to: c.contractAddress, data }, block]);
          if (!/^0x[0-9a-fA-F]{64}$/.test(result || '')) throw Error('INVALID_NFT_RESPONSE');
          return result;
        };
        const word = n => BigInt(n).toString(16).padStart(64, '0');
        const count = Number(BigInt(await call('0x70a08231' + lease.wallet.slice(2).padStart(64, '0'))));
        // Fail closed, never remove items after a truncated ownership scan.
        if (!Number.isSafeInteger(count) || count > 20) throw Error('NFT_HOLDING_LIMIT_20');
        const tokens = await Promise.all(Array.from({ length: count }, async (_, i) => {
          const token = BigInt(await call('0x2f745c59' + lease.wallet.slice(2).padStart(64, '0') + word(i))).toString();
          if ('0x' + (await call('0x6352211e' + word(token))).slice(-40).toLowerCase() !== lease.wallet) throw Error('OWNER_MISMATCH');
          return token;
        }));
        if (new Set(tokens).size !== count) throw Error('DUPLICATE_CHAIN_TOKEN');
        const items = await api.inventory(player);
        if ((await rpc('eth_getBlockByNumber', [block, false]))?.hash !== snapshot.hash) throw Error('CHAIN_CHANGED_RETRY');
        const desired = new Map(tokens.map(token => [crypto.id('11155111:' + c.contractAddress + ':' + token).slice(2, 34), token]));
        const present = new Set();
        const remove = [];
        for (const item of items.filter(x => x.inventoryItemId === ITEM)) {
          const token = desired.get(item.playersInventoryItemId);
          const d = item.instanceData || {};
          if (token && d.source === 'MYTHIC_NFT' && d.contract === c.contractAddress && d.tokenId === token && d.wallet === lease.wallet) present.add(item.playersInventoryItemId);
          else remove.push(item);
        }
        let added = 0, removed = 0;
        // Short operations make retries resumable. Stable IDs prevent duplicate grants.
        for (const item of remove) {
          if (Date.now() - started > 9500) return { status: 'PARTIAL', count, added, removed };
          await api.removeItem(player, item); removed++;
        }
        for (const [instance, tokenId] of desired) {
          if (present.has(instance)) continue;
          if (Date.now() - started > 9500) return { status: 'PARTIAL', count, added, removed };
          await api.addItem(player, ITEM, instance, { source: 'MYTHIC_NFT', chainId: 11155111, contract: c.contractAddress, tokenId, wallet: lease.wallet }); added++;
        }
        return { status: 'SYNCED', count, added, removed, wallet: lease.wallet };
      } finally {
        // If release fails/times out, the 60-second lease expires after the 15-second script lifetime.
        await change(s => { if (s.leases?.[player]?.id === lease.id) delete s.leases[player]; });
      }
    }
  };
}
module.exports = { createNft };
