#include "BigimongHomePawn.h"
#include "Camera/CameraComponent.h"
#include "Components/InputComponent.h"
#include "Components/SceneComponent.h"
#include "Components/StaticMeshComponent.h"
#include "GameFramework/PlayerController.h"
#include "Engine/StaticMesh.h"
#include "InputCoreTypes.h"
#include "UObject/ConstructorHelpers.h"

ABigimongHomePawn::ABigimongHomePawn()
{
    PrimaryActorTick.bCanEverTick = true;
    AutoPossessPlayer = EAutoReceiveInput::Player0;
    RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
    PreviewPivot = CreateDefaultSubobject<USceneComponent>(TEXT("PreviewPivot"));
    PreviewPivot->SetupAttachment(RootComponent);

    PreviewCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("PreviewCamera"));
    PreviewCamera->SetupAttachment(RootComponent);
    PreviewCamera->SetRelativeLocation(FVector(-340.f, 0.f, 115.f));
    PreviewCamera->SetRelativeRotation(FRotator(-6.f, 0.f, 0.f));
    PreviewCamera->bAutoActivate = true;

    Egg = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Egg"));
    Egg->SetupAttachment(PreviewPivot);
    Male = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Male"));
    Male->SetupAttachment(PreviewPivot);
    Female = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Female"));
    Female->SetupAttachment(PreviewPivot);

    static ConstructorHelpers::FObjectFinder<UStaticMesh> Sphere(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
    if (Sphere.Succeeded())
    {
        Egg->SetStaticMesh(Sphere.Object);
        Male->SetStaticMesh(Sphere.Object);
        Female->SetStaticMesh(Sphere.Object);
    }
    Egg->SetRelativeLocation(FVector(0.f, 0.f, 33.f));
    Egg->SetRelativeScale3D(FVector(0.48f, 0.48f, 0.66f));
    Male->SetRelativeLocation(FVector(0.f, 0.f, 82.5f));
    Male->SetRelativeScale3D(FVector(0.4f, 0.4f, 1.65f));
    Female->SetRelativeLocation(FVector(0.f, 0.f, 81.f));
    Female->SetRelativeScale3D(FVector(0.4f, 0.4f, 1.62f));

    MaleAsset = TSoftObjectPtr<UStaticMesh>(FSoftObjectPath(TEXT("/Game/Varco/SM_VARCO_Male.SM_VARCO_Male")));
    FemaleAsset = TSoftObjectPtr<UStaticMesh>(FSoftObjectPath(TEXT("/Game/Varco/SM_VARCO_Female.SM_VARCO_Female")));
}

void ABigimongHomePawn::BeginPlay()
{
    Super::BeginPlay();
    ApplyImportedModel(Male, MaleAsset, 165.f);
    ApplyImportedModel(Female, FemaleAsset, 162.f);
    RefreshVisibility();
}

void ABigimongHomePawn::ApplyImportedModel(
    UStaticMeshComponent* Target, TSoftObjectPtr<UStaticMesh>& Asset, float HeightCm)
{
    UStaticMesh* Model = Asset.LoadSynchronous();
    if (!Model)
    {
        UE_LOG(LogTemp, Warning, TEXT("VARCO model is not imported; home viewer uses a placeholder"));
        return;
    }
    const FBoxSphereBounds Bounds = Model->GetBounds();
    if (Bounds.BoxExtent.Z <= KINDA_SMALL_NUMBER)
    {
        UE_LOG(LogTemp, Error, TEXT("VARCO model has no usable height"));
        return;
    }
    const float Scale = HeightCm / (2.f * Bounds.BoxExtent.Z);
    Target->SetStaticMesh(Model);
    Target->SetRelativeScale3D(FVector(Scale));
    Target->SetRelativeLocation(FVector(0.f, 0.f, (Bounds.BoxExtent.Z - Bounds.Origin.Z) * Scale));
}

void ABigimongHomePawn::RefreshVisibility()
{
    Egg->SetVisibility(bEggSelected);
    Male->SetVisibility(!bEggSelected && !bFemaleSelected);
    Female->SetVisibility(!bEggSelected && bFemaleSelected);
}

void ABigimongHomePawn::RotatePreview(float Pixels)
{
    if (FMath::Abs(Pixels) > KINDA_SMALL_NUMBER)
    {
        PreviewPivot->AddLocalRotation(FRotator(0.f, Pixels * 0.35f, 0.f));
    }
}

void ABigimongHomePawn::ToggleSubject()
{
    bEggSelected = !bEggSelected;
    RefreshVisibility();
}

void ABigimongHomePawn::SelectMale()
{
    bFemaleSelected = false;
    bEggSelected = false;
    RefreshVisibility();
}

void ABigimongHomePawn::SelectFemale()
{
    bFemaleSelected = true;
    bEggSelected = false;
    RefreshVisibility();
}

void ABigimongHomePawn::TouchPressed(ETouchIndex::Type FingerIndex, FVector ScreenLocation)
{
    if (FingerIndex != ETouchIndex::Touch1) return;
    bTouchActive = true;
    TouchStart = FVector2D(ScreenLocation.X, ScreenLocation.Y);
    LastTouch = TouchStart;
}

void ABigimongHomePawn::TouchReleased(ETouchIndex::Type FingerIndex, FVector ScreenLocation)
{
    if (FingerIndex != ETouchIndex::Touch1 || !bTouchActive) return;
    bTouchActive = false;
    const FVector2D Difference = FVector2D(ScreenLocation.X, ScreenLocation.Y) - TouchStart;
    if (FMath::Abs(Difference.Y) >= 80.f && FMath::Abs(Difference.Y) > FMath::Abs(Difference.X))
    {
        if (Difference.Y < 0.f) SelectFemale(); else SelectMale();
    }
    else if (Difference.SizeSquared() <= 225.f)
    {
        ToggleSubject();
    }
}

void ABigimongHomePawn::Tick(float DeltaSeconds)
{
    Super::Tick(DeltaSeconds);
    if (!bTouchActive) return;
    APlayerController* Controller = Cast<APlayerController>(GetController());
    if (!Controller) return;
    float X = 0.f, Y = 0.f;
    bool bPressed = false;
    Controller->GetInputTouchState(ETouchIndex::Touch1, X, Y, bPressed);
    if (bPressed)
    {
        const FVector2D Current(X, Y);
        RotatePreview(Current.X - LastTouch.X);
        LastTouch = Current;
    }
}

void ABigimongHomePawn::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
    Super::SetupPlayerInputComponent(PlayerInputComponent);
    PlayerInputComponent->BindAxis(TEXT("RotatePreview"), this, &ABigimongHomePawn::RotatePreview);
    PlayerInputComponent->BindKey(EKeys::Tab, IE_Pressed, this, &ABigimongHomePawn::ToggleSubject);
    PlayerInputComponent->BindKey(EKeys::LeftMouseButton, IE_Pressed, this, &ABigimongHomePawn::ToggleSubject);
    PlayerInputComponent->BindKey(EKeys::M, IE_Pressed, this, &ABigimongHomePawn::SelectMale);
    PlayerInputComponent->BindKey(EKeys::F, IE_Pressed, this, &ABigimongHomePawn::SelectFemale);
    PlayerInputComponent->BindTouch(IE_Pressed, this, &ABigimongHomePawn::TouchPressed);
    PlayerInputComponent->BindTouch(IE_Released, this, &ABigimongHomePawn::TouchReleased);
}
