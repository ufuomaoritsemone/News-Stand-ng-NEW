using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using Xunit;

namespace NewsApi.Tests;

public class StatesEndpointTests
{
    private static NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public async Task GetStates_ResolvesCountsAcrossCanonicalAndLegacySlugs_IncludingFct()
    {
        using var db = CreateInMemoryDb(Guid.NewGuid().ToString());

        var lagosTopic = new Topic { Id = "t_lagos", Slug = "lagos-state", Name = "Lagos State", Type = TopicType.Place };
        var fctTopic = new Topic { Id = "t_fct", Slug = "abuja-fct", Name = "Abuja FCT", Type = TopicType.Place };
        db.Topics.AddRange(lagosTopic, fctTopic);

        var a1 = new Article { Id = "a1", Title = "Lagos Blue Line Rail", Slug = "lagos-rail-1", Category = "Metro", PublishedAt = DateTime.UtcNow };
        var a2 = new Article { Id = "a2", Title = "FCT Minister Commissions Road in Abuja", Slug = "fct-road-2", Category = "Politics", PublishedAt = DateTime.UtcNow };
        db.Articles.AddRange(a1, a2);

        db.ArticleTopics.AddRange(
            new ArticleTopic { ArticleId = a1.Id, TopicId = lagosTopic.Id, Confidence = 0.95f },
            new ArticleTopic { ArticleId = a2.Id, TopicId = fctTopic.Id, Confidence = 1.0f }
        );
        await db.SaveChangesAsync();

        var controller = new StatesController(db, NullLogger<StatesController>.Instance, new SlugService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var actionResult = await controller.GetStates();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var groups = Assert.IsType<List<StateZoneGroupDto>>(okResult.Value);

        Assert.Equal(6, groups.Count);

        var allStates = groups.SelectMany(g => g.States).ToList();
        Assert.Equal(37, allStates.Count);

        var lagos = Assert.Single(allStates, s => s.Slug == "lagos");
        Assert.Equal(1, lagos.ArticleCount);

        var fct = Assert.Single(allStates, s => s.Slug == "fct");
        Assert.Equal(1, fct.ArticleCount);
    }

    [Fact]
    public async Task GetStateBySlug_CombinesEmptySeededTopicWithTitleMatches_AndSupportsPaginationAndCategoryFilter()
    {
        using var db = CreateInMemoryDb(Guid.NewGuid().ToString());

        // Seeded topic exists for Lagos, but has 0 ArticleTopics rows ("empty topic trap" regression check)
        db.Topics.Add(new Topic
        {
            Id = "topic_lagos",
            Slug = "lagos-state",
            Name = "Lagos State",
            Type = TopicType.Place
        });

        db.Articles.AddRange(
            new Article
            {
                Id = "art_lagos_1",
                Title = "Lagos State Launches New Ferry Terminal in Ikeja",
                Summary = "Commuters in Lagos welcome the new transport hub.",
                Slug = "lagos-ferry-1",
                Category = "Metro",
                PublishedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new Article
            {
                Id = "art_lagos_2",
                Title = "Tech Startups Expand in Ikeja Hub",
                Summary = "Innovation grows across Lagos metropolitan corridor.",
                Slug = "ikeja-tech-2",
                Category = "Technology",
                PublishedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        );
        await db.SaveChangesAsync();

        var controller = new StatesController(db, NullLogger<StatesController>.Instance, new SlugService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // 1. Unfiltered query should return both articles even though ArticleTopics is empty
        var resultAll = await controller.GetStateBySlug("lagos", page: 1, pageSize: 18);
        var okAll = Assert.IsType<OkObjectResult>(resultAll.Result);
        var detailAll = Assert.IsType<StateDetailDto>(okAll.Value);

        Assert.Equal("Lagos", detailAll.Name);
        Assert.Equal(2, detailAll.ArticleCount);
        Assert.Equal(2, detailAll.Articles.Count);
        Assert.Contains("Metro", detailAll.AvailableCategories);
        Assert.Contains("Technology", detailAll.AvailableCategories);

        // 2. Category filter should return only matching category while keeping AvailableCategories intact
        var resultTech = await controller.GetStateBySlug("lagos", page: 1, pageSize: 18, category: "Technology");
        var okTech = Assert.IsType<OkObjectResult>(resultTech.Result);
        var detailTech = Assert.IsType<StateDetailDto>(okTech.Value);

        Assert.Equal(1, detailTech.ArticleCount);
        Assert.Single(detailTech.Articles);
        Assert.Equal("Technology", detailTech.Articles[0].Category);
        Assert.Equal("Technology", detailTech.ActiveCategory);
    }
}
