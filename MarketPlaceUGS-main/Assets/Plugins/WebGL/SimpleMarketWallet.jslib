mergeInto(LibraryManager.library, {
  SimpleMarketWalletRequest: function (targetPtr, jsonPtr) {
    var target = UTF8ToString(targetPtr);
    var request = JSON.parse(UTF8ToString(jsonPtr));
    var reply = function (result) {
      SendMessage(target, 'OnWalletResult', JSON.stringify(Object.assign({ id: request.id }, result)));
    };
    if (!window.simpleMarketWallet) {
      window.simpleMarketWallet = { provider: null, target: target, sending: false, discovered: [] };
      window.addEventListener('eip6963:announceProvider', function (event) {
        if (event.detail && event.detail.info && event.detail.info.rdns === 'io.metamask') {
          window.simpleMarketWallet.discovered.push(event.detail.provider);
        }
      });
    }
    var state = window.simpleMarketWallet;
    state.target = target;
    var key = 'simple-market-payment:' + request.storageKey;
    var save = function (value) { localStorage.setItem(key, JSON.stringify(value)); };
    var read = function () { return JSON.parse(localStorage.getItem(key) || 'null'); };
    var addressPattern = /^0x[0-9a-fA-F]{40}$/;
    var hashPattern = /^0x[0-9a-fA-F]{64}$/;
    (async function () {
      // Recovery reads work even when MetaMask is disconnected or on another network.
      if (request.action === 'pending') {
        var pending = read();
        return reply({ txHash: pending && pending.txHash || '', status: pending ? 'PENDING' : 'EMPTY' });
      }
      if (request.action === 'remember') {
        if (!hashPattern.test(request.txHash)) throw new Error('올바른 거래 해시를 입력하세요.');
        if (state.sending) throw new Error('MetaMask 결제 요청이 아직 열려 있습니다.');
        save({ txHash: request.txHash, startedAt: Date.now() });
        return reply({ txHash: request.txHash });
      }
      if (request.action === 'clear') {
        var existing = read();
        if (existing && existing.txHash === request.txHash) localStorage.removeItem(key);
        return reply({ status: 'EMPTY' });
      }
      if (request.action === 'forget') {
        if (state.sending) throw new Error('MetaMask 요청이 아직 열려 있습니다. 먼저 승인 또는 거절을 완료하세요.');
        localStorage.removeItem(key); // Explicit user action only; does not cancel/refund a blockchain transaction.
        return reply({ status: 'EMPTY' });
      }
      if (!state.provider) {
        window.dispatchEvent(new Event('eip6963:requestProvider'));
        await new Promise(function (resolve) { setTimeout(resolve, 200); });
        state.provider = state.discovered[0];
        if (!state.provider && window.ethereum) {
          var providers = window.ethereum.providers || [window.ethereum];
          state.provider = providers.find(function (p) { return p.isMetaMask && !p.isBraveWallet; });
        }
        if (!state.provider) throw new Error('PC 크롬에 MetaMask 확장 프로그램을 설치하고 다시 실행하세요.');
        var notify = function () { SendMessage(state.target, 'OnWalletResult', JSON.stringify({ id: 'event' })); };
        state.provider.on('accountsChanged', notify);
        state.provider.on('chainChanged', notify);
        state.provider.on('disconnect', notify);
      }
      var provider = state.provider;
      if (request.action === 'connect') {
        await provider.request({ method: 'eth_requestAccounts' });
        var network = await provider.request({ method: 'eth_chainId' });
        if (network.toLowerCase() !== '0xaa36a7') {
          await provider.request({ method: 'wallet_switchEthereumChain', params: [{ chainId: '0xaa36a7' }] });
        }
      }
      var accounts = await provider.request({ method: 'eth_accounts' });
      var chain = await provider.request({ method: 'eth_chainId' });
      var address = accounts[0] || '';
      if (request.action === 'connect' || request.action === 'status') {
        var eth = '';
        if (address && chain.toLowerCase() === '0xaa36a7') {
          var wei = BigInt(await provider.request({ method: 'eth_getBalance', params: [address, 'latest'] }));
          eth = (wei / 1000000000000000000n).toString() + '.' + (wei % 1000000000000000000n).toString().padStart(18, '0').slice(0, 6);
        }
        return reply({ address: address, chainId: chain, balanceEth: eth });
      }
      if (request.action !== 'send') throw new Error('알 수 없는 지갑 요청입니다.');
      if (state.sending || read()) throw new Error('이전 결제가 있습니다. 결제 확인을 먼저 눌러주세요.');
      if (!address || address.toLowerCase() !== (request.from || '').toLowerCase()) throw new Error('지갑 계정이 바뀌었습니다. 다시 연결하세요.');
      if (chain.toLowerCase() !== '0xaa36a7') throw new Error('Sepolia로 전환한 뒤 다시 연결하세요.');
      if (!addressPattern.test(request.to) || /^0x0{40}$/i.test(request.to) || address.toLowerCase() === request.to.toLowerCase()) throw new Error('수신 지갑과 다른 구매자 지갑을 사용하세요.');
      if (request.value !== '0x5af3107a4000' || !/^0x[0-9a-f]+$/i.test(request.data)) throw new Error('결제 정보가 올바르지 않습니다.');
      // Save BEFORE asking for approval. If the browser closes or RPC is ambiguous, do not auto-resend.
      save({ txHash: '', startedAt: Date.now() });
      state.sending = true;
      try {
        var txHash = await provider.request({ method: 'eth_sendTransaction', params: [{ from: address, to: request.to, value: request.value, data: request.data, chainId: '0xaa36a7' }] });
        if (!hashPattern.test(txHash)) throw new Error('거래 해시를 받지 못했습니다. MetaMask 활동에서 확인하세요.');
        save({ txHash: txHash, startedAt: Date.now() });
        reply({ txHash: txHash });
      } catch (e) {
        if (e.code === 4001) localStorage.removeItem(key); // Explicit user rejection: no transaction submitted.
        throw e;
      } finally { state.sending = false; }
    })().catch(function (error) {
      var message = error.code === 4001 ? 'MetaMask 요청을 취소했습니다.' : error.code === 4902 ? 'MetaMask에서 테스트 네트워크 표시를 켜고 Sepolia를 선택하세요.' : error.code === -32002 ? 'MetaMask에 이미 열린 승인 요청을 확인하세요.' : error.message || '지갑 요청 실패';
      reply({ error: message });
    });
  }
});
