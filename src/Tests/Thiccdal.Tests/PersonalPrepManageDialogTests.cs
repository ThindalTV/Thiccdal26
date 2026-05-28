using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Modules.Control.Components.PreLive;

namespace Thiccdal.Tests;

public sealed class PersonalPrepManageDialogTests
{
    [Fact]
    public async Task WhenAddingEditingAndTogglingItems_ThenChangesPersistAndChecklistReloads()
    {
        using PersonalPrepDialogTestHarness harness = new(
        [
            new CustomChecklistItemDefinition
            {
                Id = 1,
                Label = "Drink water",
                DisplayOrder = 1,
                IsEnabled = true
            }
        ]);

        IRenderedComponent<PersonalPrepManageDialog> cut = harness.Render();
        await cut.InvokeAsync(() => cut.Instance.Open());

        IElement addInput = cut.Find("input[placeholder='Drink water']");
        addInput.Input("Close email");
        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Add item").Click();

        cut.FindAll("input.personal-prep-dialog__input")[0].Input("Hydrate");
        cut.FindAll("input.personal-prep-dialog__input")[0].Blur();

        cut.Find("input[type='checkbox']").Change(false);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["Hydrate", "Close email"], harness.ManagementService.Items.Select(item => item.Label).ToArray());
            Assert.False(harness.ManagementService.Items[0].IsEnabled);
            Assert.Equal(3, harness.ChecklistService.ReloadCount);
        });
    }

    [Fact]
    public async Task WhenReorderingItems_ThenNeighborDisplayOrderSwapsAndChecklistReloads()
    {
        using PersonalPrepDialogTestHarness harness = new(
        [
            new CustomChecklistItemDefinition
            {
                Id = 1,
                Label = "First",
                DisplayOrder = 1,
                IsEnabled = true
            },
            new CustomChecklistItemDefinition
            {
                Id = 2,
                Label = "Second",
                DisplayOrder = 2,
                IsEnabled = true
            }
        ]);

        IRenderedComponent<PersonalPrepManageDialog> cut = harness.Render();
        await cut.InvokeAsync(() => cut.Instance.Open());

        cut.FindAll("button")
            .Single(button => button.TextContent.Trim() == "↓" && !button.HasAttribute("disabled"))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(["Second", "First"], harness.ManagementService.Items.Select(item => item.Label).ToArray());
            Assert.Equal([1, 2], harness.ManagementService.Items.Select(item => item.DisplayOrder).ToArray());
            Assert.Equal(1, harness.ChecklistService.ReloadCount);
        });
    }

    [Fact]
    public async Task WhenDeletingItem_ThenConfirmationAppearsAndItemIsRemovedAfterConfirm()
    {
        using PersonalPrepDialogTestHarness harness = new(
        [
            new CustomChecklistItemDefinition
            {
                Id = 1,
                Label = "Drink water",
                DisplayOrder = 1,
                IsEnabled = true
            }
        ]);

        IRenderedComponent<PersonalPrepManageDialog> cut = harness.Render();
        await cut.InvokeAsync(() => cut.Instance.Open());

        cut.FindAll("button").Single(button => button.TextContent.Trim() == "Delete").Click();
        cut.WaitForElement(".confirm-dialog");
        cut.Find(".confirm-dialog__button--danger").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(harness.ManagementService.Items);
            Assert.Equal(1, harness.ChecklistService.ReloadCount);
        });
    }

    private sealed class PersonalPrepDialogTestHarness : IDisposable
    {
        private readonly TestContext _context = new();

        public PersonalPrepDialogTestHarness(IReadOnlyList<CustomChecklistItemDefinition> items)
        {
            ManagementService = new FakeCustomChecklistItemManagementService(items);
            ChecklistService = new FakePreLiveChecklistService();
            _context.Services.AddSingleton<ICustomChecklistItemManagementService>(ManagementService);
            _context.Services.AddSingleton<IPreLiveChecklistService>(ChecklistService);
        }

        public FakeCustomChecklistItemManagementService ManagementService { get; }

        public FakePreLiveChecklistService ChecklistService { get; }

        public IRenderedComponent<PersonalPrepManageDialog> Render()
        {
            return _context.RenderComponent<PersonalPrepManageDialog>();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }

    private sealed class FakeCustomChecklistItemManagementService : ICustomChecklistItemManagementService
    {
        private int _nextId;

        public FakeCustomChecklistItemManagementService(IReadOnlyList<CustomChecklistItemDefinition> items)
        {
            Items = [.. items.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id)];
            _nextId = Items.Count == 0 ? 1 : Items.Max(item => item.Id) + 1;
        }

        public List<CustomChecklistItemDefinition> Items { get; private set; }

        public Task<IReadOnlyList<CustomChecklistItemDefinition>> List(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult<IReadOnlyList<CustomChecklistItemDefinition>>([.. Items]);
        }

        public Task<CustomChecklistItemDefinition> Create(string label, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            CustomChecklistItemDefinition item = new()
            {
                Id = _nextId++,
                Label = label.Trim(),
                DisplayOrder = Items.Count + 1,
                IsEnabled = true
            };

            Items =
            [
                .. Items,
                item
            ];

            return Task.FromResult(item);
        }

        public Task<CustomChecklistItemDefinition?> Update(CustomChecklistItemDefinition item, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            int index = Items.FindIndex(existingItem => existingItem.Id == item.Id);
            if (index < 0)
            {
                return Task.FromResult<CustomChecklistItemDefinition?>(null);
            }

            Items[index] = item with { Label = item.Label.Trim() };
            return Task.FromResult<CustomChecklistItemDefinition?>(Items[index]);
        }

        public Task<bool> Delete(int id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            int index = Items.FindIndex(item => item.Id == id);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            Items.RemoveAt(index);
            Resequence();
            return Task.FromResult(true);
        }

        public Task<bool> MoveUp(int id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(Move(id, moveEarlier: true));
        }

        public Task<bool> MoveDown(int id, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            return Task.FromResult(Move(id, moveEarlier: false));
        }

        private bool Move(int id, bool moveEarlier)
        {
            int currentIndex = Items.FindIndex(item => item.Id == id);
            if (currentIndex < 0)
            {
                return false;
            }

            int neighborIndex = moveEarlier ? currentIndex - 1 : currentIndex + 1;
            if (neighborIndex < 0 || neighborIndex >= Items.Count)
            {
                return false;
            }

            (Items[currentIndex], Items[neighborIndex]) = (Items[neighborIndex], Items[currentIndex]);
            Resequence();
            return true;
        }

        private void Resequence()
        {
            for (int index = 0; index < Items.Count; index++)
            {
                Items[index] = Items[index] with { DisplayOrder = index + 1 };
            }
        }
    }

    private sealed class FakePreLiveChecklistService : IPreLiveChecklistService
    {
        public event EventHandler? StateChanged;

        public bool AllRequiredChecked => false;

        public int RequiredUncheckedCount => 0;

        public int OptionalUncheckedCount => 0;

        public int ReloadCount { get; private set; }

        public PreLiveChecklistState GetState()
        {
            return new();
        }

        public void SetItemChecked(string itemId, bool isChecked)
        {
            _ = itemId;
            _ = isChecked;
        }

        public Task TriggerAction(string itemId, CancellationToken cancellationToken = default)
        {
            _ = itemId;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task Reload(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            ReloadCount++;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public void Reset()
        {
        }

        public void HandleGoLiveSucceeded(DateTimeOffset? startedAt = null, Guid? sessionId = null)
        {
            _ = startedAt;
            _ = sessionId;
        }
    }
}
