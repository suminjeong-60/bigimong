using UnrealBuildTool;
using System.Collections.Generic;

public class BigimongEditorTarget : TargetRules
{
    public BigimongEditorTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Editor;
        DefaultBuildSettings = BuildSettingsVersion.V5;
        ExtraModuleNames.Add("Bigimong");
    }
}
