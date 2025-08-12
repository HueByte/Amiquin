using Amiquin.Core.Attributes;
using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.Modal;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Amiquin.Bot.Commands;

/// <summary>
/// Example commands demonstrating the new Components V2 system.
/// </summary>
[Group("component", "Example commands for the new component system")]
public class ComponentExampleCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly ILogger<ComponentExampleCommands> _logger;
    private readonly IComponentHandlerService _componentHandlerService;
    private readonly IModalService _modalService;

    public ComponentExampleCommands(ILogger<ComponentExampleCommands> logger, IComponentHandlerService componentHandlerService, IModalService modalService)
    {
        _logger = logger;
        _componentHandlerService = componentHandlerService;
        _modalService = modalService;

        // Register handlers for our example components
        _componentHandlerService.RegisterHandler("example_button", HandleExampleButtonAsync);
        _componentHandlerService.RegisterHandler("example_select", HandleExampleSelectAsync);

        // Register modal handler
        _modalService.RegisterHandler("example_modal", HandleExampleModalAsync);
    }

    [SlashCommand("button", "Creates a button using the new component system")]
    public async Task CreateExampleButtonAsync()
    {
        var button = new ButtonBuilder()
            .WithLabel("Click Me!")
            .WithStyle(ButtonStyle.Primary)
            .WithCustomId(_componentHandlerService.GenerateCustomId("example_button", Context.User.Id.ToString()));

        var component = new ComponentBuilder()
            .WithButton(button)
            .Build();

        var embed = new EmbedBuilder()
            .WithTitle("Component Example")
            .WithDescription("This button uses the new Components V2 system!")
            .WithColor(Color.Blue)
            .Build();

        await ModifyOriginalResponseAsync(msg => { msg.Embed = embed; msg.Components = component; });
    }

    [SlashCommand("select", "Creates a select menu using the new component system")]
    public async Task CreateExampleSelectAsync()
    {
        var selectMenu = new SelectMenuBuilder()
            .WithPlaceholder("Choose an option...")
            .WithCustomId(_componentHandlerService.GenerateCustomId("example_select", Context.User.Id.ToString()))
            .WithMinValues(1)
            .WithMaxValues(1)
            .AddOption("Option 1", "opt1", "First option")
            .AddOption("Option 2", "opt2", "Second option")
            .AddOption("Option 3", "opt3", "Third option");

        var component = new ComponentBuilder()
            .WithSelectMenu(selectMenu)
            .Build();

        var embed = new EmbedBuilder()
            .WithTitle("Select Menu Example")
            .WithDescription("This select menu uses the new Components V2 system!")
            .WithColor(Color.Green)
            .Build();

        await ModifyOriginalResponseAsync(msg => { msg.Embed = embed; msg.Components = component; });
    }

    [SlashCommand("modal", "Shows a modal using the new modal system")]
    [IsModal]
    public async Task ShowExampleModalAsync()
    {
        var modal = _modalService.CreateModal("example_modal", "Example Modal", Context.User.Id.ToString())
            .AddTextInput("Name", "name_input", TextInputStyle.Short, "Enter your name...", 1, 100)
            .AddTextInput("Feedback", "feedback_input", TextInputStyle.Paragraph, "Tell us what you think...", 10, 500)
            .Build();

        await RespondWithModalAsync(modal);
    }

    private async Task<bool> HandleExampleButtonAsync(SocketMessageComponent component, ComponentContext context)
    {
        var userId = context.GetParameter<ulong>(0);

        var embed = new EmbedBuilder()
            .WithTitle("Button Clicked!")
            .WithDescription($"Button was clicked by <@{component.User.Id}>")
            .WithColor(Color.Orange)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        // Create a new button for another interaction
        var newButton = new ButtonBuilder()
            .WithLabel("Click Again!")
            .WithStyle(ButtonStyle.Success)
            .WithCustomId(_componentHandlerService.GenerateCustomId("example_button", userId.ToString()));

        var newComponent = new ComponentBuilder()
            .WithButton(newButton)
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = newComponent;
        });

        _logger.LogInformation("Example button handled for user {UserId}", component.User.Id);
        return true;
    }

    private async Task<bool> HandleExampleSelectAsync(SocketMessageComponent component, ComponentContext context)
    {
        var userId = context.GetParameter<ulong>(0);
        var selectedValue = component.Data.Values.FirstOrDefault() ?? "none";

        var embed = new EmbedBuilder()
            .WithTitle("Selection Made!")
            .WithDescription($"<@{component.User.Id}> selected: **{selectedValue}**")
            .WithColor(Color.Purple)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = null; // Remove the select menu
        });

        _logger.LogInformation("Example select handled for user {UserId}, selected: {Value}", component.User.Id, selectedValue);
        return true;
    }

    private async Task<bool> HandleExampleModalAsync(SocketModal modal, ModalContext context)
    {
        var userId = context.GetParameter<ulong>(0);
        var name = _modalService.GetComponentValue(modal, "name_input") ?? "Unknown";
        var feedback = _modalService.GetComponentValue(modal, "feedback_input") ?? "No feedback provided";

        var embed = new EmbedBuilder()
            .WithTitle("Modal Submitted!")
            .WithDescription($"Thank you for your submission, **{name}**!")
            .AddField("Feedback", feedback)
            .WithColor(Color.Gold)
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();

        await modal.RespondAsync(embed: embed, ephemeral: true);

        _logger.LogInformation("Example modal handled for user {UserId}, name: {Name}", modal.User.Id, name);
        return true;
    }
}