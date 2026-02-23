param webAppName string
param appServicePlanName string
param location string
param webAppUamiId string
param webAppUamiClientId string
param oauthClientIdUri string
param oauthClientSecretUri string
param hmacUri string
param sqlServerName string
param storageAccountName string

resource appServicePlan 'Microsoft.Web/serverfarms@2025-03-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
    capacity: 1
  }
}

resource webApp 'Microsoft.Web/sites@2025-03-01' = {
  name: webAppName
  location: location
  kind: 'app'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${webAppUamiId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'AzureDev'
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: webAppUamiClientId
        }
        {
          name: 'OAuth__ClientId'
          value: '@Microsoft.KeyVault(SecretUri=${oauthClientIdUri})'
        }
        {
          name: 'OAuth__ClientSecret'
          value: '@Microsoft.KeyVault(SecretUri=${oauthClientSecretUri})'
        }
        {
          name: 'Hmac'
          value: '@Microsoft.KeyVault(SecretUri=${hmacUri})'
        }
        {
          name: 'ConnectionStrings__Default'
          value: 'Server=tcp:${sqlServerName}.${environment().suffixes.sqlServerHostname},1433;Initial Catalog=lightsaver-db;Authentication=Active Directory Managed Identity;User Id=${webAppUamiClientId}'
        }
        {
          name: 'AzureStorage__AccountName'
          value: storageAccountName
        }
        {
          name: 'AzureStorage__ContainerName'
          value: 'user-resources'
        }
      ]
    }
  }
}

resource siteConfig 'Microsoft.Web/sites/config@2025-03-01' = {
  parent: webApp
  name: 'web'
  properties: {
    keyVaultReferenceIdentity: webAppUamiId
    ftpsState: 'FtpsOnly'
    minTlsVersion: '1.2'
    scmMinTlsVersion: '1.2'
  }
}

resource LightSaver_disableFTP 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2025-03-01' = {
  parent: webApp
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource LightSaver_disableSCM 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2025-03-01' = {
  parent: webApp
  name: 'scm'
  properties: {
    allow: false
  }
}

output webAppName string = webApp.name
