const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
function fixture(get){
 const sandbox={require(name){if(name.includes('cloud-save'))return {DataApi:class{}}; if(name.includes('economy'))return {ConfigurationApi:class{async getPlayerConfiguration(){return {data:{metadata:{configAssignmentHash:'hash'}}}}},CurrenciesApi:class{},InventoryApi:class{getPlayerInventory(a){return get(a)}}};return {};}};
 vm.runInNewContext(fs.readFileSync(require('node:path').join(__dirname,'../src/adapter.js'),'utf8')+'\nthis.adapter=createAdapter({projectId:"p",serviceToken:"s"});',sandbox);
 return sandbox.adapter;
}
test('inventory validation fallback scans pages without the rejected ID filter',async()=>{
 const calls=[];const api=fixture(async a=>{calls.push(a);if(a.playersInventoryItemIds)throw {response:{status:400}};if(!a.after)return {data:{results:[{playersInventoryItemId:'first'}],links:{next:'next'}}};return {data:{results:[{playersInventoryItemId:'long-id'}],links:{}}};});
 assert.equal((await api.getItem('player','long-id')).playersInventoryItemId,'long-id');assert.equal(calls.length,3);assert.equal(calls[2].after,'first');assert.equal(calls[2].playersInventoryItemIds,undefined);
});
test('inventory authorization errors are not treated as absent items',async()=>{
 let calls=0;const api=fixture(async()=>{calls++;throw {response:{status:403}}});await assert.rejects(api.getItem('player','id'));assert.equal(calls,1);
});
test('failed fallback scan cannot report item missing',async()=>{
 const api=fixture(async a=>{throw {response:{status:a.playersInventoryItemIds?400:503}}});await assert.rejects(api.getItem('player','id'));
});
