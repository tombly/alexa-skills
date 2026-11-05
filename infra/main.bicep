targetScope = 'subscription'

@minLength(1)
@maxLength(30)
@description('azd environment name, e.g. alexa-skills. Used in the resource group and resource names.')
param environmentName string

@minLength(1)
@description('Azure region for everything. It must offer the Flex Consumption plan.')
param location string

@description('Your Entra object ID (azd fills this in). Gets read/write access to the Time Clock table, for looking at or fixing data.')
param principalId string = ''

@description('Time Clock skill ID (amzn1.ask.skill.…) from the Alexa developer console.')
param timeClockSkillId string = ''

@description('Major System skill ID (amzn1.ask.skill.…) from the Alexa developer console.')
param majorSystemSkillId string = ''

@description('Instances kept running so no request waits for a cold start. 0 means pay only for use; 1 costs a few dollars a month at 512 MB.')
@minValue(0)
param alwaysReadyInstances int = 0

var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  name: 'resources'
  scope: rg
  params: {
    environmentName: environmentName
    location: location
    tags: tags
    principalId: principalId
    timeClockSkillId: timeClockSkillId
    majorSystemSkillId: majorSystemSkillId
    alwaysReadyInstances: alwaysReadyInstances
  }
}

output AZURE_LOCATION string = location
output AZURE_RESOURCE_GROUP string = rg.name
output AZURE_FUNCTION_APP_NAME string = resources.outputs.functionAppName
output SKILL_ENDPOINT_BASE_URL string = '${resources.outputs.functionAppUrl}/api'
output TIMECLOCK_TABLE_ENDPOINT string = resources.outputs.timeClockTableEndpoint
