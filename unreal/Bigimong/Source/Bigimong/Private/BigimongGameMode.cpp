#include "BigimongGameMode.h"
#include "BigimongHomePawn.h"
#include "Engine/DirectionalLight.h"
#include "Components/DirectionalLightComponent.h"
#include "Engine/World.h"

ABigimongGameMode::ABigimongGameMode()
{
    DefaultPawnClass = ABigimongHomePawn::StaticClass();
}

void ABigimongGameMode::BeginPlay()
{
    Super::BeginPlay();

    // The preview starts in an empty engine map so it needs its own light.
    FActorSpawnParameters Parameters;
    ADirectionalLight* Light = GetWorld()->SpawnActor<ADirectionalLight>(
        ADirectionalLight::StaticClass(), FVector::ZeroVector, FRotator(-45.f, 25.f, 0.f), Parameters);
    if (Light && Light->GetLightComponent())
    {
        Light->GetLightComponent()->SetIntensity(5.f);
    }
}
