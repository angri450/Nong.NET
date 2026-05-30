namespace SkillManager.Cli.Models;

public class EvalItem
{
    public int Id { get; set; }
    public string Prompt { get; set; } = "";
    public string? ExpectedOutput { get; set; }
    public List<string> Files { get; set; } = new();
    public List<string> Assertions { get; set; } = new();
    public List<string> Expectations { get; set; } = new();
}

public class EvalSet
{
    public string? SkillName { get; set; }
    public List<EvalItem> Evals { get; set; } = new();
}
