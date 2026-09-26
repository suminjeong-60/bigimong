#include "BigimongHomePawn.h"
#include "BigimongMaleAvatarSave.h"
#include "BigimongMaleEditorWidget.h"
#include "Blueprint/UserWidget.h"
#include "Camera/CameraComponent.h"
#include "Components/InputComponent.h"
#include "Components/MeshComponent.h"
#include "Components/SceneComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Kismet/GameplayStatics.h"
#include "GameFramework/PlayerController.h"
#include "Engine/StaticMesh.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "InputCoreTypes.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
    void TintSlot(UMeshComponent* Mesh, const TCHAR* SlotName, const TCHAR* Parameter,
        const BigimongMaleAvatar::Rgb& Color)
    {
        const int32 Index = Mesh->GetMaterialIndex(FName(SlotName));
        if (Index < 0) return;
        UMaterialInstanceDynamic* Material = Cast<UMaterialInstanceDynamic>(Mesh->GetMaterial(Index));
        if (!Material) Material = Mesh->CreateDynamicMaterialInstance(Index);
        if (Material)
            Material->SetVectorParameterValue(FName(Parameter),
                FLinearColor(Color.red, Color.green, Color.blue));
    }
}

ABigimongHomePawn::ABigimongHomePawn()
{
    PrimaryActorTick.bCanEverTick = true;
    AutoPossessPlayer = EAutoReceiveInput::Player0;
    RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
    PreviewPivot = CreateDefaultSubobject<USceneComponent>(TEXT("PreviewPivot"));
    PreviewPivot->SetupAttachment(RootComponent);

    PreviewCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("PreviewCamera"));
    PreviewCamera->SetupAttachment(RootComponent);
    PreviewCamera->SetRelativeLocation(FVector(0.f, 340.f, 115.f));
    PreviewCamera->SetRelativeRotation(FRotator(-3.f, -90.f, 0.f));
    PreviewCamera->bAutoActivate = true;

    Egg = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Egg"));
    Egg->SetupAttachment(PreviewPivot);
    Male = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Male"));
    Male->SetupAttachment(PreviewPivot);
    Female = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Female"));
    Female->SetupAttachment(PreviewPivot);

    MalePartsPivot = CreateDefaultSubobject<USceneComponent>(TEXT("MalePartsPivot"));
    MalePartsPivot->SetupAttachment(PreviewPivot);
    MaleBody = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("MaleBody"));
    MaleBody->SetupAttachment(MalePartsPivot);
    MaleFace = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("MaleFace"));
    MaleFace->SetupAttachment(MalePartsPivot);
    MaleEyes = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("MaleEyes"));
    MaleEyes->SetupAttachment(MalePartsPivot);
    MaleHair = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("MaleHair"));
    MaleHair->SetupAttachment(MalePartsPivot);

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
    MaleBodyAsset = TSoftObjectPtr<UStaticMesh>(FSoftObjectPath(TEXT("/Game/Varco/Male/SM_MaleBody.SM_MaleBody")));
    MaleFaceAsset = TSoftObjectPtr<USkeletalMesh>(FSoftObjectPath(TEXT("/Game/Varco/Male/SK_MaleFace.SK_MaleFace")));
    for (int32 Index = 0; Index < 15; ++Index)
    {
        MaleEyeAssets.Add(TSoftObjectPtr<UStaticMesh>(FSoftObjectPath(FString::Printf(
            TEXT("/Game/Varco/Male/SM_MaleEye_%02d.SM_MaleEye_%02d"), Index, Index))));
        MaleHairAssets.Add(TSoftObjectPtr<UStaticMesh>(FSoftObjectPath(FString::Printf(
            TEXT("/Game/Varco/Male/SM_MaleHair_%02d.SM_MaleHair_%02d"), Index, Index))));
    }
}

void ABigimongHomePawn::BeginPlay()
{
    Super::BeginPlay();
    ApplyImportedModel(Male, MaleAsset, 165.f);
    ApplyImportedModel(Female, FemaleAsset, 162.f);
    LoadMaleSelection();
    LoadMaleParts();
    RefreshVisibility();
    if (APlayerController* Controller = Cast<APlayerController>(GetController()))
    {
        MaleEditor = CreateWidget<UBigimongMaleEditorWidget>(Controller, UBigimongMaleEditorWidget::StaticClass());
        if (MaleEditor)
        {
            MaleEditor->SetTarget(this);
            MaleEditor->AddToPlayerScreen();
        }
    }
}

void ABigimongHomePawn::LoadMaleSelection()
{
    constexpr TCHAR Slot[] = TEXT("bigimong_unreal_male_avatar_v1");
    const UBigimongMaleAvatarSave* Saved = Cast<UBigimongMaleAvatarSave>(
        UGameplayStatics::LoadGameFromSlot(Slot, 0));
    if (!Saved || Saved->SchemaVersion != 1) return;
    const BigimongMaleAvatar::Selection Loaded{
        Saved->EyeShape, Saved->FaceShape, Saved->HairStyle,
        Saved->SkinTone, Saved->EyeColor, Saved->HairColor};
    if (BigimongMaleAvatar::Valid(Loaded)) MaleSelection = Loaded;
}

bool ABigimongHomePawn::SaveMaleSelection(const BigimongMaleAvatar::Selection& Candidate)
{
    UBigimongMaleAvatarSave* Saved = Cast<UBigimongMaleAvatarSave>(
        UGameplayStatics::CreateSaveGameObject(UBigimongMaleAvatarSave::StaticClass()));
    if (!Saved) return false;
    Saved->EyeShape = Candidate.eyeShape;
    Saved->FaceShape = Candidate.faceShape;
    Saved->HairStyle = Candidate.hairStyle;
    Saved->SkinTone = Candidate.skinTone;
    Saved->EyeColor = Candidate.eyeColor;
    Saved->HairColor = Candidate.hairColor;
    return UGameplayStatics::SaveGameToSlot(Saved, TEXT("bigimong_unreal_male_avatar_v1"), 0);
}

void ABigimongHomePawn::LoadMaleParts()
{
    UStaticMesh* Body = MaleBodyAsset.LoadSynchronous();
    USkeletalMesh* Face = MaleFaceAsset.LoadSynchronous();
    if (!Body || !Face)
    {
        UE_LOG(LogTemp, Warning, TEXT("Male modular body/face not imported; customization is unavailable"));
        return;
    }
    MaleBody->SetStaticMesh(Body);
    MaleFace->SetSkeletalMesh(Face);
    bMalePartsReady = ApplyMaleSelection(MaleSelection);
    if (!bMalePartsReady)
    {
        UE_LOG(LogTemp, Warning, TEXT("Male eye/hair parts missing; customization is unavailable"));
        return;
    }
    float MinZ = BIG_NUMBER;
    float MaxZ = -BIG_NUMBER;
    const auto IncludeBounds = [&MinZ, &MaxZ](const FBoxSphereBounds& Bounds)
    {
        MinZ = FMath::Min(MinZ, Bounds.Origin.Z - Bounds.BoxExtent.Z);
        MaxZ = FMath::Max(MaxZ, Bounds.Origin.Z + Bounds.BoxExtent.Z);
    };
    IncludeBounds(Body->GetBounds());
    IncludeBounds(Face->GetBounds());
    IncludeBounds(MaleEyes->GetStaticMesh()->GetBounds());
    IncludeBounds(MaleHair->GetStaticMesh()->GetBounds());
    if (MaxZ - MinZ <= KINDA_SMALL_NUMBER)
    {
        bMalePartsReady = false;
        return;
    }
    const float Scale = 165.f / (MaxZ - MinZ);
    MalePartsPivot->SetRelativeScale3D(FVector(Scale));
    MalePartsPivot->SetRelativeLocation(FVector(0.f, 0.f, -MinZ * Scale));
}

bool ABigimongHomePawn::ApplyMaleSelection(const BigimongMaleAvatar::Selection& Candidate)
{
    if (!BigimongMaleAvatar::Valid(Candidate) ||
        !MaleEyeAssets.IsValidIndex(Candidate.eyeShape - 1) ||
        !MaleHairAssets.IsValidIndex(Candidate.hairStyle - 1)) return false;
    UStaticMesh* Eyes = MaleEyeAssets[Candidate.eyeShape - 1].LoadSynchronous();
    UStaticMesh* Hair = MaleHairAssets[Candidate.hairStyle - 1].LoadSynchronous();
    if (!Eyes || !Hair) return false;
    MaleEyes->SetStaticMesh(Eyes);
    MaleHair->SetStaticMesh(Hair);

    if (ActiveFaceShape != Candidate.faceShape)
    {
        if (ActiveFaceShape > 0)
            MaleFace->SetMorphTarget(FName(*FString::Printf(TEXT("face_%02d"), ActiveFaceShape - 1)), 0.f, true);
        MaleFace->SetMorphTarget(FName(*FString::Printf(TEXT("face_%02d"), Candidate.faceShape - 1)), 1.f, false);
        ActiveFaceShape = Candidate.faceShape;
    }
    const auto Skin = BigimongMaleAvatar::SkinColor(Candidate.skinTone);
    const auto Iris = BigimongMaleAvatar::IrisColor(Candidate.eyeColor);
    const auto HairTint = BigimongMaleAvatar::HairColor(Candidate.hairColor);
    TintSlot(MaleBody, TEXT("BodySkin"), TEXT("BaseColorFactor"), Skin);
    TintSlot(MaleFace, TEXT("Skin"), TEXT("BaseColorFactor"), Skin);
    TintSlot(MaleFace, TEXT("SkinInner"), TEXT("BaseColorFactor"), Skin);
    TintSlot(MaleEyes, TEXT("Iris"), TEXT("BaseColorFactor"), Iris);
    TintSlot(MaleHair, TEXT("Hair"), TEXT("BaseColorFactor"), HairTint);
    return true;
}

int32 ABigimongHomePawn::GetMaleOption(int32 Category) const
{
    switch (Category)
    {
    case 0: return MaleSelection.eyeShape;
    case 1: return MaleSelection.faceShape;
    case 2: return MaleSelection.hairStyle;
    case 3: return MaleSelection.skinTone;
    case 4: return MaleSelection.eyeColor;
    case 5: return MaleSelection.hairColor;
    default: return 0;
    }
}

bool ABigimongHomePawn::SelectMaleOption(int32 Category, int32 OneBasedId)
{
    if (!bMalePartsReady || Category < 0 || Category >= 6) return false;
    BigimongMaleAvatar::Selection Candidate = MaleSelection;
    if (!BigimongMaleAvatar::Select(Candidate,
        static_cast<BigimongMaleAvatar::Option>(Category), OneBasedId)) return false;
    if (Candidate.eyeShape == MaleSelection.eyeShape && Candidate.faceShape == MaleSelection.faceShape &&
        Candidate.hairStyle == MaleSelection.hairStyle && Candidate.skinTone == MaleSelection.skinTone &&
        Candidate.eyeColor == MaleSelection.eyeColor && Candidate.hairColor == MaleSelection.hairColor) return true;
    // Keep both the visible model and the current selection unchanged if a part is absent.
    if (!MaleEyeAssets[Candidate.eyeShape - 1].LoadSynchronous() ||
        !MaleHairAssets[Candidate.hairStyle - 1].LoadSynchronous() ||
        !SaveMaleSelection(Candidate)) return false;
    MaleSelection = Candidate;
    ApplyMaleSelection(MaleSelection);
    if (MaleEditor) MaleEditor->Refresh();
    return true;
}

bool ABigimongHomePawn::StepMaleOption(int32 Category, int32 Direction)
{
    if (Category < 0 || Category >= 6 || !bMalePartsReady) return false;
    const auto Next = BigimongMaleAvatar::Next(MaleSelection,
        static_cast<BigimongMaleAvatar::Option>(Category), Direction);
    const int32 Value = Category == 0 ? Next.eyeShape : Category == 1 ? Next.faceShape :
        Category == 2 ? Next.hairStyle : Category == 3 ? Next.skinTone :
        Category == 4 ? Next.eyeColor : Next.hairColor;
    return SelectMaleOption(Category, Value);
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
    Male->SetVisibility(!bEggSelected && !bFemaleSelected && !bMalePartsReady);
    const bool bShowMaleParts = !bEggSelected && !bFemaleSelected && bMalePartsReady;
    MaleBody->SetVisibility(bShowMaleParts);
    MaleFace->SetVisibility(bShowMaleParts);
    MaleEyes->SetVisibility(bShowMaleParts);
    MaleHair->SetVisibility(bShowMaleParts);
    Female->SetVisibility(!bEggSelected && bFemaleSelected);
    if (MaleEditor)
        MaleEditor->SetVisibility(!bEggSelected && !bFemaleSelected ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
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
