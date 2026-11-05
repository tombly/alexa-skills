# Alexa Skills
Some fun skills for Amazon's Alexa: a skill for learning the Major System, and a simple time tracking skill.

## Backend

One Azure Functions app (C#, .NET 10, Flex Consumption plan) serves both skills:

| Skill | Endpoint | Data |
|---|---|---|
| Time Clock | `POST /api/timeclock` | Azure Table storage, table `TimeClockUserData` |
| Major System | `POST /api/majorsystem` | none |

- `src/AlexaSkills.Functions`: the app. `Alexa/` checks that each request really came from Alexa and handles the request/response JSON; `Skills/` has one folder per skill.
- `tests/AlexaSkills.Tests`: run with `dotnet test`.
- `infra/`: Bicep for the resource group, storage account, Function App, managed identity and Application Insights, deployed with `azd`.
- `models/`: each skill's interaction model, pasted into the Alexa developer console's JSON Editor (**Build → Interaction Model → JSON Editor**). The tests check each one against its skill's code.
- `icons/`: each skill's store icons, 108 and 512 pixels square, uploaded in the developer console under **Distribution → Skill Preview**.

### Run locally

```sh
cp src/AlexaSkills.Functions/local.settings.example.json src/AlexaSkills.Functions/local.settings.json
cd src/AlexaSkills.Functions && func start
```

The example settings turn off the request signature check (`Alexa__VerifyRequests=false`) so you can POST sample requests with curl. The app ignores that setting outside local development. Time Clock needs [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) for its table; without it, Time Clock still lists tasks but apologizes for anything that reads or saves time.

### Deploy

Get each skill's ID (`amzn1.ask.skill.…`) from the Alexa developer console. An endpoint rejects every request until its skill ID is set.

```sh
azd auth login
azd env new alexa-skills
azd env set TIMECLOCK_SKILL_ID amzn1.ask.skill.…
azd env set MAJORSYSTEM_SKILL_ID amzn1.ask.skill.…
azd up
```

Then for each skill in the Alexa developer console, go to **Build → Endpoint**, choose **HTTPS**, and enter `$(azd env get-value SKILL_ENDPOINT_BASE_URL)/timeclock` (or `/majorsystem`). For the SSL certificate type, choose *My development endpoint is a sub-domain of a domain that has a wildcard certificate from a certificate authority*.
