resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'stazurite${uniqueString(resourceGroup().id)}'
  location: resourceGroup().location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

resource demoAppPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-demo-app-${uniqueString(resourceGroup().id)}'
  location: resourceGroup().location
  sku: {
    name: 'B1'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource demoApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-demo-app-${uniqueString(resourceGroup().id)}'
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: demoAppPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      appSettings: [
        {
          name: 'Storage__ServiceUri'
          value: storageAccount.properties.primaryEndpoints.blob
        }
      ]
    }
  }
}

resource storageAccountDataContributorDefinition 'Microsoft.Authorization/roleDefinitions@2022-05-01-preview' existing = {
  scope: subscription()
  name: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
}

resource appServiceRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, demoApp.id, storageAccountDataContributorDefinition.id)
  properties: {
    principalId: demoApp.identity.principalId
    roleDefinitionId: storageAccountDataContributorDefinition.id
    principalType: 'ServicePrincipal'
  }
}
