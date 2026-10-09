using NotificationService.Application.DTOs;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using Xunit;

namespace NotificationService.Tests;

public class DeviceServiceTests
{
    private readonly InMemoryRepository<DeviceToken> _repo = new();
    private readonly DeviceService _sut;

    public DeviceServiceTests()
    {
        _sut = new DeviceService(_repo);
    }

    private static RegisterDeviceRequest Register(string userId, string token, Dictionary<string, string>? attributes = null) => new()
    {
        UserId = userId,
        Token = token,
        DeviceType = DeviceType.Android,
        Attributes = attributes
    };

    [Fact]
    public async Task Register_SameTokenTwice_UpdatesInsteadOfDuplicating()
    {
        await _sut.RegisterAsync("app", Register("user-1", "tok", new() { ["lang"] = "pt" }));
        await _sut.RegisterAsync("app", Register("user-2", "tok", new() { ["lang"] = "en" }));

        var device = Assert.Single(_repo.Items);
        Assert.Equal("user-2", device.UserId);
        Assert.Equal("en", device.Attributes["lang"]);
    }

    [Fact]
    public async Task Register_SameTokenInDifferentApps_KeepsSeparateRecords()
    {
        await _sut.RegisterAsync("app-a", Register("u", "tok"));
        await _sut.RegisterAsync("app-b", Register("u", "tok"));

        Assert.Equal(2, _repo.Items.Count);
    }

    [Fact]
    public async Task Unregister_DeactivatesDevice_AndRegisterReactivates()
    {
        await _sut.RegisterAsync("app", Register("u", "tok"));

        Assert.True(await _sut.UnregisterAsync("app", "tok"));
        Assert.False(_repo.Items[0].IsActive);
        Assert.False(await _sut.UnregisterAsync("app", "tok"));

        await _sut.RegisterAsync("app", Register("u", "tok"));
        Assert.True(_repo.Items[0].IsActive);
    }

    [Fact]
    public async Task List_FiltersByAppActiveAndAllAttributes()
    {
        await _sut.RegisterAsync("app", Register("a", "t1", new() { ["region"] = "sul", ["plan"] = "pro" }));
        await _sut.RegisterAsync("app", Register("b", "t2", new() { ["region"] = "sul", ["plan"] = "free" }));
        await _sut.RegisterAsync("app", Register("c", "t3", new() { ["region"] = "sul", ["plan"] = "pro" }));
        await _sut.RegisterAsync("other", Register("d", "t4", new() { ["region"] = "sul", ["plan"] = "pro" }));
        await _sut.UnregisterAsync("app", "t3");

        var result = await _sut.ListAsync("app", new DeviceQuery
        {
            Attributes = new() { ["region"] = "sul", ["plan"] = "pro" }
        });

        var only = Assert.Single(result.Items);
        Assert.Equal("a", only.UserId);
        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task List_Paginates_AndClampsPageSize()
    {
        for (var i = 0; i < 5; i++)
        {
            await _sut.RegisterAsync("app", Register($"u{i}", $"t{i}"));
        }

        var page2 = await _sut.ListAsync("app", new DeviceQuery { Page = 2, PageSize = 2 });
        var huge = await _sut.ListAsync("app", new DeviceQuery { PageSize = 10_000 });

        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(5, page2.Total);
        Assert.Equal(DeviceService.MaxPageSize, huge.PageSize);
    }
}
