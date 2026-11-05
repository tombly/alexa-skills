using AlexaSkills.Alexa;
using AlexaSkills.Skills.MajorSystem;
using AlexaSkills.Skills.TimeClock;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace AlexaSkills.Endpoints;

// One HTTPS endpoint per skill, each in its own class so a skill that can't start (say, Time Clock
// with no table configured) doesn't take the others down. They're anonymous because Alexa can't
// send a function key; SkillEndpoint checks Alexa's request signature instead.

public sealed class TimeClockFunction(SkillEndpoint endpoint, TimeClockSkill skill)
{
    [Function("TimeClock")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "timeclock")] HttpRequest request,
        CancellationToken cancellationToken) => endpoint.HandleAsync(request, skill, cancellationToken);
}

public sealed class MajorSystemFunction(SkillEndpoint endpoint, MajorSystemSkill skill)
{
    [Function("MajorSystem")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "majorsystem")] HttpRequest request,
        CancellationToken cancellationToken) => endpoint.HandleAsync(request, skill, cancellationToken);
}
