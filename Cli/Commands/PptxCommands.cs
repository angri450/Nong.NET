using System.CommandLine;
using Nong.Cli.Common;

namespace Nong.Cli.Commands;

/// <summary>Pptx command group: read (not yet available).</summary>
public static class PptxCommands
{
    public static Command Create(Option<bool> jsonOpt)
    {
        var cmd = new Command("pptx", "PowerPoint operations");
        var c = new Command("read", "Extract slide text");
        CliHelpers.SetNotImplemented(c, "Extract slide text", jsonOpt);
        cmd.AddCommand(c);
        return cmd;
    }
}