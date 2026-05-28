using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Remotes.Models;
using Thiccdal.Modules.Control.Components.Settings;

namespace Thiccdal.Tests;

public sealed class ViewerIdentitiesPanelTests
{
    [Fact]
    public void WhenSearchingAndSelectingTwoRows_ThenMergeCallsIdentityService()
    {
        using TestContext context = new();
        FakeUserIdentityService service = new();
        context.Services.AddSingleton<IUserIdentityService>(service);

        IRenderedComponent<ViewerIdentitiesPanel> cut = context.RenderComponent<ViewerIdentitiesPanel>();

        cut.Find("input[placeholder='alice']").Input("alice");
        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Search", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Twitch", cut.Markup);
            Assert.Contains("YouTube", cut.Markup);
        });

        cut.FindAll("input[type='checkbox']")[0].Change(true);
        cut.WaitForAssertion(() => Assert.Contains("1 selected", cut.Markup));
        cut.FindAll("input[type='checkbox']")[1].Change(true);
        cut.Find("input[placeholder^='Optional']").Input("Alice");

        cut.FindAll("button")
            .Single(button => button.TextContent.Contains("Merge identities", StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(service.LastMergeRequest);
            Assert.Equal(new long[] { 1, 2 }, service.LastMergeRequest!.Value.PlatformUserIds);
            Assert.Equal("Alice", service.LastMergeRequest.Value.CanonicalName);
            Assert.Contains("Merged 2 viewer row(s) into Alice.", cut.Markup);
        });
    }

    private sealed class FakeUserIdentityService : IUserIdentityService
    {
        private List<UserIdentitySearchResult> _results =
        [
            new UserIdentitySearchResult(1, PlatformEventSource.Twitch, "alice-twitch", "AliceTV", DateTime.UtcNow, null, null),
            new UserIdentitySearchResult(2, PlatformEventSource.YouTube, "alice-youtube", "Alice_YT", DateTime.UtcNow, null, null)
        ];

        public (long[] PlatformUserIds, string? CanonicalName)? LastMergeRequest { get; private set; }

        public Task<IReadOnlyList<UserIdentitySearchResult>> Search(string query, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            IReadOnlyList<UserIdentitySearchResult> filteredResults = _results
                .Where(result => result.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return Task.FromResult(filteredResults);
        }

        public Task<UserIdentityMergeResult> Merge(
            UserIdentityMergeRequest request,
            CancellationToken cancellationToken = default)
        {
            return Merge(request.PlatformUserIds, request.CanonicalName, cancellationToken);
        }

        public Task<UserIdentityMergeResult> Merge(
            IReadOnlyList<long> platformUserIds,
            string? canonicalName,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;

            long[] mergedIds = platformUserIds.OrderBy(static id => id).ToArray();
            string resolvedCanonicalName = string.IsNullOrWhiteSpace(canonicalName) ? "Alice" : canonicalName.Trim();
            LastMergeRequest = (mergedIds, canonicalName);
            _results = _results
                .Select(
                    result => mergedIds.Contains(result.PlatformUserId)
                        ? result with { UserIdentityId = 7, UserIdentityDisplayName = resolvedCanonicalName }
                        : result)
                .ToList();

            return Task.FromResult(new UserIdentityMergeResult(7, resolvedCanonicalName, mergedIds));
        }

        public Task Unlink(long platformUserId, CancellationToken cancellationToken = default)
        {
            _ = platformUserId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }
    }
}
