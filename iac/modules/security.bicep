@description('Key Vault name (must be globally unique)')
param keyVaultName string

@description('Deployment location')
param location string

@description('AAD tenant id (defaults to current tenant)')
param tenantId string

resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      ipRules: []
      virtualNetworkRules: []
    }
    softDeleteRetentionInDays: 90
  }
}

@description('Provide values at deploy time')
@secure()
param googleOAuthClientId string
@secure()
param googleOAuthClientSecret string
@secure()
param hmacServerKey string

resource googleClientId 'Microsoft.KeyVault/vaults/secrets@2025-05-01' = {
  parent: keyVault
  name: 'GoogleOAuthClientID'
  properties: {
    value: googleOAuthClientId
  }
}

resource googleClientSecret 'Microsoft.KeyVault/vaults/secrets@2025-05-01' = {
  parent: keyVault
  name: 'GoogleOAuthClientSecret'
  properties: {
    value: googleOAuthClientSecret
  }
}

resource hmacKey 'Microsoft.KeyVault/vaults/secrets@2025-05-01' = {
  parent: keyVault
  name: 'HmacServerKey'
  properties: {
    value: hmacServerKey
  }
}

output keyVaultName string = keyVault.name
