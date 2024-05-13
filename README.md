# About SFB.Web.API

[![Build Status](https://agilefactory.visualstudio.com/Financial%20Benchmarking/_apis/build/status/Service/SFB.Web.Api)](https://agilefactory.visualstudio.com/Financial%20Benchmarking/_build?definitionId=453)
[![GitHub release (latest by date)](https://agilefactory.vsrm.visualstudio.com/_apis/public/Release/badge/fc33e3f0-e73b-466d-837a-10cad68c664e/7/22)](https://agilefactory.visualstudio.com/Financial%20Benchmarking/_release?definitionId=7)

SFB Web API is the first initial implementation in providing the Department of Education's products a micro-service oriented architectural approach.

This service has decoupled existing functionality, upgraded the framework infrastructure using .Net Core in addition to decoupled reusable functionality into Nuget packages and referenced them through this Web API service.

The API has been integrated into the existing SFB infrastructure and is being consumed through a number of UI implementations.

Additional consolidation and functional specification is in progress.

## Local development

Install the prerequisites:

1. .NET 6
1. [Docker](https://docs.docker.com/get-docker/)

You will also need to authenticate with Azure DevOps in order to resolve packages from the private package feed.

Right-click on project in Visual Studio and select Manage User Secrets or in Rider, click Tools > Manage .NET Secrets. Populate the following in the `secrets.json` file:

```json
{
  "Secrets:endpoint": "https://cm-t1dv-sfb.documents.azure.com:443/",
  "Secrets:authkey": "•••",
  "Secrets:database": "sfb-dev",
  "Secrets:emCollection": "20210318000000-EM-2021-2022",
  "Secrets:sadCollection": "SADBandingTest",
  "Secrets:redisConnectionString": "127.0.0.1:6379",
  "Secrets:sadSizeLookupCollection": "SizelookupTest",
  "Secrets:sadFSMLookupCollection": "FSMlookupTest",
  "Secrets:cosmosConnectionMode": "Gateway"
}
```

To start the local Redis container run:

```bash
 ​​docker-compose up
```
