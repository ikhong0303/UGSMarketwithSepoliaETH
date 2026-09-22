const { createCloudAdapter } = require('../src/cloud-adapter');
exports.adapter = context => {
  const api = createCloudAdapter(context);
  return { ...api, requireDefinition: api.requireInventoryDefinition };
};
