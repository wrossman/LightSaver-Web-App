param keyVaultName string
param storageAccountName string
param webAppUamiPrincipalId string

// assign keyvault and storage access to the web app's identity 
resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' existing = { name: keyVaultName }
resource storageAccount 'Microsoft.Storage/storageAccounts@2025-06-01' existing = { name: storageAccountName }

// Built-in role: Key Vault Secrets User
var kvSecretsUserRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '4633458b-17de-408a-b874-0445c86b69e6'
)

// Built-in role: Storage Blob Data Contributor
var storageBlobDataContributorRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
)

resource webAppKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webAppUamiPrincipalId, kvSecretsUserRoleDefinitionId)
  scope: keyVault
  properties: {
    principalId: webAppUamiPrincipalId
    roleDefinitionId: kvSecretsUserRoleDefinitionId
    principalType: 'ServicePrincipal'
  }
}

resource storageBlobDataContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, webAppUamiPrincipalId, storageBlobDataContributorRoleDefinitionId)
  scope: storageAccount
  properties: {
    principalId: webAppUamiPrincipalId
    roleDefinitionId: storageBlobDataContributorRoleDefinitionId
    principalType: 'ServicePrincipal'
  }
}
