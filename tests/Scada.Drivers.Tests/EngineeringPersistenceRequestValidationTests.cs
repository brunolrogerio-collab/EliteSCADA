using Microsoft.AspNetCore.Http;
using Scada.Api.Persistence;

namespace Scada.Drivers.Tests;

public sealed class EngineeringPersistenceRequestValidationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("501")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void Validate_RejectsInvalidRevisionListLimit(string limit)
    {
        var context = RevisionListContext($"?limit={limit}");

        var error = EngineeringPersistenceRequestValidationFilter.Validate(context);

        Assert.Equal("Revision list limit must be between 1 and 500.", error);
    }

    [Fact]
    public void Validate_RejectsMultipleRevisionListLimits()
    {
        var context = RevisionListContext("?limit=10&limit=20");

        var error = EngineeringPersistenceRequestValidationFilter.Validate(context);

        Assert.Equal("Revision list limit must be between 1 and 500.", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("500")]
    public void Validate_AllowsSupportedRevisionListLimit(string? limit)
    {
        var context = RevisionListContext(limit is null ? string.Empty : $"?limit={limit}");

        var error = EngineeringPersistenceRequestValidationFilter.Validate(context);

        Assert.Null(error);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Validate_RejectsNonPositiveRevision(long revision)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = $"/api/engineering/persistence/demo/revisions/{revision}/preview";
        context.Request.RouteValues["revision"] = revision;

        var error = EngineeringPersistenceRequestValidationFilter.Validate(context);

        Assert.Equal("Revision must be greater than zero.", error);
    }

    [Fact]
    public void Validate_AllowsPositiveRevision()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/engineering/persistence/demo/revisions/1/preview";
        context.Request.RouteValues["revision"] = 1L;

        Assert.Null(EngineeringPersistenceRequestValidationFilter.Validate(context));
    }

    private static DefaultHttpContext RevisionListContext(string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/engineering/persistence/demo/revisions";
        context.Request.QueryString = new QueryString(queryString);
        return context;
    }
}
