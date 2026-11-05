@description('azd environment name, used in resource names.')
param environmentName string

@description('Azure region for everything.')
param location string

@description('Tags for every resource.')
param tags object

@description('Entra object ID to give read/write access to the Time Clock table, for looking at or fixing data. Empty to skip.')
param principalId string

@description('Skill ID each endpoint accepts. An endpoint rejects every request until its ID is set.')
param timeClockSkillId string
param majorSystemSkillId string

@description('Memory per instance. 512 MB is the smallest and plenty for these skills.')
@allowed([512, 2048, 4096])
param instanceMemoryMB int = 512

@description('Most instances the app may scale out to. One is plenty for these skills, and a low cap limits what a flood of junk requests can cost.')
@minValue(1)
param maximumInstanceCount int = 1

@description('Instances kept running so no request waits for a cold start. Each one is billed all month, used or not.')
@minValue(0)
param alwaysReadyInstances int

var resourceToken = uniqueString(subscription().id, resourceGroup().id, location)
var deploymentContainerName = 'app-package'
var timeClockTableName = 'TimeClockUserData'

// Built-in roles: https://learn.microsoft.com/azure/role-based-access-control/built-in-roles
var storageBlobDataOwner = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageTableDataContributor = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${environmentName}-${resourceToken}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    // Caps logging at 100 MB a day, so a burst of errors or junk traffic can't run up a bill.
    workspaceCapping: {
      dailyQuotaGb: json('0.1')
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${environmentName}-${resourceToken}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'st${resourceToken}'
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    // The app signs in with its managed identity and you with your Entra account, so account keys stay off.
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }

  resource blobService 'blobServices' = {
    name: 'default'

    resource deploymentContainer 'containers' = {
      name: deploymentContainerName
    }
  }

  resource tableService 'tableServices' = {
    name: 'default'

    resource timeClockTable 'tables' = {
      name: timeClockTableName
    }
  }
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${environmentName}-${resourceToken}'
  location: location
  tags: tags
}

// The Functions host keeps its code package and state in blobs...
resource appBlobAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, identity.id, storageBlobDataOwner)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwner)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ...and Time Clock only needs its one table.
resource appTableAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage::tableService::timeClockTable.id, identity.id, storageTableDataContributor)
  scope: storage::tableService::timeClockTable
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributor)
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource ownerTableAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(principalId)) {
  name: guid(storage::tableService::timeClockTable.id, principalId, storageTableDataContributor)
  scope: storage::tableService::timeClockTable
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributor)
    principalId: principalId
    principalType: 'User'
  }
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: 'plan-${environmentName}-${resourceToken}'
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    tier: 'FlexConsumption'
    name: 'FC1'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: 'func-${environmentName}-${resourceToken}'
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${deploymentContainerName}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: identity.id
          }
        }
      }
      scaleAndConcurrency: {
        alwaysReady: alwaysReadyInstances > 0 ? [{ name: 'http', instanceCount: alwaysReadyInstances }] : []
        instanceMemoryMB: instanceMemoryMB
        maximumInstanceCount: maximumInstanceCount
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
  }

  resource appSettings 'config' = {
    name: 'appsettings'
    properties: {
      AzureWebJobsStorage__accountName: storage.name
      AzureWebJobsStorage__credential: 'managedidentity'
      AzureWebJobsStorage__clientId: identity.properties.clientId
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.properties.ConnectionString
      // Tells DefaultAzureCredential which managed identity to use for the Time Clock table.
      AZURE_CLIENT_ID: identity.properties.clientId
      TimeClock__TableEndpoint: storage.properties.primaryEndpoints.table
      Alexa__SkillIds__TimeClock: timeClockSkillId
      Alexa__SkillIds__MajorSystem: majorSystemSkillId
    }
    dependsOn: [
      appBlobAccess
      appTableAccess
    ]
  }
}

// azd deploys with your Entra sign-in, so turn off the publishing username and password Azure creates by default.
resource scmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: functionApp
  name: 'scm'
  properties: {
    allow: false
  }
}

resource ftpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: functionApp
  name: 'ftp'
  properties: {
    allow: false
  }
}

output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output timeClockTableEndpoint string = storage.properties.primaryEndpoints.table
