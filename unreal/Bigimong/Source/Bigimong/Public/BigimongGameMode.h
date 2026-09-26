#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "BigimongGameMode.generated.h"

UCLASS()
class BIGIMONG_API ABigimongGameMode : public AGameModeBase
{
    GENERATED_BODY()

public:
    ABigimongGameMode();

protected:
    virtual void BeginPlay() override;
};
