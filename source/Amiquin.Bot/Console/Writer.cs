using System.Text.Json;
using Amiquin.Core.Attributes;
using Amiquin.Core.Utilities;
using Spectre.Console;
using Spectre.Console.Json;

namespace Amiquin.Bot.Console;

public static class Writer
{
    public static void WriteList(string header, IEnumerable<string> data)
    {
        var list = data.ToList();

        var listPanel = new Panel(string.Join("\n", list))
            .Header($"[mediumspringgreen]{header}[/]")
            .Collapse()
            .RoundedBorder()
            .BorderColor(Color.Orange1);

        AnsiConsole.Write(listPanel);
    }
    public static void WriteJsonData<T>(string header, T input)
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };

        var tempJson = JsonSerializer.Serialize(input, options);
        var objectCopy = JsonSerializer.Deserialize<T>(tempJson);

        var objectProps = typeof(T).GetProperties();
        foreach (var prop in objectProps)
        {
            var anomifyAttrib = prop.GetCustomAttributes(typeof(AnomifyAttribute), false);
            if (anomifyAttrib is null)
                continue;

            var propValue = prop.GetValue(objectCopy);
            if (propValue is null)
                continue;

            if (!Reflection.IsExtendedPrimitiveType(prop))
                return;

            var anomifiedValue = StringModifier.Anomify(propValue?.ToString() ?? "", 20);
            prop.SetValue(objectCopy, Reflection.ConvertTo(prop, anomifiedValue));
        }

        var finalJson = JsonSerializer.Serialize(objectCopy, options);
        var jsonPanel = new JsonText(finalJson);

        AnsiConsole.Write(
            new Panel(jsonPanel)
                .Header($"[mediumspringgreen]{header}[/]")
                .Collapse()
                .RoundedBorder()
                .BorderColor(Color.Orange1));
    }

    public static void WriteDictionaryData<K, V>(string header, Dictionary<K, V> data) where K : notnull
    {
        var dataGrid = new Grid()
            .AddColumn(new GridColumn().NoWrap())
            .AddColumn(new GridColumn().NoWrap());

        foreach (var record in data)
        {
            dataGrid.AddRow(
            [
                new Markup($"[mediumspringgreen]{record.Key}[/]"),
                new Markup(record.Value?.ToString() ?? "#null")
            ]);
        }

        var combinedData = new Panel(dataGrid)
            .RoundedBorder()
            .Expand()
            .BorderColor(Color.Orange1)
            .Header($"[mediumspringgreen]{header}[/]", Justify.Center);

        AnsiConsole.Write(combinedData);
    }

    public static void WriteLogo()
    {
        var iconCanvas = new CanvasImage(Path.Join(AppContext.BaseDirectory, "Assets", "logo.png"));
        iconCanvas.MaxWidth(40);

        var iconPanel = new Panel(iconCanvas)
            .Expand()
            .NoBorder();

        AnsiConsole.Write(iconPanel);
    }

}