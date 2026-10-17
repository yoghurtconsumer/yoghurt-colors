using System.IO;
using Chromatic.App;
using Chromatic.App.ViewModels;
using Chromatic.Core.Abstractions;
using Chromatic.Core.Models;
using Chromatic.Core.State;
using Chromatic.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chromatic.Ui.Tests;

public sealed class AutomationWorkflowTests
{
    [Fact] public async Task RuleAndHotkeyDraftsPersistExplicitlyWithoutEnablingEffectsOrSavingHeldColors()
    {
        var root = Path.Combine(Path.GetTempPath(), "Chromatic.AutomationUi", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var client = new Client(); using var services = CompositionRoot.Build(new(), new(Path.Combine(root, "settings.json")), client, root);
            var library = services.GetRequiredService<PresetsViewModel>(); await library.LoadAsync();
            var automation = services.GetRequiredService<AutomationViewModel>(); await automation.LoadAsync();
            var display = services.GetRequiredService<DisplayViewModel>(); display.Saturation = 220;
            var games = services.GetRequiredService<GamesViewModel>(); await games.LoadAsync();
            games.EditName = "My game"; games.ExecutablePath = @"C:\Games\mygame.exe"; games.SelectedPreset = library.All[0]; await games.SaveAsync();
            games.StageRuleCommand.Execute(null); Assert.False(Assert.Single(automation.Rules).Enabled); Assert.Empty(client.Commands);
            automation.Rules[0].Enabled = true; automation.AutomaticProfiles = true; automation.HoldEnabled = true;
            automation.HotkeysEnabled = true; automation.HotkeyPreset = library.All[0]; automation.AddHotkeyCommand.Execute(null);
            automation.CaptureDesktopCommand.Execute(null); await automation.SaveAsync();
            var command = Assert.IsType<ConfigureAutomation>(Assert.Single(client.Commands));
            Assert.True(command.Settings.Rules[0].Enabled); Assert.True(command.Settings.Boost.Enabled);
            Assert.Equal(220, command.Settings.Desktop.GlobalColor.SaturationPercent); Assert.False(client.State.Enabled); Assert.False(client.State.Held);
            var disk = (await new AutomationRepository(Path.Combine(root, "automation.json")).LoadAsync()).Value;
            Assert.Equal(220, disk.Desktop.GlobalColor.SaturationPercent); Assert.Single(disk.Hotkeys);
            Assert.DoesNotContain("held", await File.ReadAllTextAsync(Path.Combine(root, "automation.json")), StringComparison.OrdinalIgnoreCase);
            automation.Rules[0].Enabled = false; automation.HoldEnabled = false; await automation.SaveAsync();
            Assert.False(((ConfigureAutomation)client.Commands[^1]).Settings.Rules[0].Enabled);
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact] public async Task InvalidDraftDoesNotReplaceCommittedConfiguration()
    {
        var root = Path.Combine(Path.GetTempPath(), "Chromatic.AutomationUi", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var client = new Client(); using var services = CompositionRoot.Build(new(), new(Path.Combine(root, "settings.json")), client, root);
            var vm = services.GetRequiredService<AutomationViewModel>(); await vm.LoadAsync(); await vm.SaveAsync(); client.Commands.Clear();
            vm.HoldChord = "Win+F6"; await Assert.ThrowsAsync<ArgumentException>(vm.SaveAsync);
            Assert.Empty(client.Commands); Assert.False((await new AutomationRepository(Path.Combine(root, "automation.json")).LoadAsync()).Value.NeedsListener);
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class Client : IEffectsClient
    {
        public EffectsState State { get; private set; } = new(); public List<EffectsCommand> Commands { get; } = [];
        public Task<EffectsAcknowledgment> SendAsync(CommandEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Commands.Add(envelope.Command); var reduced = EffectsReducer.Reduce(State, envelope); State = reduced.State;
            return Task.FromResult(new EffectsAcknowledgment(reduced.Result, State, new(true, "Fake", null, "No hardware", State.Revision)));
        }
    }
}
