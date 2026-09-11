using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Omnom.Api.Controllers;
using Omnom.Api.Data;

namespace Omnom.Api.Services;

public sealed class RequestBudget(AppDbContext db)
{
    public async Task<bool> Take(string key, int limit, int seconds)
    {
        var window = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / seconds;
        // One atomic counter shared by all instances. Only two rows are needed.
        return await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RequestBudgets" ("Key", "Window", "Count") VALUES ({key}, {window}, 1)
            ON CONFLICT ("Key") DO UPDATE SET
              "Window" = excluded."Window",
              "Count" = CASE WHEN "RequestBudgets"."Window" = excluded."Window"
                             THEN "RequestBudgets"."Count" + 1 ELSE 1 END
            WHERE "RequestBudgets"."Window" <> excluded."Window" OR "RequestBudgets"."Count" < {limit}
            """) == 1;
    }
}

public sealed class RequestBudgetFilter(RequestBudget budget) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var action = (ControllerActionDescriptor)context.ActionDescriptor;
        var login = action.ControllerTypeInfo.AsType() == typeof(AuthController) && action.ActionName == "Verify";
        var ai = action.ControllerTypeInfo.AsType() == typeof(AiController);
        if ((login || ai) && !await budget.Take(login ? "login" : "ai", login ? 10 : 30, login ? 60 : 3600))
        {
            context.HttpContext.Response.Headers.RetryAfter = login ? "60" : "3600";
            context.Result = new ObjectResult(new { message = login
                ? "Too many unlock attempts. Please wait a minute."
                : "Hourly meal parsing limit reached. Please try again later." }) { StatusCode = 429 };
            return;
        }
        await next();
    }
}
