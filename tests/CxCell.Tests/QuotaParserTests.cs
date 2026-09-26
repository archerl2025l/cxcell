using System.Text.Json;
using Xunit;

namespace CxCell.Tests;

public sealed class QuotaParserTests
{
    [Fact]
    public void ParsesPlusFiveHourAndWeeklyWindows()
    {
        using var document = JsonDocument.Parse("""
        {
          "rateLimits": {
            "planType": "plus",
            "primary": {
              "usedPercent": 27,
              "windowDurationMins": 300,
              "resetsAt": 1790416800
            },
            "secondary": {
              "usedPercent": 41,
              "windowDurationMins": 10080,
              "resetsAt": 1790822400
            }
          }
        }
        """);

        var result = QuotaParser.Parse(document.RootElement);

        Assert.Equal("plus", result.PlanType);
        Assert.Equal(73, result.FiveHour!.RemainingPercent);
        Assert.Equal(59, result.Weekly!.RemainingPercent);
    }

    [Fact]
    public void OmitsFiveHourWhenProResponseOnlyContainsWeeklyWindow()
    {
        using var document = JsonDocument.Parse("""
        {
          "rateLimits": {
            "planType": "pro",
            "primary": {
              "usedPercent": 12,
              "windowDurationMins": 10080,
              "resetsAt": 1790822400
            },
            "secondary": null
          }
        }
        """);

        var result = QuotaParser.Parse(document.RootElement);

        Assert.Equal("pro", result.PlanType);
        Assert.Null(result.FiveHour);
        Assert.Equal(88, result.Weekly!.RemainingPercent);
    }

    [Fact]
    public void PrefersCodexBucketFromMultiBucketResponse()
    {
        using var document = JsonDocument.Parse("""
        {
          "rateLimits": {
            "planType": "plus",
            "primary": null,
            "secondary": null
          },
          "rateLimitsByLimitId": {
            "codex": {
              "planType": "plus",
              "primary": {
                "usedPercent": 5,
                "windowDurationMins": 300,
                "resetsAt": 1790416800
              },
              "secondary": {
                "usedPercent": 25,
                "windowDurationMins": 10080,
                "resetsAt": 1790822400
              }
            }
          }
        }
        """);

        var result = QuotaParser.Parse(document.RootElement);

        Assert.Equal(95, result.FiveHour!.RemainingPercent);
        Assert.Equal(75, result.Weekly!.RemainingPercent);
    }
}
