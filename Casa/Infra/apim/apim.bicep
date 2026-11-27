@description('Azure region for API Management')
param location string = resourceGroup().location

@description('APIM service instance name')
param apimName string = 'apim-casa-example'

@description('Publisher email and display name')
param publisherEmail string
param publisherName string = 'Casa Domotica'

@description('Allowed explicit browser CORS origins (never wildcard in production)')
param corsOrigins array = [
  'http://localhost:8089'
  'http://127.0.0.1:8089'
]

@description('Backend public HTTPS URLs')
param identityBackendUrl string = 'https://identity-api.example.com'
param homeBackendUrl string = 'https://home-api.example.com'
param securityBackendUrl string = 'https://security-api.example.com'
param notificationsBackendUrl string = 'https://notifications-api.example.com'
param assistantBackendUrl string = 'https://assistant-api.example.com'

resource apim 'Microsoft.ApiManagement/service@2023-05-01-preview' = {
  name: apimName
  location: location
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

var corsXmlOrigins = join(map(corsOrigins, origin => '<origin>${origin}</origin>'), '')

var globalPolicyXml = '''
<policies>
  <inbound>
    <choose>
      <when condition="@(context.Request.Url.Path.Contains("/internal/"))">
        <return-response>
          <set-status code="404" reason="Not Found" />
        </return-response>
      </when>
    </choose>
    <cors allow-credentials="false">
      <allowed-origins>__CORS_ORIGINS__</allowed-origins>
      <allowed-methods>
        <method>GET</method>
        <method>POST</method>
        <method>PUT</method>
        <method>DELETE</method>
        <method>OPTIONS</method>
      </allowed-methods>
      <allowed-headers>
        <header>Authorization</header>
        <header>Content-Type</header>
        <header>Idempotency-Key</header>
        <header>X-Correlation-ID</header>
      </allowed-headers>
    </cors>
    <rate-limit-by-key calls="120" renewal-period="60" counter-key="@(context.Request.IpAddress)" />
    <set-header name="X-Correlation-ID" exists-action="skip">
      <value>@(context.RequestId.ToString())</value>
    </set-header>
    <base />
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <set-header name="X-Correlation-ID" exists-action="override">
      <value>@(context.Request.Headers.GetValueOrDefault("X-Correlation-ID", context.RequestId.ToString()))</value>
    </set-header>
    <set-header name="Server" exists-action="delete" />
    <set-header name="X-Powered-By" exists-action="delete" />
    <set-header name="X-AspNet-Version" exists-action="delete" />
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
'''

resource globalPolicy 'Microsoft.ApiManagement/service/policies@2023-05-01-preview' = {
  parent: apim
  name: 'policy'
  properties: {
    format: 'rawxml'
    value: replace(globalPolicyXml, '__CORS_ORIGINS__', corsXmlOrigins)
  }
}

var services = [
  { name: 'identity-api', displayName: 'Casa Domotica Identity', serviceUrl: identityBackendUrl, path: 'api/v1/identity' }
  { name: 'home-api', displayName: 'Casa Domotica Home', serviceUrl: homeBackendUrl, path: 'home' }
  { name: 'security-api', displayName: 'Casa Domotica Security', serviceUrl: securityBackendUrl, path: 'api/v1/security' }
  { name: 'notifications-api', displayName: 'Casa Domotica Notifications', serviceUrl: notificationsBackendUrl, path: 'api/v1/notifications' }
  { name: 'assistant-api', displayName: 'Casa Domotica Assistant', serviceUrl: assistantBackendUrl, path: 'api/v1/assistant' }
]

resource apis 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = [for svc in services: {
  parent: apim
  name: svc.name
  properties: {
    displayName: svc.displayName
    path: svc.path
    serviceUrl: svc.serviceUrl
    protocols: ['https']
    subscriptionRequired: false
  }
}]

output gatewayUrl string = apim.properties.gatewayUrl
output principalId string = apim.identity.principalId
