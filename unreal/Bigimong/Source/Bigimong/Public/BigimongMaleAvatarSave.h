#pragma once

#include "CoreMinimal.h"
#include "GameFramework/SaveGame.h"
#include "BigimongMaleAvatarSave.generated.h"

// Separate slot from Unity's profile: saves only Unreal's male customization.
UCLASS()
class BIGIMONG_API UBigimongMaleAvatarSave : public USaveGame
{
    GENERATED_BODY()

public:
    UPROPERTY() int32 SchemaVersion = 1;
    UPROPERTY() int32 EyeShape = 4;
    UPROPERTY() int32 FaceShape = 1;
    UPROPERTY() int32 HairStyle = 1;
    UPROPERTY() int32 SkinTone = 1;
    UPROPERTY() int32 EyeColor = 2;
    UPROPERTY() int32 HairColor = 3;
};
