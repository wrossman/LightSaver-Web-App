param location string

resource webAppUserManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: 'lightsaver-webapp-identity'
  location: location
}

output webAppUamiPrincipalId string = webAppUserManagedIdentity.properties.principalId
output webAppUamiId string = webAppUserManagedIdentity.id
