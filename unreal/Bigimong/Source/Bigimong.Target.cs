using UnrealBuildTool;
using System.Collections.Generic;

public class BigimongTarget : TargetRules
{
    public BigimongTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Game;
        DefaultBuildSettings = BuildSettingsVersion.V5;
        ExtraModuleNames.Add("Bigimong");
    }
}
