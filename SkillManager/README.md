# Angri450.Nong.Skill.Manager (DEPRECATED)

This tool is **deprecated**. Use `nong skill` instead:

```bash
dotnet tool install --global Angri450.Nong.Cli
nong skill validate <dir> --json
nong skill scan <dir> --json
nong skill inventory <dir> --json
nong skill package <dir> --json
```

## For Local Debugging Only

The project remains buildable for internal compatibility but is no longer published or promoted:

```bash
dotnet build SkillManager/SkillManager.Cli.csproj -c Release
```

## Source

https://github.com/angri450/Nong.NET

## License

Apache-2.0
