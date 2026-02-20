param deploymentName string
param tenantId string = tenant().tenantId
param location string = resourceGroup().location

// pass secrets from github actions / repo secrets
@secure()
param googleOAuthClientId string
@secure()
param googleOAuthClientSecret string
@secure()
param hmacServerKey string
@secure()
param sqlServerPassword string

var webAppName string = '${deploymentName}-webApp'
var appServicePlanName string = '${deploymentName}-servicePlan'
var storageAccountName string = '${deploymentName}storage'
var sqlServerName string = '${deploymentName}-sqlserver'
var keyVaultName string = '${deploymentName}-keyvault'

module identity 'modules/identity.bicep' = {
  name: 'identity_deployment'
  params: {
    location: location
  }
}

module compute 'modules/compute.bicep' = {
  name: 'compute_deployment'
  params: {
    webAppName: webAppName
    appServicePlanName: appServicePlanName
    webAppUamiId: identity.outputs.webAppUamiId
    location: location
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage_deployment'
  params: {
    storageAccountName: storageAccountName
    location: location
  }
}

module database 'modules/database.bicep' = {
  name: 'database_deployment'
  params: {
    sqlServerName: sqlServerName
    sqlServerPassword: sqlServerPassword
    location: location
  }
}

module security 'modules/security.bicep' = {
  name: 'security_deployment'
  params: {
    googleOAuthClientId: googleOAuthClientId
    googleOAuthClientSecret: googleOAuthClientSecret
    hmacServerKey: hmacServerKey
    keyVaultName: keyVaultName
    location: location
    tenantId: tenantId
  }
}

module rbac 'modules/rbac.bicep' = {
  name: 'rbac_deployment'
  params: {
    keyVaultName: keyVaultName
    storageAccountName: storageAccountName
    webAppUamiPrincipalId: identity.outputs.webAppUamiPrincipalId
  }
}
