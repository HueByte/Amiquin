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

        var componentsV2 = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("# Component Example")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("This button uses the new Components V2 system!")));
                container.AddComponent(new SectionBuilder()
                    .WithAccessory(button));
            })
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = componentsV2;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });
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

        var componentsV2 = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("# Select Menu Example")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("This select menu uses the new Components V2 system!")));
            })
            .WithActionRow([selectMenu])
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = componentsV2;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });
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

        // Create a new button for another interaction
        var newButton = new ButtonBuilder()
            .WithLabel("Click Again!")
            .WithStyle(ButtonStyle.Success)
            .WithCustomId(_componentHandlerService.GenerateCustomId("example_button", userId.ToString()));

        var componentsV2 = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("# Button Clicked!")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent($"Button was clicked by <@{component.User.Id}>")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent($"*Timestamp: <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>*")));
                container.AddComponent(new SectionBuilder()
                    .WithAccessory(newButton));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = componentsV2;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });

        _logger.LogInformation("Example button handled for user {UserId}", component.User.Id);
        return true;
    }

    private async Task<bool> HandleExampleSelectAsync(SocketMessageComponent component, ComponentContext context)
    {
        var userId = context.GetParameter<ulong>(0);
        var selectedValue = component.Data.Values.FirstOrDefault() ?? "none";

        var componentsV2 = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("# Selection Made!")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent($"<@{component.User.Id}> selected: **{selectedValue}**")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent($"*Timestamp: <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>*")));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = componentsV2;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });

        _logger.LogInformation("Example select handled for user {UserId}, selected: {Value}", component.User.Id, selectedValue);
        return true;
    }

    private async Task<bool> HandleExampleModalAsync(SocketModal modal, ModalContext context)
    {
        var userId = context.GetParameter<ulong>(0);
        var name = _modalService.GetComponentValue(modal, "name_input") ?? "Unknown";
        var feedback = _modalService.GetComponentValue(modal, "feedback_input") ?? "No feedback provided";

        var componentsV2 = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("# Modal Submitted!")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent($"Thank you for your submission, **{name}**!")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent($"**Feedback**\n{feedback}")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent($"*Timestamp: <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>*")));
            })
            .Build();

        await modal.RespondAsync(components: componentsV2, flags: MessageFlags.ComponentsV2, ephemeral: true);

        _logger.LogInformation("Example modal handled for user {UserId}, name: {Name}", modal.User.Id, name);
        return true;
    }
}